namespace VibeProxy.Linux.Services;

public enum AuthCommand
{
    Claude,
    Codex,
    Gemini,
    Qwen
}

public readonly record struct AuthCommandResult(bool Success, string Message);
