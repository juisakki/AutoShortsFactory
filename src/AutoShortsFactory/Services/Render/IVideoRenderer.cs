using AutoShortsFactory.Models;
namespace AutoShortsFactory.Services.Render;
/// <summary>영상 렌더링 서비스 인터페이스</summary>
public interface IVideoRenderer
{
    Task<string> RenderAsync(VideoProject project, string outputPath, CancellationToken ct = default);
}
