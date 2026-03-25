using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using AutoShortsFactory.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoShortsFactory.Scheduling;

/// <summary>실패한 영상 프로젝트 재시도 Quartz Job</summary>
/// <remarks>Cron: 0 0/30 * * * ? (매 30분마다 실행)</remarks>
[DisallowConcurrentExecution]
public class RetryJob : IJob
{
    private readonly AppDbContext _db;
    private readonly VideoPipelineOrchestrator _orchestrator;
    private readonly IConfiguration _config;
    private readonly ILogger<RetryJob> _logger;

    public RetryJob(
        AppDbContext db,
        VideoPipelineOrchestrator orchestrator,
        IConfiguration config,
        ILogger<RetryJob> logger)
    {
        _db = db;
        _orchestrator = orchestrator;
        _config = config;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var maxRetries = _config.GetValue<int>("Retry:MaxRetries", 3);
        var backoffSeconds = _config.GetSection("Retry:BackoffSeconds").Get<int[]>()
            ?? new[] { 60, 300, 900 };

        _logger.LogInformation("RetryJob 실행: DB에서 실패 항목 조회 중...");

        // DB에서 실패 프로젝트 조회 (재시도 횟수 < maxRetries)
        var failedRecords = await _db.UploadRecords
            .Where(r => !r.Success && r.RetryCount < maxRetries)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (!failedRecords.Any())
        {
            _logger.LogDebug("재시도 대상 항목 없음");
            return;
        }

        _logger.LogInformation("재시도 대상: {Count}건", failedRecords.Count);

        foreach (var record in failedRecords)
        {
            ct.ThrowIfCancellationRequested();

            // 지수 백오프: retryCount에 따른 대기 시간
            var backoffIdx = Math.Min(record.RetryCount, backoffSeconds.Length - 1);
            var waitSeconds = backoffSeconds[backoffIdx];
            var retryAfter = record.UpdatedAt.AddSeconds(waitSeconds);

            if (DateTime.UtcNow < retryAfter)
            {
                _logger.LogDebug("재시도 대기 중: {Id} (남은 시간: {Seconds}초)",
                    record.ProjectId,
                    (retryAfter - DateTime.UtcNow).TotalSeconds);
                continue;
            }

            _logger.LogInformation("재시도 실행 ({RetryCount}/{MaxRetries}): {Title}",
                record.RetryCount + 1, maxRetries, record.Title);

            try
            {
                // 영상 유형에 따라 재시도 파이프라인 실행
                VideoProject retryResult;
                if (record.VideoType == VideoType.Shorts)
                    retryResult = await _orchestrator.RunShortsAsync(ct);
                else
                    retryResult = await _orchestrator.RunLongFormAsync(ct);

                // 재시도 횟수 업데이트
                record.RetryCount++;
                record.UpdatedAt = DateTime.UtcNow;

                if (retryResult.Status == VideoStatus.Uploaded)
                {
                    record.Success = true;
                    record.YouTubeUrl = retryResult.UploadUrl;
                    record.FailureReason = null;
                    _logger.LogInformation("재시도 성공: {Title}", record.Title);
                }
                else
                {
                    record.FailureReason = retryResult.FailureReason;

                    if (record.RetryCount >= maxRetries)
                    {
                        _logger.LogWarning("최대 재시도 횟수 초과 → 영구 실패 처리: {Title}", record.Title);
                    }
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "재시도 실행 실패: {Title}", record.Title);
                record.RetryCount++;
                record.UpdatedAt = DateTime.UtcNow;
                record.FailureReason = ex.Message;
                await _db.SaveChangesAsync(ct);
            }
        }
    }
}
