using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Upload;

/// <summary>유튜브 업로드 서비스 인터페이스</summary>
public interface IUploadService
{
    /// <summary>영상 프로젝트를 유튜브에 업로드 (즉시 공개 또는 예약)</summary>
    Task<string> UploadAsync(
        VideoProject project,
        DateTime? scheduledAt = null,
        CancellationToken ct = default);

    /// <summary>업로드된 영상에 썸네일 설정</summary>
    Task SetThumbnailAsync(
        string videoId,
        string thumbnailPath,
        CancellationToken ct = default);
}
