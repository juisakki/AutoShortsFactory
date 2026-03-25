using AutoShortsFactory.Models;

namespace AutoShortsFactory.Pipeline;

/// <summary>영상 생성 파이프라인 인터페이스</summary>
public interface IVideoPipeline
{
    Task<VideoProject> ExecuteAsync(CancellationToken ct = default);
}
