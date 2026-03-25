using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Metadata;

/// <summary>유튜브 메타데이터(제목/설명/태그/카테고리) 생성 서비스 인터페이스</summary>
public interface IMetadataService
{
    /// <summary>GPT로 클릭유도 제목, SEO 설명, 해시태그, 카테고리 자동 생성</summary>
    Task<VideoMetadata> GenerateAsync(
        VideoProject project,
        CancellationToken ct = default);
}

/// <summary>유튜브 메타데이터</summary>
public class VideoMetadata
{
    /// <summary>클릭 유도 제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>SEO 최적화 설명</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>해시태그 목록 (10~15개)</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>유튜브 카테고리 ID</summary>
    public string CategoryId { get; set; } = "22"; // People & Blogs
}
