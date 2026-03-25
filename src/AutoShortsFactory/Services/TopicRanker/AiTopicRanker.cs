using System.Text;
using System.Text.Json;
using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.TopicRanker;

/// <summary>GPT를 이용한 AI 주제 선별 및 랭킹</summary>
public class AiTopicRanker : ITopicRanker
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<AiTopicRanker> _logger;

    public AiTopicRanker(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        AppDbContext db,
        ILogger<AiTopicRanker> logger)
    {
        _http = httpFactory.CreateClient("OpenAI");
        _config = config;
        _db = db;
        _logger = logger;
    }

    public async Task<List<Topic>> RankAndFilterAsync(
        List<Topic> candidates,
        VideoType videoType,
        CancellationToken ct = default)
    {
        if (!candidates.Any()) return new List<Topic>();

        // SQLite에서 과거 사용 주제 조회 (중복 방지)
        var usedTitles = await _db.TopicHistories
            .Select(h => h.TopicTitle.ToLower())
            .ToListAsync(ct);

        // 이미 사용된 주제 제외
        var newCandidates = candidates
            .Where(t => !usedTitles.Contains(t.Title.ToLower()))
            .ToList();

        if (!newCandidates.Any())
        {
            _logger.LogWarning("모든 후보 주제가 이미 사용됨. 전체 후보에서 재선택");
            newCandidates = candidates;
        }

        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY")
        {
            _logger.LogWarning("OpenAI API 키 미설정. 점수 없이 후보 반환");
            return newCandidates.Take(5).ToList();
        }

        try
        {
            var topicList = string.Join("\n", newCandidates.Take(20).Select((t, i) => $"{i + 1}. {t.Title}"));
            var typeDesc = videoType == VideoType.Shorts ? "유튜브 쇼츠(15~60초)" : "유튜브 롱폼(5~15분)";

            var prompt = $"""
            다음 주제 목록에서 {typeDesc}으로 제작하기 좋은 순서대로 번호를 골라주세요.
            판단 기준:
            1. {typeDesc} 형식에 적합한가
            2. 시청자 관심도가 높은가
            3. 민감하거나 저작권 위험이 없는가
            4. 한국 시청자에게 적합한가
            
            주제 목록:
            {topicList}
            
            응답 형식 (JSON 배열만, 설명 없이):
            [{{"rank": 1, "index": 3, "score": 90}}, {{"rank": 2, "index": 1, "score": 85}}, ...]
            상위 5개만 반환하세요.
            """;

            var ranked = await CallGptAsync(prompt, apiKey, ct);
            if (ranked != null && ranked.Any())
            {
                var result = new List<Topic>();
                foreach (var r in ranked.OrderBy(x => x.Rank))
                {
                    var idx = r.Index - 1;
                    if (idx >= 0 && idx < newCandidates.Count)
                    {
                        newCandidates[idx].Score = r.Score;
                        result.Add(newCandidates[idx]);
                    }
                }
                if (result.Any()) return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI 주제 랭킹 실패. 기본 순서로 반환");
        }

        return newCandidates.Take(5).ToList();
    }

    /// <summary>OpenAI Chat API 호출하여 랭킹 결과 파싱</summary>
    private async Task<List<RankResult>?> CallGptAsync(string prompt, string apiKey, CancellationToken ct)
    {
        var model = _config["OpenAI:Model"] ?? "gpt-4o";
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 300
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "[]";

        // JSON 배열 추출
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        if (start < 0 || end < 0) return null;
        content = content[start..(end + 1)];

        return JsonSerializer.Deserialize<List<RankResult>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private record RankResult(int Rank, int Index, double Score);
}
