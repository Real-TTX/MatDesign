namespace MatDesign.Core;

/// <summary>Two apps whose accents are easy to confuse.</summary>
public sealed record Collision(MatApp A, MatApp B, double Distance);

/// <summary>A free accent for an app that shares its colour with another, with the nearest colour that is already taken.</summary>
public sealed record AccentSuggestion(MatApp App, string Accent, double NearestDistance, string NearestApp);

/// <summary>
/// Finds accents that are too close to tell apart and proposes free ones. It only advises: the registry is changed by a person.
/// </summary>
public static class PaletteAdvisor
{
    /// <summary>Below this distance (see <see cref="Colors.Distance"/>) two accents are easily confused, for instance on a small logo or a tab.</summary>
    public const double TooClose = 0.45;

    public static IReadOnlyList<Collision> Collisions(IReadOnlyList<MatApp> apps, double threshold = TooClose)
    {
        var result = new List<Collision>();
        for (var i = 0; i < apps.Count; i++)
        {
            for (var j = i + 1; j < apps.Count; j++)
            {
                var d = Colors.Distance(apps[i].AccentRgb, apps[j].AccentRgb);
                if (d < threshold)
                {
                    result.Add(new Collision(apps[i], apps[j], d));
                }
            }
        }

        return result.OrderBy(c => c.Distance).ToList();
    }

    /// <summary>
    /// For every group of apps that are too close, the first one (registry order) keeps its colour and the others get a proposal: the
    /// colour that is farthest from everything already in use and still carries white text at AA.
    /// </summary>
    public static IReadOnlyList<AccentSuggestion> Suggest(IReadOnlyList<MatApp> apps, double threshold = TooClose)
    {
        var collisions = Collisions(apps, threshold);

        // connected groups of colliding apps
        var parent = apps.ToDictionary(a => a.Id, a => a.Id);
        string Find(string x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        foreach (var c in collisions)
        {
            parent[Find(c.A.Id)] = Find(c.B.Id);
        }

        var moving = new List<MatApp>();
        foreach (var group in apps.Where(a => collisions.Any(c => c.A == a || c.B == a)).GroupBy(a => Find(a.Id)))
        {
            moving.AddRange(group.Skip(1));
        }

        moving = moving.OrderBy(a => apps.ToList().IndexOf(a)).ToList();
        var taken = apps.Where(a => !moving.Contains(a)).Select(a => (a.Name, Rgb: a.AccentRgb)).ToList();
        var suggestions = new List<AccentSuggestion>();

        foreach (var app in moving)
        {
            Rgb best = default;
            var bestScore = -1.0;
            var nearest = string.Empty;
            for (var hue = 0; hue < 360; hue += 4)
            {
                foreach (var l in new[] { 0.46, 0.52, 0.58 })
                {
                    foreach (var c in new[] { 0.12, 0.16, 0.20 })
                    {
                        var candidate = Colors.FromOklch(l, c, hue);
                        if (Colors.Contrast(candidate, Rgb.White) < 4.5)
                        {
                            continue;
                        }

                        var (score, who) = taken.Select(t => (Colors.Distance(candidate, t.Rgb), t.Name)).MinBy(t => t.Item1);
                        if (score > bestScore)
                        {
                            (best, bestScore, nearest) = (candidate, score, who);
                        }
                    }
                }
            }

            suggestions.Add(new AccentSuggestion(app, best.ToHex(), bestScore, nearest));
            taken.Add((app.Name, best));
        }

        return suggestions;
    }
}
