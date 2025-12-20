using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VibeProxy.Linux.Models;

namespace VibeProxy.Linux.Services;

public sealed class AuthStatusService : IDisposable
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-ddTHH:mm:ss.fffffffzzz",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ssZ"
    ];

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

    public event EventHandler<IReadOnlyDictionary<AuthProviderType, IReadOnlyList<AuthAccount>>>? AccountsChanged;

    public IReadOnlyDictionary<AuthProviderType, IReadOnlyList<AuthAccount>> CurrentAccounts { get; private set; } =
        new Dictionary<AuthProviderType, IReadOnlyList<AuthAccount>>();

    public IReadOnlyList<AuthAccount> GetAccounts(AuthProviderType provider) =>
        CurrentAccounts.TryGetValue(provider, out var accounts) ? accounts : [];

    public bool HasAccounts(AuthProviderType provider) =>
        CurrentAccounts.TryGetValue(provider, out var accounts) && accounts.Count > 0;

    public async Task RefreshAsync()
    {
        var accountsByProvider = new Dictionary<AuthProviderType, List<AuthAccount>>();
        foreach (var provider in Enum.GetValues<AuthProviderType>())
        {
            accountsByProvider[provider] = [];
        }

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

                if (!AuthProviderTypeExtensions.TryParseFromJson(typeProp.GetString(), out var provider))
                {
                    continue;
                }

                var email = doc.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
                var login = doc.RootElement.TryGetProperty("login", out var loginProp) ? loginProp.GetString() : null;

                DateTimeOffset? expires = null;
                // Try "expired" field first (upstream format), then "expires_at"
                var expiredStr = doc.RootElement.TryGetProperty("expired", out var expiredProp) && expiredProp.ValueKind == JsonValueKind.String
                    ? expiredProp.GetString()
                    : doc.RootElement.TryGetProperty("expires_at", out var expiresAtProp) && expiresAtProp.ValueKind == JsonValueKind.String
                        ? expiresAtProp.GetString()
                        : null;

                if (!string.IsNullOrEmpty(expiredStr))
                {
                    foreach (var format in DateFormats)
                    {
                        if (DateTimeOffset.TryParseExact(expiredStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exp))
                        {
                            expires = exp;
                            break;
                        }
                    }

                    // Fallback to general parsing
                    if (!expires.HasValue && DateTimeOffset.TryParse(expiredStr, out var fallbackExp))
                    {
                        expires = fallbackExp;
                    }
                }

                var account = new AuthAccount(
                    Path.GetFileName(file),
                    provider,
                    email,
                    login,
                    expires,
                    file);

                accountsByProvider[provider].Add(account);
            }
            catch
            {
                // ignore malformed files
            }
        }

        var snapshot = accountsByProvider.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<AuthAccount>)kvp.Value.AsReadOnly());

        CurrentAccounts = snapshot;
        AccountsChanged?.Invoke(this, snapshot);
    }

    public bool DeleteAccount(AuthAccount account)
    {
        try
        {
            if (File.Exists(account.FilePath))
            {
                File.Delete(account.FilePath);
                return true;
            }
        }
        catch
        {
        }

        return false;
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
