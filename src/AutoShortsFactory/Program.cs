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
using AutoShortsFactory.Services.Script;
using AutoShortsFactory.Services.Subtitle;
using AutoShortsFactory.Services.Thumbnail;
using AutoShortsFactory.Services.TopicRanker;
using AutoShortsFactory.Services.Trend;
using AutoShortsFactory.Services.Tts;
using AutoShortsFactory.Services.Upload;
using AutoShortsFactory.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;

// ─── Serilog 초기 로거 (호스트 빌드 전) ───────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // ─── 첫 실행 대화형 설정 ────────────────────────────────────────────────────
    Log.Information("AutoShortsFactory 시작...");

    using var bootstrapLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        builder.AddSerilog(Log.Logger, dispose: false));

    var initialSetup = new InitialSetup(bootstrapLoggerFactory.CreateLogger<InitialSetup>());
    var userProfile = await initialSetup.RunAsync();

    // ─── 호스트 빌드 ─────────────────────────────────────────────────────────────
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((ctx, services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/autoshorts-.log",
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: 30);

            // appsettings.json MinimumLevel 적용
            var minLevel = ctx.Configuration["Serilog:MinimumLevel:Default"] ?? "Information";
            if (Enum.TryParse<Serilog.Events.LogEventLevel>(minLevel, out var level))
                loggerConfig.MinimumLevel.Is(level);
        })
        .ConfigureAppConfiguration((ctx, builder) =>
        {
            builder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json",
                    optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        })
        .ConfigureServices((ctx, services) =>
        {
            var config = ctx.Configuration;

            // ── 사용자 프로필 (싱글턴) ────────────────────────────────────────
            services.AddSingleton(userProfile);

            // ── HttpClient ────────────────────────────────────────────────────
            services.AddHttpClient("OpenAI", c =>
            {
                c.BaseAddress = new Uri("https://api.openai.com/");
                c.Timeout = TimeSpan.FromSeconds(120);
            });
            services.AddHttpClient("GoogleTrends", c =>
            {
                c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                c.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient("YouTube", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient("Pexels", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(60);
            });
            services.AddHttpClient("Rss", c =>
            {
                c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                c.Timeout = TimeSpan.FromSeconds(30);
            });

            // ── EF Core + SQLite ──────────────────────────────────────────────
            var dbPath = config["Database:Path"] ?? "autoshorts.db";
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlite($"Data Source={dbPath}"));

            // ── 트렌드 수집 ───────────────────────────────────────────────────
            services.AddSingleton<GoogleTrendsCrawler>();
            services.AddSingleton<YouTubeTrendsCrawler>();
            services.AddSingleton<RssNewsCrawler>();
            services.AddSingleton<ITrendCrawler, CompositeTrendCrawler>();

            // ── 주제 선정 ─────────────────────────────────────────────────────
            services.AddScoped<ITopicRanker, AiTopicRanker>();

            // ── 대본 생성 ─────────────────────────────────────────────────────
            services.AddScoped<IScriptWriter, AiScriptWriter>();

            // ── TTS ───────────────────────────────────────────────────────────
            services.AddSingleton<ITtsService, EdgeTtsService>();

            // ── B-Roll 에셋 ───────────────────────────────────────────────────
            services.AddSingleton<IAssetService, PexelsAssetService>();

            // ── 자막 ──────────────────────────────────────────────────────────
            services.AddSingleton<ISubtitleService, TtsBasedSubtitleService>();

            // ── BGM ───────────────────────────────────────────────────────────
            services.AddSingleton<IBgmService, BgmMixer>();

            // ── 렌더링 ────────────────────────────────────────────────────────
            services.AddSingleton<IVideoRenderer, FfmpegRenderer>();

            // ── 썸네일 ────────────────────────────────────────────────────────
            services.AddSingleton<IThumbnailService, SkiaSharpThumbnailGenerator>();

            // ── 품질 검사 ─────────────────────────────────────────────────────
            services.AddSingleton<IQualityGate, VideoQualityChecker>();

            // ── 메타데이터 ────────────────────────────────────────────────────
            services.AddScoped<IMetadataService, AiMetadataService>();

            // ── 유튜브 업로드 ─────────────────────────────────────────────────
            services.AddSingleton<IUploadService, YouTubeUploadService>();

            // ── 일일 리포트 (싱글턴: 당일 항목 메모리 누적) ──────────────────
            services.AddSingleton<IReportService, DailyReportService>();

            // ── 파이프라인 ────────────────────────────────────────────────────
            services.AddScoped<ShortsPipeline>();
            services.AddScoped<LongFormPipeline>();
            services.AddScoped<VideoPipelineOrchestrator>();

            // ── Quartz 스케줄러 ───────────────────────────────────────────────
            services.AddQuartz(q =>
            {
                var shortsHours = config.GetSection("Schedule:ShortsHours")
                    .Get<int[]>() ?? new[] { 6, 10, 14 };
                var longFormHour = config.GetValue("Schedule:LongFormHour", 20);
                var reportHour = config.GetValue("Schedule:DailyReportHour", 22);
                var retryMinutes = config.GetValue("Schedule:RetryIntervalMinutes", 30);

                // 쇼츠 생성 Jobs (지정 시간마다)
                foreach (var hour in shortsHours)
                {
                    var jobKey = new JobKey($"ShortsJob_{hour:D2}");
                    q.AddJob<VideoCreationJob>(opts => opts
                        .WithIdentity(jobKey)
                        .UsingJobData(VideoCreationJob.VideoTypeKey, "Shorts"));

                    q.AddTrigger(opts => opts
                        .ForJob(jobKey)
                        .WithIdentity($"ShortsTrigger_{hour:D2}")
                        .WithCronSchedule($"0 0 {hour} * * ?"));
                }

                // 롱폼 생성 Job (@20시)
                var longFormJobKey = new JobKey("LongFormJob");
                q.AddJob<VideoCreationJob>(opts => opts
                    .WithIdentity(longFormJobKey)
                    .UsingJobData(VideoCreationJob.VideoTypeKey, "LongForm"));
                q.AddTrigger(opts => opts
                    .ForJob(longFormJobKey)
                    .WithIdentity("LongFormTrigger")
                    .WithCronSchedule($"0 0 {longFormHour} * * ?"));

                // 일일 리포트 Job (@22시)
                var reportJobKey = new JobKey("DailyReportJob");
                q.AddJob<DailyReportJob>(opts => opts.WithIdentity(reportJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(reportJobKey)
                    .WithIdentity("DailyReportTrigger")
                    .WithCronSchedule($"0 0 {reportHour} * * ?"));

                // 실패 재시도 Job (@매 30분)
                var retryJobKey = new JobKey("RetryJob");
                q.AddJob<RetryJob>(opts => opts.WithIdentity(retryJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(retryJobKey)
                    .WithIdentity("RetryTrigger")
                    .WithSimpleSchedule(s => s
                        .WithIntervalInMinutes(retryMinutes)
                        .RepeatForever()));
            });

            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        })
        .Build();

    // ─── EF Core 마이그레이션 자동 적용 ─────────────────────────────────────────
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("데이터베이스 초기화 완료");
    }

    Log.Information("AutoShortsFactory 서비스 시작");
    Log.Information("스케줄: 쇼츠 @06/10/14시, 롱폼 @20시, 리포트 @22시, 재시도 @매30분");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AutoShortsFactory 치명적 오류로 종료");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
