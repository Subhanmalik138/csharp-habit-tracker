using HabitTracker.Core;

namespace HabitTracker.Cli;

/// <summary>
/// Command-line interface for the habit tracker. Zero third-party
/// dependencies - argument parsing is hand-rolled so the tool stays
/// a single self-contained binary.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var dataPath = Environment.GetEnvironmentVariable("HABIT_TRACKER_DATA")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".habittracker", "habits.json");
        var repo = new JsonHabitRepository(dataPath);

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "add" => Add(repo, args[1..]),
                "check" => Check(repo, args[1..]),
                "list" => List(repo),
                "stats" => Stats(repo, args[1..]),
                "delete" => Delete(repo, args[1..]),
                _ => Unknown(args[0])
            };
        }
        catch (CliException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Add(IHabitRepository repo, string[] args)
    {
        var opts = ParseOptions(args, out var positional);
        if (positional.Count == 0)
            throw new CliException("usage: habits add <name> [--desc <text>] [--weekly]");

        var name = positional[0];
        if (repo.GetAll().Any(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new CliException($"a habit named '{name}' already exists.");

        repo.Add(new Habit
        {
            Name = name,
            Description = opts.GetValueOrDefault("desc"),
            Frequency = opts.ContainsKey("weekly") ? HabitFrequency.Weekly : HabitFrequency.Daily
        });

        Console.WriteLine($"added '{name}'.");
        return 0;
    }

    private static int Check(IHabitRepository repo, string[] args)
    {
        var opts = ParseOptions(args, out var positional);
        if (positional.Count == 0)
            throw new CliException("usage: habits check <name|id> [--date yyyy-MM-dd] [--note <text>]");

        var habit = Resolve(repo, positional[0]);
        DateOnly date;
        if (opts.TryGetValue("date", out var raw))
        {
            if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", out date))
                throw new CliException($"invalid date '{raw}' - use yyyy-MM-dd.");
        }
        else
        {
            date = DateOnly.FromDateTime(DateTime.Now);
        }

        if (!habit.CheckIn(date, opts.GetValueOrDefault("note")))
            throw new CliException($"'{habit.Name}' is already checked in for {date:yyyy-MM-dd}.");

        repo.Update(habit);
        var report = StreakCalculator.Analyze(habit, DateOnly.FromDateTime(DateTime.Now));
        Console.WriteLine($"checked in '{habit.Name}' for {date:yyyy-MM-dd}. current streak: {report.CurrentStreak}.");
        return 0;
    }

    private static int List(IHabitRepository repo)
    {
        var habits = repo.GetAll();
        if (habits.Count == 0)
        {
            Console.WriteLine("no habits yet - add one with: habits add <name>");
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        Console.WriteLine($"{"NAME",-24} {"FREQ",-8} {"TODAY",-8} {"STREAK",-8} DESCRIPTION");
        foreach (var h in habits)
        {
            var report = StreakCalculator.Analyze(h, today);
            var done = StreakCalculator.IsDoneFor(h, today) ? "done" : "-";
            Console.WriteLine($"{Truncate(h.Name, 24),-24} {h.Frequency,-8} {done,-8} {report.CurrentStreak,-8} {h.Description ?? ""}");
        }
        return 0;
    }

    private static int Stats(IHabitRepository repo, string[] args)
    {
        var habits = args.Length > 0
            ? new[] { Resolve(repo, args[0]) }
            : repo.GetAll().ToArray();

        if (habits.Length == 0)
        {
            Console.WriteLine("no habits yet.");
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var h in habits)
        {
            var r = StreakCalculator.Analyze(h, today);
            Console.WriteLine($"== {h.Name} ({h.Frequency.ToString().ToLower()}) ==");
            Console.WriteLine($"  current streak : {r.CurrentStreak}");
            Console.WriteLine($"  longest streak : {r.LongestStreak}");
            Console.WriteLine($"  completion     : {r.CompletedPeriods}/{r.TotalPeriods} ({r.CompletionRate:P1})");
            Console.WriteLine($"  total check-ins: {h.CheckIns.Count}");
        }
        return 0;
    }

    private static int Delete(IHabitRepository repo, string[] args)
    {
        if (args.Length == 0)
            throw new CliException("usage: habits delete <name|id>");

        var habit = Resolve(repo, args[0]);
        repo.Delete(habit.Id);
        Console.WriteLine($"deleted '{habit.Name}'.");
        return 0;
    }

    private static Habit Resolve(IHabitRepository repo, string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var id))
        {
            var byId = repo.GetById(id);
            if (byId is not null) return byId;
        }

        var matches = repo.GetAll()
            .Where(h => h.Name.Contains(nameOrId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => throw new CliException($"no habit matches '{nameOrId}'."),
            1 => matches[0],
            _ => throw new CliException(
                $"ambiguous - matches: {string.Join(", ", matches.Select(m => $"'{m.Name}'"))}")
        };
    }

    private static Dictionary<string, string?> ParseOptions(string[] args, out List<string> positional)
    {
        var opts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--"))
            {
                var key = a[2..];
                string? value = null;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    value = args[++i];
                opts[key] = value;
            }
            else
            {
                positional.Add(a);
            }
        }
        return opts;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'.");
        PrintHelp();
        return 1;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static void PrintHelp()
    {
        Console.WriteLine("""
            habits - a tiny CLI habit tracker with streak analytics

            usage:
              habits add <name> [--desc <text>] [--weekly]
              habits check <name|id> [--date yyyy-MM-dd] [--note <text>]
              habits list
              habits stats [name|id]
              habits delete <name|id>

            data is stored in ~/.habittracker/habits.json
            (override with the HABIT_TRACKER_DATA environment variable)
            """);
    }

    private sealed class CliException(string message) : Exception(message);
}
