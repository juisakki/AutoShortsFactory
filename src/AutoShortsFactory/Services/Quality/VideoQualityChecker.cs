using System.Diagnostics;
using System.Text.RegularExpressions;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Quality;

/// <summary>FFProbe/FFmpeg를 이용한 영상 품질 검사 서비스</summary>
public class VideoQualityChecker : IQualityGate
{
    private readonly ILogger<VideoQualityChecker> _logger;

    // 쇼츠 기대 해상도
    private const int ShortsWidth = 1080;
    private const int ShortsHeight = 1920;
    // 롱폼 기대 해상도
    private const int LongFormWidth = 1920;
    private const int LongFormHeight = 1080;

    public VideoQualityChecker(ILogger<VideoQualityChecker> logger)
    {
        _logger = logger;
    }

    public async Task<QualityResult> CheckAsync(VideoProject project, CancellationToken ct = default)
    {
        var result = new QualityResult { Passed = true };

        if (string.IsNullOrWhiteSpace(project.OutputVideoPath) || !File.Exists(project.OutputVideoPath))
        {
            result.Passed = false;
            result.FailureReasons.Add("영상 파일이 존재하지 않습니다.");
            return result;
        }

        _logger.LogInformation("품질 검사 시작: {Path}", project.OutputVideoPath);

        // 1. 파일 크기 검증
        var fileInfo = new FileInfo(project.OutputVideoPath);
        if (fileInfo.Length == 0)
        {
            result.Passed = false;
            result.FailureReasons.Add("파일 크기가 0입니다.");
            return result;
        }

        // 2. FFProbe로 영상 정보 조회
        var probeOutput = await RunFfprobeAsync(project.OutputVideoPath, ct);

        // 3. 영상 길이 검증
        var duration = ParseDuration(probeOutput);
        if (duration <= 0)
        {
            result.Passed = false;
            result.FailureReasons.Add("영상 길이를 파악할 수 없습니다.");
        }
        else
        {
            if (project.VideoType == VideoType.Shorts)
            {
                if (duration < 15 || duration > 60)
                {
                    result.Passed = false;
                    result.FailureReasons.Add($"쇼츠 길이 범위 초과: {duration:F1}초 (기대: 15~60초)");
                }
            }
            else
            {
                if (duration < 180 || duration > 900)
                {
                    result.Passed = false;
                    result.FailureReasons.Add($"롱폼 길이 범위 초과: {duration:F1}초 (기대: 3~15분)");
                }
            }
        }

        // 4. 해상도 검증
        var (width, height) = ParseResolution(probeOutput);
        if (width > 0 && height > 0)
        {
            int expectedW = project.VideoType == VideoType.Shorts ? ShortsWidth : LongFormWidth;
            int expectedH = project.VideoType == VideoType.Shorts ? ShortsHeight : LongFormHeight;
            if (width != expectedW || height != expectedH)
            {
                result.Passed = false;
                result.FailureReasons.Add($"해상도 불일치: {width}x{height} (기대: {expectedW}x{expectedH})");
            }
        }

        // 5. 오디오 스트림 존재 여부
        if (!probeOutput.Contains("audio") && !probeOutput.Contains("Audio"))
        {
            result.Passed = false;
            result.FailureReasons.Add("오디오 스트림이 없습니다.");
        }

        // 6. 검은 화면 비율 검사 (blackdetect)
        if (duration > 0)
        {
            var blackRatio = await CheckBlackFrameRatioAsync(project.OutputVideoPath, duration, ct);
            if (blackRatio >= 0.10)
            {
                result.Passed = false;
                result.FailureReasons.Add($"검은 화면 비율이 너무 높습니다: {blackRatio:P1} (기준: 10%)");
            }
        }

        // 7. 무음 구간 검사 (silencedetect)
        if (duration > 0)
        {
            var silenceRatio = await CheckSilenceRatioAsync(project.OutputVideoPath, duration, ct);
            if (silenceRatio >= 0.30)
            {
                result.Passed = false;
                result.FailureReasons.Add($"무음 구간 비율이 너무 높습니다: {silenceRatio:P1} (기준: 30%)");
            }
        }

        if (result.Passed)
            _logger.LogInformation("품질 검사 통과: {Path}", project.OutputVideoPath);
        else
            _logger.LogWarning("품질 검사 실패: {Reasons}", string.Join(", ", result.FailureReasons));

        return result;
    }

    /// <summary>FFProbe 실행하여 영상 정보 출력</summary>
    private async Task<string> RunFfprobeAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("ffprobe",
                $"-v quiet -show_streams -show_format \"{filePath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("ffprobe 프로세스를 시작할 수 없습니다.");
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFProbe 실행 실패");
            return string.Empty;
        }
    }

    /// <summary>FFProbe 출력에서 영상 길이 파싱</summary>
    private static double ParseDuration(string probeOutput)
    {
        var match = Regex.Match(probeOutput, @"duration=(\d+\.?\d*)");
        return match.Success && double.TryParse(match.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var d) ? d : 0;
    }

    /// <summary>FFProbe 출력에서 해상도 파싱</summary>
    private static (int width, int height) ParseResolution(string probeOutput)
    {
        var wMatch = Regex.Match(probeOutput, @"width=(\d+)");
        var hMatch = Regex.Match(probeOutput, @"height=(\d+)");

        if (wMatch.Success && hMatch.Success
            && int.TryParse(wMatch.Groups[1].Value, out var w)
            && int.TryParse(hMatch.Groups[1].Value, out var h))
            return (w, h);

        return (0, 0);
    }

    /// <summary>blackdetect 필터로 검은 화면 비율 계산</summary>
    private async Task<double> CheckBlackFrameRatioAsync(string filePath, double totalDuration, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("ffmpeg",
                $"-i \"{filePath}\" -vf blackdetect=d=0.1:pix_th=0.10 -an -f null -")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("ffmpeg(blackdetect) 프로세스를 시작할 수 없습니다.");
            var output = await process.StandardError.ReadToEndAsync(ct);
            var matches = Regex.Matches(output, @"black_duration:(\d+\.?\d*)");
            var totalBlack = matches.Sum(m => double.TryParse(
                m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v) ? v : 0);

            return totalDuration > 0 ? totalBlack / totalDuration : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "blackdetect 검사 실패 (스킵)");
            return 0;
        }
    }

    /// <summary>silencedetect 필터로 무음 구간 비율 계산</summary>
    private async Task<double> CheckSilenceRatioAsync(string filePath, double totalDuration, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("ffmpeg",
                $"-i \"{filePath}\" -af silencedetect=noise=-30dB:d=1.0 -vn -f null -")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("ffmpeg(silencedetect) 프로세스를 시작할 수 없습니다.");
            var output = await process.StandardError.ReadToEndAsync(ct);
            var matches = Regex.Matches(output, @"silence_duration: (\d+\.?\d*)");
            var totalSilence = matches.Sum(m => double.TryParse(
                m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v) ? v : 0);

            return totalDuration > 0 ? totalSilence / totalDuration : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "silencedetect 검사 실패 (스킵)");
            return 0;
        }
    }
}
