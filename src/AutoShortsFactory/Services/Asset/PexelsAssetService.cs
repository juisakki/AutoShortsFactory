using System.Text.Json;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Asset;

/// <summary>Pexels API로 B-Roll 영상 자동 다운로드</summary>
public class PexelsAssetService : IAssetService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PexelsAssetService> _logger;

    public PexelsAssetService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<PexelsAssetService> logger)
    {
        _http = httpFactory.CreateClient("Pexels");
        _config = config;
        _logger = logger;
    }

    public async Task<List<string>> DownloadAssetsAsync(List<string> keywords, VideoType videoType, string outputDir, int count = 5, CancellationToken ct = default)
    {
        var apiKey = _config["Pexels:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_PEXELS_API_KEY")
        {
            _logger.LogWarning("Pexels API 키 미설정. 에셋 다운로드 건너뜀");
            return new List<string>();
        }

        Directory.CreateDirectory(outputDir);
        var downloaded = new List<string>();

        foreach (var keyword in keywords.Take(3))
        {
            if (downloaded.Count >= count) break;
            try
            {
                var orientation = videoType == VideoType.Shorts ? "portrait" : "landscape";
                var url = $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(keyword)}&per_page=3&orientation={orientation}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Authorization", apiKey);
                var resp = await _http.SendAsync(req, ct);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var videos = doc.RootElement.GetProperty("videos");

                foreach (var video in videos.EnumerateArray())
                {
                    if (downloaded.Count >= count) break;
                    var files = video.GetProperty("video_files");
                    string? downloadUrl = null;
                    int bestWidth = 0;

                    foreach (var file in files.EnumerateArray())
                    {
                        var w = file.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
                        if (w >= 720 && w > bestWidth)
                        {
                            bestWidth = w;
                            downloadUrl = file.GetProperty("link").GetString();
                        }
                    }

                    if (downloadUrl == null) continue;

                    var videoId = video.GetProperty("id").GetInt32();
                    var filePath = Path.Combine(outputDir, $"asset_{videoId}.mp4");

                    if (!File.Exists(filePath))
                    {
                        var videoBytes = await _http.GetByteArrayAsync(downloadUrl, ct);
                        await File.WriteAllBytesAsync(filePath, videoBytes, ct);
                    }

                    downloaded.Add(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pexels 에셋 다운로드 실패: {Keyword}", keyword);
            }
        }

        _logger.LogInformation("에셋 {Count}개 다운로드 완료", downloaded.Count);
        return downloaded;
    }
}
