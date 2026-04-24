using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyTestFramework;
using OrderTests;

class Program
{
    private static readonly object _consoleLock = new object();

    static void Main(string[] args)
    {
        Console.WriteLine("=== МОДЕРНИЗИРОВАННЫЙ ТЕСТОВЫЙ ФРЕЙМВОРК (ЛР 4) ===\n");

        var assembly = typeof(AdvancvedTests).Assembly;
        var allTestJobs = DiscoverTests(assembly);

        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 8, idleTimeoutMs: 3000);

        pool.ThreadCreated += (s, e) => LogSystem($"[POOL_EVENT] Создан поток {e.ThreadId}", ConsoleColor.Cyan);
        pool.ThreadRemoved += (s, e) => LogSystem($"[POOL_EVENT] Удален поток {e.ThreadId} (простой)", ConsoleColor.DarkYellow);
        pool.ThreadHanged += (s, e) => LogSystem($"[POOL_EVENT] ОБНАРУЖЕНО ЗАВИСАНИЕ в потоке {e.ThreadId}!", ConsoleColor.Magenta);

        var executedSet = new HashSet<string>();

        Console.WriteLine("\n>>> ШАГ 1: Фильтрация (Категория 'Critical')");
        ExecuteSubset(allTestJobs, pool, job => job.Categories.Contains("Critical"), executedSet);
        Thread.Sleep(1500);

        Console.WriteLine("\n>>> ШАГ 2: Параметризация (yield return)");
        ExecuteSubset(allTestJobs, pool, job => job.Method.GetCustomAttribute<TestCaseSourceAttribute>() != null, executedSet);
        Thread.Sleep(1500);

        Console.WriteLine("\n>>> ШАГ 3: Детальный Assert (Expression Trees)");
        var exprTest = allTestJobs.FirstOrDefault(j => j.Method.Name == "TestExpressionTree");
        if (exprTest != null) pool.Execute(() => RunSingleTest(exprTest));
        Thread.Sleep(1500);

        Console.WriteLine("\n>>> ШАГ 4: Запуск остальных тестов (с учетом приоритетов)");
        var remaining = allTestJobs
            .Where(j => !executedSet.Contains(j.Method.Name + (j.Data != null ? string.Join("", j.Data) : "")))
            .OrderBy(j => j.Priority)
            .ToList();

        foreach (var job in remaining) pool.Execute(() => RunSingleTest(job));

        while (pool.TasksInQueue > 0 || pool.CurrentThreadCount > 2)
        {
            Console.Title = $"Threads: {pool.CurrentThreadCount} | Tasks: {pool.TasksInQueue}";
            Thread.Sleep(500);
        }

        Console.WriteLine("\n=== ВСЕ ТЕСТЫ И ДЕМОНСТРАЦИИ ЗАВЕРШЕНЫ ===");
        Console.ReadLine();
    }

    static void ExecuteSubset(List<TestJob> jobs, MyThreadPool pool, Predicate<TestJob> filter, HashSet<string> executed)
    {
        var filtered = jobs.Where(j => filter(j)).ToList();
        foreach (var job in filtered)
        {
            string key = job.Method.Name + (job.Data != null ? string.Join("", job.Data) : "");
            executed.Add(key);
            pool.Execute(() => RunSingleTest(job));
        }
    }

    static void RunSingleTest(TestJob job)
    {
        if (job == null || job.Method.GetCustomAttribute<IgnoreAttribute>() != null) return;

        var instance = Activator.CreateInstance(job.ClassType);
        try
        {
            job.Setup?.Invoke(instance, null);
            object res = job.Method.Invoke(instance, job.Data);
            if (res is Task t) t.GetAwaiter().GetResult();
            LogResult(job, "PASSED", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException ? ex.InnerException : ex;
            LogResult(job, $"FAILED: {inner.Message}", ConsoleColor.Red);
        }
        finally
        {
            job.Teardown?.Invoke(instance, null);
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

                if (sourceAttr != null)
                {
                    var sourceMethod = type.GetMethod(sourceAttr.MethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (sourceMethod != null)
                    {
                        var dataSet = (IEnumerable<object[]>)sourceMethod.Invoke(null, null);
                        foreach (var data in dataSet)
                            jobs.Add(new TestJob { ClassType = type, Method = method, Setup = setup, Teardown = teardown, Data = data, Priority = attr.Priority, Description = attr.Description, Categories = categories });
                        continue;
                    }
                }

                var dataRows = method.GetCustomAttributes<DataRowAttribute>().ToList();
                if (dataRows.Any())
                {
                    foreach (var row in dataRows)
                        jobs.Add(new TestJob { ClassType = type, Method = method, Setup = setup, Teardown = teardown, Data = row.Data, Priority = attr.Priority, Description = attr.Description, Categories = categories });
                }
                else
                {
                    jobs.Add(new TestJob { ClassType = type, Method = method, Setup = setup, Teardown = teardown, Data = null, Priority = attr.Priority, Description = attr.Description, Categories = categories });
                }
            }
        }
        return jobs;
    }

    static void LogResult(TestJob job, string status, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            string dataInfo = job.Data != null ? $" [{string.Join(", ", job.Data)}]" : "";
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"  [Thread {tid}] {job.Method.Name}{dataInfo} ... ");
            Console.ForegroundColor = color;
            Console.WriteLine(status);
            Console.ResetColor();
        }
    }

    static void LogSystem(string msg, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
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
    public List<string> Categories { get; set; } = new List<string>();
}