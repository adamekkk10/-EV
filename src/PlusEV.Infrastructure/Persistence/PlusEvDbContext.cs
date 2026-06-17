using Microsoft.EntityFrameworkCore;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence.Entities;

namespace PlusEV.Infrastructure.Persistence;

/// <summary>
/// EF Core context. Every persisted record carries a <see cref="Mode"/> column
/// and global query filters could be layered on in a future iteration if desired.
/// </summary>
public sealed class PlusEvDbContext : DbContext
{
    public PlusEvDbContext(DbContextOptions<PlusEvDbContext> options) : base(options) { }

    public DbSet<Bet> Bets => Set<Bet>();
    public DbSet<BankrollEntry> BankrollEntries => Set<BankrollEntry>();
    public DbSet<OddsHistoryPoint> OddsHistory => Set<OddsHistoryPoint>();
    public DbSet<AlertLogEntry> AlertLog => Set<AlertLogEntry>();
    public DbSet<BacktestRunEntity> BacktestRuns => Set<BacktestRunEntity>();
    public DbSet<DemoSessionEntity> DemoSessions => Set<DemoSessionEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        var bet = model.Entity<Bet>();
        bet.HasKey(x => x.Id);
        bet.Property(x => x.Mode).HasConversion<int>().IsRequired();
        bet.Property(x => x.Market).HasConversion<int>().IsRequired();
        bet.Property(x => x.Result).HasConversion<int>().IsRequired();
        bet.Property(x => x.DecimalOdds).HasPrecision(10, 4);
        bet.Property(x => x.Stake).HasPrecision(14, 2);
        bet.Property(x => x.Payout).HasPrecision(14, 2);
        bet.HasIndex(x => new { x.Mode, x.PlacedAt });
        bet.HasIndex(x => new { x.Mode, x.Result });
        bet.HasIndex(x => x.EventId);

        var ledger = model.Entity<BankrollEntry>();
        ledger.HasKey(x => x.Id);
        ledger.Property(x => x.Mode).HasConversion<int>().IsRequired();
        ledger.Property(x => x.Kind).HasConversion<int>().IsRequired();
        ledger.Property(x => x.Amount).HasPrecision(14, 2);
        ledger.Property(x => x.BalanceAfter).HasPrecision(14, 2);
        ledger.HasIndex(x => new { x.Mode, x.OccurredAt });

        var odds = model.Entity<OddsHistoryPoint>();
        odds.HasKey(x => x.Id);
        odds.Property(x => x.Mode).HasConversion<int>().IsRequired();
        odds.Property(x => x.Market).HasConversion<int>().IsRequired();
        odds.Property(x => x.DecimalOdds).HasPrecision(10, 4);
        odds.HasIndex(x => new { x.EventId, x.Market, x.BookKey, x.FetchedAt });

        var alerts = model.Entity<AlertLogEntry>();
        alerts.HasKey(x => x.Id);
        alerts.HasIndex(x => x.SentAt);

        var runs = model.Entity<BacktestRunEntity>();
        runs.HasKey(x => x.Id);
        runs.HasIndex(x => x.RanAt);

        var sessions = model.Entity<DemoSessionEntity>();
        sessions.HasKey(x => x.Id);
        sessions.HasIndex(x => x.StartedAt);
    }
}
