using System;

namespace VibeProxy.Linux.Services;

public sealed class NotificationService
{
    public void Show(string title, string message)
    {
        Console.WriteLine($"[{title}] {message}");
    }
}
