using System.Threading.Tasks;

namespace VibeProxy.Linux.Services;

public sealed class LaunchAtLoginService
{
    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public Task SetEnabledAsync(bool enabled) => Task.CompletedTask;
}
