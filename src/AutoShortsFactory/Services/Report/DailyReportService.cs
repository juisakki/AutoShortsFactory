using System.Text.Json;
using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Report;

/// <summary>일일 생산/업로드 결과 리포트 서비스</summary>
public class DailyReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DailyReportService> _logger;
    private readonly string _reportsPath;

    public DailyReportService(AppDbContext db, ILogger<DailyReportService> logger)
    {
        _db = db;
        _logger = logger;
        _reportsPath = Path.Combine(AppContext.BaseDirectory, "reports");
    }

    public async Task<DailyReport> GenerateAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        _logger.LogInformation("일일 리포트 생성 시작: {Date}", today.ToString("yyyy-MM-dd"));

        // 당일 업로드 이력 조회
        var records = await _db.UploadRecords
            .Where(r => r.CreatedAt >= today && r.CreatedAt < today.AddDays(1))
            .ToListAsync(ct);

        var report = new DailyReport
        {
            Date = today,
            TotalAttempted = records.Count,
            SuccessCount = records.Count(r => r.Success),
            FailureCount = records.Count(r => !r.Success),
            Entries = records.Select(r => new ReportEntry
            {
                Title = r.Title,
                VideoType = r.VideoType,
                Success = r.Success,
                UploadUrl = r.YouTubeUrl,
                FailureReason = r.FailureReason
            }).ToList()
        };

        // 콘솔 출력
        PrintReport(report);

        // 파일 저장
        await SaveReportAsync(report, ct);

        return report;
    }

    /// <summary>리포트 콘솔 출력</summary>
    private void PrintReport(DailyReport report)
    {
        _logger.LogInformation("════════════════════════════════════════");
        _logger.LogInformation("  📊 일일 생산 리포트 - {Date}", report.Date.ToString("yyyy-MM-dd"));
        _logger.LogInformation("════════════════════════════════════════");
        _logger.LogInformation("  총 시도: {Total}건", report.TotalAttempted);
        _logger.LogInformation("  성공: {Success}건 / 실패: {Failure}건",
            report.SuccessCount, report.FailureCount);
        _logger.LogInformation("────────────────────────────────────────");

        foreach (var entry in report.Entries)
        {
            var status = entry.Success ? "✅" : "❌";
            var typeStr = entry.VideoType == VideoType.Shorts ? "[쇼츠]" : "[롱폼]";
            _logger.LogInformation("  {Status} {Type} {Title}", status, typeStr, entry.Title);
            if (entry.Success && !string.IsNullOrEmpty(entry.UploadUrl))
                _logger.LogInformation("     → {Url}", entry.UploadUrl);
            if (!entry.Success && !string.IsNullOrEmpty(entry.FailureReason))
                _logger.LogInformation("     ✗ {Reason}", entry.FailureReason);
        }

        _logger.LogInformation("════════════════════════════════════════");
    }

    /// <summary>리포트를 JSON 파일로 저장 (reports/ 폴더)</summary>
    private async Task SaveReportAsync(DailyReport report, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_reportsPath);

            var fileName = $"report_{report.Date:yyyy-MM-dd}.json";
            var filePath = Path.Combine(_reportsPath, fileName);

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(filePath, json, ct);
            _logger.LogInformation("리포트 파일 저장 완료: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "리포트 파일 저장 실패");
        }
    }
}
