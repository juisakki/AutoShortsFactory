using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Upload;

/// <summary>영상 업로드 서비스 인터페이스</summary>
public interface IUploadService
{
    /// <summary>영상을 유튜브에 업로드</summary>
    Task<string> UploadAsync(VideoProject project, CancellationToken ct = default);
}
