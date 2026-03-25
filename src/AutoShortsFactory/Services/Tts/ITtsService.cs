namespace AutoShortsFactory.Services.Tts;
/// <summary>TTS 서비스 인터페이스</summary>
public interface ITtsService
{
    Task<(string audioPath, double durationSeconds)> GenerateAudioAsync(string text, string outputPath, CancellationToken ct = default);
}
