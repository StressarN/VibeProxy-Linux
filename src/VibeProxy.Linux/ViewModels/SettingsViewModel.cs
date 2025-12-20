using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using VibeProxy.Linux.Models;
using VibeProxy.Linux.Services;
using VibeProxy.Linux.Utilities;

namespace VibeProxy.Linux.ViewModels;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly CliProxyService _cliProxyService;
    private readonly ThinkingProxyServer _thinkingProxyServer;
    private readonly AuthStatusService _authStatusService;
    private readonly LaunchAtLoginService _launchAtLoginService;
    private readonly NotificationService _notificationService;
    private readonly Dictionary<AuthProviderType, bool> _authBusy = new();
    private bool _launchAtLoginEnabled;
    private bool _thinkingProxyRunning;
    private string _serverStatusText = "Server: Stopped";
    private string _qwenEmail = string.Empty;
    private AuthAccount? _accountToRemove;
    private bool _disposed;

    public SettingsViewModel()
        : this(
            new CliProxyService(Path.Combine(AppContext.BaseDirectory, "Resources")),
            new ThinkingProxyServer(),
            new AuthStatusService(),
            new LaunchAtLoginService(),
            new NotificationService())
    {
    }

    public SettingsViewModel(
        CliProxyService cliProxyService,
        ThinkingProxyServer thinkingProxyServer,
        AuthStatusService authStatusService,
        LaunchAtLoginService launchAtLoginService,
        NotificationService notificationService)
    {
        _cliProxyService = cliProxyService;
        _thinkingProxyServer = thinkingProxyServer;
        _authStatusService = authStatusService;
        _launchAtLoginService = launchAtLoginService;
        _notificationService = notificationService;

        LogLines = new ObservableCollection<string>(_cliProxyService.GetLogs());
        StatusItems = new ObservableCollection<StatusItem>();

        _cliProxyService.StatusChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(IsServerRunning));
            UpdateServerStatusText();
        };
        _cliProxyService.LogsUpdated += (_, logs) =>
        {
            LogLines.Clear();
            foreach (var line in logs)
            {
                LogLines.Add(line);
            }
        };

        _thinkingProxyServer.StatusChanged += (_, running) =>
        {
            _thinkingProxyRunning = running;
            RaisePropertyChanged(nameof(IsThinkingProxyRunning));
            UpdateServerStatusText();
        };

        _authStatusService.AccountsChanged += (_, accounts) => UpdateAccounts(accounts);

        StartCommand = new AsyncCommand(StartServerAsync);
        StopCommand = new AsyncCommand(StopServerAsync);
        CopyUrlCommand = new AsyncCommand(CopyServerUrlAsync);
        ConnectAntigravityCommand = new AsyncCommand(() => RunAuthFlowAsync(AuthProviderType.Antigravity, () => _cliProxyService.RunAuthCommandAsync(AuthCommand.Antigravity, null)));
        ConnectClaudeCommand = new AsyncCommand(() => RunAuthFlowAsync(AuthProviderType.Claude, () => _cliProxyService.RunAuthCommandAsync(AuthCommand.Claude, null)));
        ConnectCodexCommand = new AsyncCommand(() => RunAuthFlowAsync(AuthProviderType.Codex, () => _cliProxyService.RunAuthCommandAsync(AuthCommand.Codex, null)));
        ConnectCopilotCommand = new AsyncCommand(() => RunAuthFlowAsync(AuthProviderType.Copilot, () => _cliProxyService.RunAuthCommandAsync(AuthCommand.Copilot, null)));
        ConnectGeminiCommand = new AsyncCommand(() => RunAuthFlowAsync(AuthProviderType.Gemini, () => _cliProxyService.RunAuthCommandAsync(AuthCommand.Gemini, null)));
        ConnectQwenCommand = new AsyncCommand(() => RunAuthFlowAsync(AuthProviderType.Qwen, () => _cliProxyService.RunAuthCommandAsync(AuthCommand.Qwen, QwenEmail)));
        OpenAuthFolderCommand = new AsyncCommand(OpenAuthFolderAsync);
        ToggleLaunchCommand = new AsyncCommand(UpdateLaunchAtLoginAsync);

        _ = InitializeAsync();
    }

    public ObservableCollection<string> LogLines { get; }

    public ObservableCollection<StatusItem> StatusItems { get; }

    public bool IsServerRunning => _cliProxyService.IsRunning;

    public bool IsThinkingProxyRunning => _thinkingProxyRunning;

    public bool LaunchAtLoginEnabled
    {
        get => _launchAtLoginEnabled;
        set
        {
            if (SetProperty(ref _launchAtLoginEnabled, value))
            {
                _ = UpdateLaunchAtLoginAsync();
            }
        }
    }

    public string ServerStatusText
    {
        get => _serverStatusText;
        private set => SetProperty(ref _serverStatusText, value);
    }

    public string VersionText => $"VibeProxy {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0"}";

    public string QwenEmail
    {
        get => _qwenEmail;
        set => SetProperty(ref _qwenEmail, value);
    }

    public AuthAccount? AccountToRemove
    {
        get => _accountToRemove;
        set => SetProperty(ref _accountToRemove, value);
    }

    public ObservableCollection<AuthAccount> AntigravityAccounts { get; } = [];
    public ObservableCollection<AuthAccount> ClaudeAccounts { get; } = [];
    public ObservableCollection<AuthAccount> CodexAccounts { get; } = [];
    public ObservableCollection<AuthAccount> CopilotAccounts { get; } = [];
    public ObservableCollection<AuthAccount> GeminiAccounts { get; } = [];
    public ObservableCollection<AuthAccount> QwenAccounts { get; } = [];

    public bool IsAuthenticatingAntigravity => GetBusy(AuthProviderType.Antigravity);
    public bool IsAuthenticatingClaude => GetBusy(AuthProviderType.Claude);
    public bool IsAuthenticatingCodex => GetBusy(AuthProviderType.Codex);
    public bool IsAuthenticatingCopilot => GetBusy(AuthProviderType.Copilot);
    public bool IsAuthenticatingGemini => GetBusy(AuthProviderType.Gemini);
    public bool IsAuthenticatingQwen => GetBusy(AuthProviderType.Qwen);

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand ConnectAntigravityCommand { get; }
    public ICommand ConnectClaudeCommand { get; }
    public ICommand ConnectCodexCommand { get; }
    public ICommand ConnectCopilotCommand { get; }
    public ICommand ConnectGeminiCommand { get; }
    public ICommand ConnectQwenCommand { get; }
    public ICommand OpenAuthFolderCommand { get; }
    public ICommand ToggleLaunchCommand { get; }

    public async Task StartServerAsync()
    {
        try
        {
            await _cliProxyService.KillExistingProcessesAsync().ConfigureAwait(false);
            await _thinkingProxyServer.StartAsync().ConfigureAwait(false);
            var started = await _cliProxyService.StartAsync().ConfigureAwait(false);
            if (started)
            {
                _notificationService.Show("Server Started", "VibeProxy is now running on http://localhost:8317");
            }
            else
            {
                _notificationService.Show("Start Failed", "Unable to start the backend process. Check logs for details.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _notificationService.Show("Start Failed", $"Could not start the server: {ex.Message}");
        }
        finally
        {
            UpdateServerStatusText();
        }
    }

    public async Task StopServerAsync()
    {
        await _cliProxyService.StopAsync().ConfigureAwait(false);
        await _thinkingProxyServer.StopAsync().ConfigureAwait(false);
        UpdateServerStatusText();
    }

    public async Task CopyServerUrlAsync()
    {
        var url = "http://localhost:8317";
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is not null)
            {
                var clipboard = TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(url);
                }
            }
        }
        catch
        {
        }
    }

    public async Task OpenAuthFolderAsync()
    {
        try
        {
            var directory = _authStatusService.DirectoryPath;
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                ArgumentList = { directory },
                UseShellExecute = false
            });
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    private async Task InitializeAsync()
    {
        var launch = await _launchAtLoginService.IsEnabledAsync().ConfigureAwait(false);
        UpdateLaunchAtLoginFlag(launch);
        await _authStatusService.RefreshAsync().ConfigureAwait(false);
        UpdateAccounts(_authStatusService.CurrentAccounts);
        UpdateServerStatusText();
    }

    private async Task RunAuthFlowAsync(AuthProviderType provider, Func<Task<AuthCommandResult>> action)
    {
        if (GetBusy(provider))
        {
            return;
        }

        SetBusy(provider, true);
        try
        {
            AuthCommandResult result;
            try
            {
                result = await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = new AuthCommandResult(false, ex.Message);
            }
            _notificationService.Show(
                result.Success ? "Authentication Started" : "Authentication Failed",
                result.Message);

            if (result.Success)
            {
                await _authStatusService.RefreshAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            SetBusy(provider, false);
        }
    }

    private void UpdateAccounts(IReadOnlyDictionary<AuthProviderType, IReadOnlyList<AuthAccount>> snapshot)
    {
        UpdateAccountCollection(AntigravityAccounts, snapshot.GetValueOrDefault(AuthProviderType.Antigravity) ?? []);
        UpdateAccountCollection(ClaudeAccounts, snapshot.GetValueOrDefault(AuthProviderType.Claude) ?? []);
        UpdateAccountCollection(CodexAccounts, snapshot.GetValueOrDefault(AuthProviderType.Codex) ?? []);
        UpdateAccountCollection(CopilotAccounts, snapshot.GetValueOrDefault(AuthProviderType.Copilot) ?? []);
        UpdateAccountCollection(GeminiAccounts, snapshot.GetValueOrDefault(AuthProviderType.Gemini) ?? []);
        UpdateAccountCollection(QwenAccounts, snapshot.GetValueOrDefault(AuthProviderType.Qwen) ?? []);

        StatusItems.Clear();
        StatusItems.Add(new StatusItem("Antigravity", FormatAccountStatus(AntigravityAccounts)));
        StatusItems.Add(new StatusItem("Claude Code", FormatAccountStatus(ClaudeAccounts)));
        StatusItems.Add(new StatusItem("Codex", FormatAccountStatus(CodexAccounts)));
        StatusItems.Add(new StatusItem("GitHub Copilot", FormatAccountStatus(CopilotAccounts)));
        StatusItems.Add(new StatusItem("Gemini", FormatAccountStatus(GeminiAccounts)));
        StatusItems.Add(new StatusItem("Qwen", FormatAccountStatus(QwenAccounts)));
    }

    private static void UpdateAccountCollection(ObservableCollection<AuthAccount> collection, IReadOnlyList<AuthAccount> accounts)
    {
        collection.Clear();
        foreach (var account in accounts)
        {
            collection.Add(account);
        }
    }

    private static string FormatAccountStatus(ObservableCollection<AuthAccount> accounts)
    {
        if (accounts.Count == 0)
        {
            return "Not Connected";
        }

        var active = accounts.Count(a => !a.IsExpired);
        var expired = accounts.Count - active;
        if (expired == 0)
        {
            return accounts.Count == 1 ? accounts[0].DisplayName : $"{active} connected";
        }

        return $"{active} active, {expired} expired";
    }

    public async Task RemoveAccountAsync(AuthAccount account)
    {
        var wasRunning = _cliProxyService.IsRunning;

        if (wasRunning)
        {
            await StopServerAsync().ConfigureAwait(false);
        }

        var deleted = _authStatusService.DeleteAccount(account);
        if (deleted)
        {
            _notificationService.Show("Account Removed", $"Removed {account.DisplayName} from {account.Type.GetDisplayName()}");
            await _authStatusService.RefreshAsync().ConfigureAwait(false);
        }
        else
        {
            _notificationService.Show("Removal Failed", "Could not remove the account file.");
        }

        if (wasRunning)
        {
            await StartServerAsync().ConfigureAwait(false);
        }
    }

    private async Task UpdateLaunchAtLoginAsync()
    {
        await _launchAtLoginService.SetEnabledAsync(LaunchAtLoginEnabled).ConfigureAwait(false);
        var launch = await _launchAtLoginService.IsEnabledAsync().ConfigureAwait(false);
        UpdateLaunchAtLoginFlag(launch);
    }

    private void UpdateLaunchAtLoginFlag(bool enabled)
    {
        LaunchAtLoginEnabled = enabled;
    }

    private void UpdateServerStatusText()
    {
        ServerStatusText = IsServerRunning
            ? $"Server: Running (port {_thinkingProxyServer.ListeningPort})"
            : "Server: Stopped";
    }

    private bool GetBusy(AuthProviderType provider)
    {
        lock (_authBusy)
        {
            return _authBusy.TryGetValue(provider, out var busy) && busy;
        }
    }

    private void SetBusy(AuthProviderType provider, bool busy)
    {
        lock (_authBusy)
        {
            _authBusy[provider] = busy;
        }

        RaisePropertyChanged(nameof(IsAuthenticatingAntigravity));
        RaisePropertyChanged(nameof(IsAuthenticatingClaude));
        RaisePropertyChanged(nameof(IsAuthenticatingCodex));
        RaisePropertyChanged(nameof(IsAuthenticatingCopilot));
        RaisePropertyChanged(nameof(IsAuthenticatingGemini));
        RaisePropertyChanged(nameof(IsAuthenticatingQwen));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cliProxyService.Dispose();
        _authStatusService.Dispose();
        _thinkingProxyServer.Dispose();
    }

    public readonly record struct StatusItem(string Name, string Status);
}
