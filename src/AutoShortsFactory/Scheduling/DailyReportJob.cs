using AutoShortsFactory.Pipeline;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoShortsFactory.Scheduling;

/// <summary>일일 리포트 생성 Quartz Job</summary>
/// <remarks>Cron: 0 0 22 * * ? (매일 22시)</remarks>
[DisallowConcurrentExecution]
public class DailyReportJob : IJob
{
    private readonly VideoPipelineOrchestrator _orchestrator;
    private readonly ILogger<DailyReportJob> _logger;

    public DailyReportJob(VideoPipelineOrchestrator orchestrator, ILogger<DailyReportJob> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("DailyReportJob 실행: 일일 리포트 생성 중...");

        try
        {
            var report = await _orchestrator.GenerateReportAsync(ct);
            _logger.LogInformation("일일 리포트 완료: 총 {Total}건 (성공 {Success}, 실패 {Failure})",
                report.TotalAttempted, report.SuccessCount, report.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailyReportJob 실행 중 예외 발생");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
