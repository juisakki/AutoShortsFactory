using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Bgm;

/// <summary>BGM 폴더에서 랜덤으로 BGM 파일 선택</summary>
public class BgmMixer : IBgmService
{
    private readonly IConfiguration _config;
    private readonly ILogger<BgmMixer> _logger;
    private static readonly Random _rng = new();

    public BgmMixer(IConfiguration config, ILogger<BgmMixer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<string?> GetBgmFileAsync(CancellationToken ct = default)
    {
        var bgmDir = _config["Paths:Bgm"] ?? "./bgm";
        if (!Directory.Exists(bgmDir))
        {
            _logger.LogWarning("BGM 폴더 없음: {Dir}", bgmDir);
            return Task.FromResult<string?>(null);
        }

        var files = Directory.GetFiles(bgmDir, "*.mp3")
            .Concat(Directory.GetFiles(bgmDir, "*.wav"))
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogWarning("BGM 파일 없음 (bgm/ 폴더에 mp3 파일을 추가하세요)");
            return Task.FromResult<string?>(null);
        }

        var selected = files[_rng.Next(files.Length)];
        _logger.LogInformation("BGM 선택: {File}", Path.GetFileName(selected));
        return Task.FromResult<string?>(selected);
    }
}
