using AutoShortsFactory.Models;

namespace AutoShortsFactory.Pipeline;

/// <summary>영상 생성 파이프라인 인터페이스</summary>
public interface IVideoPipeline
{
    /// <summary>지정한 유형의 영상 생성 파이프라인 실행</summary>
    Task<VideoProject> RunAsync(
        VideoType videoType,
        LongFormType? longFormType = null,
        CancellationToken ct = default);
}
