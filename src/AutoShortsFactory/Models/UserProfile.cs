namespace AutoShortsFactory.Models;

/// <summary>사용자 프로필 (첫 실행 시 설정, UserProfile.json에 저장)</summary>
public class UserProfile
{
    /// <summary>틈새 주제 (예: IT, 과학, 역사)</summary>
    public string NicheTopic { get; set; } = string.Empty;

    /// <summary>정보 깊이 (1:가볍게 ~ 5:전문적)</summary>
    public int InformationDepth { get; set; } = 3;

    /// <summary>타겟층 (예: 10대, 20대, 전연령)</summary>
    public string TargetAudience { get; set; } = "전연령";

    /// <summary>말투/스타일</summary>
    public SpeechStyle Style { get; set; } = SpeechStyle.FriendlyCasual;

    /// <summary>말투 설명 문자열 반환</summary>
    public string StyleDescription => Style switch
    {
        SpeechStyle.FriendlyCasual => "친근한 반말",
        SpeechStyle.FormalPolite => "정중한 존댓말",
        SpeechStyle.Humorous => "유머러스",
        SpeechStyle.ProfessionalNews => "전문적/뉴스체",
        _ => "친근한 반말"
    };
}
