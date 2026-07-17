namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeSearchResponse(
    List<CurseForgeSearchResult> Results,
    int TotalCount
);
