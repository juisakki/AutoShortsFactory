using AutoShortsFactory.Models;
namespace AutoShortsFactory.Services.Thumbnail;
/// <summary>썸네일 생성 서비스 인터페이스</summary>
public interface IThumbnailService
{
    Task<string> GenerateAsync(VideoProject project, string outputPath, CancellationToken ct = default);
}
