namespace AutoShortsFactory.Services.Asset;
using AutoShortsFactory.Models;
/// <summary>B-Roll 에셋 다운로드 서비스 인터페이스</summary>
public interface IAssetService
{
    Task<List<string>> DownloadAssetsAsync(List<string> keywords, VideoType videoType, string outputDir, int count = 5, CancellationToken ct = default);
}
