using System.Text.Json;
using VibeProxy.Linux.Services;
using Xunit;

namespace VibeProxy.Linux.Tests;

public class ThinkingModelTransformerTests
{
    [Fact]
    public void AddsThinkingPayload_WhenModelHasThinkingSuffix()
    {
        var payload = "{\"model\":\"claude-sonnet-thinking-2000\",\"messages\":[]}";

        var (body, modified) = ThinkingModelTransformer.Apply(payload);

        Assert.True(modified);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("claude-sonnet", doc.RootElement.GetProperty("model").GetString());
        var thinking = doc.RootElement.GetProperty("thinking");
        Assert.Equal("enabled", thinking.GetProperty("type").GetString());
        Assert.Equal(2000, thinking.GetProperty("budget_tokens").GetInt32());
    }

    [Fact]
    public void CapsBudgetNearHardLimit()
    {
        var payload = "{\"model\":\"claude-3-thinking-50000\"}";

        var (body, modified) = ThinkingModelTransformer.Apply(payload);

        Assert.True(modified);
        using var doc = JsonDocument.Parse(body);
        var thinking = doc.RootElement.GetProperty("thinking");
        Assert.Equal(31999, thinking.GetProperty("budget_tokens").GetInt32());
    }

    [Fact]
    public void LeavesNonClaudeModelsAlone()
    {
        var payload = "{\"model\":\"gpt-4\"}";

        var (body, modified) = ThinkingModelTransformer.Apply(payload);

        Assert.False(modified);
        Assert.Equal(payload, body);
    }

    [Fact]
    public void NormalizesGptFastModelAndAddsPriorityServiceTier()
    {
        var payload = "{\"model\":\"gpt-5-fast\",\"messages\":[]}";

        var (body, modified) = ThinkingModelTransformer.Apply(payload);

        Assert.True(modified);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("gpt-5", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("priority", doc.RootElement.GetProperty("service_tier").GetString());
    }

    [Fact]
    public void NormalizesGptFastReasoningSuffix()
    {
        var payload = "{\"model\":\"gpt-5.2-high-fast\"}";

        var (body, modified) = ThinkingModelTransformer.Apply(payload);

        Assert.True(modified);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("gpt-5.2(high)", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("priority", doc.RootElement.GetProperty("service_tier").GetString());
    }

    [Fact]
    public void PreservesExplicitServiceTierForFastMode()
    {
        var payload = "{\"model\":\"gpt-5-codex-low-fast\",\"service_tier\":\"default\"}";

        var (body, modified) = ThinkingModelTransformer.Apply(payload);

        Assert.True(modified);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("gpt-5-codex(low)", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("default", doc.RootElement.GetProperty("service_tier").GetString());
    }
}
