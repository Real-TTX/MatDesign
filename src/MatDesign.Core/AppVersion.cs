using System.Reflection;

namespace MatDesign.Core;

/// <summary>The version of MatDesign (the informational version of this assembly, set by the build per channel for all projects alike).</summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
}
