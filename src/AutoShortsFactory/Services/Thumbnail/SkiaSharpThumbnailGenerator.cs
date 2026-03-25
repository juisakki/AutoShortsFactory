using AutoShortsFactory.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace AutoShortsFactory.Services.Thumbnail;

/// <summary>SkiaSharp으로 텍스트+이미지 썸네일 자동 생성</summary>
public class SkiaSharpThumbnailGenerator : IThumbnailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SkiaSharpThumbnailGenerator> _logger;

    private const int ThumbWidth = 1280;
    private const int ThumbHeight = 720;

    public SkiaSharpThumbnailGenerator(IConfiguration config, ILogger<SkiaSharpThumbnailGenerator> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(VideoProject project, string outputPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var title = project.GeneratedTitle ?? project.Topic.Title;

        using var bitmap = new SKBitmap(ThumbWidth, ThumbHeight);
        using var canvas = new SKCanvas(bitmap);

        DrawBackground(canvas, project);
        DrawTitle(canvas, title);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
        await using var fs = File.OpenWrite(outputPath);
        data.SaveTo(fs);

        _logger.LogInformation("썸네일 생성 완료: {Path}", outputPath);
        return outputPath;
    }

    private void DrawBackground(SKCanvas canvas, VideoProject project)
    {
        // 그라데이션 배경
        using var paint = new SKPaint();
        var colors = new SKColor[]
        {
            new SKColor(20, 20, 80),
            new SKColor(80, 20, 120)
        };
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(ThumbWidth, ThumbHeight),
            colors,
            SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, ThumbWidth, ThumbHeight), paint);
    }

    private void DrawTitle(SKCanvas canvas, string title)
    {
        // 텍스트 외곽선 (그림자)
        using var strokePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 90,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };
        // 텍스트 채우기
        using var fillPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 90,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };

        // 긴 제목을 여러 줄로 분리
        var lines = WrapText(title, fillPaint, ThumbWidth - 80);
        var lineHeight = 110f;
        var totalHeight = lines.Count * lineHeight;
        var startY = (ThumbHeight - totalHeight) / 2f + 80f;

        for (int i = 0; i < lines.Count; i++)
        {
            var y = startY + i * lineHeight;
            canvas.DrawText(lines[i], ThumbWidth / 2f, y, strokePaint);
            canvas.DrawText(lines[i], ThumbWidth / 2f, y, fillPaint);
        }
    }

    private static List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var test = string.IsNullOrEmpty(current) ? word : current + " " + word;
            if (paint.MeasureText(test) > maxWidth && !string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (!string.IsNullOrEmpty(current)) lines.Add(current);
        return lines.Any() ? lines : new List<string> { text };
    }
}
