using System.Globalization;
using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>
/// The versions the tests use for releases, all taken from the version of the toolkit (<see cref="AppVersion.MatUi"/>), so that <c>node tools/bump.js minor|major|patch</c> never needs a test edit:
/// a release is not lower than the toolkit (<c>ReleaseService.CheckVersion</c>), so the first release a test makes is <see cref="First"/> and the ones after it are <see cref="Next"/>.
/// A test that needs a version from the outside (a copy of an app, a format example) may still write a literal; one that makes a release, or reads the version out of a file, never does.
/// </summary>
internal static class Ver
{
    /// <summary>The version of the toolkit, <c>M.m.p</c>.</summary>
    public static string Toolkit => AppVersion.MatUi;

    /// <summary>The first version a release may have: the version of the toolkit.</summary>
    public static string First => Toolkit;

    /// <summary><paramref name="patches"/> patch numbers above the toolkit: <c>Next(0)</c> is <see cref="First"/>, <c>Next(1)</c> the version a second release gets.</summary>
    public static string Next(int patches = 1)
    {
        var (major, minor, patch) = Parts(Toolkit);
        return $"{major}.{minor}.{patch + patches}";
    }

    /// <summary>The next major version of the toolkit, <c>M+1.0.0</c>: a newer version that a copy of the toolkit is behind.</summary>
    public static string NextMajor => $"{Parts(Toolkit).Major + 1}.0.0";

    /// <summary>A version that is lower than the toolkit's, so that a release with it is refused (a toolkit at 0.0.0 has none).</summary>
    public static string Below
    {
        get
        {
            var (major, minor, patch) = Parts(Toolkit);
            if (patch > 0)
            {
                return $"{major}.{minor}.{patch - 1}";
            }

            if (minor > 0)
            {
                return $"{major}.{minor - 1}.9";
            }

            return major > 0
                ? $"{major - 1}.9.9"
                : throw new InvalidOperationException("The toolkit is at 0.0.0: no version is lower than it.");
        }
    }

    /// <summary>A version that is none of the releases of a test and that no app can mistake for one: well above <see cref="Next"/> of a handful of releases.</summary>
    public static string Far => Next(500);

    /// <summary>The file a release holds the toolkit's files in: <c>matui-&lt;version&gt;.zip</c>, or with another name, <c>acmeui-&lt;version&gt;.zip</c>.</summary>
    public static string Zip(string version, string name = "matui") => $"{name}-{version}.zip";

    private static (int Major, int Minor, int Patch) Parts(string version)
    {
        var parts = version.Split('.').Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        return (parts[0], parts[1], parts[2]);
    }
}
