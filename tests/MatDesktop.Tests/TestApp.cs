using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Styling;
using Avalonia.Threading;
using MatDesktop;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(MatDesktop.Tests.TestApp))]

namespace MatDesktop.Tests;

/// <summary>An application with MatDesktop's theme, rendered headless with Skia (real pixels, no screen). The accent is the one of the sample program (Matmon).</summary>
public sealed class TestApp : Application
{
    /// <summary>The accent of the sample program of the tests and the pictures.</summary>
    public const string Accent = "#295cff";

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public override void Initialize()
    {
        Logger.Sink = AvaloniaLog.Instance;
        MatTheme.Install(this, new MatThemeOptions(Accent));
    }
}

/// <summary>What Avalonia logged at the level of a warning or an error (bindings that fail, resources that are missing).</summary>
public sealed class AvaloniaLog : ILogSink
{
    private readonly List<string> _lines = [];

    public static AvaloniaLog Instance { get; } = new();

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public void Clear()
    {
        lock (_lines)
        {
            _lines.Clear();
        }
    }

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        lock (_lines)
        {
            _lines.Add($"{level} {area}: {messageTemplate} ({source?.GetType().Name})");
        }
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        lock (_lines)
        {
            _lines.Add($"{level} {area}: {messageTemplate} [{string.Join(", ", propertyValues)}] ({source?.GetType().Name})");
        }
    }
}

/// <summary>Helpers of the window tests: the theme, a settled layout, a picture.</summary>
internal static class Ui
{
    [ModuleInitializer]
    internal static void English()
    {
        // the texts of the tests are English whatever the language of the machine; the choice by the system is tested on its own (TextTests)
        MatText.Language = "en";
    }

    public static void Theme(bool dark) => MatTheme.SetAppearance(dark ? MatAppearance.Dark : MatAppearance.Light);

    public static T Show<T>(T window)
        where T : Window
    {
        window.Show();
        Settle(window);
        return window;
    }

    public static void Settle(TopLevel window)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    public static Avalonia.Media.Imaging.WriteableBitmap Frame(Window window)
    {
        Settle(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return window.CaptureRenderedFrame() ?? throw new InvalidOperationException("nothing was rendered");
    }

    public static bool IsDark => Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
}

/// <summary>Where the files of the repository are.</summary>
internal static class Paths
{
    public static string Root { get; } = FindRoot();

    public static string Package => Path.Combine(Root, "src", "MatDesktop");

    public static string Tokens => Path.Combine(Root, "src", "MatUi", "wwwroot", "css", "matui.tokens.css");

    public static string Sprite => Path.Combine(Root, "src", "MatUi", "wwwroot", "img", "matui-icons.svg");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MatDesign.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root (MatDesign.slnx) was not found above " + AppContext.BaseDirectory);
    }
}
