using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VibeProxy.Linux.Services;
using VibeProxy.Linux.ViewModels;

namespace VibeProxy.Linux;

public sealed partial class MainWindow : Window
{
    private static bool IsTilingWm =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"));

    private readonly TrayService _trayService;
    private readonly SettingsViewModel _viewModel;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = DataContext as SettingsViewModel ?? new SettingsViewModel();
        DataContext = _viewModel;

        if (IsTilingWm)
            ApplyTilingWmOverrides();

        _trayService = new TrayService();
        _trayService.Initialize(this, _viewModel);
    }

    private void ApplyTilingWmOverrides()
    {
        SystemDecorations = SystemDecorations.Full;
        CanResize = true;
        Background = new SolidColorBrush(Color.Parse("#0F1117"));

        var outer = this.FindControl<Border>("OuterBorder");
        if (outer is not null)
        {
            outer.Margin = new Thickness(0);
            outer.CornerRadius = new CornerRadius(0);
            outer.BoxShadow = new BoxShadows(default);
        }

        var titleBar = this.FindControl<Border>("TitleBarBorder");
        if (titleBar is not null)
            titleBar.CornerRadius = new CornerRadius(0);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnCloseWindow(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
