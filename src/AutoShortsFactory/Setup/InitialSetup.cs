using System.Text.Json;
using AutoShortsFactory.Models;

namespace AutoShortsFactory.Setup;

/// <summary>첫 실행 시 콘솔 대화형 초기 설정</summary>
public class InitialSetup
{
    private const string ProfilePath = "UserProfile.json";

    /// <summary>UserProfile 로드 또는 대화형으로 생성</summary>
    public static async Task<UserProfile> RunAsync()
    {
        // 이미 설정 파일이 있으면 불러오기
        if (File.Exists(ProfilePath))
        {
            var existing = await LoadProfileAsync();
            if (existing != null)
            {
                Console.WriteLine($"✅ 기존 설정을 불러옵니다: {ProfilePath}");
                return existing;
            }
        }

        // 첫 실행 대화형 설정
        return await RunInteractiveSetupAsync();
    }

    /// <summary>대화형 콘솔 설정 진행</summary>
    private static async Task<UserProfile> RunInteractiveSetupAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("   🎬 AutoShortsFactory - 초기 설정");
        Console.WriteLine("═══════════════════════════════════════════");

        var profile = new UserProfile();

        // 1. 틈새 주제
        Console.WriteLine("1. 틈새 주제 (예: IT, 과학, 역사, 먹방...)");
        Console.Write("   → 입력 또는 Enter(자동): ");
        var niche = Console.ReadLine()?.Trim() ?? string.Empty;
        profile.NicheTopic = niche;

        // 2. 정보 깊이
        Console.WriteLine("2. 정보 깊이 (1:가볍게 ~ 5:전문적)");
        Console.Write("   → 입력 또는 Enter(자동=3): ");
        var depthInput = Console.ReadLine()?.Trim();
        profile.InformationDepth = int.TryParse(depthInput, out var depth) && depth >= 1 && depth <= 5
            ? depth
            : 3;

        // 3. 타겟층
        Console.WriteLine("3. 타겟층 (예: 10대, 20대, 직장인, 전연령...)");
        Console.Write("   → 입력 또는 Enter(자동=전연령): ");
        var target = Console.ReadLine()?.Trim();
        profile.TargetAudience = string.IsNullOrWhiteSpace(target) ? "전연령" : target;

        // 4. 말투/스타일
        Console.WriteLine("4. 말투/스타일");
        Console.WriteLine("   [1] 친근한 반말  [2] 정중한 존댓말");
        Console.WriteLine("   [3] 유머러스    [4] 전문적/뉴스체");
        Console.Write("   → 선택 또는 Enter(자동=1): ");
        var styleInput = Console.ReadLine()?.Trim();
        profile.Style = int.TryParse(styleInput, out var style) && style >= 1 && style <= 4
            ? (SpeechStyle)style
            : SpeechStyle.FriendlyCasual;

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("✅ 설정 완료!");
        Console.WriteLine($"  틈새 주제: {(string.IsNullOrEmpty(profile.NicheTopic) ? "(자동)" : profile.NicheTopic)}");
        Console.WriteLine($"  정보 깊이: {profile.InformationDepth}/5");
        Console.WriteLine($"  타겟층:    {profile.TargetAudience}");
        Console.WriteLine($"  말투:      {profile.StyleDescription}");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        await SaveProfileAsync(profile);
        return profile;
    }

    /// <summary>프로필을 JSON 파일로 저장</summary>
    private static async Task SaveProfileAsync(UserProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(ProfilePath, json);
        Console.WriteLine($"💾 설정이 {ProfilePath}에 저장되었습니다.");
    }

    /// <summary>JSON 파일에서 프로필 로드</summary>
    private static async Task<UserProfile?> LoadProfileAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(ProfilePath);
            return JsonSerializer.Deserialize<UserProfile>(json);
        }
        catch
        {
            return null;
        }
    }
}
