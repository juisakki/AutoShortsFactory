using AutoShortsFactory.Models;
using AutoShortsFactory.Pipeline;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoShortsFactory.Scheduling;

/// <summary>영상 생성 Quartz Job</summary>
/// <remarks>
/// 쇼츠 Job Cron: 0 0 6,10,14 * * ? (06시, 10시, 14시에 각 1건)
/// 롱폼 Job Cron: 0 0 20 * * ? (20시에 1건)
/// </remarks>
[DisallowConcurrentExecution]
public class VideoCreationJob : IJob
{
    private readonly VideoPipelineOrchestrator _orchestrator;
    private readonly ILogger<VideoCreationJob> _logger;

    /// <summary>JobDataMap 키: 영상 유형 (Shorts / LongForm)</summary>
    public const string VideoTypeKey = "VideoType";

    public VideoCreationJob(VideoPipelineOrchestrator orchestrator, ILogger<VideoCreationJob> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        // JobDataMap에서 영상 유형 읽기
        var videoTypeStr = context.MergedJobDataMap.GetString(VideoTypeKey) ?? "Shorts";
        var videoType = Enum.TryParse<VideoType>(videoTypeStr, out var vt) ? vt : VideoType.Shorts;

        _logger.LogInformation("VideoCreationJob 실행: {VideoType}", videoType);

        try
        {
            VideoProject result;
            if (videoType == VideoType.Shorts)
                result = await _orchestrator.RunShortsAsync(ct);
            else
                result = await _orchestrator.RunLongFormAsync(ct);

            _logger.LogInformation("VideoCreationJob 완료: {Status} - {Title}",
                result.Status, result.GeneratedTitle ?? result.Topic.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VideoCreationJob 실행 중 예외 발생");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
