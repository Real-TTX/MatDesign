using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MatDesktop;

/// <summary>What the Info window of a tray shows (TRAY-05).</summary>
/// <param name="AppName">The name of the program (<c>Example Agent</c>).</param>
/// <param name="Version">The version number.</param>
/// <param name="Channel">The channel (release, nightly, local).</param>
/// <param name="State">The state, as a dot and a word.</param>
/// <param name="Sentence">One sentence about the state (<c>Connected to monitor.example.com since 09:12.</c>).</param>
/// <param name="Facts">The facts as rows: machine, server or port, mode, started, version, data folder, what else matters. Never a secret (AGENT-03).</param>
public sealed record MatInfo(string AppName, string Version, string Channel, MatState State, string Sentence, IReadOnlyList<MatFact> Facts)
{
    /// <summary>The logo of the header, 44 px.</summary>
    public IImage? Logo { get; init; }

    /// <summary>Opens the page of the program, or null when it has none (then there is no Open button).</summary>
    public Action? Open { get; init; }

    /// <summary>Opens the folder of the logs.</summary>
    public Action? OpenLogFolder { get; init; }
}

/// <summary>
/// The one window of a tray (TRAY-05), about 630 × 500: a header with the logo, the name and "Info"; the state as a dot, a word and one sentence; the facts as rows; a footer with
/// <c>Version 1.4.2 · release</c> at the left and Open (if there is a page), Log folder and Close at the right. Further windows (connection details, set up) are reached from the menu or a
/// button of Info that the program adds to <see cref="Body"/>.
/// </summary>
public sealed class MatInfoWindow : MatWindow
{
    /// <summary>The width of the window.</summary>
    public const double InfoWidth = 630;

    /// <summary>The height of the window.</summary>
    public const double InfoHeight = 500;

    /// <summary>An Info window for <paramref name="info"/>.</summary>
    public MatInfoWindow(MatInfo info)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Width = InfoWidth;
        Height = InfoHeight;
        MinWidth = 480;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = $"{info.AppName} – {MatText.Get("info.title")}";

        var header = MatChrome.Header(info.Logo, info.AppName, MatText.Get("info.title"));

        StateDot = new MatStateDot { State = info.State, DotSize = 12 };
        StateDot.Children.OfType<TextBlock>().First().FontSize = MatTokens.Type.FsLg;
        var sentence = new TextBlock { Text = info.Sentence, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 18) }.Use(TextBlock.ForegroundProperty, "Mat.TextMuted");
        Facts = new MatFacts { Facts = info.Facts };
        var card = new Border { Child = Facts, Padding = new Thickness(16, 2) }.Themed("MatCard");
        Body = new StackPanel { Margin = new Thickness(28, 22, 28, 20), Children = { StateDot, sentence, card } };

        OpenButton = new Button { Content = MatText.Get("info.open"), IsVisible = info.Open is not null }.Themed("MatButton.Primary");
        OpenButton.Click += (_, _) => info.Open?.Invoke();
        LogFolderButton = new Button { Content = MatText.Get("info.logFolder"), IsVisible = info.OpenLogFolder is not null };
        LogFolderButton.Click += (_, _) => info.OpenLogFolder?.Invoke();
        CloseButton = new Button { Content = MatText.Get("info.close"), IsCancel = true };
        CloseButton.Click += (_, _) => Close();
        VersionText = MatChrome.VersionText(info.Version, info.Channel);
        var footer = MatChrome.Footer(VersionText, OpenButton, LogFolderButton, CloseButton);

        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(header);
        dock.Children.Add(footer);
        dock.Children.Add(new ScrollViewer { Content = Body, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = dock;
    }

    /// <summary>What the window shows.</summary>
    public MatInfo Info { get; }

    /// <summary>The state with its word.</summary>
    public MatStateDot StateDot { get; }

    /// <summary>The facts.</summary>
    public MatFacts Facts { get; }

    /// <summary>The body under the header: the state, the sentence, the facts; a program may add a button for a further window.</summary>
    public StackPanel Body { get; }

    /// <summary>The version at the left of the footer.</summary>
    public TextBlock VersionText { get; }

    /// <summary>Open (hidden when the program has no page).</summary>
    public Button OpenButton { get; }

    /// <summary>Log folder.</summary>
    public Button LogFolderButton { get; }

    /// <summary>Close.</summary>
    public Button CloseButton { get; }
}

/// <summary>The header and the footer that the Info window and the setup window share (AGENT-05 names the same chrome for the WinForms trays).</summary>
internal static class MatChrome
{
    /// <summary>A header on the surface with a line under it: the logo (44 px), the name and the mode or the window in the muted colour.</summary>
    public static Border Header(IImage? logo, string name, string mode, TextBlock? modeText = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        if (logo is not null)
        {
            row.Children.Add(new Image { Source = logo, Width = 44, Height = 44 });
        }

        row.Children.Add(new TextBlock { Text = name, FontSize = MatTokens.Type.Fs2xl, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var label = modeText ?? new TextBlock();
        label.Text = mode;
        label.FontSize = MatTokens.Type.FsLg;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Margin = new Thickness(0, 4, 0, 0);
        label.Use(TextBlock.ForegroundProperty, "Mat.TextMuted");
        row.Children.Add(label);
        return new Border { Padding = new Thickness(28, 18), BorderThickness = new Thickness(0, 0, 0, 1), Child = row }
            .Use(Border.BackgroundProperty, "Mat.Surface")
            .Use(Border.BorderBrushProperty, "Mat.Border");
    }

    /// <summary><c>Version 1.4.2 · release</c> in the muted colour, with the sentence of a local build as its tooltip.</summary>
    public static TextBlock VersionText(string version, string channel)
    {
        var text = new TextBlock { Text = MatVersion.Format(version, channel), VerticalAlignment = VerticalAlignment.Center, FontSize = MatTokens.Type.FsSm }
            .Use(TextBlock.ForegroundProperty, "Mat.TextMuted");
        if (MatVersion.LocalNote(channel) is { } note)
        {
            ToolTip.SetTip(text, note);
        }

        return text;
    }

    /// <summary>A footer in the colour of the menu with a line above it: the version at the left, the buttons at the right in the order given (the primary action first).</summary>
    public static Border Footer(TextBlock version, params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }

        var dock = new DockPanel();
        DockPanel.SetDock(row, Dock.Right);
        dock.Children.Add(row);
        dock.Children.Add(version);
        return new Border { Padding = new Thickness(28, 12), BorderThickness = new Thickness(0, 1, 0, 0), Child = dock, MinHeight = 58 }
            .Use(Border.BackgroundProperty, "Mat.NavBg")
            .Use(Border.BorderBrushProperty, "Mat.Border");
    }
}
