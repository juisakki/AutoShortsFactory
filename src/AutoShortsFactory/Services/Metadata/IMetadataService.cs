using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Metadata;

/// <summary>영상 메타데이터 생성 서비스 인터페이스</summary>
public interface IMetadataService
{
    /// <summary>AI를 이용해 제목/설명/태그/카테고리 생성</summary>
    Task<VideoMetadata> GenerateAsync(VideoProject project, CancellationToken ct = default);
}

/// <summary>AI 생성 영상 메타데이터</summary>
public class VideoMetadata
{
    /// <summary>클릭 유도 제목 (한국어)</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>SEO 최적화 설명 (해시태그 포함)</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>관련 해시태그 목록 (10~15개)</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>유튜브 카테고리 ID</summary>
    public string CategoryId { get; set; } = "22"; // People & Blogs 기본값
}
