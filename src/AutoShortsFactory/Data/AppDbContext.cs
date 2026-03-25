using Microsoft.EntityFrameworkCore;

namespace AutoShortsFactory.Data;

/// <summary>EF Core SQLite DbContext</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>주제 사용 이력 테이블</summary>
    public DbSet<TopicHistory> TopicHistories => Set<TopicHistory>();

    /// <summary>업로드 이력 테이블</summary>
    public DbSet<UploadRecord> UploadRecords => Set<UploadRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 주제 이력: 제목에 인덱스 추가 (중복 조회 성능)
        modelBuilder.Entity<TopicHistory>()
            .HasIndex(t => t.TopicTitle);

        // 업로드 이력: 프로젝트 ID에 인덱스 추가
        modelBuilder.Entity<UploadRecord>()
            .HasIndex(u => u.ProjectId);

        // 업로드 이력: 날짜 기반 조회용 인덱스
        modelBuilder.Entity<UploadRecord>()
            .HasIndex(u => u.CreatedAt);
    }
}
