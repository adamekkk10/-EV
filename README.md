# PlusEV

A desktop application for **+EV (positive expected value) sports betting**.
PlusEV pulls real-time odds from multiple sportsbooks, devigs a sharp benchmark
(Pinnacle by default), compares the resulting "true" probabilities against every
other book's offered price, and surfaces the bets whose expected value clears a
configurable threshold.

It ships with:

- A live +EV opportunity table, arbitrage/middle scanners, a demo bot for
  paper trading, a walk-forward backtester, a CLV-driven reality-check
  dashboard, multi-channel alerting (Telegram / Discord / desktop),
  and a bound settings editor.
- A modular, MVVM-based Avalonia UI that runs on Windows, macOS, and Linux.
- Extensive math (Kelly, three devigging methods, confidence scoring,
  Welford-streamed statistics) covered by xUnit tests.

**This is a tool, not a money machine.** Read the
[Honest limitations](#honest-limitations) section before you commit real capital.

---

## Contents

- [Architecture](#architecture)
- [Solution layout](#solution-layout)
- [Setup](#setup)
- [Running](#running)
- [Publishing single-file executables](#publishing-single-file-executables)
- [The math](#the-math)
  - [Devigging](#devigging)
  - [Kelly staking](#kelly-staking)
  - [Confidence score](#confidence-score)
  - [CLV and the reality check](#clv-and-the-reality-check)
- [Backtesting methodology](#backtesting-methodology)
- [Demo mode](#demo-mode)
- [Alerting](#alerting)
- [GUI tour](#gui-tour)
- [Honest limitations](#honest-limitations)

---

## Architecture

```
+------------------+        +-----------------------+
|  PlusEV.UI       |  MVVM  |  CommunityToolkit.Mvvm|
|  (Avalonia)      +--------+  ViewModels + Views   |
+--------+---------+        +-----------+-----------+
         |                              |
         v                              v
+------------------+        +-----------------------+
|  PlusEV.Application      |  Background services    |
|  OpportunityFeed         |  OddsIngestionService   |
|  DemoBot, Backtester     |  AlertDispatcher        |
|  Reality-check service   |                         |
+--------+-----------------+------+------------------+
         |                              |
         v                              v
+------------------+        +-----------------------+
|  PlusEV.Infrastructure   |  EF Core + SQLite       |
|  TheOddsApiProvider      |  Telegram / Discord /   |
|  Refit + Polly           |  Desktop alert channels |
|  QuestPDF reports        |                         |
+--------+-----------------+------+------------------+
         |                              |
         v                              v
+------------------+        +-----------------------+
|  PlusEV.Core             |  Pure domain + math     |
|  Domain models           |  IDevigMethod strategies|
|  EvEngine, Arb & Middle  |  Kelly, CLV statistics  |
|  Backtesting types       |  Options POCOs          |
+--------------------------+--------------------------+
```

The three operating modes (LIVE / DEMO / BACKTEST) are separated by a `Mode`
enum column on every persisted record; the UI also carries a visible mode toggle
so you always know which world you're in.

## Solution layout

```
PlusEV.sln
Directory.Build.props              # common <LangVersion>, <Nullable>, XML docs
src/
  PlusEV.Core/                     # pure domain + math, no IO, no EF Core
  PlusEV.Infrastructure/           # EF Core, Odds API (Refit + Polly), alerts, PDF
  PlusEV.Application/              # services, hosted workers, backtester, demo bot
  PlusEV.UI/                       # Avalonia app (entry point)
tests/
  PlusEV.Tests/                    # xUnit + FluentAssertions
samples/
  historical-odds.csv              # sample dataset for the backtester
  historical-results.csv           # sample results for settling the above
```

## Setup

Prerequisites:

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An API key from [The Odds API](https://the-odds-api.com) (free tier works for
  development)

```bash
git clone <this-repo>
cd -EV
dotnet restore
```

Put your API key in `src/PlusEV.UI/appsettings.json` under `OddsApi.ApiKey`, or
drop an `appsettings.override.json` next to the executable (the Settings view
writes one for you). Environment variables prefixed `PLUSEV_` also override
(e.g. `PLUSEV_OddsApi__ApiKey=...`).

### Database

EF Core uses SQLite. The first run creates `plusev.db` in the binary directory
via `EnsureCreated`. To switch to migrations:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial --project src/PlusEV.Infrastructure --startup-project src/PlusEV.UI
```

Then flip `DatabaseInitializer.UseMigrations` to `true`.

## Running

```bash
dotnet run --project src/PlusEV.UI
```

Run the tests with:

```bash
dotnet test
```

## Publishing single-file executables

Each RID gets a single self-contained binary:

```bash
# Windows
dotnet publish src/PlusEV.UI -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

# macOS (Apple Silicon)
dotnet publish src/PlusEV.UI -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true

# macOS (Intel)
dotnet publish src/PlusEV.UI -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true

# Linux
dotnet publish src/PlusEV.UI -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

The output lives in `src/PlusEV.UI/bin/Release/net8.0/<rid>/publish/`.

## The math

### Devigging

Every book builds in a margin ("vig") so a two-way market's implied
probabilities sum to more than 1. To estimate true probabilities we have to
undo that margin. Three strategies are implemented via `IDevigMethod`:

- **Multiplicative (proportional).** `p̂_i = p_i / Σ p_j`. Simple and fast;
  removes vig uniformly. Good baseline.
- **Power.** Finds `k` such that `Σ p_i^k = 1` and uses `p_i^k`. Matches the
  observed favourite-longshot bias better than multiplicative — it removes
  proportionally more vig from longshots.
- **Shin (1992).** Models vig as arising from insider traders. Performs
  particularly well on two-way markets.

Pick one in Settings. The backtester lets you re-run with a different method
without leaving the UI.

### Kelly staking

Full Kelly maximises expected log bankroll but is practically too aggressive for
sports betting because (a) true edge is estimated with noise and (b) book limits
punish streaky sizing. PlusEV defaults to **quarter Kelly** (0.25) with:

- a **hard cap** of 2% of bankroll per bet (configurable);
- a **soft cap** at 5× your rolling average stake (prevents Kelly from
  recommending a $900 bet because one +9% edge showed up on a $1,000 roll).

Flat-staking (fixed % of bankroll regardless of edge) is available as an
alternative via `Bankroll.UseFlatStaking`.

### Confidence score

0–100 number that blends five inputs so you can rank opportunities beyond
raw EV%:

- EV magnitude (weight 0.45 — 10% EV saturates)
- Market depth (number of books offering the price)
- Pinnacle overround (tighter is sharper)
- Time to event (prefers the 1–24h window)
- Line stability across sharp books

See `ConfidenceScore.cs` for the exact weights and rationale.

### CLV and the reality check

**Closing Line Value** is the primary KPI. Over small samples, ROI is mostly
variance; CLV is the real edge indicator — if you consistently beat the closing
line, you're finding edge faster than the market.

The Reality Check view runs a **Welford-streamed mean and variance** on your
settled bets' CLV, computes a **t-statistic**, and renders one of:

- *Insufficient data* — `n < 50`
- *Edge not significant* — `|t| ≤ 2`
- *Edge emerging* — `t > 2` but below the sample threshold
- *Edge confirmed* — `t > 2.5`, `n ≥ 500`
- *Negative edge detected* — persistent negative CLV

We use the Math.NET `StudentT` distribution for p-values. See
`ClvStatistics.cs` for the implementation.

## Backtesting methodology

**Walk-forward is enforced.** The UI won't let you run a backtest without an
in-sample AND an out-of-sample date range. Parameter tuning — EV threshold,
Kelly fraction, devigging method — is only valid on the in-sample range; the
out-of-sample metrics are computed automatically against the same parameter set
and shown side-by-side. If in-sample ROI exceeds out-of-sample ROI by more than
5 percentage points you get a bright overfitting warning.

Why bother? Because **it is trivial to find parameters that look great in
sample and lose money live**. Walk-forward is the cheapest defence you have.

Settlement in the backtester:

1. The EV engine evaluates every snapshot in the configured range.
2. Each qualifying opportunity becomes a simulated bet.
3. If a matching `EventResult` is present (via the imported results CSV),
   `BetSettlement` marks the bet Won / Lost / Push / Void.
4. CLV is computed by pulling the last pre-kickoff snapshot for the same book /
   market / selection and applying `ExpectedValue.Clv`.

Metrics produced per phase: total bets, wins/losses/pushes/voids, ROI%, profit,
ending bankroll, max drawdown, win rate, average CLV, Sharpe ratio, longest
losing streak, plus the full bankroll growth curve and simulated-bet ledger.

Both phases get a PDF report via QuestPDF (`BacktestPdfReport`).

## Demo mode

Demo mode is a fully isolated, paper-trading world:

- Storage is separated by `Mode.Demo` — live and demo rows **cannot be mixed**.
- `DemoBot` subscribes to the live `OpportunityFeed` and, when it finds a bet
  that passes its aggressiveness preset + filters, places a virtual bet against
  its own virtual bankroll.
- A background settlement loop polls for real results (via
  `IEventResultProvider`) and settles virtual bets automatically.
- The DemoBot view shows a timestamped activity feed bound to an
  `ObservableCollection<DemoActivityEntry>`. Every placement and settlement
  streams in in real time.
- A kill switch on the view cancels the shared `CancellationTokenSource` that
  drives the bot and its settlement loop.

Presets:

| Aggressiveness | Min EV | Kelly fraction | Hard cap |
| -------------- | ------ | -------------- | -------- |
| Conservative   | 4.0%   | 0.15           | 1.0%     |
| Balanced       | 2.5%   | 0.25           | 2.0%     |
| Aggressive     | 1.5%   | 0.50           | 3.0%     |

## Alerting

Three channels ship out of the box, each with its own EV / sport filters:

- **Telegram** — set `Alerts.Telegram.BotToken` and `ChatId`.
- **Discord** — set `Alerts.Discord.WebhookUrl`.
- **Desktop** — `notify-send` on Linux, `osascript` on macOS, best-effort on
  Windows.

The dispatcher (`AlertDispatcher`) is a `BackgroundService` that drains an
unbounded channel and fans out to every enabled channel. Failures in one
channel don't affect the others; every attempt is persisted to `AlertLog` so
you can audit what went out.

## GUI tour

Top-level shell:

- **Mode toggle** (LIVE / DEMO / BACKTEST) with the three color accents
  (green / blue / purple).
- Sidebar navigation: Live Opportunities, Arbitrage & Middles, Demo Bot,
  Backtesting, Bet Tracker, Bankroll, Reality Check, Line History, Alerts,
  Settings.
- Status bar: API remaining, last refresh, average fetch latency, connection.

Each view is MVVM, uses `CommunityToolkit.Mvvm` `[ObservableProperty]` and
`[RelayCommand]`, and never performs IO on the UI thread.

> **Screenshots** live under `docs/screenshots/` once you run `dotnet publish`
> and capture them. A placeholder folder isn't committed because binaries
> don't belong in git.

## Honest limitations

- **Account limiting.** Books will limit or close you when you win
  consistently. Successful +EV bettors rotate across many accounts, use
  prepaid cards, and keep stakes looking "recreational". PlusEV does nothing
  about that — it's a tool for finding edge, not hiding you.
- **Execution latency.** You will always be slower than a bot sitting in the
  book's data centre. By the time you click, the price may be gone. The
  Latency tracker exists to show you the typical gap; don't be surprised if
  your live CLV is worse than your backtested CLV.
- **Overfitting.** Even with walk-forward enforcement, if you run enough
  variations you will eventually find "parameters that work". Treat every
  backtest run as a hypothesis, not a guarantee.
- **Survivorship bias in historical data.** Historical feeds often only
  include lines that actually traded; books that went offline or changed
  pricing engines are quietly absent. Results may be rosier than reality.
- **Arbitrage is fast-moving.** The arb scanner assumes you can hit both legs
  at the same price. In practice, one leg often moves before you can place
  the other — treat the displayed guaranteed return as an upper bound.
- **This is not financial advice.** Gambling is gambling. Expected value is a
  long-run concept; in the short run you will lose money you deserved to win
  and occasionally vice versa. Don't bet money you can't afford to lose, and
  if you can't stop, [get help](https://www.begambleaware.org/).

---

## License

Copyright (c) 2026. Distributed for educational and research use; see the
source for per-file XML documentation.
