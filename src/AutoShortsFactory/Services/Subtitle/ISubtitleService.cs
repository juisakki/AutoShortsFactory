using AutoShortsFactory.Models;
using ModelScript = AutoShortsFactory.Models.Script;
namespace AutoShortsFactory.Services.Subtitle;
/// <summary>자막 생성 서비스 인터페이스</summary>
public interface ISubtitleService
{
    Task<string> GenerateSrtAsync(ModelScript script, double totalDurationSeconds, string outputPath, CancellationToken ct = default);
}
