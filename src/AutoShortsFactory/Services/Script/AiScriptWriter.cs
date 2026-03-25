using System.Text;
using System.Text.Json;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.ScriptWriter;

/// <summary>GPT를 이용한 AI 대본 생성 서비스</summary>
public class AiScriptWriter : IScriptWriter
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AiScriptWriter> _logger;

    public AiScriptWriter(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<AiScriptWriter> logger)
    {
        _http = httpFactory.CreateClient("OpenAI");
        _config = config;
        _logger = logger;
    }

    public async Task<Script> WriteScriptAsync(
        Topic topic,
        VideoType videoType,
        UserProfile profile,
        LongFormType? longFormType = null,
        CancellationToken ct = default)
    {
        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY")
        {
            _logger.LogWarning("OpenAI API 키 미설정. 샘플 대본 반환");
            return CreateSampleScript(topic, videoType, longFormType);
        }

        try
        {
            var prompt = BuildPrompt(topic, videoType, profile, longFormType);
            var model = _config["OpenAI:Model"] ?? "gpt-4o";

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = BuildSystemPrompt(profile) },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = videoType == VideoType.Shorts ? 500 : 2000
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

            return ParseScript(content, videoType, longFormType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 대본 생성 실패: {Topic}", topic.Title);
            return CreateSampleScript(topic, videoType, longFormType);
        }
    }

    /// <summary>시스템 프롬프트 (사용자 프로필 반영)</summary>
    private static string BuildSystemPrompt(UserProfile profile)
    {
        return $"""
        당신은 유튜브 영상 대본 전문 작가입니다.
        말투: {profile.StyleDescription}
        정보 깊이: {profile.InformationDepth}/5 ({GetDepthDescription(profile.InformationDepth)})
        타겟층: {profile.TargetAudience}
        {(string.IsNullOrEmpty(profile.NicheTopic) ? "" : $"틈새 주제 전문가: {profile.NicheTopic}")}
        
        대본 작성 규칙:
        - 자연스럽게 말하듯 작성
        - 시청자 흥미를 끄는 내용
        - 한국어로 작성
        """;
    }

    /// <summary>정보 깊이 설명</summary>
    private static string GetDepthDescription(int depth) => depth switch
    {
        1 => "매우 가벼운 내용",
        2 => "가벼운 내용",
        3 => "일반적인 깊이",
        4 => "깊은 내용",
        5 => "전문적인 깊이",
        _ => "일반적인 깊이"
    };

    /// <summary>대본 생성 프롬프트 구성</summary>
    private static string BuildPrompt(Topic topic, VideoType videoType, UserProfile profile, LongFormType? longFormType)
    {
        if (videoType == VideoType.Shorts)
        {
            return $"""
            주제: {topic.Title}
            형식: 유튜브 쇼츠 (15~60초 분량)
            
            다음 형식으로 대본을 작성하세요:
            [훅] 시청자의 주목을 끄는 첫 문장 (1~2문장)
            [본문] 핵심 내용 (3~5문장)
            [CTA] 구독/좋아요 유도 문구 (1문장)
            
            각 섹션을 [훅], [본문], [CTA] 태그로 구분하여 작성하세요.
            전체 읽기 시간: 약 30~45초
            """;
        }
        else if (longFormType == LongFormType.Top10)
        {
            return $"""
            주제: {topic.Title} - TOP 10 랭킹
            형식: 유튜브 롱폼 (5~10분 분량)
            
            "N위는 ○○입니다" 구조로 TOP 10 랭킹 대본을 작성하세요.
            [훅] 시청자의 주목을 끄는 첫 문장
            [본문] 10위부터 1위까지 순위별 설명 (각 2~3문장)
            [CTA] 구독/좋아요 유도 문구
            
            각 섹션을 [훅], [본문], [CTA] 태그로 구분하세요.
            """;
        }
        else
        {
            return $"""
            주제: {topic.Title} - 역사/과학 해설
            형식: 유튜브 롱폼 (5~10분 분량)
            
            도입→전개→핵심→마무리 구조로 해설 대본을 작성하세요.
            [훅] 흥미로운 도입부
            [본문] 전개→핵심 내용 (상세 설명)
            [CTA] 구독/좋아요 유도 문구
            
            각 섹션을 [훅], [본문], [CTA] 태그로 구분하세요.
            """;
        }
    }

    /// <summary>GPT 응답에서 대본 파싱</summary>
    private static Script ParseScript(string content, VideoType videoType, LongFormType? longFormType)
    {
        var hook = ExtractSection(content, "[훅]", "[본문]");
        var body = ExtractSection(content, "[본문]", "[CTA]");
        var cta = ExtractSection(content, "[CTA]", null);

        // 본문을 줄 단위로 분리
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var estimatedDuration = videoType == VideoType.Shorts
            ? EstimateDuration(hook + body + cta, 4.5) // 초당 4.5자 (한국어)
            : EstimateDuration(hook + body + cta, 4.5);

        return new Script
        {
            Hook = hook.Trim(),
            Lines = lines,
            Cta = cta.Trim(),
            VideoType = videoType,
            LongFormType = longFormType,
            EstimatedDurationSeconds = Math.Clamp(estimatedDuration,
                videoType == VideoType.Shorts ? 15 : 180,
                videoType == VideoType.Shorts ? 60 : 900)
        };
    }

    /// <summary>섹션 태그 사이 텍스트 추출</summary>
    private static string ExtractSection(string content, string startTag, string? endTag)
    {
        var startIdx = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return content;
        startIdx += startTag.Length;

        if (endTag == null) return content[startIdx..].Trim();

        var endIdx = content.IndexOf(endTag, startIdx, StringComparison.OrdinalIgnoreCase);
        if (endIdx < 0) return content[startIdx..].Trim();

        return content[startIdx..endIdx].Trim();
    }

    /// <summary>텍스트 길이로 재생 시간 추정</summary>
    private static double EstimateDuration(string text, double charsPerSecond)
        => text.Length / charsPerSecond;

    /// <summary>API 키 없을 때 사용하는 샘플 대본</summary>
    private static Script CreateSampleScript(Topic topic, VideoType videoType, LongFormType? longFormType)
    {
        return new Script
        {
            Hook = $"오늘 알아볼 주제는 '{topic.Title}'입니다!",
            Lines = new List<string>
            {
                $"{topic.Title}에 대해 자세히 알아보겠습니다.",
                "이 주제는 요즘 정말 핫하게 떠오르고 있습니다.",
                "함께 살펴봐요!"
            },
            Cta = "구독과 좋아요 부탁드립니다!",
            VideoType = videoType,
            LongFormType = longFormType,
            EstimatedDurationSeconds = videoType == VideoType.Shorts ? 30 : 300
        };
    }
}
