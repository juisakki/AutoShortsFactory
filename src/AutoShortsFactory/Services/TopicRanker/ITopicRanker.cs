using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.TopicRanker;

/// <summary>주제 랭킹/필터링 서비스 인터페이스</summary>
public interface ITopicRanker
{
    /// <summary>후보 주제 목록을 AI로 평가하고 최적 주제를 선별</summary>
    Task<List<Topic>> RankAndFilterAsync(
        List<Topic> candidates,
        VideoType videoType,
        CancellationToken ct = default);
}
