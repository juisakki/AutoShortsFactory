using System.Text.Json;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Trend;

/// <summary>YouTube Data API v3로 인기/트렌드 영상 수집</summary>
public class YouTubeTrendsCrawler : ITrendCrawler
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<YouTubeTrendsCrawler> _logger;

    public YouTubeTrendsCrawler(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<YouTubeTrendsCrawler> logger)
    {
        _http = httpFactory.CreateClient("YouTube");
        _config = config;
        _logger = logger;
    }

    public async Task<List<Topic>> GetTrendingTopicsAsync(CancellationToken cancellationToken = default)
    {
        var topics = new List<Topic>();
        var apiKey = _config["YouTube:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_YOUTUBE_API_KEY")
        {
            _logger.LogWarning("YouTube API 키가 설정되지 않아 트렌드 수집을 건너뜁니다");
            return topics;
        }

        try
        {
            // YouTube 인기 동영상 조회 (한국 기준)
            var url = $"https://www.googleapis.com/youtube/v3/videos" +
                      $"?part=snippet&chart=mostPopular&regionCode=KR&maxResults=20&key={apiKey}";

            var response = await _http.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(response);

            var items = doc.RootElement.GetProperty("items");
            foreach (var item in items.EnumerateArray())
            {
                var snippet = item.GetProperty("snippet");
                var title = snippet.GetProperty("title").GetString() ?? string.Empty;
                var tags = snippet.TryGetProperty("tags", out var tagsEl)
                    ? tagsEl.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).Take(5).ToList()
                    : new List<string>();

                if (string.IsNullOrWhiteSpace(title)) continue;

                topics.Add(new Topic
                {
                    Title = title,
                    Source = "YouTube",
                    Keywords = tags.Any() ? tags : new List<string> { title },
                    CollectedAt = DateTime.UtcNow
                });
            }

            _logger.LogInformation("YouTube에서 {Count}개 주제 수집 완료", topics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube 트렌드 수집 중 오류 발생");
        }

        return topics;
    }
}
