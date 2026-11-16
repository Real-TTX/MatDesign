using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace MatDesktop.Tests;

/// <summary>
/// What 0.2.0 added from moving programs onto MatDesktop: the foot, the update link, the fold shortcut and the trimmed version of the menu (DESK-04, DESK-07, SETUP-06, SETUP-07), the filled
/// icon, a tray that draws its icon only when the state's look changes and tells when its menu opens, the window icon of every window, the texts and the word mark of the setup, the dialog
/// chrome and the new control themes (COLOR-08).
/// </summary>
public sealed class ReleaseTwoTests
{
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void The_menu_has_a_foot_of_the_programs_own_above_the_version()
    {
        Ui.Theme(false);
        var card = new Border { Child = new TextBlock { Text = "Keep entries for 30 days" } }.Themed("MatCard");
        var nav = new MatNav { AppName = "Example", Version = "1.4.2", Foot = card };
        var window = Ui.Show(new MatWindow { Width = 1000, Height = 600, Content = new DockPanel { Children = { nav } } });

        Assert.True(card.IsEffectivelyVisible);
        var cardTop = card.TranslatePoint(default, nav)!.Value.Y;
        var versionTop = nav.VersionButton.TranslatePoint(default, nav)!.Value.Y;
        Assert.True(cardTop + card.Bounds.Height <= versionTop, $"the foot ({cardTop}) is not above the version ({versionTop})");

        nav.Foot = null;
        Ui.Settle(window);
        Assert.Null(card.Parent is ContentControl host ? host.Content : null);
        window.Close();
    }

    [AvaloniaFact]
    public void A_waiting_update_is_a_link_above_the_version_and_an_icon_on_the_rail()
    {
        Ui.Theme(false);
        var nav = new MatNav { AppName = "Example", Version = "1.4.2" };
        var requested = 0;
        nav.UpdateRequested += (_, _) => requested++;
        var window = Ui.Show(new MatWindow { Width = 1000, Height = 600, Content = new DockPanel { Children = { nav } } });
        Assert.False(nav.UpdateButton.IsVisible);

        nav.UpdateVersion = "1.5.0";
        Ui.Settle(window);
        Assert.True(nav.UpdateButton.IsEffectivelyVisible);
        Assert.Contains(nav.UpdateButton.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Update to 1.5.0" && t.IsVisible);
        Assert.Equal("Update to 1.5.0 available", ToolTip.GetTip(nav.UpdateButton));
        Assert.Equal("Update to 1.5.0 available", Avalonia.Automation.AutomationProperties.GetName(nav.UpdateButton));
        Assert.True(nav.UpdateButton.TranslatePoint(default, nav)!.Value.Y < nav.VersionButton.TranslatePoint(default, nav)!.Value.Y);
        Click(nav.UpdateButton);
        Assert.Equal(1, requested);

        // folded: the icon only, the words in the tooltip
        nav.IsFolded = true;
        Ui.Settle(window);
        Assert.True(nav.UpdateButton.IsEffectivelyVisible);
        Assert.DoesNotContain(nav.UpdateButton.GetLogicalDescendants().OfType<TextBlock>(), t => t.IsVisible);
        Assert.Equal("Update to 1.5.0 available", ToolTip.GetTip(nav.UpdateButton));

        nav.UpdateVersion = null;
        Ui.Settle(window);
        Assert.False(nav.UpdateButton.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void The_fold_button_names_its_shortcut_and_a_long_version_is_trimmed_with_all_of_it_in_the_tooltip()
    {
        Ui.Theme(false);
        var nav = new MatNav { AppName = "Example", Version = "local-20261115", Channel = MatVersion.Local };
        var window = Ui.Show(new MatWindow { Width = 800, Height = 600, Content = new DockPanel { Children = { nav } } });
        Assert.Equal("Fold the menu", ToolTip.GetTip(nav.FoldButton));
        nav.FoldShortcut = "Ctrl+B";
        Assert.Equal("Fold the menu (Ctrl+B)", ToolTip.GetTip(nav.FoldButton));
        Assert.Equal("Fold the menu", Avalonia.Automation.AutomationProperties.GetName(nav.FoldButton));

        // 210 px of menu in a narrow window: the version does not fit and is trimmed, never cut or wrapped; the tooltip has it all
        Ui.Settle(window);
        var text = Assert.IsType<TextBlock>(nav.VersionButton.Content);
        Assert.Equal("Version local-20261115 · local", text.Text);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.True(nav.VersionButton.Bounds.Right <= nav.FoldButton.Bounds.Left + 0.5, $"the version ({nav.VersionButton.Bounds}) runs under the fold button ({nav.FoldButton.Bounds})");
        Assert.True(text.DesiredSize.Height < 24, "the version wrapped");
        Assert.Equal("About Example – Version local-20261115 · local", ToolTip.GetTip(nav.VersionButton));

        nav.IsFolded = true;
        Assert.Equal("Open the menu (Ctrl+B)", ToolTip.GetTip(nav.FoldButton));
        window.Close();
    }

    [AvaloniaFact]
    public void A_filled_icon_fills_the_inside_of_the_same_geometry()
    {
        Ui.Theme(false);
        Bitmap Draw(bool filled)
        {
            var icon = new MatIcon { Icon = "star", Size = 48, IsFilled = filled, Foreground = Brushes.Black };
            var host = new Window { Content = icon, SizeToContent = SizeToContent.WidthAndHeight };
            host.Show();
            icon.Measure(new Size(48, 48));
            icon.Arrange(new Rect(0, 0, 48, 48));
            var bitmap = new RenderTargetBitmap(new PixelSize(48, 48));
            bitmap.Render(icon);
            host.Close();
            return bitmap;
        }

        using var open = Draw(false);
        using var filled = Draw(true);
        Assert.Equal(0, ThemeTests.Pixel(open, 24, 26).A);      // the middle of the star is empty when it is stroked
        Assert.Equal(255, ThemeTests.Pixel(filled, 24, 26).A);  // and inked when it is filled
        Assert.Equal(ThemeTests.Pixel(open, 24, 3).A, ThemeTests.Pixel(filled, 24, 3).A); // the tip is the same: the outline stays
    }

    [AvaloniaFact]
    public void The_tray_draws_its_icon_only_when_the_look_of_the_state_changes_and_says_when_its_menu_opens()
    {
        var menu = new MatTrayMenu("Example", "1.4.2", () => { }) { Open = () => { } };
        using var tray = new MatTray(Avalonia.Application.Current!, menu, ThemeTests.Mark());
        Assert.Equal(1, tray.IconDraws);

        // a new item, a new tooltip, a paused state whose words change: no new picture
        menu.AddOwn("Sync now", () => { });
        menu.TooltipText = "Synchronised at 14:32";
        tray.Refresh();
        Assert.Equal(1, tray.IconDraws);
        Assert.Equal("Example: Running\nSynchronised at 14:32", tray.ToolTipText);
        Assert.Contains(tray.NativeMenu.Items.OfType<NativeMenuItem>(), i => i.Header == "Sync now");

        menu.State = MatState.Custom("Paused until 14:45", "--mat-warning", MatStateMark.Pause);
        tray.Refresh();
        Assert.Equal(2, tray.IconDraws);
        menu.State = MatState.Custom("Paused until 15:00", "--mat-warning", MatStateMark.Pause);
        tray.Refresh();
        Assert.Equal(2, tray.IconDraws);
        menu.State = MatState.Running;
        tray.Refresh();
        Assert.Equal(3, tray.IconDraws);

        // the menu opens: the program fills the model, the menu is built after it
        var opened = 0;
        tray.MenuOpening += (_, _) =>
        {
            opened++;
            menu.ClearOwn();
            menu.AddOwn("Copied just now", () => { });
        };
        // what the exporter of macOS or Linux does when the menu opens (the bridge is internal to Avalonia)
        var raise = typeof(INativeMenuExporterEventsImplBridge).GetMethod("RaiseOpening", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
        raise.Invoke(tray.NativeMenu, null);
        Assert.Equal(1, opened);
        Assert.Contains(tray.NativeMenu.Items.OfType<NativeMenuItem>(), i => i.Header == "Copied just now");
        Assert.DoesNotContain(tray.NativeMenu.Items.OfType<NativeMenuItem>(), i => i.Header == "Sync now");
        Assert.Equal(3, tray.IconDraws);
    }

    [AvaloniaFact]
    public void Open_shows_its_shortcut_in_the_native_menu()
    {
        var menu = new MatTrayMenu("Example", "1.4.2", () => { }) { Open = () => { }, OpenShortcut = "Ctrl+Shift+V" };
        var open = MatTray.ToNativeMenu(menu).Items.OfType<NativeMenuItem>().Single(i => i.Header == "Open Example");
        Assert.Equal(Avalonia.Input.KeyGesture.Parse("Ctrl+Shift+V"), open.Gesture);

        // a shortcut the menu cannot read as a key goes after a tab
        menu.OpenShortcut = "Ctrl+Option+V";
        var mac = MatTray.ToNativeMenu(menu).Items.OfType<NativeMenuItem>().Single(i => i.Header!.StartsWith("Open Example", StringComparison.Ordinal));
        Assert.Equal("Open Example\tCtrl+Option+V", mac.Header);
    }

    [AvaloniaFact]
    public void Every_window_takes_the_default_icon()
    {
        Ui.Theme(false);
        var icon = new WindowIcon(ThemeTests.Mark(32));
        Assert.Null(new MatWindow().Icon);
        MatWindow.DefaultIcon = icon;
        try
        {
            Assert.Same(icon, new MatWindow().Icon);
            Assert.Same(icon, new MatInfoWindow(new MatInfo("Example", "1.4.2", "release", MatState.Running, "Fine.", [])).Icon);
            var own = new WindowIcon(ThemeTests.Mark(16));
            Assert.Same(own, new MatWindow { Icon = own }.Icon);
        }
        finally
        {
            MatWindow.DefaultIcon = null;
        }

        Assert.Null(new MatThemeOptions("#295cff").Icon);
    }

    [AvaloniaFact]
    public void The_setup_names_the_programs_data_and_takes_a_word_mark()
    {
        Ui.Theme(false);
        var mark = new TextBlock { Text = "ExampleMark" };
        var window = Ui.Show(new MatSetupWindow(new MatSetup("Example", "1.4.2", "release", MatSetupMode.Uninstall,
            [MatSetupStep.Uninstall("The program goes.")], _ => Task.CompletedTask)
        {
            WordMark = mark,
            DeleteDataText = "Also delete my history",
            DeleteDataDescription = "The entries of the clipboard on this computer.",
        }));

        var rows = window.GetLogicalDescendants().OfType<MatSettingRow>().ToList();
        Assert.Contains(rows, r => r.Title == "Also delete my history" && r.Description == "The entries of the clipboard on this computer.");
        Assert.False(window.DeleteData);
        Assert.True(mark.IsEffectivelyVisible);
        Assert.Equal("Example", Avalonia.Automation.AutomationProperties.GetName(mark));
        Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Example");
        window.Close();

        // without them the texts of MatDesktop
        var plain = Ui.Show(new MatSetupWindow(new MatSetup("Example", "1.4.2", "release", MatSetupMode.Uninstall, [MatSetupStep.Uninstall("The program goes.")], _ => Task.CompletedTask)));
        Assert.Contains(plain.GetLogicalDescendants().OfType<MatSettingRow>(), r => r.Title == "Also delete my data");
        plain.Close();
    }

    [AvaloniaFact]
    public void A_dialog_of_the_programs_own_takes_the_header_and_the_footer()
    {
        Ui.Theme(false);
        var ok = new Button { Content = "Pause" }.Themed("MatButton.Primary");
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var header = MatDialogChrome.Header(null, "Example", "Pause");
        var footer = MatDialogChrome.Footer(null, ok, cancel);
        var window = Ui.Show(new MatWindow { Width = 400, Height = 300, Content = MatDialogChrome.Compose(header, new TextBlock { Text = "How long?" }, footer) });

        Assert.True(header.Bounds.Bottom <= footer.Bounds.Top);
        Assert.Equal(window.Bounds.Height, footer.Bounds.Bottom, 1);
        var row = (StackPanel)ok.Parent!;
        Assert.Equal([ok, cancel], row.Children);
        Assert.Equal("Version 1.4.2 · release", MatDialogChrome.VersionText("1.4.2", "release").Text);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("MatChoiceRow")]
    [InlineData("MatCardRow")]
    public void A_chosen_row_is_a_tint_with_the_accent_line_all_around(string theme)
    {
        Ui.Theme(false);
        var list = new ListBox { ItemsSource = new[] { "First", "Second" }, SelectedIndex = 1, Width = 300 }.Themed("MatList");
        list.Use(ItemsControl.ItemContainerThemeProperty, theme);
        var window = Ui.Show(new MatWindow { Width = 400, Height = 300, Content = list });
        var chosen = list.ContainerFromIndex(1)!;
        var other = list.ContainerFromIndex(0)!;
        var frame = chosen.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Frame");
        var otherFrame = other.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Frame");
        var palette = MatPalette.For(TestApp.Accent, dark: false);
        Assert.Equal(MatTheme.ToColor(palette.Accent), Assert.IsAssignableFrom<ISolidColorBrush>(frame.BorderBrush).Color);
        Assert.Equal(MatTheme.ToColor(palette["--mat-accent-soft"]), Assert.IsAssignableFrom<ISolidColorBrush>(frame.Background).Color);

        // the line is all around (never a bar at one side, COLOR-08)
        var line = frame.BorderThickness;
        Assert.True(line.Left > 0 && line.Left == line.Right && line.Top == line.Bottom && line.Left == line.Top, $"{line}");
        Assert.NotEqual(MatTheme.ToColor(palette.Accent), (otherFrame.BorderBrush as ISolidColorBrush)?.Color);
        window.Close();
    }

    [AvaloniaFact]
    public void The_warning_button_and_the_soft_badge_are_in_the_colours_of_the_tokens()
    {
        Ui.Theme(false);
        var resume = new Button { Content = "Resume" }.Themed("MatButton.Warning");
        var badge = new Border { Child = new TextBlock { Text = "OCR" } }.Themed("MatBadge.Soft");
        var window = Ui.Show(new MatWindow { Width = 400, Height = 200, Content = new StackPanel { Children = { resume, badge } } });
        var palette = MatPalette.For(TestApp.Accent, dark: false);
        Assert.Equal(MatTheme.ToColor(palette["--mat-warning-text"]), Assert.IsAssignableFrom<ISolidColorBrush>(resume.Foreground).Color);
        Assert.Equal(MatTheme.ToColor(palette["--mat-warning-bg"]), Assert.IsAssignableFrom<ISolidColorBrush>(resume.Background).Color);
        Assert.Equal(MatTheme.ToColor(palette["--mat-accent-soft-strong"]), Assert.IsAssignableFrom<ISolidColorBrush>(badge.Background).Color);
        Assert.Equal(MatTheme.ToColor(palette.AccentText), Assert.IsAssignableFrom<ISolidColorBrush>(((TextBlock)badge.Child!).Foreground).Color);
        window.Close();
    }
}
