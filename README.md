# Habit Tracker

A zero-dependency command-line habit tracker in C# / .NET 8, built around one
idea: **streaks are the product**. It records daily and weekly habits in a
local JSON file and reports current streak, longest streak, and completion
rate — the numbers that actually keep a habit alive.

## Why this exists

Most habit apps are cloud accounts with subscriptions. This one is a single
self-contained binary, your data is a readable JSON file on your own machine,
and the streak math is pure, tested, and easy to reason about. It is also a
good tour of everyday professional C#: domain modeling, an interface-based
persistence layer, atomic file writes, and a hand-rolled CLI.

## Key design decisions

1. **Calendar dates, not timestamps.** Check-ins are stored as `DateOnly`.
   Streaks are computed over calendar days (or ISO weeks), so checking in
   three times on Monday never inflates a streak — a common bug in naive
   timestamp-diff implementations.
2. **A "live" streak survives today.** The current streak counts back from
   today, but if today isn't done yet it starts from yesterday. A streak only
   dies when a full required period passes with no check-in — which is how
   users actually think about streaks.
3. **Atomic persistence.** The repository writes to a temp file and renames it
   into place, so a crash mid-save can never leave a half-written data file.
   A process-wide lock keeps overlapping CLI invocations safe.
4. **No CLI framework.** Argument parsing is ~40 lines of hand-rolled code.
   The tool has zero third-party runtime dependencies, which keeps the binary
   small and the supply chain trivial.

## Project structure

```
src/HabitTracker.Core/    Domain model (Habit, CheckIn), StreakCalculator,
                          IHabitRepository + JsonHabitRepository
src/HabitTracker.Cli/     Console app: add / check / list / stats / delete
tests/HabitTracker.Tests/ xUnit tests for streak math and persistence
```

## Requirements

- .NET 8 SDK

## Run it

```bash
dotnet run --project src/HabitTracker.Cli -- add "Read 20 pages" --desc "Before bed"
dotnet run --project src/HabitTracker.Cli -- add "Gym" --weekly

dotnet run --project src/HabitTracker.Cli -- check "Read 20 pages" --note "Chapter 4"
dotnet run --project src/HabitTracker.Cli -- check "Gym" --date 2026-10-13

dotnet run --project src/HabitTracker.Cli -- list
dotnet run --project src/HabitTracker.Cli -- stats
dotnet run --project src/HabitTracker.Cli -- delete "Gym"
```

Or publish a single binary:

```bash
dotnet publish src/HabitTracker.Cli -c Release -o ./publish
./publish/habits list
```

Commands at a glance:

| Command | What it does |
|---|---|
| `add <name> [--desc <text>] [--weekly]` | Track a new habit (daily by default) |
| `check <name\|id> [--date yyyy-MM-dd] [--note <text>]` | Log a completion (idempotent per period) |
| `list` | All habits with today's status and current streak |
| `stats [name\|id]` | Current/longest streak, completion rate, totals |
| `delete <name\|id>` | Remove a habit and its history |

Data lives in `~/.habittracker/habits.json`; set `HABIT_TRACKER_DATA` to use a
different path (handy for testing or syncing via your own dotfiles).

## Run the tests

```bash
dotnet test
```

14 xUnit tests cover streak edge cases (gaps, missed weeks, duplicate
check-ins, empty habits) and repository round-trips, including corrupt-file
handling.

## Tech stack

- C# 12 / .NET 8, nullable reference types enabled
- `System.Text.Json` for persistence (no ORM, no database)
- xUnit + `Microsoft.NET.Test.Sdk` for tests
- MIT licensed
