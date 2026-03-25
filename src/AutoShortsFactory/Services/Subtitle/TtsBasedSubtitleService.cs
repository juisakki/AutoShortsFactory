using System.Text;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;
using ModelScript = AutoShortsFactory.Models.Script;

namespace AutoShortsFactory.Services.Subtitle;

/// <summary>TTS 오디오 실제 길이 기반 자막 타이밍 생성 (SRT 형식)</summary>
public class TtsBasedSubtitleService : ISubtitleService
{
    private readonly ILogger<TtsBasedSubtitleService> _logger;

    public TtsBasedSubtitleService(ILogger<TtsBasedSubtitleService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateSrtAsync(ModelScript script, double totalDurationSeconds, string outputPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // 모든 대본 줄 수집
        var allLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(script.Hook)) allLines.Add(script.Hook);
        allLines.AddRange(script.Lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        if (!string.IsNullOrWhiteSpace(script.Cta)) allLines.Add(script.Cta);

        if (!allLines.Any())
        {
            await File.WriteAllTextAsync(outputPath, string.Empty, ct);
            return outputPath;
        }

        // 텍스트 길이 비율로 타이밍 계산
        var totalChars = allLines.Sum(l => l.Length);
        var entries = new List<SubtitleEntry>();
        double currentTime = 0;

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            var ratio = (double)line.Length / totalChars;
            var duration = totalDurationSeconds * ratio;

            entries.Add(new SubtitleEntry
            {
                Index = i + 1,
                StartTime = TimeSpan.FromSeconds(currentTime),
                EndTime = TimeSpan.FromSeconds(currentTime + duration),
                Text = line
            });

            currentTime += duration;
        }

        // SRT 파일 생성
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine(entry.Index.ToString());
            sb.AppendLine($"{FormatSrtTime(entry.StartTime)} --> {FormatSrtTime(entry.EndTime)}");
            sb.AppendLine(entry.Text);
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8, ct);
        _logger.LogInformation("자막 파일 생성 완료: {Path} ({Count}개 항목)", outputPath, entries.Count);
        return outputPath;
    }

    private static string FormatSrtTime(TimeSpan ts)
        => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
}
