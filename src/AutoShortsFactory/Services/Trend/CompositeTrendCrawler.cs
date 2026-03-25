using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Trend;

/// <summary>Google Trends, YouTube, RSS 3개 소스 통합 트렌드 크롤러</summary>
public class CompositeTrendCrawler : ITrendCrawler
{
    private readonly GoogleTrendsCrawler _googleTrends;
    private readonly YouTubeTrendsCrawler _youTube;
    private readonly RssNewsCrawler _rss;
    private readonly ILogger<CompositeTrendCrawler> _logger;

    public CompositeTrendCrawler(
        GoogleTrendsCrawler googleTrends,
        YouTubeTrendsCrawler youTube,
        RssNewsCrawler rss,
        ILogger<CompositeTrendCrawler> logger)
    {
        _googleTrends = googleTrends;
        _youTube = youTube;
        _rss = rss;
        _logger = logger;
    }

    public async Task<List<Topic>> GetTrendingTopicsAsync(CancellationToken cancellationToken = default)
    {
        // 세 소스를 병렬로 수집
        var tasks = new[]
        {
            _googleTrends.GetTrendingTopicsAsync(cancellationToken),
            _youTube.GetTrendingTopicsAsync(cancellationToken),
            _rss.GetTrendingTopicsAsync(cancellationToken)
        };

        var results = await Task.WhenAll(tasks);

        // 전체 합치기
        var allTopics = results.SelectMany(r => r).ToList();

        // 제목 기준 중복 제거 (대소문자/공백 무시)
        var deduplicated = allTopics
            .GroupBy(t => NormalizeTitle(t.Title))
            .Select(g => g.OrderByDescending(t => t.Source == "GoogleTrends" ? 2 : t.Source == "YouTube" ? 1 : 0).First())
            .ToList();

        _logger.LogInformation("통합 크롤러: 전체 {Total}개 → 중복 제거 후 {Dedup}개", allTopics.Count, deduplicated.Count);

        return deduplicated;
    }

    /// <summary>제목 정규화 (중복 비교용)</summary>
    private static string NormalizeTitle(string title)
        => title.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
}
