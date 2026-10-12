using System.Reflection;

namespace MatDesign.Core;

/// <summary>
/// The versions of MatDesign. <see cref="Current"/> is the build of the site (the informational version of this assembly, set by the build per channel for all
/// projects alike: <c>0.1.42-20261010</c>, <c>nightly-42-20261010</c>, <c>local-20261010</c>). <see cref="MatUi"/> is the version of the toolkit that the apps copy
/// (semantic versioning, <c>docs/VERSIONING.md</c>): the one number an app compares with its copy to know whether to update.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";

    /// <summary>The version of the toolkit MatUi: three numbers, the last release (what is not released yet is "Unreleased" in the changelog).</summary>
    public static string MatUi { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "MatUiVersion")?.Value
            ?? "0.0.0";
}
