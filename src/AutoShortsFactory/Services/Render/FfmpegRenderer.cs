using System.Diagnostics;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Render;

/// <summary>FFmpeg 기반 영상 렌더링 서비스 (쇼츠 1080x1920 / 롱폼 1920x1080)</summary>
public class FfmpegRenderer : IVideoRenderer
{
    private readonly IConfiguration _config;
    private readonly ILogger<FfmpegRenderer> _logger;

    public FfmpegRenderer(IConfiguration config, ILogger<FfmpegRenderer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> RenderAsync(VideoProject project, string outputPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var isShorts = project.VideoType == VideoType.Shorts;
        var width = isShorts ? 1080 : 1920;
        var height = isShorts ? 1920 : 1080;

        // FFmpeg 인수 구성
        var args = BuildFfmpegArgs(project, outputPath, width, height);
        _logger.LogInformation("FFmpeg 렌더링 시작: {Output}", outputPath);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg 프로세스 시작 실패");
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"FFmpeg 렌더링 실패 (코드: {proc.ExitCode}): {err[..Math.Min(500, err.Length)]}");
        }

        _logger.LogInformation("FFmpeg 렌더링 완료: {Output}", outputPath);
        return outputPath;
    }

    private string BuildFfmpegArgs(VideoProject project, string outputPath, int width, int height)
    {
        var parts = new List<string> { "-y" };

        // 입력: B-Roll 영상 (첫 번째)
        if (project.AssetPaths.Any())
        {
            // 루프로 B-Roll 반복 (영상 길이만큼)
            parts.Add($"-stream_loop -1 -i \"{project.AssetPaths[0]}\"");
        }
        else
        {
            // 에셋 없으면 검은 화면 생성
            parts.Add($"-f lavfi -i color=c=black:s={width}x{height}:r=30");
        }

        // 입력: TTS 오디오
        if (!string.IsNullOrEmpty(project.AudioPath) && File.Exists(project.AudioPath))
        {
            parts.Add($"-i \"{project.AudioPath}\"");
        }

        // 입력: BGM (있으면)
        var hasBgm = !string.IsNullOrEmpty(project.BgmPath) && File.Exists(project.BgmPath);
        if (hasBgm)
        {
            parts.Add($"-stream_loop -1 -i \"{project.BgmPath}\"");
        }

        // 필터 그래프
        var fontsDir = _config["Paths:Fonts"] ?? "./fonts";
        var filters = new List<string>();

        // 비디오 스케일 + 크롭
        filters.Add($"[0:v]scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1[vscaled]");

        // 자막 번인 (SRT 파일 있을 때)
        if (!string.IsNullOrEmpty(project.SubtitlePath) && File.Exists(project.SubtitlePath))
        {
            var srtPath = project.SubtitlePath.Replace("\\", "/").Replace(":", "\\:");
            var fontsize = project.VideoType == VideoType.Shorts ? 60 : 48;
            filters.Add($"[vscaled]subtitles='{srtPath}':force_style='FontSize={fontsize},PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000,Outline=3,Shadow=2,Alignment=2'[vout]");
        }
        else
        {
            filters.Add("[vscaled]copy[vout]");
        }

        // 오디오 믹싱
        var audioInputIdx = 1; // TTS는 두 번째 입력
        if (hasBgm)
        {
            var bgmIdx = project.AssetPaths.Any() ? 2 : 1;
            audioInputIdx = project.AssetPaths.Any() ? 1 : 0;
            // BGM -18dB 줄이기, TTS와 믹스
            filters.Add($"[{audioInputIdx}:a]volume=1.0[tts];[{bgmIdx}:a]volume=0.12[bgm];[tts][bgm]amix=inputs=2:duration=first[aout]");
        }
        else if (project.AssetPaths.Any())
        {
            filters.Add($"[1:a]volume=1.0[aout]");
        }
        else
        {
            filters.Add($"[0:a]volume=1.0[aout]");
        }

        parts.Add($"-filter_complex \"{string.Join(";", filters)}\"");
        parts.Add("-map [vout]");
        parts.Add("-map [aout]");

        // 출력 길이를 TTS 오디오 길이로 제한
        if (project.AudioDurationSeconds > 0)
        {
            parts.Add($"-t {project.AudioDurationSeconds:F2}");
        }

        parts.Add("-c:v libx264 -preset fast -crf 23");
        parts.Add("-c:a aac -b:a 128k");
        parts.Add("-movflags +faststart");
        parts.Add($"\"{outputPath}\"");

        return string.Join(" ", parts);
    }
}
