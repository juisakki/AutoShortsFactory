using AutoShortsFactory.Models;

namespace AutoShortsFactory.Data;

/// <summary>주제 사용 이력 엔티티 (중복 주제 방지용)</summary>
public class TopicHistory
{
    /// <summary>기본 키</summary>
    public int Id { get; set; }

    /// <summary>사용된 주제 제목</summary>
    public string TopicTitle { get; set; } = string.Empty;

    /// <summary>영상 유형</summary>
    public VideoType VideoType { get; set; }

    /// <summary>사용 일시 (UTC)</summary>
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
