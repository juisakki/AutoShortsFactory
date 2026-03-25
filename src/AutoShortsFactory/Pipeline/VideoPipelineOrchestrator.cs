using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Pipeline;

/// <summary>쇼츠/롱폼 파이프라인을 조율하는 오케스트레이터</summary>
public class VideoPipelineOrchestrator
{
    private readonly ShortsPipeline _shortsPipeline;
    private readonly LongFormPipeline _longFormPipeline;
    private readonly ILogger<VideoPipelineOrchestrator> _logger;

    public VideoPipelineOrchestrator(
        ShortsPipeline shortsPipeline,
        LongFormPipeline longFormPipeline,
        ILogger<VideoPipelineOrchestrator> logger)
    {
        _shortsPipeline = shortsPipeline;
        _longFormPipeline = longFormPipeline;
        _logger = logger;
    }

    /// <summary>쇼츠 영상 생성 실행</summary>
    public Task<VideoProject> RunShortsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("오케스트레이터: 쇼츠 파이프라인 실행");
        return _shortsPipeline.RunAsync(VideoType.Shorts, null, ct);
    }

    /// <summary>롱폼 영상 생성 실행</summary>
    public Task<VideoProject> RunLongFormAsync(
        LongFormType? longFormType = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("오케스트레이터: 롱폼 파이프라인 실행 (유형={Type})", longFormType?.ToString() ?? "자동");
        return _longFormPipeline.RunAsync(VideoType.LongForm, longFormType, ct);
    }

    /// <summary>실패한 프로젝트 재시도 실행</summary>
    public async Task<VideoProject> RetryAsync(
        VideoProject failedProject,
        CancellationToken ct = default)
    {
        if (failedProject.RetryCount >= 3)
        {
            _logger.LogWarning("최대 재시도 횟수 초과 (3회): {Title}", failedProject.Topic?.Title);
            return failedProject;
        }

        failedProject.RetryCount++;
        _logger.LogInformation("재시도 #{Count}: {Title}", failedProject.RetryCount, failedProject.Topic?.Title);

        // 지수 백오프 대기: RetryCount=1→20초, 2→40초, 3→80초
        var delayMs = (int)Math.Pow(2, failedProject.RetryCount) * 5000;
        await Task.Delay(delayMs, ct);

        return failedProject.VideoType == VideoType.Shorts
            ? await _shortsPipeline.RunAsync(VideoType.Shorts, null, ct)
            : await _longFormPipeline.RunAsync(VideoType.LongForm, failedProject.LongFormType, ct);
    }
}
