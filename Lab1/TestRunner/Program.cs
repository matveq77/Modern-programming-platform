using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyTestFramework;
using OrderTests;

class Program
{
    private static readonly object _consoleLock = new object();
    private static int _totalPassed = 0;
    private static int _totalFailed = 0;
    private static int _totalIgnored = 0;

    static void Main(string[] args)
    {
        PrintHeader("=ФРЕЙМВОРК ТЕСТИРОВАНИЯ");

        var assembly = typeof(AdvancedTests).Assembly;
        var allTestJobs = DiscoverTests(assembly);

        Console.WriteLine($"\nОбнаружено тестов: {allTestJobs.Count}\n");

        //Demo_LR1_AssertMethods();
        //Demo_LR1_SharedContext();
        //Demo_LR2_Sequential_vs_Parallel(allTestJobs);
        //Demo_LR3_ThreadPoolLoad(allTestJobs);
        Demo_LR4_ParameterizedTests(allTestJobs);
        Demo_LR4_EventSystem();
        Demo_LR4_FilterByDelegate(allTestJobs);
        Demo_LR4_ExpressionTree();

        PrintFinalSummary();
        Console.WriteLine("\nНажмите Enter для выхода...");
        Console.ReadLine();
    }

    static void Demo_LR1_AssertMethods()
    {
        PrintSection("ЛР 1 — Проверки (Assert методы)");

        var cases = new (string name, Action action, bool shouldFail)[]
        {
            ("AreEqual(10, 10)",           () => Assert.AreEqual(10, 10),                                    false),
            ("AreNotEqual(1, 2)",          () => Assert.AreNotEqual(1, 2),                                   false),
            ("IsTrue(5 > 3)",              () => Assert.IsTrue(5 > 3),                                       false),
            ("IsFalse(1 > 10)",            () => Assert.IsFalse(1 > 10),                                     false),
            ("IsNotNull(\"hello\")",       () => Assert.IsNotNull("hello"),                                  false),
            ("IsNull(null)",               () => Assert.IsNull(null),                                        false),
            ("Contains(\"ell\",\"hello\")",() => Assert.Contains("ell", "hello"),                            false),
            ("IsGreaterThan(10, 5)",       () => Assert.IsGreaterThan(10m, 5m),                              false),
            ("IsInstanceOf<string>",       () => Assert.IsInstanceOf<string>("test"),                        false),
            ("Throws<ArgumentException>",  () => Assert.Throws<ArgumentException>(() => throw new ArgumentException()), false),
            ("AreEqual ПРОВАЛ(1!=2)",      () => Assert.AreEqual(1, 2),                                      true),
            ("IsTrue ПРОВАЛ(false)",       () => Assert.IsTrue(false),                                       true),
        };

        foreach (var (name, action, shouldFail) in cases)
        {
            try
            {
                action();
                if (shouldFail)
                    PrintResult(name, "НЕОЖИДАННЫЙ УСПЕХ", ConsoleColor.Yellow);
                else
                    PrintResult(name, "OK", ConsoleColor.Green);
            }
            catch (TestAssertionException ex)
            {
                if (shouldFail)
                    PrintResult(name, $"ОЖИДАЕМЫЙ ПРОВАЛ: {ex.Message}", ConsoleColor.Yellow);
                else
                    PrintResult(name, $"ОШИБКА: {ex.Message}", ConsoleColor.Red);
            }
        }
    }

    static void Demo_LR1_SharedContext()
    {
        PrintSection("Разделяемый контекст (Shared Context)");

        var sharedContext = new Dictionary<string, object>
        {
            ["connectionString"] = "Server=localhost;Database=TestDB",
            ["maxRetries"] = 3,
            ["timeout"] = 5000
        };

        Console.WriteLine("  Общий контекст создан и передается в тесты:");
        foreach (var kv in sharedContext)
            Console.WriteLine($"    {kv.Key} = {kv.Value}");

        for (int i = 1; i <= 3; i++)
        {
            string connStr = (string)sharedContext["connectionString"];
            Assert.IsNotNull(connStr);
            Assert.Contains("localhost", connStr);
            Console.WriteLine($"  [Тест {i}] Использует контекст — строка подключения: {connStr} ... OK");
        }

        Console.WriteLine("  Разделяемый контекст успешно использован во всех тестах.");
    }

    static void Demo_LR2_Sequential_vs_Parallel(List<TestJob> jobs)
    {
        PrintSection("Сравнение последовательного и параллельного выполнения");

        var loadJobs = jobs.Where(j => j.Categories.Contains("Load") || j.Categories.Contains("Async")).Take(10).ToList();
        if (loadJobs.Count < 3) loadJobs = jobs.Take(10).ToList();

        Console.WriteLine($"  Тестов для сравнения: {loadJobs.Count}");

        Stopwatch sw = Stopwatch.StartNew();
        foreach (var job in loadJobs)
            RunSingleTest(job, verbose: false);
        sw.Stop();
        long seqMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"\n  [ПОСЛЕДОВАТЕЛЬНО] Завершено за {seqMs} мс");

        sw.Restart();
        int maxDeg = 4;
        Console.WriteLine($"  MaxDegreeOfParallelism = {maxDeg}");
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDeg };
        Parallel.ForEach(loadJobs, options, job => RunSingleTest(job, verbose: false));
        sw.Stop();
        long parMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"  [ПАРАЛЛЕЛЬНО]      Завершено за {parMs} мс");

        double speedup = seqMs > 0 ? (double)seqMs / parMs : 1;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  УСКОРЕНИЕ: x{speedup:F2} (параллельный в {speedup:F2} раза быстрее)");
        Console.ResetColor();
    }

    static void Demo_LR3_ThreadPoolLoad(List<TestJob> jobs)
    {
        PrintSection("Динамический пул потоков (50+ запусков, неравномерная нагрузка)");

        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 8, idleTimeoutMs: 2000, taskMaxDurationMs: 8000);

        pool.ThreadCreated += (s, e) => LogPool($"  ↑   #{e.ThreadId,-5} | {e.Message}", ConsoleColor.Green);
        pool.ThreadRemoved += (s, e) => LogPool($"  ↓   #{e.ThreadId,-5} | {e.Message}", ConsoleColor.DarkYellow);
        pool.ThreadHanged += (s, e) => LogPool($"  ✗   #{e.ThreadId,-5} | {e.Message}", ConsoleColor.Magenta);

        int tasksSubmitted = 0;
        object statsLock = new object();
        int passed = 0, failed = 0;

        void SubmitJob(TestJob job)
        {
            Interlocked.Increment(ref tasksSubmitted);
            pool.Execute(() =>
            {
                bool ok = RunSingleTest(job, verbose: false);
                lock (statsLock) { if (ok) passed++; else failed++; }
            });
        }

        Console.WriteLine(" Пиковая нагрузка — 25 задач одновременно");
        for (int i = 0; i < 25; i++) SubmitJob(jobs[i % jobs.Count]);
        PrintPoolState(pool, "после первого пика");
        Thread.Sleep(2000);

        Console.WriteLine("\n Период бездействия (2 сек)...");
        Thread.Sleep(2000);
        PrintPoolState(pool, "после паузы");

        Console.WriteLine("\n Средняя нагрузка — 15 задач");
        for (int i = 0; i < 15; i++)
        {
            SubmitJob(jobs[i % jobs.Count]);
            Thread.Sleep(50);
        }
        PrintPoolState(pool, "во время средней нагрузки");
        Thread.Sleep(2000);

        Console.WriteLine("\n Период бездействия (2 сек)...");
        Thread.Sleep(2000);
        PrintPoolState(pool, "после второй паузы");

        Console.WriteLine("\n Единичные подачи — 10 задач с интервалами");
        for (int i = 0; i < 10; i++)
        {
            SubmitJob(jobs[i % jobs.Count]);
            Thread.Sleep(300);
        }
        PrintPoolState(pool, "после единичных подач");

        pool.WaitAllTasks(30000);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  ИТОГ ПУЛА: Отправлено={tasksSubmitted} | Пройдено={passed} | Упало={failed}");
        Console.WriteLine($"  Потоков сейчас: {pool.CurrentThreadCount}");
        Console.ResetColor();

        Console.WriteLine($"\n  Не менее 50 запусков — выполнено ({tasksSubmitted} запусков)");
    }

    static void Demo_LR4_ParameterizedTests(List<TestJob> jobs)
    {
        PrintSection("Параметризованные тесты (yield return / TestCaseSource)");

        var paramJobs = jobs.Where(j =>
            j.Method.GetCustomAttribute<TestCaseSourceAttribute>() != null).ToList();

        Console.WriteLine($"  Найдено параметризованных тест-кейсов: {paramJobs.Count}");
        Console.WriteLine();

        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 6, idleTimeoutMs: 1500);
        var done = new CountdownEvent(paramJobs.Count);

        foreach (var job in paramJobs)
        {
            pool.Execute(() =>
            {
                bool ok = RunSingleTest(job, verbose: true);
                done.Signal();
            });
        }

        done.Wait(30000);
        pool.WaitAllTasks();
    }

    static void Demo_LR4_EventSystem()
    {
        PrintSection("Система событий пула потоков (жизненный цикл)");

        var log = new List<string>();
        object logLock = new object();

        using var pool = new MyThreadPool(minThreads: 1, maxThreads: 5, idleTimeoutMs: 1000, taskMaxDurationMs: 4000);

        pool.ThreadCreated += (s, e) => { lock (logLock) log.Add($"    [{e.Timestamp:HH:mm:ss.fff}] THREAD_CREATED   #{e.ThreadId}"); };
        pool.ThreadRemoved += (s, e) => { lock (logLock) log.Add($"    [{e.Timestamp:HH:mm:ss.fff}] THREAD_REMOVED   #{e.ThreadId}"); };
        pool.TaskStarted += (s, e) => { lock (logLock) log.Add($"    [{e.Timestamp:HH:mm:ss.fff}] TASK_STARTED     на #{e.ThreadId}"); };
        pool.TaskCompleted += (s, e) => { lock (logLock) log.Add($"    [{e.Timestamp:HH:mm:ss.fff}] TASK_COMPLETED   на #{e.ThreadId}"); };
        pool.ThreadHanged += (s, e) => { lock (logLock) log.Add($"    [{e.Timestamp:HH:mm:ss.fff}] THREAD_HANGED    #{e.ThreadId}"); };

        Console.WriteLine("  Запускаем 8 задач и наблюдаем все события:");

        var latch = new CountdownEvent(8);
        for (int i = 0; i < 8; i++)
        {
            int n = i;
            pool.Execute(() =>
            {
                Thread.Sleep(200 + n * 30);
                latch.Signal();
            });
        }

        latch.Wait(10000);
        pool.WaitAllTasks(5000);
        Thread.Sleep(1500);

        foreach (var entry in log)
        {
            Console.ForegroundColor = entry.Contains("CREATED") ? ConsoleColor.Green
                : entry.Contains("REMOVED") ? ConsoleColor.DarkYellow
                : entry.Contains("STARTED") ? ConsoleColor.White
                : entry.Contains("COMPLETED") ? ConsoleColor.Gray
                : ConsoleColor.Magenta;
            Console.WriteLine(entry);
            Console.ResetColor();
        }

        Console.WriteLine($"\n  Всего событий зафиксировано: {log.Count}");
    }

    static void Demo_LR4_FilterByDelegate(List<TestJob> jobs)
    {
        PrintSection("Фильтрация тестов делегатами");

        Predicate<TestJob> filterCritical = job => job.Categories.Contains("Critical");
        Predicate<TestJob> filterByAuthor = job => {
            var actualAuthor = job.Method.GetCustomAttribute<AuthorAttribute>()?.Name
                             ?? job.ClassType.GetCustomAttribute<AuthorAttribute>()?.Name;

            return actualAuthor == "Иванов И.И.";
        };
        Predicate<TestJob> filterHighPrio = job =>
        {
            var p = job.Method.GetCustomAttribute<PriorityAttribute>();
            return p != null && p.Level <= 2;
        };
        Predicate<TestJob> filterSmoke = job => job.Categories.Contains("Smoke");
        Predicate<TestJob> filterParam = job => job.Method.GetCustomAttribute<TestCaseSourceAttribute>() != null;

        var filters = new (string label, Predicate<TestJob> filter)[]
        {
            ("Категория 'Critical'",                filterCritical),
            ("Автор 'Иванов И.И.'",                 filterByAuthor),
            ("Приоритет <= 2",                      filterHighPrio),
            ("Категория 'Smoke'",                   filterSmoke),
            ("Параметризованные (yield return)",    filterParam),
        };

        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 6, idleTimeoutMs: 1000);

        foreach (var (label, filter) in filters)
        {
            var subset = jobs.Where(j => filter(j)).ToList();
            Console.WriteLine($"\n  Фильтр: [{label}] — найдено {subset.Count} тест(ов)");

            if (subset.Count == 0) { Console.WriteLine("    (нет тестов)"); continue; }

            var done = new CountdownEvent(subset.Count);
            int passed = 0, failed = 0;
            object lk = new object();

            foreach (var job in subset)
            {
                pool.Execute(() =>
                {
                    bool ok = RunSingleTest(job, verbose: true);
                    lock (lk) { if (ok) passed++; else failed++; }
                    done.Signal();
                });
            }

            done.Wait(20000);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"    Итог: PASSED={passed} FAILED={failed}");
            Console.ResetColor();
            Thread.Sleep(500);
        }
    }

    static void Demo_LR4_ExpressionTree()
    {
        PrintSection("Assert.That с деревом выражений");

        Console.WriteLine("  Метод Assert.That принимает Expression<Func<bool>> и при провале");
        Console.WriteLine("  разбирает дерево выражений: выводит операнды, оператор и структуру.\n");

        var cases = new (string desc, System.Linq.Expressions.Expression<Func<bool>> expr, bool shouldFail)[]
        {
            ("5 > 3  (успешно)",  () => 5 > 3,   false),
            ("10 == 10 (успешно)", () => 10 == 10, false),
            ("42 > 50 (провал)",  () => 42 > 50,  true),
            ("7 == 8  (провал)",  () => 7 == 8,   true),
            ("3 < 1   (провал)",  () => 3 < 1,    true),
        };

        foreach (var (desc, expr, shouldFail) in cases)
        {
            try
            {
                Assert.That(expr);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✔ {desc}");
                Console.ResetColor();
            }
            catch (TestAssertionException ex)
            {
                Console.ForegroundColor = shouldFail ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.WriteLine($"  ✗ {desc}");
                Console.ForegroundColor = ConsoleColor.Gray;
                foreach (var line in ex.Message.Split('\n'))
                    Console.WriteLine($"      {line.Trim()}");
                Console.ResetColor();
            }
        }
    }

    static bool RunSingleTest(TestJob job, bool verbose = true)
    {
        if (job.Method.GetCustomAttribute<IgnoreAttribute>() != null)
        {
            Interlocked.Increment(ref _totalIgnored);
            if (verbose) PrintTestLine(job, "IGNORED", ConsoleColor.Gray);
            return true;
        }

        var instance = Activator.CreateInstance(job.ClassType);
        var timeoutAttr = job.Method.GetCustomAttribute<TimeoutAttribute>();

        try
        {
            job.Setup?.Invoke(instance, null);

            object result;
            if (timeoutAttr != null)
            {
                using var cts = new CancellationTokenSource();
                var task = Task.Run(() =>
                {
                    var r = job.Method.Invoke(instance, job.Data);
                    if (r is Task t) t.GetAwaiter().GetResult();
                });
                bool completed = task.Wait(timeoutAttr.Milliseconds);
                if (!completed)
                {
                    cts.Cancel();
                    throw new TimeoutException($"Превышено время {timeoutAttr.Milliseconds}мс");
                }
            }
            else
            {
                result = job.Method.Invoke(instance, job.Data);
                if (result is Task t) t.GetAwaiter().GetResult();
            }

            Interlocked.Increment(ref _totalPassed);
            if (verbose) PrintTestLine(job, "PASSED", ConsoleColor.Green);
            return true;
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException ? ex.InnerException : ex;
            Interlocked.Increment(ref _totalFailed);
            if (verbose) PrintTestLine(job, $"FAILED: {inner?.Message}", ConsoleColor.Red);
            return false;
        }
        finally
        {
            try { job.Teardown?.Invoke(instance, null); } catch { }
        }
    }

    static List<TestJob> DiscoverTests(Assembly assembly)
    {
        var jobs = new List<TestJob>();
        var types = assembly.GetTypes().Where(t => t.GetCustomAttribute<TestClassAttribute>() != null);

        foreach (var type in types)
        {
            var methods = type.GetMethods();
            var setup = methods.FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
            var teardown = methods.FirstOrDefault(m => m.GetCustomAttribute<TeardownAttribute>() != null);

            foreach (var method in methods.Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null))
            {
                var attr = method.GetCustomAttribute<TestMethodAttribute>();
                var categories = method.GetCustomAttributes<CategoryAttribute>().Select(c => c.Name).ToList();
                var sourceAttr = method.GetCustomAttribute<TestCaseSourceAttribute>();
                int priority = method.GetCustomAttribute<PriorityAttribute>()?.Level ?? attr.Priority;
                string author = method.GetCustomAttribute<AuthorAttribute>()?.Name
                              ?? type.GetCustomAttribute<AuthorAttribute>()?.Name
                              ?? "Неизвестен";

                if (sourceAttr != null)
                {
                    var srcMethod = type.GetMethod(sourceAttr.MethodName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (srcMethod != null)
                    {
                        var dataSet = (IEnumerable<object[]>)srcMethod.Invoke(null, null);
                        foreach (var data in dataSet)
                            jobs.Add(new TestJob { ClassType = type, Method = method, Setup = setup, Teardown = teardown, Data = data, Priority = priority, Description = attr.Description, Categories = categories, Author = author });
                        continue;
                    }
                }

                var dataRows = method.GetCustomAttributes<DataRowAttribute>().ToList();
                if (dataRows.Any())
                {
                    foreach (var row in dataRows)
                        jobs.Add(new TestJob { ClassType = type, Method = method, Setup = setup, Teardown = teardown, Data = row.Data, Priority = priority, Description = attr.Description, Categories = categories, Author = author });
                }
                else
                {
                    jobs.Add(new TestJob { ClassType = type, Method = method, Setup = setup, Teardown = teardown, Data = null, Priority = priority, Description = attr.Description, Categories = categories, Author = author });
                }
            }
        }

        return jobs;
    }

    static void PrintTestLine(TestJob job, string status, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            string dataInfo = job.Data != null ? $"({string.Join(", ", job.Data)})" : "";
            string methodName = job.Method.Name;
            string author = job.Author ?? "";

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"    [T#{tid,-3}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{methodName}{dataInfo,-30} ");
            if (!string.IsNullOrEmpty(author))
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write($"[{author}] ");
            }
            Console.ForegroundColor = color;
            Console.WriteLine(status);
            Console.ResetColor();
        }
    }

    static void PrintPoolState(MyThreadPool pool, string context)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"    │ Состояние пула ({context}): потоков={pool.CurrentThreadCount}, в очереди={pool.TasksInQueue}");
            Console.ResetColor();
        }
    }

    static void PrintResult(string name, string result, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            Console.Write($"    {name,-45} ");
            Console.ForegroundColor = color;
            Console.WriteLine(result);
            Console.ResetColor();
        }
    }

    static void LogPool(string msg, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
    }

    static void PrintSection(string title)
    {
        lock (_consoleLock)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"┌─ {title}");
            Console.ResetColor();
        }
    }

    static void PrintHeader(string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('═', 60));
        Console.WriteLine(title);
        Console.WriteLine(new string('═', 60));
        Console.ResetColor();
    }

    static void PrintFinalSummary()
    {
        PrintSection("ИТОГОВАЯ СТАТИСТИКА");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"    PASSED:  {_totalPassed}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"    FAILED:  {_totalFailed}");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"    IGNORED: {_totalIgnored}");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"    TOTAL:   {_totalPassed + _totalFailed + _totalIgnored}");
        Console.ResetColor();
    }
}

public class TestJob
{
    public Type ClassType { get; set; }
    public MethodInfo Method { get; set; }
    public MethodInfo Setup { get; set; }
    public MethodInfo Teardown { get; set; }
    public object[] Data { get; set; }
    public int Priority { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public List<string> Categories { get; set; } = new List<string>();
}