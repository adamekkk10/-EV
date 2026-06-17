using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.Application.Services;

/// <summary>
/// Bankroll ledger operations. Reads the running balance from the last
/// <see cref="BankrollEntry"/> for the given <see cref="Mode"/>. Writes go through this service
/// so the Live/Demo partitions are never mixed.
/// </summary>
public sealed class BankrollService
{
    private readonly PlusEvDbContext _db;
    private readonly ILogger<BankrollService> _logger;

    public BankrollService(PlusEvDbContext db, ILogger<BankrollService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<decimal> GetCurrentBalanceAsync(Mode mode, CancellationToken ct = default)
    {
        var last = await _db.BankrollEntries
            .AsNoTracking()
            .Where(e => e.Mode == mode)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return last?.BalanceAfter ?? 0m;
    }

    public async Task EnsureInitialAsync(Mode mode, decimal starting, CancellationToken ct = default)
    {
        if (await _db.BankrollEntries.AnyAsync(e => e.Mode == mode, ct).ConfigureAwait(false)) return;
        _db.BankrollEntries.Add(new BankrollEntry
        {
            Mode = mode,
            Kind = BankrollEntryKind.Initial,
            Amount = starting,
            BalanceAfter = starting,
            OccurredAt = DateTimeOffset.UtcNow,
            Note = "Initial bankroll",
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Initialised {Mode} bankroll at {Amount}", mode, starting);
    }

    public async Task<decimal> AppendAsync(
        Mode mode,
        BankrollEntryKind kind,
        decimal amount,
        Guid? relatedBetId = null,
        string? note = null,
        CancellationToken ct = default)
    {
        var balance = await GetCurrentBalanceAsync(mode, ct).ConfigureAwait(false) + amount;
        _db.BankrollEntries.Add(new BankrollEntry
        {
            Mode = mode,
            Kind = kind,
            Amount = amount,
            BalanceAfter = balance,
            OccurredAt = DateTimeOffset.UtcNow,
            RelatedBetId = relatedBetId,
            Note = note,
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return balance;
    }

    public async Task<decimal?> RollingAverageStakeAsync(Mode mode, int window = 25, CancellationToken ct = default)
    {
        var stakes = await _db.Bets
            .AsNoTracking()
            .Where(b => b.Mode == mode)
            .OrderByDescending(b => b.PlacedAt)
            .Select(b => b.Stake)
            .Take(window)
            .ToListAsync(ct).ConfigureAwait(false);
        if (stakes.Count == 0) return null;
        return stakes.Average();
    }
}
