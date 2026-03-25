using System.Text;
using System.Text.Json;
using AutoShortsFactory.Models;
using AutoShortsFactory.Services.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Metadata;

/// <summary>OpenAI GPT를 이용한 영상 메타데이터 자동 생성 서비스</summary>
public class AiMetadataService : IMetadataService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AiMetadataService> _logger;

    public AiMetadataService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<AiMetadataService> logger)
    {
        _http = httpFactory.CreateClient("OpenAI");
        _config = config;
        _logger = logger;
    }

    public async Task<VideoMetadata> GenerateAsync(VideoProject project, CancellationToken ct = default)
    {
        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY")
        {
            _logger.LogWarning("OpenAI API 키 미설정. 기본 메타데이터 반환");
            return CreateDefaultMetadata(project);
        }

        try
        {
            var prompt = BuildPrompt(project);
            var model = _config["OpenAI:Model"] ?? "gpt-4o";

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = GetSystemPrompt() },
                    new { role = "user", content = prompt }
                },
                temperature = 0.8,
                max_tokens = 800
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
                .GetString() ?? string.Empty;

            return ParseMetadata(content, project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메타데이터 생성 실패: {Topic}", project.Topic.Title);
            return CreateDefaultMetadata(project);
        }
    }

    /// <summary>시스템 프롬프트</summary>
    private static string GetSystemPrompt() =>
        """
        당신은 유튜브 SEO 전문가입니다.
        영상 메타데이터(제목, 설명, 태그)를 한국어로 작성합니다.
        JSON 형식으로만 응답하세요.
        """;

    /// <summary>메타데이터 생성 프롬프트</summary>
    private static string BuildPrompt(VideoProject project)
    {
        var isShorts = project.VideoType == VideoType.Shorts;
        var scriptSummary = project.Script.FullText.Length > 500
            ? project.Script.FullText[..500] + "..."
            : project.Script.FullText;

        var titleHint = isShorts
            ? "이모지 포함 클릭 유도 제목 + #Shorts"
            : "TOP10 또는 주제 키워드 포함 제목";
        var ruleHint = isShorts
            ? "- 제목에 이모지 포함 필수\n- 태그에 #Shorts 필수"
            : "- 제목에 TOP10 또는 주제 핵심 키워드 포함";
        var videoTypeLabel = isShorts ? "유튜브 쇼츠 (Shorts)" : "유튜브 롱폼";

        return $$"""
        영상 유형: {{videoTypeLabel}}
        주제: {{project.Topic.Title}}
        대본 요약:
        {{scriptSummary}}
        
        다음 JSON 형식으로 메타데이터를 생성하세요:
        {
          "title": "{{titleHint}}",
          "description": "SEO 최적화 설명 (3~5줄, 해시태그 포함)",
          "tags": ["태그1", "태그2", ...],
          "categoryId": "유튜브 카테고리 ID (숫자 문자열)"
        }
        
        규칙:
        {{ruleHint}}
        - 한국어로 작성
        - 태그는 10개 이상
        """;
    }

    /// <summary>GPT 응답에서 메타데이터 파싱</summary>
    private VideoMetadata ParseMetadata(string content, VideoProject project)
    {
        try
        {
            // JSON 블록 추출
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0)
                return CreateDefaultMetadata(project);

            var json = content[jsonStart..(jsonEnd + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            var categoryId = root.TryGetProperty("categoryId", out var c) ? c.GetString() ?? "22" : "22";

            var tags = new List<string>();
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsEl.EnumerateArray())
                {
                    var tagStr = tag.GetString();
                    if (!string.IsNullOrWhiteSpace(tagStr))
                        tags.Add(tagStr);
                }
            }

            // 쇼츠: #Shorts 태그 필수
            if (project.VideoType == VideoType.Shorts && !tags.Contains("#Shorts"))
                tags.Insert(0, "#Shorts");

            return new VideoMetadata
            {
                Title = title,
                Description = description,
                Tags = tags,
                CategoryId = categoryId
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "메타데이터 JSON 파싱 실패");
            return CreateDefaultMetadata(project);
        }
    }

    /// <summary>API 실패 시 기본 메타데이터 생성</summary>
    private static VideoMetadata CreateDefaultMetadata(VideoProject project)
    {
        var isShorts = project.VideoType == VideoType.Shorts;
        var title = isShorts
            ? $"🔥 {project.Topic.Title} #Shorts"
            : $"TOP10 {project.Topic.Title} | 완벽 정리";

        var tags = new List<string>(project.Topic.Keywords)
        {
            project.Topic.Title,
            "유튜브",
            "한국어"
        };

        if (isShorts)
            tags.Insert(0, "#Shorts");

        return new VideoMetadata
        {
            Title = title,
            Description = $"{project.Topic.Title}에 대해 알아봅니다.\n\n{string.Join(" ", tags.Take(10).Select(t => $"#{t.TrimStart('#')}"))}",
            Tags = tags.Take(15).ToList(),
            CategoryId = "22"
        };
    }
}
