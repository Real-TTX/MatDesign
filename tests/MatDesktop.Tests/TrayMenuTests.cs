namespace MatDesktop.Tests;

/// <summary>The tray menu of TRAY-01 in the order of the rule, whatever a program has and in whatever order it adds it; the version of SETUP-07; the states of TRAY-02.</summary>
public sealed class TrayMenuTests
{
    private static readonly MatTrayRole[] Order =
    [
        MatTrayRole.Title, MatTrayRole.State, MatTrayRole.Open, MatTrayRole.Own, MatTrayRole.Info, MatTrayRole.SetUp, MatTrayRole.Settings, MatTrayRole.LogFolder,
        MatTrayRole.InstallUpdate, MatTrayRole.Service, MatTrayRole.HideIcon, MatTrayRole.Quit,
    ];

    private static void Nothing()
    {
    }

    /// <summary>Every combination of what a program can have: own items, Info, Set up or Settings or neither, the log folder, an update that waits, services, Hide icon.</summary>
    public static TheoryData<int> Combinations()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 2 * 2 * 3 * 2 * 2 * 2 * 2; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Every_combination_is_in_the_order_of_the_rule_with_quit_last_and_no_separator_twice(int combination)
    {
        var bits = combination;
        bool Take(int states, out int value)
        {
            value = bits % states;
            bits /= states;
            return value > 0;
        }

        var menu = new MatTrayMenu("Example Agent", "1.4.2", Nothing) { State = MatState.Waiting };
        var own = Take(2, out _);
        var info = Take(2, out _);
        Take(3, out var configure);
        var log = Take(2, out _);
        var update = Take(2, out _);
        var services = Take(2, out _);
        var hide = Take(2, out _);

        // added in an order the menu does not have: the last first
        if (hide) { menu.HideIcon = Nothing; }
        if (services) { menu.AddService(MatServiceAction.Restart, Nothing).AddService(MatServiceAction.Start, Nothing).AddService(MatServiceAction.Stop, Nothing, needsElevation: true); }
        if (update) { menu.InstallUpdate = Nothing; }
        if (log) { menu.LogFolder = Nothing; }
        if (configure == 1) { menu.SetUp = Nothing; }
        if (configure == 2) { menu.Settings = Nothing; }
        if (info) { menu.Info = Nothing; }
        if (own) { menu.AddOwn("Pause", Nothing).AddOwn("Sync now", Nothing); }
        menu.Open = Nothing;

        var entries = menu.Build();
        var roles = entries.Where(e => e.Role != MatTrayRole.Separator).Select(e => e.Role).ToList();
        Assert.Equal(roles.OrderBy(r => Array.IndexOf(Order, r)), roles);
        Assert.Equal([MatTrayRole.Title, MatTrayRole.State, MatTrayRole.Open], roles.Take(3));
        Assert.Equal(MatTrayRole.Quit, entries[^1].Role);
        Assert.NotEqual(MatTrayRole.Separator, entries[0].Role);
        for (var i = 1; i < entries.Count; i++)
        {
            Assert.False(entries[i].Role == MatTrayRole.Separator && entries[i - 1].Role == MatTrayRole.Separator, "two separators");
        }

        // a separator after the state, around the program's own items, before the services and before Quit
        Assert.Equal(MatTrayRole.Separator, entries[2].Role);
        Assert.Equal(MatTrayRole.Separator, entries[entries.ToList().FindLastIndex(e => e.Role != MatTrayRole.Quit && e.Role != MatTrayRole.HideIcon)].Role);
        if (own)
        {
            var first = entries.ToList().FindIndex(e => e.Role == MatTrayRole.Own);
            Assert.Equal(MatTrayRole.Separator, entries[first - 1].Role);
            Assert.Equal(MatTrayRole.Separator, entries[first + 2].Role);
            Assert.Equal(["Pause", "Sync now"], entries.Where(e => e.Role == MatTrayRole.Own).Select(e => e.Text));
        }

        Assert.False(entries[0].IsEnabled);
        Assert.False(entries[1].IsEnabled);
        Assert.Equal("Example Agent 1.4.2", entries[0].Text);
        Assert.Equal("Waiting", entries[1].Text);
        Assert.Single(entries, e => e.IsDefault);
        Assert.True(entries.Single(e => e.IsDefault).Role == MatTrayRole.Open);
        Assert.Equal("Open Example Agent", entries.Single(e => e.Role == MatTrayRole.Open).Text);
        Assert.Equal(update, roles.Contains(MatTrayRole.InstallUpdate));
        if (services)
        {
            Assert.Equal(["Start service …", "Stop service …", "Restart service …"], entries.Where(e => e.Role == MatTrayRole.Service).Select(e => e.Text));
            Assert.All(entries.Where(e => e.Role == MatTrayRole.Service), e => Assert.True(e.NeedsElevation));
        }

        if (hide)
        {
            Assert.Equal(MatTrayRole.HideIcon, entries[^2].Role);
        }

        // no Uninstall in a tray, in any language (TRAY-01, SETUP-04)
        Assert.DoesNotContain(entries, e => e.Text.Contains("Uninstall", StringComparison.OrdinalIgnoreCase) || e.Text.Contains("Deinstall", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(MatText.English.Where(t => t.Key.StartsWith("tray.", StringComparison.Ordinal)), t => t.Value.Contains("Uninstall", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_menu_needs_open_and_has_set_up_or_settings_not_both()
    {
        var menu = new MatTrayMenu("Example", "1.0.0", Nothing);
        Assert.Throws<InvalidOperationException>(() => menu.Build());
        menu.Open = Nothing;
        menu.SetUp = Nothing;
        menu.Settings = Nothing;
        Assert.Throws<InvalidOperationException>(() => menu.Build());
        menu.SetUp = null;
        Assert.Contains(menu.Build(), e => e.Role == MatTrayRole.Settings && e.Text == "Settings …");
    }

    [Fact]
    public void The_smallest_menu_is_name_state_open_and_quit()
    {
        var menu = new MatTrayMenu("Example", "2.0.1", Nothing) { Open = Nothing, State = MatState.NotSetUp };
        Assert.Equal(
            ["Example 2.0.1", "Not set up", string.Empty, "Open Example", string.Empty, "Quit"],
            menu.Build().Select(e => e.Text));
    }

    [Fact]
    public void An_own_item_without_an_action_is_shown_disabled_and_a_cleared_menu_forgets_them()
    {
        var menu = new MatTrayMenu("Example", "1.0.0", Nothing) { Open = Nothing };
        menu.AddOwn("Nothing copied yet", null);
        Assert.False(menu.Build().Single(e => e.Role == MatTrayRole.Own).IsEnabled);
        menu.ClearOwn();
        Assert.DoesNotContain(menu.Build(), e => e.Role == MatTrayRole.Own);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_left_click_opens_on_windows_and_shows_the_menu_elsewhere(bool windows, bool opens)
        => Assert.Equal(opens, MatTrayMenu.LeftClickOpens(windows));

    [Fact]
    public void The_four_states_have_their_colour_their_word_and_their_mark()
    {
        Assert.Equal(
            [("Running", "--mat-success"), ("Waiting", "--mat-warning"), ("Stopped", "--mat-danger"), ("Not set up", "--mat-text-muted")],
            MatState.Standard.Select(s => (s.Word, s.ColorToken)));
        Assert.Equal(MatStateMark.None, MatState.Running.TemplateMark);
        Assert.All(MatState.Standard.Skip(1), s => Assert.NotEqual(MatStateMark.None, s.TemplateMark));
        Assert.Equal("Example Agent: Running", MatState.Running.Tooltip("Example Agent"));
        var paused = MatState.Custom("Paused", "--mat-warning", MatStateMark.Pause);
        Assert.Equal(MatStateKind.Custom, paused.Kind);
        Assert.Throws<ArgumentException>(() => MatState.Custom("Odd", "--mat-nope", MatStateMark.Dot));
    }

    [Theory]
    [InlineData("1.4.2", "release", "Version 1.4.2 · release")]
    [InlineData("nightly-212-20261115", "nightly", "Version nightly-212-20261115 · nightly")]
    [InlineData("local-20261115", "local", "Version local-20261115 · local")]
    public void The_version_reads_the_same_everywhere(string version, string channel, string expected)
    {
        Assert.Equal(expected, MatVersion.Format(version, channel));
        // the value of a row labelled Version is the same text without the word
        Assert.Equal(expected, "Version " + MatVersion.Value(version, channel));
        Assert.Equal(channel != "local", MatVersion.GetsUpdates(channel));
        Assert.Equal(channel == "local", MatVersion.LocalNote(channel) is not null);
    }

    [Fact]
    public void Open_may_say_what_it_opens_and_stays_the_default_in_its_place()
    {
        var menu = new MatTrayMenu("Example Agent", "1.4.2", () => { }) { Open = () => { }, OpenText = "Open Example Kiosk", Info = () => { } };
        var open = menu.Build().Single(e => e.Role == MatTrayRole.Open);
        Assert.Equal("Open Example Kiosk", open.Text);
        Assert.True(open.IsDefault);
        Assert.Equal(MatTrayRole.Open, menu.Build().First(e => e.IsEnabled).Role);
        menu.OpenText = " ";
        Assert.Equal(MatText.Format("tray.open", "Example Agent"), menu.Build().Single(e => e.Role == MatTrayRole.Open).Text);
    }

    [Fact]
    public void The_grey_of_not_set_up_is_seen_on_a_light_and_a_dark_task_bar_and_the_danger_line_is_in_the_palette()
    {
        var light = MatPalette.For(null, dark: false);
        foreach (var bar in new[] { "#f3f3f3", "#202020" })
        {
            Assert.True(MatColor.Contrast(light[MatState.NotSetUp.TrayColorToken], MatRgba.Parse(bar)) >= 3, bar);
        }

        Assert.Equal("--mat-text-muted", MatState.NotSetUp.ColorToken);
        Assert.All(MatState.Standard.Where(s => s.Kind != MatStateKind.NotSetUp), s => Assert.Equal(s.ColorToken, s.TrayColorToken));
        Assert.Equal(MatColor.Transparent(light.Danger, 0.45), light.DangerLine);
    }

    [Fact]
    public void The_version_needs_a_number_and_a_channel_and_the_tray_names_the_number()
    {
        Assert.Throws<ArgumentException>(() => MatVersion.Format(" ", "release"));
        Assert.Throws<ArgumentException>(() => MatVersion.Format("1.0.0", ""));
        Assert.Equal("Example Agent 1.4.2", MatVersion.Title("Example Agent", "1.4.2"));
        Assert.Equal("A local build gets no updates.", MatVersion.LocalNote("local"));
        Assert.Equal("Version 1.4.2 · release", MatText.German["version"].Replace("{0}", "1.4.2").Replace("{1}", "release"));
    }
}
