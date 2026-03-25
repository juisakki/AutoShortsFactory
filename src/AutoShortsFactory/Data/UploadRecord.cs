using AutoShortsFactory.Models;

namespace AutoShortsFactory.Data;

/// <summary>유튜브 업로드 이력 엔티티</summary>
public class UploadRecord
{
    /// <summary>기본 키</summary>
    public int Id { get; set; }

    /// <summary>영상 프로젝트 ID</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>영상 제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>영상 유형</summary>
    public VideoType VideoType { get; set; }

    /// <summary>업로드 성공 여부</summary>
    public bool Success { get; set; }

    /// <summary>유튜브 동영상 URL</summary>
    public string? YouTubeUrl { get; set; }

    /// <summary>실패 사유</summary>
    public string? FailureReason { get; set; }

    /// <summary>재시도 횟수</summary>
    public int RetryCount { get; set; }

    /// <summary>레코드 생성 일시 (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>레코드 수정 일시 (UTC)</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
