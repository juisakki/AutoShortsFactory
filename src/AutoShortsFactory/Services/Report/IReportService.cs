using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Report;

/// <summary>일일 리포트 서비스 인터페이스</summary>
public interface IReportService
{
    /// <summary>오늘 업로드된 영상 기록 추가</summary>
    void AddEntry(ReportEntry entry);

    /// <summary>일일 리포트 생성 및 저장</summary>
    Task<DailyReport> GenerateReportAsync(CancellationToken ct = default);
}
