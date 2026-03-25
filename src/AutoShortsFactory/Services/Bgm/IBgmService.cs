namespace AutoShortsFactory.Services.Bgm;
/// <summary>BGM 서비스 인터페이스</summary>
public interface IBgmService
{
    Task<string?> GetBgmFileAsync(CancellationToken ct = default);
}
