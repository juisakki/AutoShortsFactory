using System.Text.Json;
using AutoShortsFactory.Models;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Setup;

/// <summary>첫 실행 시 대화형 설정 (틈새주제/정보깊이/타겟층/말투) → UserProfile.json 저장</summary>
public class InitialSetup
{
    private const string ProfilePath = "UserProfile.json";
    private readonly ILogger<InitialSetup> _logger;

    public InitialSetup(ILogger<InitialSetup> logger)
    {
        _logger = logger;
    }

    /// <summary>UserProfile.json이 없을 때만 대화형 설정 실행, 있으면 로드만 수행</summary>
    public async Task<UserProfile> RunAsync(CancellationToken ct = default)
    {
        // 이미 설정 파일이 있으면 로드
        if (File.Exists(ProfilePath))
        {
            var existing = await LoadProfileAsync(ct);
            if (existing is not null)
            {
                _logger.LogInformation("기존 UserProfile 로드 완료: {NicheTopic}", existing.NicheTopic);
                return existing;
            }
        }

        // 첫 실행: 대화형 설정
        return await RunInteractiveSetupAsync(ct);
    }

    /// <summary>대화형 콘솔 설정 진행</summary>
    private async Task<UserProfile> RunInteractiveSetupAsync(CancellationToken ct)
    {
        var profile = new UserProfile();

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("   🎬 AutoShortsFactory - 초기 설정");
        Console.WriteLine("   (Enter 입력 시 자동 설정이 적용됩니다)");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine();

        // 1. 틈새 주제
        Console.Write("1. 틈새 주제를 입력하세요 (예: IT, 과학, 역사, 먹방 / Enter=자동): ");
        var niche = Console.ReadLine()?.Trim();
        profile.NicheTopic = string.IsNullOrWhiteSpace(niche) ? string.Empty : niche;
        Console.WriteLine($"   → 틈새 주제: {(string.IsNullOrEmpty(profile.NicheTopic) ? "(자동)" : profile.NicheTopic)}");
        Console.WriteLine();

        // 2. 정보 깊이
        Console.Write("2. 정보 깊이를 입력하세요 (1:가볍게 ~ 5:전문적 / Enter=3): ");
        var depthStr = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(depthStr) && int.TryParse(depthStr, out var depth) && depth >= 1 && depth <= 5)
            profile.InformationDepth = depth;
        else
            profile.InformationDepth = 3;
        Console.WriteLine($"   → 정보 깊이: {profile.InformationDepth}");
        Console.WriteLine();

        // 3. 타겟층
        Console.Write("3. 타겟층을 입력하세요 (예: 10대, 20대, 직장인, 전연령 / Enter=전연령): ");
        var target = Console.ReadLine()?.Trim();
        profile.TargetAudience = string.IsNullOrWhiteSpace(target) ? "전연령" : target;
        Console.WriteLine($"   → 타겟층: {profile.TargetAudience}");
        Console.WriteLine();

        // 4. 말투/스타일
        Console.WriteLine("4. 말투/스타일을 선택하세요:");
        Console.WriteLine("   [1] 친근한 반말   [2] 정중한 존댓말");
        Console.WriteLine("   [3] 유머러스       [4] 전문적/뉴스체");
        Console.Write("   → 선택 (Enter=1 친근한 반말): ");
        var styleStr = Console.ReadLine()?.Trim();

        profile.Style = styleStr switch
        {
            "2" => SpeechStyle.FormalPolite,
            "3" => SpeechStyle.Humorous,
            "4" => SpeechStyle.ProfessionalNews,
            _ => SpeechStyle.FriendlyCasual
        };
        Console.WriteLine($"   → 말투: {profile.StyleDescription}");
        Console.WriteLine();

        // 설정 요약 표시
        Console.WriteLine("───────────────────────────────────────────────────");
        Console.WriteLine("✅ 설정 완료!");
        Console.WriteLine($"   틈새 주제: {(string.IsNullOrEmpty(profile.NicheTopic) ? "(자동)" : profile.NicheTopic)}");
        Console.WriteLine($"   정보 깊이: {profile.InformationDepth}/5");
        Console.WriteLine($"   타겟층  : {profile.TargetAudience}");
        Console.WriteLine($"   말투    : {profile.StyleDescription}");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine();

        // 파일 저장
        await SaveProfileAsync(profile, ct);
        _logger.LogInformation("UserProfile 저장 완료: {Path}", ProfilePath);

        return profile;
    }

    /// <summary>UserProfile.json 저장</summary>
    private static async Task SaveProfileAsync(UserProfile profile, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(ProfilePath, json, ct);
    }

    /// <summary>UserProfile.json 로드</summary>
    private static async Task<UserProfile?> LoadProfileAsync(CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(ProfilePath, ct);
            return JsonSerializer.Deserialize<UserProfile>(json);
        }
        catch
        {
            return null;
        }
    }
}
