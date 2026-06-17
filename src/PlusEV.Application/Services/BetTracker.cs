using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.Application.Services;

/// <summary>
/// Persistence-backed bet tracker. Guards the <see cref="Mode"/> invariant and keeps
/// bankroll ledger entries in sync when bets are placed / settled.
/// </summary>
public sealed class BetTracker
{
    private readonly PlusEvDbContext _db;
    private readonly BankrollService _bankroll;
    private readonly ILogger<BetTracker> _logger;

    public BetTracker(PlusEvDbContext db, BankrollService bankroll, ILogger<BetTracker> logger)
    {
        _db = db;
        _bankroll = bankroll;
        _logger = logger;
    }

    public async Task<Bet> RecordAsync(Bet bet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bet);
        if (bet.Stake <= 0m) throw new ArgumentOutOfRangeException(nameof(bet), "Stake must be positive.");
        _db.Bets.Add(bet);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _bankroll.AppendAsync(bet.Mode, BankrollEntryKind.BetStaked, -bet.Stake, bet.Id,
            $"Stake on {bet.Selection}", ct).ConfigureAwait(false);
        _logger.LogInformation("Recorded {Mode} bet {Id} stake={Stake} on {Selection}",
            bet.Mode, bet.Id, bet.Stake, bet.Selection);
        return bet;
    }

    public async Task<Bet?> SettleAsync(Guid betId, BetResult result, double? closingDecimal,
        CancellationToken ct = default)
    {
        var bet = await _db.Bets.FirstOrDefaultAsync(b => b.Id == betId, ct).ConfigureAwait(false);
        if (bet is null) return null;
        if (bet.Result != BetResult.Pending)
        {
            _logger.LogWarning("Attempted to settle already-settled bet {Id}", betId);
            return bet;
        }

        bet.Result = result;
        bet.SettledAt = DateTimeOffset.UtcNow;
        bet.ClosingDecimalOdds = closingDecimal;
        if (closingDecimal is { } close && close > 1d)
            bet.Clv = Core.Math.ExpectedValue.Clv((double)bet.DecimalOdds, close);

        decimal delta = result switch
        {
            BetResult.Won => bet.Stake + (bet.Stake * (bet.DecimalOdds - 1m)),
            BetResult.Lost => 0m,
            BetResult.Push or BetResult.Void => bet.Stake,
            _ => 0m,
        };
        bet.Payout = delta;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (delta > 0m)
        {
            await _bankroll.AppendAsync(bet.Mode, BankrollEntryKind.BetSettlement, delta, bet.Id,
                $"{result} on {bet.Selection}", ct).ConfigureAwait(false);
        }

        return bet;
    }

    public IQueryable<Bet> Query(Mode mode) =>
        _db.Bets.AsNoTracking().Where(b => b.Mode == mode).OrderByDescending(b => b.PlacedAt);
}
