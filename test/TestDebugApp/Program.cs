namespace TestDebugApp;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            RunInteractiveMenu();
            return 0;
        }

        if (!TryParseScenario(args, out var scenarioName, out var scenarioAction, out var parseError))
        {
            Console.WriteLine($"[Scenario] Invalid arguments: {parseError}");
            Console.WriteLine("Usage: --scenario basic|exception|threading|performance|async");
            return 2;
        }

        return RunScenario(scenarioName!, scenarioAction!);
    }

    static void RunInteractiveMenu()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== VS Debugger MCP Test App ===");
            Console.WriteLine("1. Basic variables & loop");
            Console.WriteLine("2. Exception test");
            Console.WriteLine("3. Multithreading test");
            Console.WriteLine("4. Slow loop (performance test)");
            Console.WriteLine("5. Async/await test");
            Console.WriteLine("0. Exit");
            Console.Write("> ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1": TestBasic(); break;
                case "2": TestException(); break;
                case "3": TestThreading(); break;
                case "4": TestPerformance(); break;
                case "5": TestAsync(); break;
                case "0": return;
                default: Console.WriteLine("Invalid option."); break;
            }
        }
    }

    static int RunScenario(string scenarioName, Action scenarioAction)
    {
        Console.WriteLine($"[Scenario] START: {scenarioName}");

        try
        {
            scenarioAction();
            Console.WriteLine($"[Scenario] END: {scenarioName} (success)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scenario] END: {scenarioName} (failed)");
            Console.WriteLine($"[Scenario] Failure reason: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    static bool TryParseScenario(
        string[] args,
        out string? scenarioName,
        out Action? scenarioAction,
        out string? parseError)
    {
        scenarioName = null;
        scenarioAction = null;
        parseError = null;

        if (args.Length < 2 || args[0] != "--scenario")
        {
            parseError = "Expected --scenario <name>.";
            return false;
        }

        scenarioName = args[1].Trim().ToLowerInvariant();
        scenarioAction = scenarioName switch
        {
            "basic" => TestBasic,
            "exception" => TestException,
            "threading" => TestThreading,
            "performance" => TestPerformance,
            "async" => TestAsync,
            _ => null
        };

        if (scenarioAction is null)
        {
            parseError = $"Unknown scenario '{args[1]}'.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Simple variables and a loop — good for testing:
    /// BreakpointAdd, DebugGetLocals, DebugEvaluate, DebugStepOver,
    /// BreakpointAddConditional, WatchEvaluateMultiple
    /// </summary>
    static void TestBasic()
    {
        Console.WriteLine("[Basic] Starting...");

        var name = "DebugTest";
        var count = 10;
        var items = new List<string>();

        for (int i = 0; i < count; i++)
        {
            var item = $"{name}_{i}";
            items.Add(item);                   // <-- set breakpoint here
            Console.WriteLine($"  Added: {item}");
        }

        Console.WriteLine($"[Basic] Done. Total items: {items.Count}");
    }

    /// <summary>
    /// Throws a NullReferenceException inside a call chain — good for testing:
    /// ExceptionGetCurrent, ExceptionEnableBreak, DebugGetCallStack,
    /// DebugStepInto, DebugInspectVariable
    /// </summary>
    static void TestException()
    {
        Console.WriteLine("[Exception] Starting...");

        try
        {
            var user = GetUser(42);
            Console.WriteLine($"  User name length: {user.Name.Length}");  // NullRef here
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Exception] Caught: {ex.GetType().Name}: {ex.Message}");
        }
    }

    static User GetUser(int id)
    {
        // Simulate: user exists but Name is null
        return new User { Id = id, Name = null! };
    }

    /// <summary>
    /// Multiple threads incrementing a shared counter without locking — good for testing:
    /// DebugGetThreads, DebugFreezeThread, DebugThawThread, DebugSwitchThread,
    /// BreakpointAddTracepoint, DebugBreakAll
    /// </summary>
    static void TestThreading()
    {
        Console.WriteLine("[Threading] Starting 4 threads, each incrementing shared counter 10000 times...");

        int sharedCounter = 0;
        const int iterations = 10000;
        var threads = new Thread[4];

        for (int t = 0; t < threads.Length; t++)
        {
            var threadIndex = t;
            threads[t] = new Thread(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    sharedCounter++;           // <-- race condition: no lock
                }
                Console.WriteLine($"  Thread {threadIndex} done.");
            });
            threads[t].Name = $"Worker-{t}";
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        var expected = threads.Length * iterations;
        Console.WriteLine($"[Threading] Expected: {expected}, Actual: {sharedCounter}, Lost: {expected - sharedCounter}");
    }

    /// <summary>
    /// Deliberately slow O(n^3) loop — good for testing:
    /// BreakpointAddHitCount, BreakpointAddTracepoint with tick count,
    /// DebugBreakAll, DebugGetCallStack, DebugEvaluate
    /// </summary>
    static void TestPerformance()
    {
        Console.WriteLine("[Performance] Running slow O(n^3) computation (n=200)...");

        const int n = 200;
        long sum = 0;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    sum += (i * j) - k;        // <-- hot inner loop
                }
            }

            if (i % 50 == 0)
                Console.WriteLine($"  Progress: {i}/{n}");
        }

        Console.WriteLine($"[Performance] Done. Sum: {sum}");
    }

    /// <summary>
    /// Async workflow with Task.WhenAll and awaited methods — good for testing:
    /// DebugGetCallStack on async frames, DebugStepOver/Into across awaits,
    /// DebugEvaluate on task-related locals, WatchEvaluate on nested objects
    /// </summary>
    static void TestAsync()
    {
        Console.WriteLine("[Async] Starting async workflow...");

        var result = LoadDashboardAsync().GetAwaiter().GetResult();

        Console.WriteLine($"[Async] Done. User: {result.UserName}, Metrics: {result.Metrics.Count}, TotalScore: {result.TotalScore}");
    }

    static async Task<DashboardResult> LoadDashboardAsync()
    {
        var user = new User { Id = 7, Name = "AsyncUser" };
        var tasks = Enumerable.Range(1, 3)
            .Select(metricId => FetchMetricAsync(user, metricId))
            .ToArray();

        var metrics = await Task.WhenAll(tasks);    // <-- async breakpoint here
        var totalScore = metrics.Sum(metric => metric.Score);

        return new DashboardResult
        {
            UserName = user.Name,
            Metrics = metrics.ToList(),
            TotalScore = totalScore
        };
    }

    static async Task<Metric> FetchMetricAsync(User user, int metricId)
    {
        await Task.Delay(50 + (metricId * 25));

        return new Metric
        {
            Id = metricId,
            Label = $"{user.Name}_metric_{metricId}",
            Score = metricId * 10
        };
    }
}

class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

class Metric
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public int Score { get; set; }
}

class DashboardResult
{
    public string UserName { get; set; } = "";
    public List<Metric> Metrics { get; set; } = new();
    public int TotalScore { get; set; }
}
