using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VibeProxy.Linux.ViewModels;
using VibeProxy.Linux.Utilities;

namespace VibeProxy.Linux.Services;

public sealed class TrayService
{
    private TrayIcon? _trayIcon;
    private readonly WindowIcon _activeIcon;
    private readonly WindowIcon _inactiveIcon;

    public TrayService()
    {
        _activeIcon = LoadIcon("avares://VibeProxy.Linux/Resources/icon-active.png");
        _inactiveIcon = LoadIcon("avares://VibeProxy.Linux/Resources/icon-inactive.png");
    }

    public void Initialize(Window window, SettingsViewModel viewModel)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        _trayIcon = new TrayIcon
        {
            Icon = _inactiveIcon,
            ToolTipText = "VibeProxy"
        };

        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("Open Settings") { Command = new Utilities.RelayCommand(() => ShowWindow(window)) });
        menu.Items.Add(new NativeMenuItem("Start Server") { Command = new Utilities.AsyncCommand(viewModel.StartServerAsync) });
        menu.Items.Add(new NativeMenuItem("Stop Server") { Command = new Utilities.AsyncCommand(viewModel.StopServerAsync) });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Quit") { Command = new Utilities.RelayCommand(() => desktop.Shutdown()) });

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += (_, _) => ShowWindow(window);
        _trayIcon.IsVisible = true;

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.IsServerRunning))
            {
                _trayIcon.Icon = viewModel.IsServerRunning ? _activeIcon : _inactiveIcon;
            }
        };

        _trayIcon.Icon = viewModel.IsServerRunning ? _activeIcon : _inactiveIcon;
    }

    private static void ShowWindow(Window window)
    {
        window.Show();
        window.Activate();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
    }

    private static WindowIcon LoadIcon(string uri)
    {
        var assets = AssetLoader.Open(new Uri(uri));
        using var stream = assets;
        return new WindowIcon(new Bitmap(stream));
    }
}
