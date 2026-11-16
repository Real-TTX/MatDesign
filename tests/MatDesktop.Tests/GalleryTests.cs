using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;

namespace MatDesktop.Tests;

/// <summary>
/// The pictures of MatDesktop (docs/DESKTOP.md, section 6): every window drawn without a screen from fixed sample data (the sample program "Matmon Agent", the computer ANNA-PC, the
/// user Anna, version 1.4.2 on the release channel, a pinned clock), never from the machine the test runs on. Light and dark, at their natural size. Only with
/// <c>MATDESKTOP_SHOTS=&lt;folder&gt;</c>; the site shows a choice of them (src/MatDesign.Web/wwwroot/img/desktop, prefix <c>matdesktop-</c>).
/// </summary>
public sealed class GalleryTests
{
    private const string App = "Matmon Agent";
    private const string Computer = "ANNA-PC";
    private const string Version = "1.4.2";
    private const string Server = "monitor.example.com";

    private static readonly string? Folder = Environment.GetEnvironmentVariable("MATDESKTOP_SHOTS");

    private static readonly (string Name, Func<Bitmap, Window> Make)[] Windows =
    [
        ("info", Info),
        ("setup-welcome", logo => SetupAt(logo, MatSetupStepKind.Welcome)),
        ("setup-connect", logo => SetupAt(logo, MatSetupStepKind.Connect)),
        ("setup-options", logo => SetupAt(logo, MatSetupStepKind.Options)),
        ("setup-progress", logo => SetupAt(logo, MatSetupStepKind.Progress)),
        ("setup-finish", logo => SetupAt(logo, MatSetupStepKind.Finish)),
        ("setup-uninstall", Uninstall),
        ("main-open", logo => MainWindow(logo, folded: false)),
        ("main-folded", logo => MainWindow(logo, folded: true)),
        ("notification", _ => new MatNotification("Connection lost", $"{App} cannot reach {Server}. It tries again every minute.", isWarning: true, onClick: null)),
    ];

    [AvaloniaFact]
    public void The_windows_are_drawn_from_sample_data_in_light_and_dark()
    {
        if (Folder is null)
        {
            return; // pictures only when asked for (MATDESKTOP_SHOTS)
        }

        Directory.CreateDirectory(Folder);
        var logo = ThemeTests.Mark(128);
        foreach (var dark in new[] { false, true })
        {
            Ui.Theme(dark);
            foreach (var (name, make) in Windows)
            {
                var window = make(logo);
                window.Show();
                Ui.Settle(window);
                if (window is MatSetupWindow setup && name == "setup-progress")
                {
                    Progress(setup);
                }

                if (window is MatSetupWindow done && name == "setup-finish")
                {
                    Finish(done);
                }

                Save(Ui.Frame(window), $"matdesktop-{name}-{(dark ? "dark" : "light")}.png");
                if (window is MatSetupWindow working && working.IsWorking)
                {
                    continue; // a window that is working cannot be closed; the test ends with it
                }

                window.Close();
            }
        }

        Ui.Theme(false);
        var mark = ThemeTests.Mark(64);
        foreach (var state in MatState.Standard)
        {
            var key = state.Kind.ToString().ToLowerInvariant();
            File.WriteAllBytes(Path.Combine(Folder, $"matdesktop-tray-{key}.png"), MatTrayIcons.Png(mark, state, template: false));
            File.WriteAllBytes(Path.Combine(Folder, $"matdesktop-tray-macos-{key}.png"), MatTrayIcons.Png(Template(mark), state, template: true));
        }

        Assert.All(Directory.GetFiles(Folder, "matdesktop-*.png"), f => Assert.True(new FileInfo(f).Length < 200_000, f));
    }

    private static void Save(Bitmap frame, string name)
    {
        frame.Save(Path.Combine(Folder!, name));
    }

    private static MatInfoWindow Info(Bitmap logo) => new(new MatInfo(App, Version, MatVersion.Release, MatState.Running,
        $"Connected to {Server}. The last report went out at 14:31.",
        [
            new MatFact("Computer", Computer),
            new MatFact("Server", $"https://{Server}"),
            new MatFact("Mode", "Service, started by the system"),
            new MatFact("Started", "12 November 2026, 09:12"),
            new MatFact("Version", MatVersion.Value(Version, MatVersion.Release), IsMono: true),
            new MatFact("Data folder", @"C:\ProgramData\Matmon Agent", IsMono: true),
        ])
    {
        Logo = logo,
        Open = () => { },
        OpenLogFolder = () => { },
    });

    private static MatSetup Definition(Bitmap logo, MatSetupMode mode, Func<MatSetupRun, Task> run) => new(App, Version, MatVersion.Release, mode,
        mode == MatSetupMode.Uninstall
            ? [MatSetupStep.Uninstall($"The service {App}, its tray, the entry in the start menu and the entry in Installed apps are removed. The server keeps the history of this computer.")]
            :
            [
                MatSetupStep.Welcome($"The {App} watches this computer and reports to your Matmon server. It runs as a service of Windows and needs administrator rights once, when the installation starts.",
                    new TextBlock { Text = @"It is installed to C:\Program Files\Matmon Agent.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Themed("MatText.Muted")),
                MatSetupStep.Connect("The address of your Matmon server and the enrolment code from its page Computers > Add. The code is checked before anything is installed.", ConnectFields(), () => Task.FromResult<string?>(null)),
                MatSetupStep.WithOptions(null,
                    MatSetupOption.Autostart("The tray icon in the notification area; the service runs anyway"),
                    MatSetupOption.AutomaticUpdates("New versions are installed at night"),
                    MatSetupOption.DesktopShortcut("A start menu entry is always made")),
            ],
        run)
    {
        Logo = logo,
        Finish = MatSetupStep.Finish($"The {App} runs as a service and reports to {Server}. Its icon is in the notification area of the task bar."),
    };

    private static Control ConnectFields() => new StackPanel
    {
        Children =
        {
            new MatFormField { Label = "Server", Hint = "Where Matmon runs", Content = new TextBox { Text = $"https://{Server}" } },
            new MatFormField { Label = "Enrolment code", Hint = "Single use, valid for 24 hours", Content = new TextBox { Text = "K7QM-4TXC-9RHA-B2NW-P6FE" }.Use(TextBox.FontFamilyProperty, "Mat.Mono") },
        },
    };

    private static readonly TaskCompletionSource Never = new();

    private static MatSetupWindow SetupAt(Bitmap logo, MatSetupStepKind kind)
    {
        var window = new MatSetupWindow(Definition(logo, MatSetupMode.Setup, kind == MatSetupStepKind.Progress ? _ => Never.Task : _ => Task.CompletedTask));
        if (kind is MatSetupStepKind.Connect or MatSetupStepKind.Options)
        {
            window.GoTo(kind);
        }

        return window;
    }

    private static void Progress(MatSetupWindow window)
    {
        _ = window.RunAsync();
        window.Report(new MatSetupReport($"Enrol with {Server}", MatSetupStepState.Done));
        window.Report(new MatSetupReport("Copy the program", MatSetupStepState.Done));
        window.Report(new MatSetupReport($"Install the service {App}", MatSetupStepState.Running, 65));
        window.Report(new MatSetupReport("Start the tray at sign-in", MatSetupStepState.Waiting));
        Ui.Settle(window);
    }

    private static void Finish(MatSetupWindow window)
    {
        window.RunAsync().GetAwaiter().GetResult();
        Ui.Settle(window);
    }

    private static MatSetupWindow Uninstall(Bitmap logo) => new(Definition(logo, MatSetupMode.Uninstall, _ => Task.CompletedTask));

    private static MatWindow MainWindow(Bitmap logo, bool folded)
    {
        var bar = new MatTitleBar { AppName = "Matmon", Logo = logo, CloseKey = "Esc", ShowCaptionButtons = true, Center = new TextBox { Watermark = "Search checks", Width = 420, HorizontalAlignment = HorizontalAlignment.Left }.Themed("MatSearchBox") };
        var nav = new MatNav { AppName = "Matmon", Version = Version, Channel = MatVersion.Release, TitleBar = bar, IsFolded = folded };
        nav.Items.Add(new MatNavItem("dashboard", "Overview", "Ctrl+1") { IsCurrent = true });
        nav.Items.Add(new MatNavItem("activity", "Checks", "Ctrl+2"));
        nav.Items.Add(new MatNavItem("bell", "Alerts", "Ctrl+3"));
        nav.Items.Add(new MatNavItem("server", "Computers", "Ctrl+4"));
        nav.Settings = new MatNavItem("settings", "Settings", "Ctrl+,");

        var state = new Border
        {
            Padding = new Thickness(16, 4),
            Child = new StackPanel
            {
                Children =
                {
                    new Border { Padding = new Thickness(0, 12, 0, 4), Child = new MatStateDot { State = MatState.Running } },
                    new TextBlock { Text = $"Connected to {Server}. The last report went out at 14:31.", Margin = new Thickness(0, 0, 0, 8) }.Themed("MatText.Muted"),
                    new MatFacts { Facts = [new MatFact("Computer", Computer), new MatFact("Checks", "24, all passing"), new MatFact("Version", MatVersion.Value(Version, MatVersion.Release), IsMono: true)] },
                },
            },
        }.Themed("MatCard");
        var settings = new Border
        {
            Padding = new Thickness(16, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new MatSettingRow { Title = "Start the tray at sign-in", Description = "The service runs anyway", Content = new ToggleSwitch { IsChecked = true, OnContent = string.Empty, OffContent = string.Empty } },
                    new MatSettingRow { Title = "Appearance", Description = "Follows Windows unless you choose", Content = new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, SelectedIndex = 0, MinWidth = 140 }, BorderThickness = new Thickness(0) },
                },
            },
        }.Themed("MatCard");
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new Button { Content = "Report now" }.Themed("MatButton.Primary"),
                new Button { Content = "Open log folder" },
            },
        };
        var page = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(MatTokens.Size.ContentPad, 24),
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { new TextBlock { Text = "Overview" }.Themed("MatText.PageTitle"), state, settings, actions },
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumnSpan(bar, 2);
        Grid.SetRow(nav, 1);
        var scroll = new ScrollViewer { Content = page };
        Grid.SetRow(scroll, 1);
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(bar);
        grid.Children.Add(nav);
        grid.Children.Add(scroll);
        var window = new MatWindow { Width = 1000, Height = 640, Title = "Matmon", Content = grid };
        return window;
    }

    /// <summary>The mark as a macOS template image: its shape in black (the alpha is what counts).</summary>
    private static Bitmap Template(Bitmap mark)
    {
        var size = mark.PixelSize;
        var shape = new Border
        {
            Width = size.Width,
            Height = size.Height,
            CornerRadius = new CornerRadius(size.Width * 0.22),
            BorderThickness = new Thickness(size.Width * 0.08),
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#000000")),
            Child = new MatIcon { Icon = "activity", Size = size.Width * 0.6, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#000000")) },
        };
        shape.Measure(new Size(size.Width, size.Height));
        shape.Arrange(new Rect(0, 0, size.Width, size.Height));
        var host = new Window { Content = shape };
        host.Show();
        shape.Measure(new Size(size.Width, size.Height));
        shape.Arrange(new Rect(0, 0, size.Width, size.Height));
        var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(shape);
        host.Close();
        return bitmap;
    }
}
