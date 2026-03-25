using System.Diagnostics;
using FFMpegCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Tts;

/// <summary>Edge TTS CLI를 이용한 한국어 TTS 서비스</summary>
public class EdgeTtsService : ITtsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EdgeTtsService> _logger;

    public EdgeTtsService(IConfiguration config, ILogger<EdgeTtsService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<(string audioPath, double durationSeconds)> GenerateAudioAsync(
        string text, string outputPath, CancellationToken ct = default)
    {
        var voice = _config["EdgeTts:Voice"] ?? "ko-KR-SunHiNeural";
        var python = _config["EdgeTts:PythonPath"] ?? "python";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // edge-tts CLI 호출
        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = $"-m edge_tts --voice {voice} --text \"{EscapeText(text)}\" --write-media \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("edge-tts 프로세스 시작 실패");
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            _logger.LogWarning("edge-tts 경고: {Err}", err);
        }

        double duration = 0;
        if (File.Exists(outputPath))
        {
            try
            {
                var info = await FFProbe.AnalyseAsync(outputPath, cancellationToken: ct);
                duration = info.Duration.TotalSeconds;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "오디오 길이 분석 실패");
                // 텍스트 길이로 추정 (초당 4.5자)
                duration = text.Length / 4.5;
            }
        }

        return (outputPath, duration);
    }

    private static string EscapeText(string text)
        => text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
}
