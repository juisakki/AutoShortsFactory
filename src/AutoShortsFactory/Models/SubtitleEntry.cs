namespace AutoShortsFactory.Models;

/// <summary>자막 항목 (텍스트, 시작/끝 시간)</summary>
public class SubtitleEntry
{
    /// <summary>자막 순번 (1부터 시작)</summary>
    public int Index { get; set; }

    /// <summary>자막 시작 시간</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>자막 종료 시간</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>자막 텍스트</summary>
    public string Text { get; set; } = string.Empty;
}
