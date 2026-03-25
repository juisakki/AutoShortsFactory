using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Report;

/// <summary>일일 리포트 서비스 인터페이스</summary>
public interface IReportService
{
    /// <summary>당일 생산/업로드 결과를 집계하여 리포트 생성</summary>
    Task<DailyReport> GenerateAsync(CancellationToken ct = default);
}
