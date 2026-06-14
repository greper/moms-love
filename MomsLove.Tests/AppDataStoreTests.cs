using MomsLove.Core;

namespace MomsLove.Tests;

public class AppDataStoreTests
{
    [Fact]
    public async Task Store_RoundTripsConfigAndUsage()
    {
        var root = Path.Combine(Path.GetTempPath(), "MomsLoveTests", Guid.NewGuid().ToString("N"));
        var store = new AppDataStore(root);
        var config = new AppConfig();
        config.Policy.SessionMinutes = 45;
        config.Rules.Add(new ControlRule
        {
            DisplayName = "测试游戏",
            Path = @"C:\Games\Test.exe",
            Type = ControlRuleType.Executable
        });
        var usage = new DailyUsage
        {
            Date = new DateOnly(2026, 6, 14),
            State = PlayState.Cooldown,
            SessionsStarted = 1
        };

        await store.SaveConfigAsync(config);
        await store.SaveUsageAsync(usage);

        var loadedConfig = await store.LoadConfigAsync();
        var loadedUsage = await store.LoadUsageAsync(new DateOnly(2026, 6, 14));

        Assert.Equal(45, loadedConfig.Policy.SessionMinutes);
        Assert.Single(loadedConfig.Rules);
        Assert.Equal(PlayState.Cooldown, loadedUsage.State);
        Assert.Equal(1, loadedUsage.SessionsStarted);
    }

    [Fact]
    public async Task Store_LoadUsageResetsDifferentDate()
    {
        var root = Path.Combine(Path.GetTempPath(), "MomsLoveTests", Guid.NewGuid().ToString("N"));
        var store = new AppDataStore(root);
        await store.SaveUsageAsync(new DailyUsage
        {
            Date = new DateOnly(2026, 6, 14),
            State = PlayState.LockedForToday,
            SessionsStarted = 2
        });

        var usage = await store.LoadUsageAsync(new DateOnly(2026, 6, 15));

        Assert.Equal(new DateOnly(2026, 6, 15), usage.Date);
        Assert.Equal(PlayState.Idle, usage.State);
        Assert.Equal(0, usage.SessionsStarted);
    }
}
