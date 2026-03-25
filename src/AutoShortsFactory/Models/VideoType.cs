namespace AutoShortsFactory.Models;

/// <summary>영상 유형 (쇼츠 / 롱폼)</summary>
public enum VideoType
{
    Shorts,
    LongForm
}

/// <summary>롱폼 영상 세부 유형</summary>
public enum LongFormType
{
    Top10,
    HistoryScience
}

/// <summary>사용자 말투/스타일</summary>
public enum SpeechStyle
{
    /// <summary>친근한 반말</summary>
    FriendlyCasual = 1,
    /// <summary>정중한 존댓말</summary>
    FormalPolite = 2,
    /// <summary>유머러스</summary>
    Humorous = 3,
    /// <summary>전문적/뉴스체</summary>
    ProfessionalNews = 4
}

/// <summary>영상 프로젝트 처리 상태</summary>
public enum VideoStatus
{
    Pending,
    ScriptGenerated,
    TtsGenerated,
    AssetsDownloaded,
    Rendered,
    QualityPassed,
    QualityFailed,
    Uploaded,
    Failed
}
