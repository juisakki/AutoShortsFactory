namespace AutoShortsFactory.Models;

/// <summary>영상 프로젝트 전체 모델 (주제, 대본, 에셋, 상태 등)</summary>
public class VideoProject
{
    /// <summary>프로젝트 고유 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>선정된 트렌드 주제</summary>
    public Topic Topic { get; set; } = new();

    /// <summary>AI 생성 대본</summary>
    public Script Script { get; set; } = new();

    /// <summary>영상 유형 (쇼츠 / 롱폼)</summary>
    public VideoType VideoType { get; set; }

    /// <summary>롱폼 세부 유형</summary>
    public LongFormType? LongFormType { get; set; }

    /// <summary>TTS 오디오 파일 경로</summary>
    public string? AudioPath { get; set; }

    /// <summary>TTS 오디오 재생 시간 (초)</summary>
    public double AudioDurationSeconds { get; set; }

    /// <summary>B-Roll 에셋 파일 경로 목록</summary>
    public List<string> AssetPaths { get; set; } = new();

    /// <summary>SRT 자막 파일 경로</summary>
    public string? SubtitlePath { get; set; }

    /// <summary>렌더링된 영상 파일 경로</summary>
    public string? OutputVideoPath { get; set; }

    /// <summary>썸네일 이미지 파일 경로</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>BGM 파일 경로</summary>
    public string? BgmPath { get; set; }

    /// <summary>현재 처리 상태</summary>
    public VideoStatus Status { get; set; } = VideoStatus.Pending;

    /// <summary>유튜브 업로드 URL</summary>
    public string? UploadUrl { get; set; }

    /// <summary>유튜브 동영상 ID</summary>
    public string? YouTubeVideoId { get; set; }

    /// <summary>AI 생성 제목</summary>
    public string? GeneratedTitle { get; set; }

    /// <summary>AI 생성 설명</summary>
    public string? GeneratedDescription { get; set; }

    /// <summary>AI 생성 태그/해시태그 목록</summary>
    public List<string> GeneratedTags { get; set; } = new();

    /// <summary>실패 사유</summary>
    public string? FailureReason { get; set; }

    /// <summary>재시도 횟수</summary>
    public int RetryCount { get; set; }

    /// <summary>프로젝트 생성 시각 (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
