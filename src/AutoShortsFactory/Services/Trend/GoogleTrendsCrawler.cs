using System.ServiceModel.Syndication;
using System.Xml;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Trend;

/// <summary>Google Trends RSS 피드로 트렌드 수집</summary>
public class GoogleTrendsCrawler : ITrendCrawler
{
    private readonly HttpClient _http;
    private readonly ILogger<GoogleTrendsCrawler> _logger;

    // Google Trends 한국 실시간 트렌드 RSS URL
    private const string TrendsRssUrl =
        "https://trends.google.com/trends/trendingsearches/daily/rss?geo=KR";

    public GoogleTrendsCrawler(IHttpClientFactory httpFactory, ILogger<GoogleTrendsCrawler> logger)
    {
        _http = httpFactory.CreateClient("GoogleTrends");
        _logger = logger;
    }

    public async Task<List<Topic>> GetTrendingTopicsAsync(CancellationToken cancellationToken = default)
    {
        var topics = new List<Topic>();

        try
        {
            var xml = await _http.GetStringAsync(TrendsRssUrl, cancellationToken);

            using var reader = XmlReader.Create(new StringReader(xml));
            var feed = SyndicationFeed.Load(reader);

            foreach (var item in feed.Items.Take(20))
            {
                var title = item.Title?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(title)) continue;

                topics.Add(new Topic
                {
                    Title = title,
                    Source = "GoogleTrends",
                    Keywords = new List<string> { title },
                    CollectedAt = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Google Trends에서 {Count}개 주제 수집 완료", topics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Trends 수집 중 오류 발생");
        }

        return topics;
    }
}
