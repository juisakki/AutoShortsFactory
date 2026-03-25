namespace AutoShortsFactory.Models;

/// <summary>영상 대본 모델</summary>
public class Script
{
    /// <summary>훅 - 시청자 주목을 끄는 첫 문장</summary>
    public string Hook { get; set; } = string.Empty;

    /// <summary>본문 대본 줄 목록</summary>
    public List<string> Lines { get; set; } = new();

    /// <summary>CTA - 행동 유도 문구 (구독, 좋아요 등)</summary>
    public string Cta { get; set; } = string.Empty;

    /// <summary>영상 유형</summary>
    public VideoType VideoType { get; set; }

    /// <summary>롱폼 세부 유형 (롱폼인 경우)</summary>
    public LongFormType? LongFormType { get; set; }

    /// <summary>예상 재생 시간 (초)</summary>
    public double EstimatedDurationSeconds { get; set; }

    /// <summary>전체 대본 텍스트 반환 (줄 합치기)</summary>
    public string FullText => string.Join("\n", new[] { Hook }
        .Concat(Lines)
        .Append(Cta)
        .Where(s => !string.IsNullOrWhiteSpace(s)));
}
