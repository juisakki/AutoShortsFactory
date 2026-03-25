using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Quality;

/// <summary>FFProbe 기반 영상 품질 검사 서비스</summary>
public class VideoQualityChecker : IQualityGate
{
    private readonly IConfiguration _config;
    private readonly ILogger<VideoQualityChecker> _logger;

    // 품질 기준값
    private const double MaxBlackFrameRatio = 0.10; // 검은화면 10% 이상 실패
    private const double MaxSilenceRatio = 0.30;    // 무음 30% 이상 실패
    private const int MinWidth = 540;
    private const int MinHeight = 960;
    private const long MaxFileSizeBytes = 256L * 1024 * 1024; // 256MB

    public VideoQualityChecker(
        IConfiguration config,
        ILogger<VideoQualityChecker> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QualityResult> CheckAsync(string videoPath, CancellationToken ct = default)
    {
        _logger.LogInformation("품질 검사 시작: {Path}", videoPath);

        var result = new QualityResult();

        if (!File.Exists(videoPath))
        {
            result.Passed = false;
            result.FailureReasons.Add($"영상 파일이 없습니다: {videoPath}");
            return result;
        }

        result.FileSizeBytes = new FileInfo(videoPath).Length;

        // 1. FFProbe로 기본 정보 조회
        await ProbeVideoInfoAsync(videoPath, result, ct);

        // 2. blackdetect 필터로 검은화면 비율 검사
        await CheckBlackFramesAsync(videoPath, result, ct);

        // 3. silencedetect 필터로 무음 비율 검사
        await CheckSilenceAsync(videoPath, result, ct);

        // 4. 전체 통과 여부 판정
        result.Passed = result.FailureReasons.Count == 0;

        if (result.Passed)
            _logger.LogInformation("품질 검사 통과: {Path}", videoPath);
        else
            _logger.LogWarning("품질 검사 실패: {Reasons}", string.Join(", ", result.FailureReasons));

        return result;
    }

    /// <summary>FFProbe로 영상 기본 정보(길이/해상도/오디오) 조회</summary>
    private async Task ProbeVideoInfoAsync(string videoPath, QualityResult result, CancellationToken ct)
    {
        try
        {
            var ffprobePath = _config["FFmpeg:FFprobePath"] ?? "ffprobe";
            var args = $"-v quiet -print_format json -show_streams -show_format \"{videoPath}\"";
            var output = await RunProcessAsync(ffprobePath, args, ct);

            // 길이 파싱
            var durationMatch = Regex.Match(output, "\"duration\":\\s*\"([\\d.]+)\"");
            if (durationMatch.Success)
                result.DurationSeconds = double.Parse(durationMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            // 너비/높이 파싱
            var widthMatch = Regex.Match(output, "\"width\":\\s*(\\d+)");
            var heightMatch = Regex.Match(output, "\"height\":\\s*(\\d+)");
            if (widthMatch.Success) result.Width = int.Parse(widthMatch.Groups[1].Value);
            if (heightMatch.Success) result.Height = int.Parse(heightMatch.Groups[1].Value);

            // 오디오 스트림 확인
            result.HasAudio = output.Contains("\"codec_type\": \"audio\"") ||
                              output.Contains("\"codec_type\":\"audio\"");

            // 검증
            if (result.DurationSeconds < 3)
                result.FailureReasons.Add($"영상 길이가 너무 짧습니다: {result.DurationSeconds:F1}초 (최소 3초)");

            if (result.Width > 0 && result.Width < MinWidth)
                result.FailureReasons.Add($"영상 너비가 부족합니다: {result.Width}px (최소 {MinWidth}px)");

            if (result.Height > 0 && result.Height < MinHeight)
                result.FailureReasons.Add($"영상 높이가 부족합니다: {result.Height}px (최소 {MinHeight}px)");

            if (result.FileSizeBytes > MaxFileSizeBytes)
                result.FailureReasons.Add($"파일 크기 초과: {result.FileSizeBytes / 1024 / 1024}MB (최대 256MB)");

            if (!result.HasAudio)
                result.FailureReasons.Add("오디오 스트림이 없습니다.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFProbe 실행 실패. 품질 검사 일부 생략");
        }
    }

    /// <summary>blackdetect 필터로 검은화면 비율 측정</summary>
    private async Task CheckBlackFramesAsync(string videoPath, QualityResult result, CancellationToken ct)
    {
        if (result.DurationSeconds <= 0) return;

        try
        {
            var ffmpegPath = _config["FFmpeg:FFmpegPath"] ?? "ffmpeg";
            // blackdetect: 0.98 이상 밝기 값이 없는 구간 감지
            var args = $"-i \"{videoPath}\" -vf blackdetect=d=0.1:pic_th=0.98 -an -f null - 2>&1";
            var output = await RunProcessAsync(ffmpegPath, args, ct);

            // 검은화면 구간 합산
            var totalBlack = 0.0;
            var matches = Regex.Matches(output, "black_duration:([\\.\\d]+)");
            foreach (Match m in matches)
                totalBlack += double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            result.BlackFrameRatio = result.DurationSeconds > 0
                ? totalBlack / result.DurationSeconds
                : 0;

            if (result.BlackFrameRatio > MaxBlackFrameRatio)
                result.FailureReasons.Add(
                    $"검은화면 비율 초과: {result.BlackFrameRatio:P0} (기준 {MaxBlackFrameRatio:P0})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "blackdetect 검사 실패");
        }
    }

    /// <summary>silencedetect 필터로 무음 비율 측정</summary>
    private async Task CheckSilenceAsync(string videoPath, QualityResult result, CancellationToken ct)
    {
        if (!result.HasAudio || result.DurationSeconds <= 0) return;

        try
        {
            var ffmpegPath = _config["FFmpeg:FFmpegPath"] ?? "ffmpeg";
            // silencedetect: -35dB 이하 0.5초 이상 무음 구간 감지
            var args = $"-i \"{videoPath}\" -af silencedetect=noise=-35dB:d=0.5 -vn -f null - 2>&1";
            var output = await RunProcessAsync(ffmpegPath, args, ct);

            // 무음 구간 합산
            var totalSilence = 0.0;
            var durationMatches = Regex.Matches(output, "silence_duration: ([\\.\\d]+)");
            foreach (Match m in durationMatches)
                totalSilence += double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            result.SilenceRatio = result.DurationSeconds > 0
                ? totalSilence / result.DurationSeconds
                : 0;

            if (result.SilenceRatio > MaxSilenceRatio)
                result.FailureReasons.Add(
                    $"무음 비율 초과: {result.SilenceRatio:P0} (기준 {MaxSilenceRatio:P0})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "silencedetect 검사 실패");
        }
    }

    /// <summary>외부 프로세스를 비동기로 실행하고 출력을 반환</summary>
    private static async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        var stdout = await outputTask;
        var stderr = await errorTask;
        return stdout + stderr;
    }
}
