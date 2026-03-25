using AutoShortsFactory.Services.Report;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoShortsFactory.Scheduling;

/// <summary>일일 리포트 생성 Quartz Job (@22시)</summary>
[DisallowConcurrentExecution]
public class DailyReportJob : IJob
{
    private readonly IReportService _reportService;
    private readonly ILogger<DailyReportJob> _logger;

    public DailyReportJob(
        IReportService reportService,
        ILogger<DailyReportJob> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        _logger.LogInformation("DailyReportJob 실행: {FireTime}", context.FireTimeUtc.ToLocalTime());

        try
        {
            var report = await _reportService.GenerateReportAsync(ct);
            _logger.LogInformation(
                "일일 리포트 생성 완료: 총 {Total}건 (성공 {Success}건, 실패 {Failure}건)",
                report.TotalAttempted, report.SuccessCount, report.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailyReportJob 오류");
        }
    }
}
