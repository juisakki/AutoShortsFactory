using AutoShortsFactory.Models;

namespace AutoShortsFactory.Services.Script;

/// <summary>대본 생성 서비스 인터페이스</summary>
public interface IScriptWriter
{
    /// <summary>주제와 유형에 맞는 대본 AI 생성</summary>
    Task<Script> WriteScriptAsync(
        Topic topic,
        VideoType videoType,
        UserProfile profile,
        LongFormType? longFormType = null,
        CancellationToken ct = default);
}
