using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VibeProxy.Linux.Models;

namespace VibeProxy.Linux.Services;

public sealed class AuthStatusService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    public AuthStatusService()
    {
        DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cli-proxy-api");
        Directory.CreateDirectory(DirectoryPath);

        _watcher = new FileSystemWatcher(DirectoryPath, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += (_, _) => _ = RefreshAsync();
        _watcher.Changed += (_, _) => _ = RefreshAsync();
        _watcher.Deleted += (_, _) => _ = RefreshAsync();
        _watcher.Renamed += (_, _) => _ = RefreshAsync();
    }

    public string DirectoryPath { get; }

    public event EventHandler<IReadOnlyDictionary<AuthProviderType, AuthStatus>>? StatusesChanged;

    public IReadOnlyDictionary<AuthProviderType, AuthStatus> CurrentStatuses { get; private set; } = new Dictionary<AuthProviderType, AuthStatus>();

    public async Task RefreshAsync()
    {
        var snapshot = new Dictionary<AuthProviderType, AuthStatus>
        {
            [AuthProviderType.Claude] = new(AuthProviderType.Claude),
            [AuthProviderType.Codex] = new(AuthProviderType.Codex),
            [AuthProviderType.Gemini] = new(AuthProviderType.Gemini),
            [AuthProviderType.Qwen] = new(AuthProviderType.Qwen)
        };

        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var text = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("type", out var typeProp))
                {
                    continue;
                }

                if (!Enum.TryParse<AuthProviderType>(typeProp.GetString(), true, out var provider))
                {
                    continue;
                }

                var email = doc.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
                DateTimeOffset? expires = null;
                if (doc.RootElement.TryGetProperty("expires_at", out var expiresProp) && expiresProp.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(expiresProp.GetString(), out var exp))
                {
                    expires = exp;
                }

                snapshot[provider] = new AuthStatus(provider, true, email, expires);
            }
            catch
            {
                // ignore malformed files
            }
        }

        CurrentStatuses = snapshot;
        StatusesChanged?.Invoke(this, snapshot);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Dispose();
    }
}
