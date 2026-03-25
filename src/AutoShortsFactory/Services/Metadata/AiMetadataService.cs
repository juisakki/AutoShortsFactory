using System.Text;
using System.Text.Json;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Metadata;

/// <summary>GPT 기반 유튜브 메타데이터(제목/설명/해시태그/카테고리) 자동 생성 서비스</summary>
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

    /// <inheritdoc/>
    public async Task<VideoMetadata> GenerateAsync(
        VideoProject project,
        CancellationToken ct = default)
    {
        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY")
        {
            _logger.LogWarning("OpenAI API 키 미설정. 기본 메타데이터 반환");
            return CreateDefaultMetadata(project);
        }

        try
        {
            var model = _config["OpenAI:Model"] ?? "gpt-4o";
            var prompt = BuildPrompt(project);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "당신은 유튜브 SEO 전문가입니다. 클릭율과 검색 노출을 최대화하는 제목과 설명, 해시태그를 생성합니다."
                    },
                    new { role = "user", content = prompt }
                },
                temperature = 0.8,
                max_tokens = 600
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(req, ct);
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
            _logger.LogError(ex, "메타데이터 생성 중 오류 발생");
            return CreateDefaultMetadata(project);
        }
    }

    /// <summary>GPT 프롬프트 구성</summary>
    private static string BuildPrompt(VideoProject project)
    {
        var videoTypeText = project.VideoType == VideoType.Shorts ? "유튜브 쇼츠(60초 이하)" : "유튜브 롱폼";
        var sb = new StringBuilder();
        sb.AppendLine($"다음 {videoTypeText} 영상에 대한 유튜브 메타데이터를 생성해주세요.");
        sb.AppendLine();
        sb.AppendLine($"주제: {project.Topic.Title}");
        sb.AppendLine($"대본 요약: {project.Script.Hook} {string.Join(" ", project.Script.Lines.Take(3))}");
        sb.AppendLine();
        sb.AppendLine("아래 형식으로만 응답하세요 (다른 내용 없이):");
        sb.AppendLine("TITLE: [클릭유도 제목 - 50자 이내]");
        sb.AppendLine("DESCRIPTION: [SEO 최적화 설명 - 150~200자, 키워드 포함, 마지막에 구독/좋아요 CTA]");
        sb.AppendLine("TAGS: [태그1, 태그2, ... (10~15개, #없이)]");
        sb.AppendLine("CATEGORY: [유튜브 카테고리 ID (숫자만, 예: 22=사람과블로그, 28=과학기술, 24=엔터테인먼트, 25=뉴스정치)]");

        if (project.VideoType == VideoType.Shorts)
            sb.AppendLine("\n참고: 쇼츠 전용 태그 'Shorts'를 반드시 포함하세요.");

        return sb.ToString();
    }

    /// <summary>GPT 응답 파싱</summary>
    private VideoMetadata ParseMetadata(string content, VideoProject project)
    {
        var metadata = new VideoMetadata();

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Title = line["TITLE:".Length..].Trim();
            }
            else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Description = line["DESCRIPTION:".Length..].Trim();
            }
            else if (line.StartsWith("TAGS:", StringComparison.OrdinalIgnoreCase))
            {
                var tagsStr = line["TAGS:".Length..].Trim();
                metadata.Tags = tagsStr
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().TrimStart('#'))
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }
            else if (line.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase))
            {
                var catStr = line["CATEGORY:".Length..].Trim();
                // 숫자만 추출
                var digits = new string(catStr.TakeWhile(char.IsDigit).ToArray());
                if (!string.IsNullOrEmpty(digits))
                    metadata.CategoryId = digits;
            }
        }

        // 제목 미파싱 시 기본값
        if (string.IsNullOrWhiteSpace(metadata.Title))
            metadata.Title = project.Topic.Title;

        // 쇼츠: Shorts 태그 보장
        if (project.VideoType == VideoType.Shorts &&
            !metadata.Tags.Contains("Shorts", StringComparer.OrdinalIgnoreCase))
        {
            metadata.Tags.Insert(0, "Shorts");
        }

        return metadata;
    }

    /// <summary>API 키 없을 때 기본 메타데이터 생성</summary>
    private static VideoMetadata CreateDefaultMetadata(VideoProject project)
    {
        var tags = new List<string>(project.Topic.Keywords.Take(10));

        if (project.VideoType == VideoType.Shorts &&
            !tags.Contains("Shorts", StringComparer.OrdinalIgnoreCase))
        {
            tags.Insert(0, "Shorts");
        }

        return new VideoMetadata
        {
            Title = project.Topic.Title,
            Description = $"{project.Script.Hook}\n\n{string.Join("\n", project.Script.Lines.Take(5))}\n\n#구독 #좋아요",
            Tags = tags,
            CategoryId = "22"
        };
    }
}
