namespace AutoShortsFactory.Models;

/// <summary>일일 생산/업로드 결과 리포트</summary>
public class DailyReport
{
    /// <summary>리포트 날짜</summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>시도 총 건수</summary>
    public int TotalAttempted { get; set; }

    /// <summary>성공 건수</summary>
    public int SuccessCount { get; set; }

    /// <summary>실패 건수</summary>
    public int FailureCount { get; set; }

    /// <summary>개별 항목 목록</summary>
    public List<ReportEntry> Entries { get; set; } = new();
}

/// <summary>리포트 개별 항목</summary>
public class ReportEntry
{
    /// <summary>영상 제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>영상 유형</summary>
    public VideoType VideoType { get; set; }

    /// <summary>성공 여부</summary>
    public bool Success { get; set; }

    /// <summary>유튜브 업로드 URL</summary>
    public string? UploadUrl { get; set; }

    /// <summary>실패 사유</summary>
    public string? FailureReason { get; set; }
}
