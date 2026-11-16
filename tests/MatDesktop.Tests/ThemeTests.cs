using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace MatDesktop.Tests;

/// <summary>The theme in a real (headless) application: the icons of the sprite, every resource the sources use in both modes, every control theme drawn in light and dark.</summary>
public sealed partial class ThemeTests
{
    [GeneratedRegex(@"<symbol id=""mat-i-([a-z0-9-]+)""")]
    private static partial Regex Symbol();

    [GeneratedRegex(@"x:Key=""MatIcon\.([a-z0-9-]+)""")]
    private static partial Regex IconKey();

    [GeneratedRegex(@"\{(?:Dynamic|Static)Resource (Mat[A-Za-z]*\.[A-Za-z0-9.]+|Mat[A-Z][A-Za-z]+)\}")]
    private static partial Regex XamlKey();

    [GeneratedRegex(@"""(Mat\.[A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)?)""|Themed\(""([A-Za-z.]+)""\)")]
    private static partial Regex CodeKey();

    [AvaloniaFact]
    public void Every_symbol_of_the_sprite_is_a_geometry_that_parses_on_the_grid_of_24()
    {
        var sprite = Symbol().Matches(File.ReadAllText(Paths.Sprite)).Select(m => m.Groups[1].Value).ToList();
        var icons = IconKey().Matches(File.ReadAllText(Path.Combine(Paths.Package, "Themes", "MatIcons.axaml"))).Select(m => m.Groups[1].Value).ToList();
        Assert.True(sprite.Count >= 118, $"{sprite.Count} symbols");
        Assert.Equal(sprite.Order(StringComparer.Ordinal), icons.Order(StringComparer.Ordinal));
        foreach (var name in sprite)
        {
            var geometry = Assert.IsAssignableFrom<Geometry>(MatIcon.Find(name));
            var bounds = geometry.Bounds;
            Assert.True(bounds.Width > 0 || bounds.Height > 0, name);
            Assert.True(bounds.X >= -2 && bounds.Y >= -2 && bounds.Right <= 26 && bounds.Bottom <= 26, $"{name}: {bounds}");
        }

        // the icons desktop programs drew for themselves are in the sprite now (DESK-09)
        Assert.All(new[] { "clipboard", "paste", "pin", "keyboard", "power" }, n => Assert.Contains(n, sprite));
        Assert.NotNull(MatIcon.Find("palette.Fill"));
    }

    [AvaloniaFact]
    public void Every_resource_the_sources_use_is_there_in_light_and_in_dark()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Paths.Package, "*.*", SearchOption.AllDirectories).Where(f => !f.Contains("obj") && !f.Contains("bin")))
        {
            var text = File.ReadAllText(file);
            if (file.EndsWith(".axaml", StringComparison.Ordinal))
            {
                keys.UnionWith(XamlKey().Matches(text).Select(m => m.Groups[1].Value));
            }
            else if (file.EndsWith(".cs", StringComparison.Ordinal))
            {
                keys.UnionWith(CodeKey().Matches(text).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
            }
        }

        keys.UnionWith(MatState.Standard.Select(s => MatTheme.KeyOf(s.ColorToken)));
        keys.UnionWith(MatTokens.Colors.Select(c => MatTheme.KeyOf(c.Token)));
        keys.UnionWith(MatTokens.AccentLight.Select(r => MatTheme.KeyOf(r.Token)));
        keys.UnionWith(MatTheme.DerivedKeys);
        Assert.Contains("Mat.Surface", keys);
        Assert.Contains("MatButton.Primary", keys);
        keys.UnionWith(["Mat.RadiusXs", "Mat.RadiusSm", "Mat.Radius", "Mat.RadiusLg", "Mat.RadiusXl", "Mat.RadiusPill", "Mat.ControlHeight", "Mat.ControlHeightSm", "Mat.ControlHeightLg", "Mat.FocusRingWidth", "Mat.FontSizeXs", "Mat.FontSizeSm", "Mat.FontSize", "Mat.FontSizeLg", "Mat.FontSizeXl", "Mat.FontSize2xl", "Mat.FontSize3xl", "Mat.Font", "Mat.Mono"]);
        keys.UnionWith(new[] { 1, 2, 3, 4, 5, 6, 8, 10, 12 }.SelectMany(n => new[] { $"Mat.Space{n}", $"Mat.Space{n}.All" }));

        var app = Application.Current!;
        var missing = new List<string>();
        foreach (var key in keys)
        {
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                if (!app.TryGetResource(key, variant, out var value) || value is null)
                {
                    missing.Add($"{key} ({variant})");
                }
            }
        }

        Assert.True(missing.Count == 0, "missing: " + string.Join(", ", missing));
    }

    [AvaloniaFact]
    public void A_brush_of_the_theme_is_the_palette_of_the_accent_in_each_mode()
    {
        var app = Application.Current!;
        foreach (var (variant, dark) in new[] { (ThemeVariant.Light, false), (ThemeVariant.Dark, true) })
        {
            var palette = MatPalette.For(TestApp.Accent, dark);
            foreach (var token in new[] { "--mat-surface", "--mat-accent", "--mat-on-accent", "--mat-accent-soft-strong", "--mat-border", "--mat-text-muted" })
            {
                Assert.True(app.TryGetResource(MatTheme.KeyOf(token), variant, out var value));
                Assert.Equal(MatTheme.ToColor(palette[token]), Assert.IsType<SolidColorBrush>(value).Color);
            }

            Assert.True(app.TryGetResource("SystemAccentColor", variant, out var accent));
            Assert.Equal(MatTheme.ToColor(palette.Accent), accent);
        }

        Assert.Equal("Mat.Surface2", MatTheme.KeyOf("--mat-surface-2"));
        Assert.Equal("Mat.NavBg", MatTheme.KeyOf("--mat-nav-bg"));
        Assert.Equal("Mat.AccentSoftStrong", MatTheme.KeyOf("--mat-accent-soft-strong"));
        Assert.Equal(new CornerRadius(MatTokens.Size.Radius), app.FindResource("Mat.Radius"));
        Assert.Equal(34d, app.FindResource("Mat.ControlHeight"));
    }

    [AvaloniaFact]
    public void The_appearance_is_system_light_or_dark()
    {
        var app = Application.Current!;
        MatTheme.SetAppearance(MatAppearance.Dark);
        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
        MatTheme.SetAppearance(MatAppearance.Light);
        Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);
        MatTheme.SetAppearance(MatAppearance.System);
        Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);
        Assert.Equal(MatAppearance.System, MatTheme.Appearance);
    }

    /// <summary>A window with every control theme of MatDesktop and the Fluent controls it colours.</summary>
    internal static MatWindow Everything()
    {
        var nav = new MatNav { AppName = "Example", Version = "1.4.2", Channel = "release", Height = 360 };
        nav.Items.Add(new MatNavItem("dashboard", "Overview", "Ctrl+1") { IsCurrent = true });
        nav.Items.Add(new MatNavItem("list", "Checks"));
        nav.Settings = new MatNavItem("settings", "Settings");
        var buttons = new WrapPanel
        {
            Children =
            {
                new Button { Content = "Secondary" },
                new Button { Content = "Primary" }.Themed("MatButton.Primary"),
                new Button { Content = "Delete" }.Themed("MatButton.Danger"),
                new Button { Content = "Ghost" }.Themed("MatButton.Ghost"),
                new Button { Content = new MatIcon { Icon = "search" } }.Themed("MatButton.Icon"),
                new Button { Content = "A link" }.Themed("MatButton.Link"),
                new Border { Child = new TextBlock { Text = "Ctrl" } }.Themed("MatKeyCap"),
                new Border { Child = new TextBlock { Text = "3" } }.Themed("MatBadge"),
            },
        };
        var tabs = new TabControl { ItemsSource = new[] { new TabItem { Header = "General", Content = new TextBlock { Text = "One" } }, new TabItem { Header = "Updates" } }, SelectedIndex = 0 };
        var body = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock { Text = "Page title" }.Themed("MatText.PageTitle"),
                new TextBlock { Text = "Section title" }.Themed("MatText.SectionTitle"),
                new TextBlock { Text = "Muted text" }.Themed("MatText.Muted"),
                new TextBlock { Text = "Faint text" }.Themed("MatText.Faint"),
                buttons,
                new TextBox { Watermark = "Search" }.Themed("MatSearchBox"),
                new ProgressBar { Value = 40 }.Themed("MatProgress"),
                new Border { Child = new MatSettingRow { Title = "Start at sign-in", Description = "In the tray", Content = new ToggleSwitch { IsChecked = true } } }.Themed("MatCard"),
                new MatFormField { Label = "Server", Hint = "The address", Error = "Not reachable", Content = new TextBox { Text = "monitor.example.com" } },
                new MatFacts { Facts = [new MatFact("Computer", "ANNA-PC"), new MatFact("Port", "8443", IsMono: true)] },
                new MatStateDot { State = MatState.Waiting },
                new MatCopyField("Password", "x7Q-2mP"),
                new CheckBox { Content = "A check", IsChecked = true },
                new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, SelectedIndex = 0 },
                tabs,
            },
        };
        var bar = new MatTitleBar { AppName = "Example", Center = new TextBox { Watermark = "Search" }.Themed("MatSearchBox") };
        nav.TitleBar = bar;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetColumnSpan(bar, 2);
        Grid.SetRow(nav, 1);
        var scroll = new ScrollViewer { Content = body };
        Grid.SetRow(scroll, 1);
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(bar);
        grid.Children.Add(nav);
        grid.Children.Add(scroll);
        return new MatWindow { Width = 1000, Height = 1100, Content = grid };
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_control_theme_draws_in_light_and_dark_without_a_warning(bool dark)
    {
        Ui.Theme(dark);
        AvaloniaLog.Instance.Clear();
        var window = Ui.Show(Everything());
        using var frame = Ui.Frame(window);
        var lines = AvaloniaLog.Instance.Lines;
        window.Close();
        Assert.True(lines.Count == 0, string.Join("\n", lines));

        // the page shows the background of the mode: the corner under the window
        var bg = MatPalette.For(TestApp.Accent, dark).Bg;
        var pixel = Pixel(frame, (int)frame.Size.Width - 3, (int)frame.Size.Height - 3);
        Assert.True(Near(pixel, MatTheme.ToColor(bg)), $"{(dark ? "dark" : "light")}: {pixel} instead of {bg}");
    }

    [AvaloniaFact]
    public void The_menu_folds_to_a_rail_narrows_in_a_narrow_window_and_its_version_opens_about()
    {
        Ui.Theme(false);
        var nav = new MatNav { AppName = "Example", Version = "1.4.2", Channel = "release" };
        var overview = new MatNavItem("dashboard", "Overview");
        nav.Items.Add(overview);
        nav.Settings = new MatNavItem("settings", "Settings");
        var folds = new List<bool>();
        var about = 0;
        nav.FoldChanged += (_, folded) => folds.Add(folded);
        nav.AboutRequested += (_, _) => about++;
        var window = Ui.Show(new MatWindow { Width = 1180, Height = 600, Content = new DockPanel { Children = { nav } } });

        Assert.Equal(MatNav.OpenWidth, nav.Width);
        Assert.Equal("Version 1.4.2 · release", nav.VersionButton.Content);
        nav.VersionButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, about);

        nav.FoldButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Ui.Settle(window);
        Assert.True(nav.IsFolded);
        Assert.Equal(MatNav.RailWidth, nav.Width);
        Assert.Equal([true], folds);
        Assert.True(overview.IsFolded);
        Assert.Equal("Overview", ToolTip.GetTip(overview));

        nav.IsFolded = false;
        Assert.Equal(MatNav.NarrowWidth, MatNav.WidthFor(false, 800));
        nav.ApplyWindowWidth(800);
        Assert.Equal(MatNav.NarrowWidth, nav.Width);

        overview.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Same(overview, nav.Current);
        Assert.True(overview.Classes.Contains(":current") || overview.IsCurrent);
        window.Close();
    }

    [AvaloniaFact]
    public void The_title_bar_has_the_caption_buttons_of_the_system_with_names_and_close_can_be_turned_into_hide()
    {
        Ui.Theme(false);
        var bar = new MatTitleBar { AppName = "Example", CloseKey = "Esc", ShowCaptionButtons = true };
        var window = Ui.Show(new MatWindow { Width = 900, Height = 300, Title = "Example", Content = new DockPanel { Children = { bar } } });
        Assert.Equal(["Minimise", "Maximise", "Close"], bar.CaptionButtons.Select(Avalonia.Automation.AutomationProperties.GetName));
        Assert.Equal("Close (Esc)", ToolTip.GetTip(bar.CaptionButtons[2]));
        Assert.All(bar.CaptionButtons, b => Assert.Equal((46d, 32d), (b.Width, b.Height)));
        Assert.Equal("Example", window.Title);

        var asked = 0;
        bar.CloseRequested += (_, e) =>
        {
            asked++;
            e.Handled = true; // a program with a tray hides the window instead (DESK-06)
        };
        bar.CaptionButtons[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Ui.Settle(window);
        Assert.Equal(1, asked);
        Assert.True(window.IsVisible);

        bar.CaptionButtons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Ui.Settle(window);
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal("Restore", Avalonia.Automation.AutomationProperties.GetName(bar.CaptionButtons[1]));
        window.Close();
    }

    [AvaloniaFact]
    public void The_tray_icons_draw_the_dot_of_the_state_in_its_colour_and_a_shape_for_macos()
    {
        var mark = Mark();
        var palette = MatPalette.For(TestApp.Accent, dark: false);
        foreach (var state in MatState.Standard)
        {
            using var icon = MatTrayIcons.Draw(mark, state, template: false);
            var dot = Pixel(icon, 48, 48);
            Assert.True(Near(dot, MatTheme.ToColor(palette[state.TrayColorToken])), $"{state.Word}: {dot}");

            // the gap around the dot is transparent, the rest of the mark is drawn
            Assert.Equal(0, Pixel(icon, 31, 48).A);
            Assert.Equal(255, Pixel(icon, 10, 10).A);
        }

        using (var running = MatTrayIcons.Draw(mark, MatState.Running, template: true))
        {
            Assert.Equal(255, Pixel(running, 48, 48).A); // no dot: the mark itself
        }

        using (var stopped = MatTrayIcons.Draw(mark, MatState.Stopped, template: true))
        {
            Assert.Equal(0, Pixel(stopped, 48, 48).A); // the cross is cut out of the dot
            Assert.Equal(255, Pixel(stopped, 48, 39).A);
        }

        var png = MatTrayIcons.Png(mark, MatState.Waiting, template: false);
        Assert.Equal((byte)'P', png[1]);
    }

    [AvaloniaFact]
    public void The_tray_menu_becomes_a_native_menu_in_its_order()
    {
        var menu = new MatTrayMenu("Example Agent", "1.4.2", () => { }) { Open = () => { }, Info = () => { }, LogFolder = () => { } };
        menu.AddService(MatServiceAction.Restart, () => { });
        var native = MatTray.ToNativeMenu(menu);
        var texts = native.Items.Select(i => i is NativeMenuItem item ? item.Header : "-").ToList();
        Assert.Equal(["Example Agent 1.4.2", "Running", "-", "Open Example Agent", "-", "Info …", "Open log folder", "-", "Restart service …", "-", "Quit"], texts);
        var items = native.Items.OfType<NativeMenuItem>().ToList();
        Assert.False(items[0].IsEnabled);
        Assert.False(items[1].IsEnabled);
        Assert.NotNull(items.Single(i => i.Header == "Restart service …").Icon);
        Assert.NotNull(items.Single(i => i.Header == "Quit").Command);
    }

    /// <summary>The mark of the sample program: the glyph of the app in white on its accent (drawn with MatDesktop's own icon; the colours are the test's).</summary>
    internal static Bitmap Mark(int size = 64)
    {
        var tile = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.22),
            Background = new SolidColorBrush(Color.Parse(TestApp.Accent)),
            Child = new MatIcon { Icon = "activity", Size = size * 0.6, Foreground = new SolidColorBrush(Color.Parse("#ffffff")), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
        };
        var host = new Window { Content = tile, SizeToContent = SizeToContent.WidthAndHeight };
        host.Show();
        tile.Measure(new Size(size, size));
        tile.Arrange(new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        bitmap.Render(tile);
        host.Close();
        return bitmap;
    }

    internal static Color Pixel(Bitmap bitmap, int x, int y)
    {
        var size = bitmap.PixelSize;
        var buffer = new byte[size.Width * size.Height * 4];
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(buffer.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), memory, buffer.Length, size.Width * 4);
            System.Runtime.InteropServices.Marshal.Copy(memory, buffer, 0, buffer.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(memory);
        }

        var i = (y * size.Width + x) * 4;
        var format = bitmap.Format ?? Avalonia.Platform.PixelFormat.Bgra8888;
        var (r, g, b, a) = format == Avalonia.Platform.PixelFormat.Rgba8888
            ? (buffer[i], buffer[i + 1], buffer[i + 2], buffer[i + 3])
            : (buffer[i + 2], buffer[i + 1], buffer[i], buffer[i + 3]);

        // premultiplied: back to the colour
        return a == 0 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(a, (byte)Math.Min(255, r * 255 / a), (byte)Math.Min(255, g * 255 / a), (byte)Math.Min(255, b * 255 / a));
    }

    internal static bool Near(Color a, Color b, int tolerance = 3) =>
        Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;
}
