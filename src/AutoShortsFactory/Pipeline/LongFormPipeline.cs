using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using AutoShortsFactory.Services.Asset;
using AutoShortsFactory.Services.Bgm;
using AutoShortsFactory.Services.Metadata;
using AutoShortsFactory.Services.Quality;
using AutoShortsFactory.Services.Render;
using AutoShortsFactory.Services.Report;
using AutoShortsFactory.Services.Script;
using AutoShortsFactory.Services.Subtitle;
using AutoShortsFactory.Services.Thumbnail;
using AutoShortsFactory.Services.TopicRanker;
using AutoShortsFactory.Services.Trend;
using AutoShortsFactory.Services.Tts;
using AutoShortsFactory.Services.Upload;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Pipeline;

/// <summary>롱폼(TOP10 랭킹 / 역사·과학 해설) 전용 영상 생성 파이프라인</summary>
public class LongFormPipeline : IVideoPipeline
{
    private readonly ITrendCrawler _trendCrawler;
    private readonly ITopicRanker _topicRanker;
    private readonly IScriptWriter _scriptWriter;
    private readonly ITtsService _ttsService;
    private readonly IAssetService _assetService;
    private readonly ISubtitleService _subtitleService;
    private readonly IBgmService _bgmService;
    private readonly IVideoRenderer _renderer;
    private readonly IThumbnailService _thumbnailService;
    private readonly IQualityGate _qualityGate;
    private readonly IMetadataService _metadataService;
    private readonly IUploadService _uploadService;
    private readonly IReportService _reportService;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly UserProfile _userProfile;
    private readonly ILogger<LongFormPipeline> _logger;

    public LongFormPipeline(
        ITrendCrawler trendCrawler,
        ITopicRanker topicRanker,
        IScriptWriter scriptWriter,
        ITtsService ttsService,
        IAssetService assetService,
        ISubtitleService subtitleService,
        IBgmService bgmService,
        IVideoRenderer renderer,
        IThumbnailService thumbnailService,
        IQualityGate qualityGate,
        IMetadataService metadataService,
        IUploadService uploadService,
        IReportService reportService,
        AppDbContext db,
        IConfiguration config,
        UserProfile userProfile,
        ILogger<LongFormPipeline> logger)
    {
        _trendCrawler = trendCrawler;
        _topicRanker = topicRanker;
        _scriptWriter = scriptWriter;
        _ttsService = ttsService;
        _assetService = assetService;
        _subtitleService = subtitleService;
        _bgmService = bgmService;
        _renderer = renderer;
        _thumbnailService = thumbnailService;
        _qualityGate = qualityGate;
        _metadataService = metadataService;
        _uploadService = uploadService;
        _reportService = reportService;
        _db = db;
        _config = config;
        _userProfile = userProfile;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<VideoProject> RunAsync(
        VideoType videoType,
        LongFormType? longFormType = null,
        CancellationToken ct = default)
    {
        // 롱폼 유형 결정 (교대 방식: 짝수일=TOP10, 홀수일=역사/과학)
        var resolvedLongFormType = longFormType
            ?? (DateTime.Today.Day % 2 == 0 ? LongFormType.Top10 : LongFormType.HistoryScience);

        var project = new VideoProject
        {
            VideoType = VideoType.LongForm,
            LongFormType = resolvedLongFormType
        };
        var workDir = CreateWorkDirectory(project.Id);

        try
        {
            _logger.LogInformation("🎬 롱폼 파이프라인 시작: {Type}, ProjectId={Id}",
                resolvedLongFormType, project.Id);

            // 1. 트렌드 수집 및 주제 선정
            var candidates = await _trendCrawler.GetTrendingTopicsAsync(ct);
            var ranked = await _topicRanker.RankAndFilterAsync(candidates, VideoType.LongForm, ct);
            project.Topic = ranked.FirstOrDefault() ?? new Topic { Title = "이번 주 TOP10 화제" };

            _logger.LogInformation("선정 주제: {Topic}", project.Topic.Title);

            // 2. 대본 생성 (롱폼: TOP10 목록 또는 역사/과학 해설)
            project.Script = await _scriptWriter.WriteScriptAsync(
                project.Topic, VideoType.LongForm, _userProfile, resolvedLongFormType, ct);
            project.Status = VideoStatus.ScriptGenerated;

            // 3. TTS 생성 (롱폼은 분량이 많음)
            var audioPath = Path.Combine(workDir, "audio.mp3");
            (project.AudioPath, project.AudioDurationSeconds) =
                await _ttsService.GenerateAudioAsync(project.Script.FullText, audioPath, ct);
            project.Status = VideoStatus.TtsGenerated;

            // 4. B-Roll 에셋 다운로드 (롱폼은 더 많은 에셋 필요)
            project.AssetPaths = await _assetService.DownloadAssetsAsync(
                project.Topic.Keywords, VideoType.LongForm, workDir, count: 15, ct);
            project.Status = VideoStatus.AssetsDownloaded;

            // 5. 자막 생성
            var subtitlePath = Path.Combine(workDir, "subtitles.srt");
            project.SubtitlePath = await _subtitleService.GenerateSrtAsync(
                project.Script, project.AudioDurationSeconds, subtitlePath, ct);

            // 6. BGM 선택
            project.BgmPath = await _bgmService.GetBgmFileAsync(ct);

            // 7. 영상 렌더링 (롱폼: 1920x1080)
            var outputPath = Path.Combine(workDir, "output.mp4");
            project.OutputVideoPath = await _renderer.RenderAsync(project, outputPath, ct);
            project.Status = VideoStatus.Rendered;

            // 8. 썸네일 생성
            var thumbPath = Path.Combine(workDir, "thumbnail.jpg");
            project.ThumbnailPath = await _thumbnailService.GenerateAsync(project, thumbPath, ct);

            // 9. 품질 검사
            var qualityResult = await _qualityGate.CheckAsync(project.OutputVideoPath!, ct);
            if (!qualityResult.Passed)
            {
                project.Status = VideoStatus.QualityFailed;
                project.FailureReason = string.Join("; ", qualityResult.FailureReasons);
                _logger.LogWarning("롱폼 품질 검사 실패: {Reasons}", project.FailureReason);
                RecordFailure(project);
                return project;
            }
            project.Status = VideoStatus.QualityPassed;

            // 10. AI 메타데이터 생성
            var metadata = await _metadataService.GenerateAsync(project, ct);
            project.GeneratedTitle = metadata.Title;
            project.GeneratedDescription = metadata.Description;
            project.GeneratedTags = metadata.Tags;

            // 11. 유튜브 업로드 (롱폼: 즉시 공개)
            project.YouTubeVideoId = await _uploadService.UploadAsync(project, null, ct);
            project.UploadUrl = $"https://youtu.be/{project.YouTubeVideoId}";

            // 12. 썸네일 업로드
            if (!string.IsNullOrWhiteSpace(project.ThumbnailPath))
            {
                await _uploadService.SetThumbnailAsync(project.YouTubeVideoId!, project.ThumbnailPath, ct);
            }

            project.Status = VideoStatus.Uploaded;
            _logger.LogInformation("✅ 롱폼 업로드 완료: {Url}", project.UploadUrl);

            // 13. DB 기록
            await SaveUploadRecordAsync(project, true, ct);
            await SaveTopicHistoryAsync(project.Topic, ct);

            // 14. 리포트 항목 추가
            _reportService.AddEntry(new ReportEntry
            {
                Title = project.GeneratedTitle ?? project.Topic.Title,
                VideoType = VideoType.LongForm,
                Success = true,
                UploadUrl = project.UploadUrl
            });

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "롱폼 파이프라인 오류: {ProjectId}", project.Id);
            project.Status = VideoStatus.Failed;
            project.FailureReason = ex.Message;
            RecordFailure(project);
            return project;
        }
    }

    private void RecordFailure(VideoProject project)
    {
        _reportService.AddEntry(new ReportEntry
        {
            Title = project.GeneratedTitle ?? project.Topic?.Title ?? "Unknown",
            VideoType = VideoType.LongForm,
            Success = false,
            FailureReason = project.FailureReason
        });
    }

    private async Task SaveUploadRecordAsync(VideoProject project, bool success, CancellationToken ct)
    {
        _db.UploadRecords.Add(new UploadRecord
        {
            ProjectId = project.Id,
            Title = project.GeneratedTitle ?? project.Topic.Title,
            VideoType = project.VideoType,
            Success = success,
            YouTubeUrl = project.UploadUrl,
            FailureReason = project.FailureReason,
            RetryCount = project.RetryCount
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveTopicHistoryAsync(Topic topic, CancellationToken ct)
    {
        _db.TopicHistories.Add(new TopicHistory
        {
            TopicTitle = topic.Title,
            VideoType = VideoType.LongForm
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string CreateWorkDirectory(string projectId)
    {
        var dir = Path.Combine("workspace", "longform", projectId);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
