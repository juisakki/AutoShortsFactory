using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Quality;

/// <summary>영상 품질 검사 게이트 인터페이스</summary>
public interface IQualityGate
{
    /// <summary>영상 파일의 품질을 검사하고 통과 여부 반환</summary>
    Task<QualityResult> CheckAsync(string videoPath, CancellationToken ct = default);
}

/// <summary>품질 검사 결과</summary>
public class QualityResult
{
    /// <summary>품질 검사 통과 여부</summary>
    public bool Passed { get; set; }

    /// <summary>영상 길이 (초)</summary>
    public double DurationSeconds { get; set; }

    /// <summary>영상 너비 (픽셀)</summary>
    public int Width { get; set; }

    /// <summary>영상 높이 (픽셀)</summary>
    public int Height { get; set; }

    /// <summary>파일 크기 (바이트)</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>오디오 존재 여부</summary>
    public bool HasAudio { get; set; }

    /// <summary>검은화면 비율 (0.0~1.0)</summary>
    public double BlackFrameRatio { get; set; }

    /// <summary>무음 비율 (0.0~1.0)</summary>
    public double SilenceRatio { get; set; }

    /// <summary>실패 사유 목록</summary>
    public List<string> FailureReasons { get; set; } = new();
}
