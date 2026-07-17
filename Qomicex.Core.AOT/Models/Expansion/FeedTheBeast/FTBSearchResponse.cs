namespace Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;

public record FTBSearchResponse(
    List<ModpackInfo> Results,
    int TotalCount
);
