using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using AutoShortsFactory.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoShortsFactory.Scheduling;

/// <summary>실패 영상 재시도 Quartz Job (@매 30분)</summary>
[DisallowConcurrentExecution]
public class RetryJob : IJob
{
    private readonly VideoPipelineOrchestrator _orchestrator;
    private readonly AppDbContext _db;
    private readonly ILogger<RetryJob> _logger;

    public RetryJob(
        VideoPipelineOrchestrator orchestrator,
        AppDbContext db,
        ILogger<RetryJob> logger)
    {
        _orchestrator = orchestrator;
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        _logger.LogInformation("RetryJob 실행: {FireTime}", context.FireTimeUtc.ToLocalTime());

        try
        {
            // DB에서 오늘 실패한 항목 중 재시도 횟수가 3회 미만인 것 조회
            var todayStart = DateTime.UtcNow.Date;
            var failedRecords = await _db.UploadRecords
                .Where(r => !r.Success
                    && r.CreatedAt >= todayStart
                    && r.RetryCount < 3)
                .OrderBy(r => r.CreatedAt)
                .Take(5) // 한 번에 최대 5건만 재시도
                .ToListAsync(ct);

            if (!failedRecords.Any())
            {
                _logger.LogDebug("재시도할 실패 항목이 없습니다.");
                return;
            }

            _logger.LogInformation("재시도 대상: {Count}건", failedRecords.Count);

            foreach (var record in failedRecords)
            {
                try
                {
                    _logger.LogInformation("재시도: {Title} (시도 #{Count})",
                        record.Title, record.RetryCount + 1);

                    // 재시도용 VideoProject 재구성
                    var failedProject = new VideoProject
                    {
                        Id = record.ProjectId,
                        VideoType = record.VideoType,
                        RetryCount = record.RetryCount
                    };
                    failedProject.Topic = new Topic { Title = record.Title };

                    var result = await _orchestrator.RetryAsync(failedProject, ct);

                    // DB 업데이트
                    record.RetryCount = result.RetryCount;
                    record.Success = result.Status == VideoStatus.Uploaded;
                    record.YouTubeUrl = result.UploadUrl;
                    record.FailureReason = result.FailureReason;
                    record.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);

                    _logger.LogInformation("재시도 결과: {Status}", result.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "항목 재시도 중 오류: {Title}", record.Title);

                    record.RetryCount++;
                    record.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryJob 오류");
        }
    }
}
