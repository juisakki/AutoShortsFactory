using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Quality;

/// <summary>영상 품질 검사 서비스 인터페이스</summary>
public interface IQualityGate
{
    /// <summary>렌더링된 영상의 품질을 검사하고 결과 반환</summary>
    Task<QualityResult> CheckAsync(VideoProject project, CancellationToken ct = default);
}

/// <summary>품질 검사 결과</summary>
public class QualityResult
{
    /// <summary>검사 통과 여부</summary>
    public bool Passed { get; set; }

    /// <summary>실패 사유 목록</summary>
    public List<string> FailureReasons { get; set; } = new();
}
