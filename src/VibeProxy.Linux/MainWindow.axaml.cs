using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VibeProxy.Linux.Services;
using VibeProxy.Linux.ViewModels;

namespace VibeProxy.Linux;

public sealed partial class MainWindow : Window
{
    private readonly TrayService _trayService;
    private readonly SettingsViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = DataContext as SettingsViewModel ?? new SettingsViewModel();
        DataContext = _viewModel;

        _trayService = new TrayService();
        _trayService.Initialize(this, _viewModel);
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
        WindowState = WindowState.Minimized;
    }

    private void OnCloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
