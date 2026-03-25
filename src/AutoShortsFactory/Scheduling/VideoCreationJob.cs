using AutoShortsFactory.Models;
using AutoShortsFactory.Pipeline;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoShortsFactory.Scheduling;

/// <summary>영상 생성 Quartz Job (쇼츠: @06/10/14시, 롱폼: @20시)</summary>
[DisallowConcurrentExecution]
public class VideoCreationJob : IJob
{
    /// <summary>JobDataMap 키: 영상 유형 ("Shorts" 또는 "LongForm")</summary>
    public const string VideoTypeKey = "VideoType";

    /// <summary>JobDataMap 키: 롱폼 세부 유형 (옵션)</summary>
    public const string LongFormTypeKey = "LongFormType";

    private readonly VideoPipelineOrchestrator _orchestrator;
    private readonly ILogger<VideoCreationJob> _logger;

    public VideoCreationJob(
        VideoPipelineOrchestrator orchestrator,
        ILogger<VideoCreationJob> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var videoTypeStr = context.MergedJobDataMap.GetString(VideoTypeKey) ?? "Shorts";
        var isShorts = videoTypeStr.Equals("Shorts", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("VideoCreationJob 실행: {Type}, 스케줄={FireTime}",
            videoTypeStr, context.FireTimeUtc.ToLocalTime());

        try
        {
            VideoProject result;
            if (isShorts)
            {
                result = await _orchestrator.RunShortsAsync(ct);
            }
            else
            {
                // 롱폼 유형 파싱
                LongFormType? longFormType = null;
                var longFormTypeStr = context.MergedJobDataMap.GetString(LongFormTypeKey);
                if (!string.IsNullOrEmpty(longFormTypeStr) &&
                    Enum.TryParse<LongFormType>(longFormTypeStr, out var parsedType))
                {
                    longFormType = parsedType;
                }
                result = await _orchestrator.RunLongFormAsync(longFormType, ct);
            }

            _logger.LogInformation("VideoCreationJob 완료: Status={Status}", result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VideoCreationJob 오류");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
