namespace AutoShortsFactory.Models;

/// <summary>트렌드 주제 모델</summary>
public class Topic
{
    /// <summary>주제 고유 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>주제 제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>수집 소스 (GoogleTrends, YouTube, RSS)</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>AI 평가 점수 (0~100)</summary>
    public double Score { get; set; }

    /// <summary>관련 키워드 목록</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>수집 시각 (UTC)</summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}
