using System.ServiceModel.Syndication;
using System.Xml;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Trend;

/// <summary>주요 한국 뉴스 RSS 피드 수집</summary>
public class RssNewsCrawler : ITrendCrawler
{
    private readonly HttpClient _http;
    private readonly ILogger<RssNewsCrawler> _logger;

    // 주요 한국 뉴스 RSS 피드 목록
    private static readonly string[] RssFeeds =
    {
        "https://www.yonhapnewstv.co.kr/category/news/headline/feed/",
        "https://news.sbs.co.kr/news/SectionRss.do?sectionId=01",
        "https://rss.etnews.com/Section901.xml"
    };

    public RssNewsCrawler(IHttpClientFactory httpFactory, ILogger<RssNewsCrawler> logger)
    {
        _http = httpFactory.CreateClient("RssNews");
        _logger = logger;
    }

    public async Task<List<Topic>> GetTrendingTopicsAsync(CancellationToken cancellationToken = default)
    {
        var topics = new List<Topic>();

        foreach (var feedUrl in RssFeeds)
        {
            try
            {
                var xml = await _http.GetStringAsync(feedUrl, cancellationToken);
                using var reader = XmlReader.Create(new StringReader(xml));
                var feed = SyndicationFeed.Load(reader);

                foreach (var item in feed.Items.Take(10))
                {
                    var title = item.Title?.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    topics.Add(new Topic
                    {
                        Title = title,
                        Source = "RSS",
                        Keywords = new List<string> { title },
                        CollectedAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RSS 피드 수집 실패: {Url}", feedUrl);
            }
        }

        _logger.LogInformation("RSS 뉴스에서 {Count}개 주제 수집 완료", topics.Count);
        return topics;
    }
}
