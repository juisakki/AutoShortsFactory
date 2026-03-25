using AutoShortsFactory.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YouTubeVideoStatus = Google.Apis.YouTube.v3.Data.VideoStatus;

namespace AutoShortsFactory.Services.Upload;

/// <summary>YouTube Data API v3 OAuth 2.0 기반 영상 업로드 서비스</summary>
public class YouTubeUploadService : IUploadService
{
    private readonly IConfiguration _config;
    private readonly ILogger<YouTubeUploadService> _logger;

    public YouTubeUploadService(
        IConfiguration config,
        ILogger<YouTubeUploadService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        VideoProject project,
        DateTime? scheduledAt = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("유튜브 업로드 시작: {Title}", project.GeneratedTitle ?? project.Topic.Title);

        var youtubeService = await GetYouTubeServiceAsync(ct);

        // 영상 메타데이터 구성
        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = project.GeneratedTitle ?? project.Topic.Title,
                Description = project.GeneratedDescription ?? string.Empty,
                Tags = BuildTags(project),
                CategoryId = "22", // People & Blogs
                DefaultLanguage = "ko",
                DefaultAudioLanguage = "ko"
            },
            Status = new YouTubeVideoStatus
            {
                PrivacyStatus = DeterminePrivacy(scheduledAt),
                PublishAt = scheduledAt?.ToUniversalTime(),
                SelfDeclaredMadeForKids = false
            }
        };

        // 영상 파일 업로드
        var videoPath = project.OutputVideoPath
            ?? throw new InvalidOperationException("업로드할 영상 파일 경로가 없습니다.");

        string videoId;
        await using (var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read))
        {
            var insertRequest = youtubeService.Videos.Insert(
                video,
                "snippet,status",
                fileStream,
                "video/*");

            insertRequest.ProgressChanged += (progress) =>
            {
                switch (progress.Status)
                {
                    case UploadStatus.Uploading:
                        _logger.LogDebug("업로드 중: {BytesSent} 바이트 전송", progress.BytesSent);
                        break;
                    case UploadStatus.Failed:
                        _logger.LogError("업로드 실패: {Exception}", progress.Exception);
                        break;
                }
            };

            insertRequest.ResponseReceived += (video) =>
            {
                _logger.LogInformation("업로드 완료: https://youtu.be/{VideoId}", video.Id);
            };

            var result = await insertRequest.UploadAsync(ct);
            if (result.Status == UploadStatus.Failed)
            {
                throw new Exception($"유튜브 업로드 실패: {result.Exception?.Message}");
            }

            videoId = insertRequest.ResponseBody?.Id
                ?? throw new Exception("업로드 응답에 동영상 ID가 없습니다.");
        }

        _logger.LogInformation("유튜브 업로드 성공. VideoId: {VideoId}", videoId);
        return videoId;
    }

    /// <inheritdoc/>
    public async Task SetThumbnailAsync(
        string videoId,
        string thumbnailPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath) || !File.Exists(thumbnailPath))
        {
            _logger.LogWarning("썸네일 파일이 없어 썸네일 설정을 건너뜁니다: {Path}", thumbnailPath);
            return;
        }

        _logger.LogInformation("썸네일 설정 시작: VideoId={VideoId}", videoId);

        var youtubeService = await GetYouTubeServiceAsync(ct);

        await using var thumbStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read);
        var extension = Path.GetExtension(thumbnailPath).ToLowerInvariant();
        var mimeType = extension == ".png" ? "image/png" : "image/jpeg";

        var thumbRequest = youtubeService.Thumbnails.Set(videoId, thumbStream, mimeType);
        var result = await thumbRequest.UploadAsync(ct);

        if (result.Status == UploadStatus.Failed)
        {
            _logger.LogWarning("썸네일 설정 실패: {Exception}", result.Exception?.Message);
        }
        else
        {
            _logger.LogInformation("썸네일 설정 완료: VideoId={VideoId}", videoId);
        }
    }

    /// <summary>OAuth 2.0 인증 후 YouTubeService 반환</summary>
    private async Task<YouTubeService> GetYouTubeServiceAsync(CancellationToken ct)
    {
        var clientSecretPath = _config["YouTube:ClientSecretPath"] ?? "client_secret.json";
        var tokenStorePath = _config["YouTube:TokenStorePath"] ?? "token_store";

        UserCredential credential;

        if (!File.Exists(clientSecretPath))
        {
            throw new FileNotFoundException(
                $"YouTube OAuth 인증 파일을 찾을 수 없습니다: {clientSecretPath}\n" +
                "Google Cloud Console에서 OAuth 2.0 클라이언트 ID를 생성하고 client_secret.json을 배치하세요.",
                clientSecretPath);
        }

        await using var stream = new FileStream(clientSecretPath, FileMode.Open, FileAccess.Read);
        credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.YoutubeForceSsl },
            "user",
            ct,
            new FileDataStore(tokenStorePath, true));

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "AutoShortsFactory"
        });
    }

    /// <summary>쇼츠 태그 구성 (#Shorts 필수 포함)</summary>
    private IList<string> BuildTags(VideoProject project)
    {
        var tags = new List<string>(project.GeneratedTags);

        // 쇼츠: #Shorts 태그 필수
        if (project.VideoType == VideoType.Shorts)
        {
            if (!tags.Contains("Shorts", StringComparer.OrdinalIgnoreCase))
                tags.Insert(0, "Shorts");
            if (!tags.Contains("#Shorts", StringComparer.OrdinalIgnoreCase))
                tags.Insert(0, "#Shorts");
        }

        // 중복 제거
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>공개 설정 결정 (예약 업로드 여부)</summary>
    private static string DeterminePrivacy(DateTime? scheduledAt)
    {
        if (scheduledAt.HasValue && scheduledAt.Value > DateTime.UtcNow.AddMinutes(5))
            return "private"; // 예약 업로드: 예약 시간까지 비공개
        return "public"; // 즉시 공개
    }
}
