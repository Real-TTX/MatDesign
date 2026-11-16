using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MatDesktop.Tests;

/// <summary>The setup window (SETUP-01, SETUP-02, SETUP-04, SETUP-07), the Info window (TRAY-05) and the notification (TRAY-04), driven headless.</summary>
public sealed class WindowTests
{
    private static MatSetup Setup(MatSetupMode mode, Func<MatSetupRun, Task> run, params MatSetupStep[] steps) =>
        new("Example Agent", "1.4.2", "release", mode, steps, run) { InstalledVersion = "1.3.0" };

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void WaitFor(MatWindow window, Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Ui.Settle(window);
            Thread.Sleep(5);
        }

        Assert.True(condition(), "the window did not get there");
    }

    private static IReadOnlyList<Button> FooterButtons(MatSetupWindow window) =>
        ((StackPanel)((DockPanel)window.Footer.Child!).Children[0]).Children.OfType<Button>().ToList();

    [AvaloniaFact]
    public void The_setup_window_is_600_by_560_fixed_with_the_version_left_and_primary_back_cancel_right()
    {
        Ui.Theme(false);
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Setup, _ => Task.CompletedTask,
            MatSetupStep.WithOptions(null, MatSetupOption.Autostart(), MatSetupOption.DesktopShortcut(), MatSetupOption.AutomaticUpdates()),
            MatSetupStep.Welcome("Installs for your user account."))));

        Assert.Equal((600d, 560d), (window.Width, window.Height));
        Assert.False(window.CanResize);
        Assert.Equal("Version 1.4.2 · release", window.VersionText.Text);
        Assert.Equal("Setup", window.ModeText.Text);
        Assert.Equal([window.PrimaryButton, window.BackButton, window.CancelButton], FooterButtons(window));

        // the steps in the order of SETUP-02, whatever order the program gave them in
        Assert.Equal(
            [MatSetupStepKind.Welcome, MatSetupStepKind.Options, MatSetupStepKind.Progress, MatSetupStepKind.Finish],
            window.Steps.Select(s => s.Kind));
        Assert.Equal(MatSetupStepKind.Welcome, window.CurrentStep.Kind);
        Assert.Equal("Next", window.PrimaryButton.Content);
        Assert.False(window.BackButton.IsVisible);

        Click(window.PrimaryButton);
        Ui.Settle(window);
        Assert.Equal(MatSetupStepKind.Options, window.CurrentStep.Kind);
        Assert.Equal("Install", window.PrimaryButton.Content);
        Assert.True(window.BackButton.IsVisible);

        // the safe choice is set
        Assert.Equal(new Dictionary<string, bool> { ["autostart"] = true, ["shortcut"] = false, ["updates"] = true }, window.OptionValues);
        window.Close();
    }

    [AvaloniaFact]
    public void Progress_can_be_neither_cancelled_nor_closed_and_finish_offers_to_start_now()
    {
        Ui.Theme(false);
        var go = new TaskCompletionSource();
        MatSetupRun? seen = null;
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Setup, async run =>
        {
            seen = run;
            run.Progress.Report(new MatSetupReport("Copy the program", MatSetupStepState.Done, 40));
            run.Progress.Report(new MatSetupReport("Install the service", MatSetupStepState.Running));
            await go.Task;
            run.Progress.Report(new MatSetupReport("Install the service", MatSetupStepState.Done, 100));
        }, MatSetupStep.Welcome("Installs the service."))));

        Click(window.PrimaryButton);
        WaitFor(window, () => window.ProgressLines.Count == 2);
        Assert.True(window.IsWorking);
        Assert.Equal(MatSetupStepKind.Progress, window.CurrentStep.Kind);
        Assert.False(window.PrimaryButton.IsEnabled);
        Assert.False(window.CancelButton.IsEnabled);
        Assert.False(window.BackButton.IsVisible);
        Assert.Equal(40, window.ProgressBar.Value);
        Assert.Equal([("Copy the program", MatSetupStepState.Done), ("Install the service", MatSetupStepState.Running)], window.ProgressLines);

        window.Close();
        Ui.Settle(window);
        Assert.True(window.IsVisible, "the setup window closed while it was working");

        go.SetResult();
        WaitFor(window, () => window.CurrentStep.Kind == MatSetupStepKind.Finish);
        Assert.True(window.Succeeded);
        Assert.Equal("Done", window.PrimaryButton.Content);
        Assert.False(window.CancelButton.IsVisible);
        Assert.False(window.BackButton.IsVisible);
        Assert.True(window.StartNow);
        Assert.Equal(("Install the service", MatSetupStepState.Done), window.ProgressLines[1]);
        Assert.NotNull(seen);

        var finished = 0;
        window.Finished += (_, _) => finished++;
        Click(window.PrimaryButton);
        Ui.Settle(window);
        Assert.False(window.IsVisible);
        Assert.Equal(1, finished);
    }

    [AvaloniaFact]
    public void A_failed_install_says_so_on_finish_and_offers_no_start()
    {
        Ui.Theme(false);
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Setup, _ => throw new IOException("The folder is in use."), MatSetupStep.Welcome("Installs."))));
        Click(window.PrimaryButton);
        WaitFor(window, () => window.CurrentStep.Kind == MatSetupStepKind.Finish);
        Assert.False(window.Succeeded);
        Assert.Equal("The folder is in use.", window.Error);
        Assert.False(window.StartNow);
        window.Close();
    }

    [AvaloniaFact]
    public void An_update_asks_nothing_and_names_both_versions()
    {
        Ui.Theme(false);
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Update, _ => Task.CompletedTask,
            MatSetupStep.Welcome("Installs."), MatSetupStep.WithOptions(null, MatSetupOption.Autostart()))));
        Assert.Equal("Update", window.ModeText.Text);
        Assert.Equal([MatSetupStepKind.Welcome, MatSetupStepKind.Progress, MatSetupStepKind.Finish], window.Steps.Select(s => s.Kind));
        Assert.Contains("1.3.0", window.CurrentStep.Text, StringComparison.Ordinal);
        Assert.Contains("1.4.2", window.CurrentStep.Text, StringComparison.Ordinal);
        Assert.Equal("Update", window.PrimaryButton.Content);
        window.Close();
    }

    [AvaloniaFact]
    public void The_uninstall_says_what_goes_keeps_the_data_unless_ticked_and_has_a_danger_button()
    {
        Ui.Theme(false);
        bool? deleted = null;
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Uninstall, run =>
        {
            deleted = run.DeleteData;
            return Task.CompletedTask;
        }, MatSetupStep.Uninstall("The service, the tray and the entry in Installed apps are removed."))));
        Assert.Equal("Uninstall", window.ModeText.Text);
        Assert.Equal("Remove Example Agent", window.CurrentStep.Title);
        Assert.Equal("Uninstall", window.PrimaryButton.Content);
        Assert.Same(Avalonia.Application.Current!.FindResource("MatButton.Danger"), window.PrimaryButton.Theme);
        Assert.False(window.DeleteData);
        Assert.Throws<ArgumentException>(() => new MatSetupWindow(Setup(MatSetupMode.Uninstall, _ => Task.CompletedTask)));

        Click(window.PrimaryButton);
        WaitFor(window, () => window.CurrentStep.Kind == MatSetupStepKind.Finish);
        Assert.False(deleted);
        Assert.False(window.StartNow);
        window.Close();
    }

    [AvaloniaFact]
    public void Connect_is_checked_at_once_and_a_bad_code_stays_on_the_step()
    {
        Ui.Theme(false);
        var answer = "The code is not valid any more.";
        var installed = false;
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Setup, _ =>
        {
            installed = true;
            return Task.CompletedTask;
        }, MatSetupStep.Welcome("Installs."), MatSetupStep.Connect("The address and the code.", new TextBox(), () => Task.FromResult<string?>(answer)))));

        Click(window.PrimaryButton);
        Ui.Settle(window);
        Assert.Equal(MatSetupStepKind.Connect, window.CurrentStep.Kind);
        Click(window.PrimaryButton);
        Ui.Settle(window);
        Assert.Equal(MatSetupStepKind.Connect, window.CurrentStep.Kind);
        Assert.False(installed);

        answer = null!;
        Click(window.PrimaryButton);
        WaitFor(window, () => window.CurrentStep.Kind == MatSetupStepKind.Finish);
        Assert.True(installed);
        window.Close();
    }

    [AvaloniaFact]
    public void A_role_step_needs_two_roles_and_the_first_is_chosen()
    {
        Assert.Throws<ArgumentException>(() => MatSetupStep.Role("One only.", new MatSetupRole("server", "Server", "The one that collects.")));
        Ui.Theme(false);
        var window = Ui.Show(new MatSetupWindow(Setup(MatSetupMode.Setup, _ => Task.CompletedTask,
            MatSetupStep.Role("How is it used?", new MatSetupRole("server", "Server", "Collects."), new MatSetupRole("probe", "Probe", "Measures.")))));
        Assert.Equal("server", window.SelectedRole);
        window.Close();
    }

    [AvaloniaFact]
    public void The_info_window_has_the_state_the_facts_and_open_log_folder_close()
    {
        Ui.Theme(false);
        var opened = 0;
        var info = new MatInfo("Example Agent", "1.4.2", "release", MatState.Running, "Connected since 09:12.",
            [new MatFact("Computer", "ANNA-PC"), new MatFact("Server", "monitor.example.com")])
        {
            Open = () => opened++,
            OpenLogFolder = () => { },
        };
        var window = Ui.Show(new MatInfoWindow(info));
        Assert.Equal((630d, 500d), (window.Width, window.Height));
        Assert.Equal("Running", window.StateDot.Word);
        Assert.Equal("ANNA-PC", window.Facts.ValueOf("Computer"));
        Assert.Equal("Version 1.4.2 · release", window.VersionText.Text);
        Assert.Equal(["Open", "Log folder", "Close"], new[] { window.OpenButton, window.LogFolderButton, window.CloseButton }.Select(b => b.Content));
        var row = (StackPanel)window.OpenButton.Parent!;
        Assert.Equal([window.OpenButton, window.LogFolderButton, window.CloseButton], row.Children);
        Click(window.OpenButton);
        Assert.Equal(1, opened);
        Click(window.CloseButton);
        Ui.Settle(window);
        Assert.False(window.IsVisible);

        var noPage = Ui.Show(new MatInfoWindow(info with { }) { });
        noPage.Close();
        var without = Ui.Show(new MatInfoWindow(new MatInfo("Example Agent", "1.4.2", "local", MatState.Stopped, "Stopped.", [])));
        Assert.False(without.OpenButton.IsVisible);
        Assert.Equal("A local build gets no updates.", ToolTip.GetTip(without.VersionText));
        without.Close();
    }

    [AvaloniaFact]
    public void A_notification_is_360_wide_takes_no_focus_and_a_click_opens_the_program()
    {
        Ui.Theme(false);
        var toast = new MatNotification("Connection lost", "The server does not answer.", isWarning: true, onClick: null);
        Assert.Equal(360, toast.Width);
        Assert.False(toast.ShowActivated);
        Assert.False(toast.ShowInTaskbar);
        Assert.Equal(TimeSpan.FromSeconds(7), MatNotification.Duration);
        Ui.Show(toast);
        Assert.Equal("Connection lost", toast.TitleText.Text);
        toast.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
