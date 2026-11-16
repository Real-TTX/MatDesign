using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace MatDesktop.Tests;

/// <summary>
/// The icons as plain segments for WinForms (Tokens/MatIconPaths.g.cs, DESK-09): every symbol of the sprite is there, and each draws the same picture as the Avalonia geometry of the same
/// icon (MatIcon.&lt;name&gt;): the points along the segments lie on the line of the geometry, and both ink the same pixels when they are drawn.
/// </summary>
public sealed class IconPathsTests
{
    /// <summary>How far apart the two pictures may be, in units of the grid of 24 (a line of the icon is 2 wide).</summary>
    private const double Tolerance = 0.12;

    /// <summary>The segments as an Avalonia geometry, drawn the way the README tells a WinForms program to draw them.</summary>
    private static StreamGeometry Build(IReadOnlyList<MatPathSegment> segments)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var open = false;
        foreach (var s in segments)
        {
            switch (s.Verb)
            {
                case MatPathVerb.MoveTo:
                    if (open)
                    {
                        context.EndFigure(false);
                    }

                    context.BeginFigure(new Point(s.X, s.Y), isFilled: true);
                    open = true;
                    break;
                case MatPathVerb.LineTo:
                    context.LineTo(new Point(s.X, s.Y));
                    break;
                case MatPathVerb.CubicTo:
                    context.CubicBezierTo(new Point(s.X1, s.Y1), new Point(s.X2, s.Y2), new Point(s.X, s.Y));
                    break;
                case MatPathVerb.Close:
                    context.EndFigure(true);
                    open = false;
                    break;
            }
        }

        if (open)
        {
            context.EndFigure(false);
        }

        return geometry;
    }


    /// <summary>The points along the segments themselves, without Avalonia: the ends and the curves evaluated.</summary>
    private static List<Point> Walk(IReadOnlyList<MatPathSegment> segments)
    {
        var points = new List<Point>();
        Point current = default, start = default;
        foreach (var s in segments)
        {
            switch (s.Verb)
            {
                case MatPathVerb.MoveTo:
                    current = start = new Point(s.X, s.Y);
                    points.Add(current);
                    break;
                case MatPathVerb.LineTo:
                    points.AddRange(Line(current, new Point(s.X, s.Y)));
                    current = new Point(s.X, s.Y);
                    break;
                case MatPathVerb.CubicTo:
                    for (var t = 0.05; t <= 1.0001; t += 0.05)
                    {
                        var u = 1 - t;
                        points.Add(new Point(
                            (u * u * u * current.X) + (3 * u * u * t * s.X1) + (3 * u * t * t * s.X2) + (t * t * t * s.X),
                            (u * u * u * current.Y) + (3 * u * u * t * s.Y1) + (3 * u * t * t * s.Y2) + (t * t * t * s.Y)));
                    }

                    current = new Point(s.X, s.Y);
                    break;
                case MatPathVerb.Close:
                    points.AddRange(Line(current, start));
                    current = start;
                    break;
            }
        }

        return points;

        static IEnumerable<Point> Line(Point a, Point b)
        {
            for (var t = 0.1; t <= 1.0001; t += 0.1)
            {
                yield return new Point(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
            }
        }
    }

    /// <summary>Which pixels of a 96 × 96 picture a geometry inks (alpha over half), stroked like MatIcon (a line of 2 on the grid, round ends) or filled.</summary>
    private static bool[] Ink(Geometry geometry, bool fill)
    {
        const int size = 96;
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(size, size));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateScale(size / 24.0, size / 24.0)))
        {
            if (fill)
            {
                context.DrawGeometry(Brushes.Black, null, geometry);
            }
            else
            {
                context.DrawGeometry(null, new Pen(Brushes.Black, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
            }
        }

        var buffer = new byte[size * size * 4];
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(buffer.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size, size), memory, buffer.Length, size * 4);
            System.Runtime.InteropServices.Marshal.Copy(memory, buffer, 0, buffer.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(memory);
        }

        return Enumerable.Range(0, size * size).Select(i => buffer[(i * 4) + 3] > 127).ToArray();
    }

    [AvaloniaFact]
    public void Every_icon_of_the_sprite_is_there_as_segments_that_start_with_a_move()
    {
        var sprite = MatIconPathsNames();
        Assert.Equal(sprite.Order(StringComparer.Ordinal), MatIconPaths.Names.Order(StringComparer.Ordinal));
        Assert.Null(MatIconPaths.Find("no-such-icon"));
        Assert.Equal(24, MatIconPaths.Grid);
        Assert.Equal(2, MatIconPaths.StrokeWidth);
        foreach (var name in MatIconPaths.Names)
        {
            var icon = MatIconPaths.Find(name)!;
            Assert.True(icon.Stroke.Count + icon.Fill.Count > 0, name);
            foreach (var part in new[] { icon.Stroke, icon.Fill }.Where(p => p.Count > 0))
            {
                Assert.Equal(MatPathVerb.MoveTo, part[0].Verb);

                // after a Close a figure goes on only from a MoveTo, so a reader needs no state but the current point
                for (var i = 1; i < part.Count; i++)
                {
                    Assert.False(part[i - 1].Verb == MatPathVerb.Close && part[i].Verb != MatPathVerb.MoveTo, $"{name}: {part[i].Verb} after Close");
                }

                Assert.All(part, s => Assert.True(s.X is >= -1 and <= 25 && s.Y is >= -1 and <= 25, $"{name}: {s}"));
            }
        }
    }

    private static List<string> MatIconPathsNames() =>
        System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Paths.Sprite), @"<symbol id=""mat-i-([a-z0-9-]+)""").Select(m => m.Groups[1].Value).ToList();

    [AvaloniaFact]
    public void Every_icon_draws_the_same_picture_as_its_avalonia_geometry()
    {
        // Skia flattens a curve for device pixels: on the grid of 24 that is coarse, so both are compared 20 times larger
        const double scale = 20;
        var thin = new Pen(Brushes.Transparent, Tolerance * 2 * scale, lineCap: PenLineCap.Round);
        var findings = new List<string>();
        foreach (var name in MatIconPaths.Names)
        {
            var icon = MatIconPaths.Find(name)!;
            foreach (var (segments, key) in new[] { (icon.Stroke, name), (icon.Fill, name + ".Fill") })
            {
                var avalonia = MatIcon.Find(key);
                if (segments.Count == 0)
                {
                    // no part of this kind: Avalonia has none either (or the empty M0,0 of an icon that is only filled)
                    Assert.True(avalonia is null || avalonia.Bounds.Width + avalonia.Bounds.Height < 0.01, key);
                    continue;
                }

                Assert.NotNull(avalonia);
                var mine = Build(segments);

                // the segments lie on the line of the Avalonia geometry
                var walked = Walk(segments);
                var large = avalonia.Clone();
                large.Transform = new ScaleTransform(scale, scale);
                var off = walked.Where(p => !large.StrokeContains(thin, p * scale)).ToList();
                if (off.Count > 0)
                {
                    findings.Add($"{key}: {off.Count} of {walked.Count} points off the sprite's path (first {off.FirstOrDefault()})");
                }

                // and both draw the same pixels, stroked like MatIcon at 96 px (a line of 8 px)
                var fill = key.EndsWith(".Fill", StringComparison.Ordinal);
                var theirs = Ink(avalonia, fill);
                var ours = Ink(mine, fill);
                var union = theirs.Zip(ours).Count(p => p.First || p.Second);
                var differ = theirs.Zip(ours).Count(p => p.First != p.Second);
                if (union == 0 || differ > Math.Max(8, union * 0.03))
                {
                    findings.Add($"{key}: {differ} of {union} pixels differ");
                }
            }
        }

        Assert.True(findings.Count == 0, string.Join("\n", findings));
    }

    [AvaloniaFact]
    public void An_arc_becomes_cubic_curves_that_stay_on_the_circle()
    {
        // the circle of the sprite (r 9 around 12,12): four curves of 90 degrees, every point at 9 from the centre within a hair
        var circle = MatIconPaths.Find("circle")!.Stroke;
        Assert.Equal(4, circle.Count(s => s.Verb == MatPathVerb.CubicTo));
        Assert.DoesNotContain(circle, s => s.Verb == MatPathVerb.LineTo);
        Assert.All(Walk(circle), p => Assert.InRange(Math.Sqrt(((p.X - 12) * (p.X - 12)) + ((p.Y - 12) * (p.Y - 12))), 8.99, 9.01));

        // a rounded rectangle: lines and the four corners as curves
        var sidebar = MatIconPaths.Find("sidebar")!.Stroke;
        Assert.Equal(4, sidebar.Count(s => s.Verb == MatPathVerb.CubicTo));
        Assert.Contains(sidebar, s => s.Verb == MatPathVerb.Close);

        // the comparison of pixels sees a wrong picture: x is not plus
        var x = Ink(Build(MatIconPaths.Find("x")!.Stroke), fill: false);
        var plus = Ink(MatIcon.Find("plus")!, fill: false);
        Assert.True(x.Zip(plus).Count(p => p.First != p.Second) > x.Zip(plus).Count(p => p.First || p.Second) * 0.3);
    }

    [Fact]
    public void The_readme_shows_how_a_winforms_program_strokes_them()
    {
        var readme = File.ReadAllText(Path.Combine(Paths.Package, "README.md"));
        Assert.Contains("MatIconPaths.Find", readme, StringComparison.Ordinal);
        Assert.Contains("GraphicsPath", readme, StringComparison.Ordinal);
        Assert.Contains("AddBezier", readme, StringComparison.Ordinal);
        Assert.Contains("LineCap.Round", readme, StringComparison.Ordinal);
    }
}
