using AutoShortsFactory.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoShortsFactory.Services.Upload;

/// <summary>YouTube Data API v3를 이용한 영상 업로드 서비스</summary>
public class YouTubeUploadService : IUploadService
{
    private readonly IConfiguration _config;
    private readonly ILogger<YouTubeUploadService> _logger;

    public YouTubeUploadService(IConfiguration config, ILogger<YouTubeUploadService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> UploadAsync(VideoProject project, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(project.OutputVideoPath) || !File.Exists(project.OutputVideoPath))
            throw new FileNotFoundException("업로드할 영상 파일이 없습니다.", project.OutputVideoPath);

        _logger.LogInformation("유튜브 업로드 시작: {Title}", project.GeneratedTitle);

        var youtubeService = await CreateYouTubeServiceAsync(ct);

        // 영상 메타데이터 구성
        var video = BuildVideoResource(project);

        using var fileStream = new FileStream(project.OutputVideoPath, FileMode.Open, FileAccess.Read);
        var insertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");

        // 업로드 진행률 로깅
        insertRequest.ProgressChanged += progress =>
        {
            switch (progress.Status)
            {
                case UploadStatus.Uploading:
                    _logger.LogInformation("업로드 진행중: {Bytes} bytes 전송됨", progress.BytesSent);
                    break;
                case UploadStatus.Failed:
                    _logger.LogError("업로드 실패: {Exception}", progress.Exception?.Message);
                    break;
            }
        };

        insertRequest.ResponseReceived += uploadedVideo =>
        {
            _logger.LogInformation("업로드 완료: VideoId={VideoId}", uploadedVideo.Id);
        };

        var uploadResponse = await insertRequest.UploadAsync(ct);

        if (uploadResponse.Status == UploadStatus.Failed)
            throw new Exception($"유튜브 업로드 실패: {uploadResponse.Exception?.Message}");

        var videoId = insertRequest.ResponseBody?.Id
            ?? throw new Exception("업로드 후 VideoId를 가져오지 못했습니다.");

        // 썸네일 설정
        if (!string.IsNullOrWhiteSpace(project.ThumbnailPath) && File.Exists(project.ThumbnailPath))
        {
            await SetThumbnailAsync(youtubeService, videoId, project.ThumbnailPath, ct);
        }

        var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
        _logger.LogInformation("유튜브 업로드 성공: {Url}", videoUrl);
        return videoUrl;
    }

    /// <summary>YouTube 서비스 인스턴스 생성 (OAuth 2.0 인증)</summary>
    private async Task<YouTubeService> CreateYouTubeServiceAsync(CancellationToken ct)
    {
        var clientSecretPath = _config["YouTube:ClientSecretPath"] ?? "client_secret.json";
        var appName = _config["YouTube:ApplicationName"] ?? "AutoShortsFactory";

        if (!File.Exists(clientSecretPath))
            throw new FileNotFoundException($"client_secret.json 파일이 없습니다: {clientSecretPath}");

        using var stream = new FileStream(clientSecretPath, FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { YouTubeService.Scope.YoutubeUpload },
            "user",
            ct,
            new FileDataStore("YouTube.AutoShortsFactory", true));

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = appName
        });
    }

    /// <summary>업로드할 Video 리소스 구성</summary>
    private static Video BuildVideoResource(VideoProject project)
    {
        var title = project.GeneratedTitle ?? project.Topic.Title;
        var description = project.GeneratedDescription ?? string.Empty;
        var tags = project.GeneratedTags.Any()
            ? project.GeneratedTags
            : new List<string> { project.Topic.Title };

        // 쇼츠: #Shorts 태그 필수 포함
        if (project.VideoType == VideoType.Shorts && !tags.Contains("#Shorts"))
            tags = tags.Prepend("#Shorts").ToList();

        return new Video
        {
            Snippet = new VideoSnippet
            {
                Title = title,
                Description = description,
                Tags = tags,
                CategoryId = "22" // People & Blogs
            },
            Status = new Google.Apis.YouTube.v3.Data.VideoStatus
            {
                PrivacyStatus = "public"
            }
        };
    }

    /// <summary>썸네일 설정</summary>
    private async Task SetThumbnailAsync(YouTubeService service, string videoId, string thumbnailPath, CancellationToken ct)
    {
        try
        {
            using var thumbStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read);
            var request = service.Thumbnails.Set(videoId, thumbStream, "image/jpeg");
            await request.UploadAsync(ct);
            _logger.LogInformation("썸네일 설정 완료: VideoId={VideoId}", videoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "썸네일 설정 실패 (업로드는 성공): VideoId={VideoId}", videoId);
        }
    }
}
