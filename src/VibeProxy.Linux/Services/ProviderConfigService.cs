using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace VibeProxy.Linux.Services;

public sealed class ProviderConfigService
{
    private static readonly Dictionary<string, string> OAuthProviderKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = "claude",
        ["codex"] = "codex",
        ["gemini"] = "gemini-cli",
        ["github-copilot"] = "github-copilot",
        ["antigravity"] = "antigravity",
        ["qwen"] = "qwen"
    };

    private readonly string _bundledConfigPath;
    private readonly string _settingsPath;
    private readonly string _authDirectory;
    private readonly Dictionary<string, bool> _enabledProviders = new(StringComparer.OrdinalIgnoreCase);

    public ProviderConfigService(string bundledConfigPath)
    {
        _bundledConfigPath = bundledConfigPath;
        var configRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(configRoot))
        {
            configRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        var settingsDir = Path.Combine(configRoot, "vibeproxy-linux");
        _settingsPath = Path.Combine(settingsDir, "settings.json");
        _authDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cli-proxy-api");

        LoadSettings();
    }

    public bool IsProviderEnabled(string providerKey)
    {
        return !_enabledProviders.TryGetValue(providerKey, out var enabled) || enabled;
    }

    public void SetProviderEnabled(string providerKey, bool enabled)
    {
        _enabledProviders[providerKey] = enabled;
        SaveSettings();
        _ = GetConfigPath();
    }

    public string GetConfigPath() => GetConfigPath(_bundledConfigPath);

    public string GetConfigPath(string bundledConfigPath)
    {
        if (string.IsNullOrWhiteSpace(bundledConfigPath) || !File.Exists(bundledConfigPath))
        {
            return bundledConfigPath;
        }

        Directory.CreateDirectory(_authDirectory);

        var disabledProviders = GetDisabledProviders();
        var zaiKeys = LoadZaiKeys();

        if (disabledProviders.Count == 0 && zaiKeys.Count == 0)
        {
            return bundledConfigPath;
        }

        var bundledContent = File.ReadAllText(bundledConfigPath);
        var mergedContent = BuildMergedConfig(bundledContent, disabledProviders, zaiKeys);
        var mergedPath = Path.Combine(_authDirectory, "merged-config.yaml");

        try
        {
            File.WriteAllText(mergedPath, mergedContent);
            TrySetPermissions(mergedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            return bundledConfigPath;
        }

        return mergedPath;
    }

    public bool SaveZaiApiKey(string apiKey, out string message)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            message = "API key is required.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(_authDirectory);

            var payload = new Dictionary<string, string>
            {
                ["type"] = "zai",
                ["email"] = MaskApiKey(apiKey),
                ["api_key"] = apiKey,
                ["created"] = DateTimeOffset.UtcNow.ToString("O")
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            var filename = $"zai-{Guid.NewGuid():N}.json";
            var path = Path.Combine(_authDirectory, filename);
            File.WriteAllText(path, json);
            TrySetPermissions(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            _ = GetConfigPath();
            message = "API key saved.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to save API key: {ex.Message}";
            return false;
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("enabledProviders", out var providers))
            {
                foreach (var entry in providers.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.True || entry.Value.ValueKind == JsonValueKind.False)
                    {
                        _enabledProviders[entry.Name] = entry.Value.GetBoolean();
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settingsDir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            var settings = new Dictionary<string, object>
            {
                ["enabledProviders"] = _enabledProviders
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
        }
    }

    private List<string> GetDisabledProviders()
    {
        var disabled = new List<string>();
        foreach (var mapping in OAuthProviderKeys)
        {
            if (!IsProviderEnabled(mapping.Key))
            {
                disabled.Add(mapping.Value);
            }
        }

        disabled.Sort(StringComparer.OrdinalIgnoreCase);
        return disabled;
    }

    private List<string> LoadZaiKeys()
    {
        var keys = new List<string>();
        if (!Directory.Exists(_authDirectory))
        {
            return keys;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(_authDirectory, "zai-*.json"))
            {
                var text = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("api_key", out var apiKey) && apiKey.ValueKind == JsonValueKind.String)
                {
                    var value = apiKey.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        keys.Add(value);
                    }
                }
            }
        }
        catch
        {
        }

        return keys;
    }

    private string BuildMergedConfig(string baseConfig, List<string> disabledProviders, List<string> zaiKeys)
    {
        var builder = new StringBuilder(baseConfig);
        if (!baseConfig.EndsWith('\n'))
        {
            builder.AppendLine();
        }

        if (disabledProviders.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("# Provider exclusions (auto-added by VibeProxy)");
            builder.AppendLine("# Disabled providers have all models excluded");
            builder.AppendLine("oauth-excluded-models:");
            foreach (var provider in disabledProviders)
            {
                builder.AppendLine($"  {provider}:");
                builder.AppendLine("    - \"*\"");
            }
        }

        if (zaiKeys.Count > 0 && IsProviderEnabled("zai"))
        {
            builder.AppendLine();
            builder.AppendLine("# Z.AI GLM Provider (auto-added by VibeProxy)");
            builder.AppendLine("openai-compatibility:");
            builder.AppendLine("  - name: \"zai\"");
            builder.AppendLine("    base-url: \"https://api.z.ai/api/coding/paas/v4\"");
            builder.AppendLine("    api-key-entries:");
            foreach (var key in zaiKeys)
            {
                var escapedKey = key
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
                builder.AppendLine($"      - api-key: \"{escapedKey}\"");
            }

            builder.AppendLine("    models:");
            builder.AppendLine("      - name: \"glm-4.7\"");
            builder.AppendLine("        alias: \"glm-4.7\"");
            builder.AppendLine("      - name: \"glm-4-plus\"");
            builder.AppendLine("        alias: \"glm-4-plus\"");
            builder.AppendLine("      - name: \"glm-4-air\"");
            builder.AppendLine("        alias: \"glm-4-air\"");
            builder.AppendLine("      - name: \"glm-4-flash\"");
            builder.AppendLine("        alias: \"glm-4-flash\"");
        }

        return builder.ToString();
    }

    private static string MaskApiKey(string apiKey)
    {
        if (apiKey.Length <= 8)
        {
            return "****";
        }

        var prefixLength = Math.Min(8, apiKey.Length);
        var suffixLength = Math.Min(4, apiKey.Length - prefixLength);
        var prefix = apiKey[..prefixLength];
        var suffix = apiKey[^suffixLength..];
        return suffixLength > 0 ? $"{prefix}...{suffix}" : $"{prefix}...";
    }

    private static void TrySetPermissions(string path, UnixFileMode mode)
    {
        try
        {
            File.SetUnixFileMode(path, mode);
        }
        catch
        {
        }
    }
}
