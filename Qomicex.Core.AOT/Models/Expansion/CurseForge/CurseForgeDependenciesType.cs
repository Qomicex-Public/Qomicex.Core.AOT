namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public enum CurseForgeDependenciesType
{
    EmbeddedLibrary = 1,
    OptionalDependency = 2,
    RequiredDependency = 3,
    Tool = 4,
    Incompatible = 5,
    Include = 6
}
