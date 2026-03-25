using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Trend;

/// <summary>트렌드 수집 서비스 인터페이스</summary>
public interface ITrendCrawler
{
    /// <summary>트렌딩 주제 목록 수집</summary>
    Task<List<Topic>> GetTrendingTopicsAsync(CancellationToken cancellationToken = default);
}
