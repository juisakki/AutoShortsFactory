using AutoShortsFactory.Data;
using AutoShortsFactory.Models;
using AutoShortsFactory.Pipeline;
using AutoShortsFactory.Scheduling;
using AutoShortsFactory.Services.Asset;
using AutoShortsFactory.Services.Bgm;
using AutoShortsFactory.Services.Metadata;
using AutoShortsFactory.Services.Quality;
using AutoShortsFactory.Services.Render;
using AutoShortsFactory.Services.Report;
using AutoShortsFactory.Services.ScriptWriter;
using AutoShortsFactory.Services.Subtitle;
using AutoShortsFactory.Services.Thumbnail;
using AutoShortsFactory.Services.TopicRanker;
using AutoShortsFactory.Services.Trend;
using AutoShortsFactory.Services.Tts;
using AutoShortsFactory.Services.Upload;
using AutoShortsFactory.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Serilog;

// ── Serilog 사전 설정 (호스트 빌드 전 로그용) ──
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("AutoShortsFactory 시작 중...");

    // ── 초기 설정 (UserProfile 로드 또는 대화형 생성) ──
    var userProfile = await InitialSetup.RunAsync();

    // ── Host 빌드 ──
    var builder = Host.CreateApplicationBuilder(args);

    // Serilog 설정 (콘솔 + 파일 로깅)
    var minLevel = builder.Environment.IsDevelopment()
        ? Serilog.Events.LogEventLevel.Debug
        : Serilog.Events.LogEventLevel.Information;

    builder.Services.AddSerilog((_, logConfig) =>
        logConfig
            .MinimumLevel.Is(minLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day));

    // ── EF Core SQLite ──
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite("Data Source=autoshorts.db"));

    // ── HttpClient 등록 ──
    builder.Services.AddHttpClient("OpenAI", client =>
    {
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromMinutes(2);
    });

    builder.Services.AddHttpClient("Pexels", client =>
    {
        var apiKey = builder.Configuration["Pexels:ApiKey"] ?? string.Empty;
        client.BaseAddress = new Uri("https://api.pexels.com/");
        client.DefaultRequestHeaders.Add("Authorization", apiKey);
        client.Timeout = TimeSpan.FromMinutes(5);
    });

    builder.Services.AddHttpClient("YouTube", client =>
    {
        client.Timeout = TimeSpan.FromMinutes(10);
    });

    // ── UserProfile 단일 인스턴스 등록 ──
    builder.Services.AddSingleton(userProfile);

    // ── 트렌드 서비스 ──
    builder.Services.AddSingleton<GoogleTrendsCrawler>();
    builder.Services.AddSingleton<YouTubeTrendsCrawler>();
    builder.Services.AddSingleton<RssNewsCrawler>();
    builder.Services.AddSingleton<ITrendCrawler, CompositeTrendCrawler>();

    // ── 주제 선정 / 대본 / TTS / 에셋 / 자막 / BGM / 렌더링 / 썸네일 ──
    builder.Services.AddScoped<ITopicRanker, AiTopicRanker>();
    builder.Services.AddScoped<IScriptWriter, AiScriptWriter>();
    builder.Services.AddScoped<ITtsService, EdgeTtsService>();
    builder.Services.AddScoped<IAssetService, PexelsAssetService>();
    builder.Services.AddScoped<ISubtitleService, TtsBasedSubtitleService>();
    builder.Services.AddScoped<IBgmService, BgmMixer>();
    builder.Services.AddScoped<IVideoRenderer, FfmpegRenderer>();
    builder.Services.AddScoped<IThumbnailService, SkiaSharpThumbnailGenerator>();

    // ── 업로드 / 품질 / 메타데이터 / 리포트 ──
    builder.Services.AddScoped<IUploadService, YouTubeUploadService>();
    builder.Services.AddScoped<IQualityGate, VideoQualityChecker>();
    builder.Services.AddScoped<IMetadataService, AiMetadataService>();
    builder.Services.AddScoped<IReportService, DailyReportService>();

    // ── 파이프라인 ──
    builder.Services.AddScoped<ShortsPipeline>();
    builder.Services.AddScoped<LongFormPipeline>();
    builder.Services.AddScoped<VideoPipelineOrchestrator>();

    // ── Quartz.NET 스케줄러 ──
    var shCron = builder.Configuration["Schedule:ShortsCron"] ?? "0 0 6,10,14 * * ?";
    var lfCron = builder.Configuration["Schedule:LongFormCron"] ?? "0 0 20 * * ?";
    var rpCron = builder.Configuration["Schedule:ReportCron"] ?? "0 0 22 * * ?";
    var rtCron = builder.Configuration["Schedule:RetryCron"] ?? "0 0/30 * * * ?";

    builder.Services.AddQuartz(q =>
    {
        // 쇼츠 Job (Cron: 06시, 10시, 14시)
        var shortsJobKey = new JobKey("ShortsJob");
        q.AddJob<VideoCreationJob>(opts => opts
            .WithIdentity(shortsJobKey)
            .UsingJobData(VideoCreationJob.VideoTypeKey, nameof(VideoType.Shorts)));

        q.AddTrigger(opts => opts
            .ForJob(shortsJobKey)
            .WithIdentity("ShortsTrigger")
            .WithCronSchedule(shCron));

        // 롱폼 Job (Cron: 20시)
        var longFormJobKey = new JobKey("LongFormJob");
        q.AddJob<VideoCreationJob>(opts => opts
            .WithIdentity(longFormJobKey)
            .UsingJobData(VideoCreationJob.VideoTypeKey, nameof(VideoType.LongForm)));

        q.AddTrigger(opts => opts
            .ForJob(longFormJobKey)
            .WithIdentity("LongFormTrigger")
            .WithCronSchedule(lfCron));

        // 일일 리포트 Job (Cron: 22시)
        var reportJobKey = new JobKey("DailyReportJob");
        q.AddJob<DailyReportJob>(opts => opts.WithIdentity(reportJobKey));

        q.AddTrigger(opts => opts
            .ForJob(reportJobKey)
            .WithIdentity("DailyReportTrigger")
            .WithCronSchedule(rpCron));

        // 재시도 Job (Cron: 매 30분)
        var retryJobKey = new JobKey("RetryJob");
        q.AddJob<RetryJob>(opts => opts.WithIdentity(retryJobKey));

        q.AddTrigger(opts => opts
            .ForJob(retryJobKey)
            .WithIdentity("RetryTrigger")
            .WithCronSchedule(rtCron));
    });

    builder.Services.AddQuartzHostedService(opt =>
    {
        opt.WaitForJobsToComplete = true;
    });

    var host = builder.Build();

    // ── EF Core DB 자동 Migration ──
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("DB Migration 완료");
    }

    Log.Information("AutoShortsFactory 시작 완료. 스케줄러 실행 중...");
    Log.Information("쇼츠 Cron: {Cron}", shCron);
    Log.Information("롱폼 Cron: {Cron}", lfCron);
    Log.Information("리포트 Cron: {Cron}", rpCron);
    Log.Information("재시도 Cron: {Cron}", rtCron);

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AutoShortsFactory 시작 실패");
}
finally
{
    await Log.CloseAndFlushAsync();
}
