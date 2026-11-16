using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaTrayIcon = Avalonia.Controls.TrayIcon;

namespace MatDesktop;

/// <summary>
/// The tray icon of a program with Avalonia: the icon with the dot of the state (<see cref="MatTrayIcons"/>, a template image on macOS), the tooltip <c>&lt;App&gt;: &lt;state&gt;</c> and the
/// menu of <see cref="MatTrayMenu"/> (TRAY-01), built again each time it opens. A left click on Windows runs Open; on macOS and Linux every click opens the menu (TRAY-03).
/// </summary>
public sealed class MatTray : IDisposable
{
    private readonly AvaloniaTrayIcon _icon;
    private readonly NativeMenu _menu = new();
    private readonly Bitmap _mark;
    private readonly bool _template = OperatingSystem.IsMacOS();

    /// <summary>A tray icon for <paramref name="app"/> with the menu <paramref name="menu"/> and the mark of the app (a square PNG of 64 px or more).</summary>
    public MatTray(Application app, MatTrayMenu menu, Bitmap mark)
    {
        ArgumentNullException.ThrowIfNull(app);
        Menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _mark = mark ?? throw new ArgumentNullException(nameof(mark));
        _icon = new AvaloniaTrayIcon { Menu = _menu, IsVisible = true };
        if (_template)
        {
            MacOSProperties.SetIsTemplateIcon(_icon, true);
        }

        _icon.Clicked += (_, _) =>
        {
            if (MatTrayMenu.LeftClickOpens(OperatingSystem.IsWindows()))
            {
                Menu.Open?.Invoke();
            }
        };
        _menu.Opening += (_, _) => Fill(_menu, Menu);
        AvaloniaTrayIcon.SetIcons(app, [_icon]);
        Refresh();
    }

    /// <summary>The model of the menu.</summary>
    public MatTrayMenu Menu { get; }

    /// <summary>Shows the state of the model: the icon, the tooltip and the menu. Call it after the state or an item changed.</summary>
    public void Refresh()
    {
        _icon.Icon = MatTrayIcons.Icon(_mark, Menu.State, _template);
        _icon.ToolTipText = Menu.State.Tooltip(Menu.AppName);
        Fill(_menu, Menu);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _icon.IsVisible = false;
        _icon.Dispose();
    }

    /// <summary>
    /// The menu as Avalonia's <see cref="NativeMenu"/>: the entries of <see cref="MatTrayMenu.Build"/> in their order, the disabled ones disabled, the shield of the sprite on an action that needs
    /// administrator rights. Avalonia's menu cannot draw an item in bold: the default item is the first item that acts, and Windows runs it on a left click.
    /// </summary>
    public static NativeMenu ToNativeMenu(MatTrayMenu menu)
    {
        var native = new NativeMenu();
        Fill(native, menu);
        return native;
    }

    private static void Fill(NativeMenu native, MatTrayMenu menu)
    {
        native.Items.Clear();
        foreach (var entry in menu.Build())
        {
            if (entry.Role == MatTrayRole.Separator)
            {
                native.Items.Add(new NativeMenuItemSeparator());
                continue;
            }

            var item = new NativeMenuItem(entry.Text) { IsEnabled = entry.IsEnabled };
            if (entry.Run is { } run && entry.IsEnabled)
            {
                item.Command = new Command(run);
            }

            if (entry.NeedsElevation && Shield() is { } shield)
            {
                item.Icon = shield;
            }

            native.Items.Add(item);
        }
    }

    /// <summary>The shield of the sprite, 16 px, in the text colour of light mode (a menu of the system is light or dark by itself; the shape carries the meaning).</summary>
    private static Bitmap? Shield()
    {
        if (MatIcon.Find("shield") is null)
        {
            return null;
        }

        var icon = new MatIcon { Icon = "shield", Size = 16, Foreground = new Avalonia.Media.SolidColorBrush(MatTheme.ToColor(MatTokens.Light.TextMuted)) };
        icon.Measure(new Size(16, 16));
        icon.Arrange(new Rect(0, 0, 16, 16));
        var bitmap = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(192, 192));
        bitmap.Render(icon);
        return bitmap;
    }

    /// <summary>A menu item runs its action through a command on the UI thread (works on Windows and in the menu bar of macOS).</summary>
    private sealed class Command(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Dispatcher.UIThread.Post(action);
    }
}
