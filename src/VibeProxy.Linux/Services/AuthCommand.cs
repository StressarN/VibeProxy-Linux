namespace VibeProxy.Linux.Services;

public enum AuthCommand
{
    Antigravity,
    Claude,
    Codex,
    Copilot,
    Gemini,
    Qwen
}

public readonly record struct AuthCommandResult(bool Success, string Message);
