using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Alerts;
using PlusEV.Infrastructure.Persistence;
using Xunit;

namespace PlusEV.Tests;

public class AlertDispatcherTests
{
    private sealed class CapturingChannel : IAlertChannel
    {
        public List<AlertMessage> Sent { get; } = new();
        public bool ShouldFail { get; set; }
        public string Key { get; }
        public string DisplayName => Key;
        public bool Enabled => true;

        public CapturingChannel(string key) => Key = key;

        public bool ShouldSend(AlertMessage _) => Enabled;

        public Task SendAsync(AlertMessage message, CancellationToken ct = default)
        {
            if (ShouldFail) throw new InvalidOperationException("boom");
            Sent.Add(message);
            return Task.CompletedTask;
        }

        public Task TestAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (AlertDispatcher dispatcher, CapturingChannel ok, CapturingChannel broken, IServiceProvider sp)
        Build()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PlusEvDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var ok = new CapturingChannel("ok");
        var broken = new CapturingChannel("broken") { ShouldFail = true };
        services.AddSingleton<IAlertChannel>(ok);
        services.AddSingleton<IAlertChannel>(broken);
        services.AddSingleton<AlertDispatcher>();
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<AlertDispatcher>();
        return (dispatcher, ok, broken, sp);
    }

    [Fact]
    public async Task Enqueued_messages_fan_out_to_all_enabled_channels()
    {
        var (dispatcher, ok, broken, sp) = Build();
        await dispatcher.StartAsync(CancellationToken.None);

        var msg = new AlertMessage("t", "b", AlertSeverity.Info, AlertCategory.System, DateTimeOffset.UtcNow);
        await dispatcher.EnqueueAsync(msg);

        // Allow the background worker to drain the queue.
        await WaitAsync(() => ok.Sent.Count > 0, TimeSpan.FromSeconds(2));
        await dispatcher.StopAsync(CancellationToken.None);

        ok.Sent.Should().ContainSingle(m => m.Title == "t");
        broken.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Channel_failure_is_logged_but_does_not_crash_dispatcher()
    {
        var (dispatcher, ok, broken, sp) = Build();
        await dispatcher.StartAsync(CancellationToken.None);

        await dispatcher.EnqueueAsync(new AlertMessage("t", "b",
            AlertSeverity.Info, AlertCategory.System, DateTimeOffset.UtcNow));
        await WaitAsync(() => ok.Sent.Count > 0, TimeSpan.FromSeconds(2));

        await dispatcher.EnqueueAsync(new AlertMessage("t2", "b2",
            AlertSeverity.Info, AlertCategory.System, DateTimeOffset.UtcNow));
        await WaitAsync(() => ok.Sent.Count > 1, TimeSpan.FromSeconds(2));

        await dispatcher.StopAsync(CancellationToken.None);
        ok.Sent.Should().HaveCount(2);
    }

    [Fact]
    public void FromOpportunity_produces_human_summary()
    {
        var (dispatcher, _, _, _) = Build();
        var opp = new EvOpportunity
        {
            EventId = "e1",
            EventLabel = "Away @ Home",
            Sport = new Sport("nba", "NBA"),
            Market = MarketType.H2H,
            Selection = "Lakers",
            Book = Bookmaker.DraftKings,
            OfferedDecimalOdds = 2.10m,
            OfferedAmericanOdds = 110,
            TrueProbability = 0.52d,
            EvPercent = 0.042d,
            Confidence = 78d,
            RecommendedStake = 23.50m,
            KellyFraction = 0.04d,
            EventStart = DateTimeOffset.UtcNow.AddHours(3),
            IdentifiedAt = DateTimeOffset.UtcNow,
            OddsFetchedAt = DateTimeOffset.UtcNow,
        };
        var msg = dispatcher.FromOpportunity(opp);
        msg.Title.Should().Contain("4.20");
        msg.Body.Should().Contain("Lakers").And.Contain("DraftKings");
        msg.Category.Should().Be(AlertCategory.Opportunity);
    }

    private static async Task WaitAsync(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (condition()) return;
            await Task.Delay(25);
        }
    }
}
