using System;
using System.Collections.Generic;

namespace VibeProxy.Linux.Models;

public enum AuthProviderType
{
    Antigravity,
    Claude,
    Codex,
    Copilot,
    Gemini,
    Qwen,
    Zai
}

public static class AuthProviderTypeExtensions
{
    private static readonly Dictionary<string, AuthProviderType> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["antigravity"] = AuthProviderType.Antigravity,
        ["claude"] = AuthProviderType.Claude,
        ["codex"] = AuthProviderType.Codex,
        ["github-copilot"] = AuthProviderType.Copilot,
        ["copilot"] = AuthProviderType.Copilot,
        ["gemini"] = AuthProviderType.Gemini,
        ["qwen"] = AuthProviderType.Qwen,
        ["zai"] = AuthProviderType.Zai
    };

    public static bool TryParseFromJson(string? typeString, out AuthProviderType provider)
    {
        if (!string.IsNullOrWhiteSpace(typeString) && TypeMap.TryGetValue(typeString, out provider))
        {
            return true;
        }

        provider = default;
        return false;
    }

    public static string GetDisplayName(this AuthProviderType provider) => provider switch
    {
        AuthProviderType.Antigravity => "Antigravity",
        AuthProviderType.Claude => "Claude Code",
        AuthProviderType.Codex => "Codex",
        AuthProviderType.Copilot => "GitHub Copilot",
        AuthProviderType.Gemini => "Gemini",
        AuthProviderType.Qwen => "Qwen",
        AuthProviderType.Zai => "Z.AI GLM",
        _ => provider.ToString()
    };
}

public sealed record AuthAccount(
    string Id,
    AuthProviderType Type,
    string? Email,
    string? Login,
    DateTimeOffset? ExpiresAt,
    string FilePath)
{
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;

    public string DisplayName => !string.IsNullOrWhiteSpace(Email) ? Email
        : !string.IsNullOrWhiteSpace(Login) ? Login
        : Id;
}

public sealed class AuthStatus
{
    public AuthStatus(AuthProviderType type, bool isAuthenticated = false, string? email = null, DateTimeOffset? expiresAt = null)
    {
        Type = type;
        IsAuthenticated = isAuthenticated;
        Email = email;
        ExpiresAt = expiresAt;
    }

    public AuthProviderType Type { get; }

    public bool IsAuthenticated { get; init; }

    public string? Email { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool IsExpired => IsAuthenticated && ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;

    public string DisplayText
    {
        get
        {
            if (!IsAuthenticated)
            {
                return "Not Connected";
            }

            var label = string.IsNullOrWhiteSpace(Email) ? "Connected" : Email!;
            return IsExpired ? $"{label} (expired)" : label;
        }
    }

    public AuthStatus With(bool? isAuthenticated = null, string? email = null, DateTimeOffset? expiresAt = null)
    {
        return new AuthStatus(
            Type,
            isAuthenticated ?? IsAuthenticated,
            email ?? Email,
            expiresAt ?? ExpiresAt);
    }
}
