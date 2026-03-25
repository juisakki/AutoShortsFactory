using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using AutoShortsFactory.Services.Asset;
using AutoShortsFactory.Services.Bgm;
using AutoShortsFactory.Services.Metadata;
using AutoShortsFactory.Services.Quality;
using AutoShortsFactory.Services.Render;
using AutoShortsFactory.Services.ScriptWriter;
using AutoShortsFactory.Services.Subtitle;
using AutoShortsFactory.Services.Thumbnail;
using AutoShortsFactory.Services.TopicRanker;
using AutoShortsFactory.Services.Trend;
using AutoShortsFactory.Services.Tts;
using AutoShortsFactory.Services.Upload;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Pipeline;

/// <summary>롱폼 전용 파이프라인 (16:9, 3~15분)</summary>
public class LongFormPipeline : IVideoPipeline
{
    private readonly ITrendCrawler _trendCrawler;
    private readonly ITopicRanker _topicRanker;
    private readonly IScriptWriter _scriptWriter;
    private readonly ITtsService _tts;
    private readonly IAssetService _assets;
    private readonly ISubtitleService _subtitle;
    private readonly IBgmService _bgm;
    private readonly IVideoRenderer _renderer;
    private readonly IQualityGate _qualityGate;
    private readonly IMetadataService _metadata;
    private readonly IThumbnailService _thumbnail;
    private readonly IUploadService _uploader;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<LongFormPipeline> _logger;
    private readonly UserProfile _profile;

    public LongFormPipeline(
        ITrendCrawler trendCrawler,
        ITopicRanker topicRanker,
        IScriptWriter scriptWriter,
        ITtsService tts,
        IAssetService assets,
        ISubtitleService subtitle,
        IBgmService bgm,
        IVideoRenderer renderer,
        IQualityGate qualityGate,
        IMetadataService metadata,
        IThumbnailService thumbnail,
        IUploadService uploader,
        AppDbContext db,
        IConfiguration config,
        ILogger<LongFormPipeline> logger,
        UserProfile profile)
    {
        _trendCrawler = trendCrawler;
        _topicRanker = topicRanker;
        _scriptWriter = scriptWriter;
        _tts = tts;
        _assets = assets;
        _subtitle = subtitle;
        _bgm = bgm;
        _renderer = renderer;
        _qualityGate = qualityGate;
        _metadata = metadata;
        _thumbnail = thumbnail;
        _uploader = uploader;
        _db = db;
        _config = config;
        _logger = logger;
        _profile = profile;
    }

    public async Task<VideoProject> ExecuteAsync(CancellationToken ct = default)
    {
        var project = new VideoProject { VideoType = VideoType.LongForm };
        var outputDir = _config["Paths:Output"] ?? "./output";
        var tempDir = _config["Paths:Temp"] ?? "./temp";
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(tempDir);

        // TOP10 또는 역사/과학 유형 선택 (교대)
        var longFormType = DateTime.Now.DayOfYear % 2 == 0
            ? LongFormType.Top10
            : LongFormType.HistoryScience;
        project.LongFormType = longFormType;

        try
        {
            // 1. 트렌드 수집
            _logger.LogInformation("[롱폼] 1/12 트렌드 수집 중...");
            var topics = await _trendCrawler.GetTrendingTopicsAsync(ct);

            // 2. 주제 선정 (롱폼용)
            _logger.LogInformation("[롱폼] 2/12 AI 주제 선정 중 ({Type})...", longFormType);
            var ranked = await _topicRanker.RankAndFilterAsync(topics, VideoType.LongForm, ct);
            if (!ranked.Any())
                throw new InvalidOperationException("선정된 주제가 없습니다.");
            project.Topic = ranked.First();
            project.Status = VideoStatus.Pending;

            // 3. 대본 생성 (TOP10 or 역사/과학)
            _logger.LogInformation("[롱폼] 3/12 AI 대본 생성 중: {Title}", project.Topic.Title);
            project.Script = await _scriptWriter.WriteScriptAsync(
                project.Topic, VideoType.LongForm, _profile, longFormType, ct);
            project.Status = VideoStatus.ScriptGenerated;

            // 4. TTS 생성
            _logger.LogInformation("[롱폼] 4/12 TTS 음성 생성 중...");
            var audioPath = Path.Combine(tempDir, $"{project.Id}_audio.mp3");
            (project.AudioPath, project.AudioDurationSeconds) =
                await _tts.GenerateAudioAsync(project.Script.FullText, audioPath, ct);
            project.Status = VideoStatus.TtsGenerated;

            // 5. B-Roll 에셋 다운로드 (롱폼은 더 많은 클립)
            _logger.LogInformation("[롱폼] 5/12 B-Roll 에셋 다운로드 중 (다중 클립)...");
            var assetDir = Path.Combine(tempDir, project.Id);
            Directory.CreateDirectory(assetDir);
            project.AssetPaths = await _assets.DownloadAssetsAsync(
                project.Topic.Keywords, VideoType.LongForm, assetDir, count: 15, ct);
            project.Status = VideoStatus.AssetsDownloaded;

            // 6. 자막 생성
            _logger.LogInformation("[롱폼] 6/12 자막 생성 중...");
            var srtPath = Path.Combine(tempDir, $"{project.Id}_subtitle.srt");
            project.SubtitlePath = await _subtitle.GenerateSrtAsync(
                project.Script, project.AudioDurationSeconds, srtPath, ct);

            // 7. BGM 선택
            _logger.LogInformation("[롱폼] 7/12 BGM 선택 중...");
            project.BgmPath = await _bgm.GetBgmFileAsync(ct);

            // 8. 렌더링 (1920x1080, 전환 효과 포함)
            _logger.LogInformation("[롱폼] 8/12 영상 렌더링 중 (1920x1080)...");
            var videoPath = Path.Combine(outputDir, $"{project.Id}_longform.mp4");
            project.OutputVideoPath = await _renderer.RenderAsync(project, videoPath, ct);
            project.Status = VideoStatus.Rendered;

            // 9. 품질 검사
            _logger.LogInformation("[롱폼] 9/12 품질 검사 중...");
            var quality = await _qualityGate.CheckAsync(project, ct);
            if (!quality.Passed)
            {
                project.Status = VideoStatus.QualityFailed;
                project.FailureReason = string.Join("; ", quality.FailureReasons);
                throw new InvalidOperationException($"품질 검사 실패: {project.FailureReason}");
            }
            project.Status = VideoStatus.QualityPassed;

            // 10. 메타데이터 생성
            _logger.LogInformation("[롱폼] 10/12 AI 메타데이터 생성 중...");
            var meta = await _metadata.GenerateAsync(project, ct);
            project.GeneratedTitle = meta.Title;
            project.GeneratedDescription = meta.Description;
            project.GeneratedTags = meta.Tags;

            // 11. 썸네일 생성
            _logger.LogInformation("[롱폼] 11/12 썸네일 생성 중...");
            var thumbPath = Path.Combine(outputDir, $"{project.Id}_thumb.jpg");
            project.ThumbnailPath = await _thumbnail.GenerateAsync(project, thumbPath, ct);

            // 12. 유튜브 업로드
            _logger.LogInformation("[롱폼] 12/12 유튜브 업로드 중...");
            project.UploadUrl = await _uploader.UploadAsync(project, ct);
            project.Status = VideoStatus.Uploaded;

            _logger.LogInformation("[롱폼] 파이프라인 완료: {Title} → {Url}",
                project.GeneratedTitle, project.UploadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[롱폼] 파이프라인 실패: {Stage}", project.Status);
            if (project.Status != VideoStatus.QualityFailed)
                project.Status = VideoStatus.Failed;
            project.FailureReason ??= ex.Message;
        }
        finally
        {
            await SaveUploadRecordAsync(project);
        }

        return project;
    }

    private async Task SaveUploadRecordAsync(VideoProject project)
    {
        try
        {
            _db.UploadRecords.Add(new UploadRecord
            {
                ProjectId = project.Id,
                Title = project.GeneratedTitle ?? project.Topic.Title,
                VideoType = project.VideoType,
                Success = project.Status == VideoStatus.Uploaded,
                YouTubeUrl = project.UploadUrl,
                FailureReason = project.FailureReason
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업로드 이력 DB 저장 실패");
        }
    }
}
