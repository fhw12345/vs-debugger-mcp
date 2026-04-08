namespace TestDebugApp;

class Program
{
    static void Main(string[] args)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== VS Debugger MCP Test App ===");
            Console.WriteLine("1. Basic variables & loop");
            Console.WriteLine("2. Exception test");
            Console.WriteLine("3. Multithreading test");
            Console.WriteLine("4. Slow loop (performance test)");
            Console.WriteLine("0. Exit");
            Console.Write("> ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1": TestBasic(); break;
                case "2": TestException(); break;
                case "3": TestThreading(); break;
                case "4": TestPerformance(); break;
                case "0": return;
                default: Console.WriteLine("Invalid option."); break;
            }
        }
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
}

class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
