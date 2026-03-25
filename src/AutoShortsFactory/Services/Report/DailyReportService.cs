using System.Text.Json;
using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Report;

/// <summary>일일 생산/업로드 결과 집계 및 리포트 서비스</summary>
public class DailyReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DailyReportService> _logger;
    private readonly List<ReportEntry> _todayEntries = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DailyReportService(
        AppDbContext db,
        ILogger<DailyReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void AddEntry(ReportEntry entry)
    {
        _lock.Wait();
        try
        {
            _todayEntries.Add(entry);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<DailyReport> GenerateReportAsync(CancellationToken ct = default)
    {
        // DB에서 오늘 업로드 이력 조회
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var dbRecords = await _db.UploadRecords
            .Where(r => r.CreatedAt >= todayStart && r.CreatedAt < todayEnd)
            .ToListAsync(ct);

        // 메모리 내 항목 + DB 항목 병합
        await _lock.WaitAsync(ct);
        List<ReportEntry> allEntries;
        try
        {
            allEntries = new List<ReportEntry>(_todayEntries);
        }
        finally
        {
            _lock.Release();
        }

        // DB 이력을 ReportEntry로 변환 (중복 제거)
        var existingTitles = allEntries.Select(e => e.Title).ToHashSet();
        foreach (var record in dbRecords)
        {
            if (!existingTitles.Contains(record.Title))
            {
                allEntries.Add(new ReportEntry
                {
                    Title = record.Title,
                    VideoType = record.VideoType,
                    Success = record.Success,
                    UploadUrl = record.YouTubeUrl,
                    FailureReason = record.FailureReason
                });
            }
        }

        var report = new DailyReport
        {
            Date = DateTime.Today,
            TotalAttempted = allEntries.Count,
            SuccessCount = allEntries.Count(e => e.Success),
            FailureCount = allEntries.Count(e => !e.Success),
            Entries = allEntries
        };

        // 콘솔 출력
        PrintReport(report);

        // JSON 파일 저장
        await SaveReportAsync(report, ct);

        return report;
    }

    /// <summary>리포트를 콘솔에 출력</summary>
    private void PrintReport(DailyReport report)
    {
        _logger.LogInformation("═══════════════════════════════════════════");
        _logger.LogInformation("   📊 AutoShortsFactory 일일 리포트");
        _logger.LogInformation("   날짜: {Date:yyyy-MM-dd}", report.Date);
        _logger.LogInformation("═══════════════════════════════════════════");
        _logger.LogInformation("  총 시도: {Total}건", report.TotalAttempted);
        _logger.LogInformation("  ✅ 성공: {Success}건", report.SuccessCount);
        _logger.LogInformation("  ❌ 실패: {Failure}건", report.FailureCount);
        _logger.LogInformation("───────────────────────────────────────────");

        foreach (var entry in report.Entries)
        {
            var status = entry.Success ? "✅" : "❌";
            var typeText = entry.VideoType == VideoType.Shorts ? "쇼츠" : "롱폼";
            _logger.LogInformation("  {Status} [{Type}] {Title}", status, typeText, entry.Title);
            if (!string.IsNullOrWhiteSpace(entry.UploadUrl))
                _logger.LogInformation("        → {Url}", entry.UploadUrl);
            if (!entry.Success && !string.IsNullOrWhiteSpace(entry.FailureReason))
                _logger.LogInformation("        ⚠ {Reason}", entry.FailureReason);
        }

        _logger.LogInformation("═══════════════════════════════════════════");
    }

    /// <summary>리포트를 JSON 파일로 저장</summary>
    private static async Task SaveReportAsync(DailyReport report, CancellationToken ct)
    {
        var reportDir = Path.Combine("reports");
        Directory.CreateDirectory(reportDir);

        var fileName = $"report_{report.Date:yyyyMMdd}.json";
        var filePath = Path.Combine(reportDir, fileName);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await File.WriteAllTextAsync(filePath, json, ct);
    }
}
