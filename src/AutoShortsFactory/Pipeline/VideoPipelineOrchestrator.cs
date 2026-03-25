using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using AutoShortsFactory.Services.Report;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Pipeline;

/// <summary>전체 파이프라인 오케스트레이터: 하루 쇼츠 3건 + 롱폼 1건 실행</summary>
public class VideoPipelineOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReportService _reportService;
    private readonly IConfiguration _config;
    private readonly ILogger<VideoPipelineOrchestrator> _logger;

    // 실패 재시도 큐 (ProjectId → 남은 재시도 횟수)
    private readonly Queue<(string projectId, int retriesLeft)> _retryQueue = new();

    public VideoPipelineOrchestrator(
        IServiceProvider serviceProvider,
        IReportService reportService,
        IConfiguration config,
        ILogger<VideoPipelineOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _reportService = reportService;
        _config = config;
        _logger = logger;
    }

    /// <summary>쇼츠 1건 실행</summary>
    public async Task<VideoProject> RunShortsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("쇼츠 파이프라인 실행");
        var pipeline = _serviceProvider.GetRequiredService<ShortsPipeline>();
        var result = await pipeline.ExecuteAsync(ct);

        if (result.Status == VideoStatus.Failed || result.Status == VideoStatus.QualityFailed)
        {
            var maxRetries = _config.GetValue<int>("Retry:MaxRetries", 3);
            _retryQueue.Enqueue((result.Id, maxRetries));
            _logger.LogWarning("쇼츠 실패 → 재시도 큐 추가: {Id}", result.Id);
        }

        return result;
    }

    /// <summary>롱폼 1건 실행</summary>
    public async Task<VideoProject> RunLongFormAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("롱폼 파이프라인 실행");
        var pipeline = _serviceProvider.GetRequiredService<LongFormPipeline>();
        var result = await pipeline.ExecuteAsync(ct);

        if (result.Status == VideoStatus.Failed || result.Status == VideoStatus.QualityFailed)
        {
            var maxRetries = _config.GetValue<int>("Retry:MaxRetries", 3);
            _retryQueue.Enqueue((result.Id, maxRetries));
            _logger.LogWarning("롱폼 실패 → 재시도 큐 추가: {Id}", result.Id);
        }

        return result;
    }

    /// <summary>일일 리포트 생성</summary>
    public async Task<DailyReport> GenerateReportAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("일일 리포트 생성 중...");
        return await _reportService.GenerateAsync(ct);
    }

    /// <summary>재시도 큐에서 항목 가져오기</summary>
    public (string projectId, int retriesLeft)? DequeueRetry()
    {
        if (_retryQueue.Count == 0) return null;
        return _retryQueue.Dequeue();
    }

    /// <summary>재시도 큐 항목 수</summary>
    public int RetryQueueCount => _retryQueue.Count;
}
