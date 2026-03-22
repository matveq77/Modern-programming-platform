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

    static void Main(string[] args)
    {
        Console.WriteLine("=== ЛАБОРАТОРНАЯ РАБОТА №3: ДИНАМИЧЕСКИЙ ПУЛ ПОТОКОВ ===\n");

        var assembly = typeof(ManagerTests).Assembly;
        // Собираем тесты и сразу СОРТИРУЕМ по Priority (из ЛР 1)
        var allTestJobs = DiscoverTests(assembly)
            .OrderBy(j => j.Priority)
            .ToList();

        // 1. ДЕМОНСТРАЦИЯ ЭФФЕКТИВНОСТИ МАСШТАБИРОВАНИЯ
        RunEfficiencyBenchmark(allTestJobs);

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine(">>> ПЕРЕХОД К МОДЕЛИРОВАНИЮ СЛОЖНОЙ НАГРУЗКИ");

        // 2. ОСНОВНОЙ ПУЛ ДЛЯ СЦЕНАРИЕВ
        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 10, idleTimeoutMs: 3000, taskMaxDurationMs: 5000);

        // Сценарий: Всплеск нагрузки + Зависание
        Console.WriteLine("\n>>> Сценарий: Пиковая нагрузка и проверка замены зависших");

        // Запускаем 40 тестов "пачкой"
        for (int i = 0; i < 40; i++)
        {
            var job = allTestJobs[i % allTestJobs.Count];
            pool.Execute(() => RunRealTest(job));
        }

        // Добавляем зависающую задачу (Доп. задание)
        pool.Execute(() => {
            int tid = Thread.CurrentThread.ManagedThreadId;
            lock (_consoleLock) Console.WriteLine($"  [Thread {tid}] !!! ВНИМАНИЕ: Запущен бесконечный тест...");
            while (true) { Thread.Sleep(1000); }
        });

        // Ожидание сжатия пула
        Console.WriteLine("\n>>> Ожидание бездействия (10 сек) для адаптивного сжатия...");
        Thread.Sleep(10000);

        Console.WriteLine("\nДЕМОНСТРАЦИЯ ЗАВЕРШЕНА.");
    }

    static void RunEfficiencyBenchmark(List<TestJob> jobs)
    {
        Console.WriteLine(">>> ТЕСТ ЭФФЕКТИВНОСТИ МАСШТАБИРОВАНИЯ (пачка из 20 тяжелых тестов)");
        var benchmarkJobs = jobs.Where(j => j.Method.Name.Contains("Heavy")).Take(20).ToList();

        // Тест 1: Статический пул (всего 2 потока)
        Stopwatch sw = Stopwatch.StartNew();
        using (var staticPool = new MyThreadPool(2, 2)) // min = max = 2
        {
            CountdownEvent countdown = new CountdownEvent(benchmarkJobs.Count);
            foreach (var j in benchmarkJobs)
                staticPool.Execute(() => { RunRealTest(j); countdown.Signal(); });
            countdown.Wait();
        }
        sw.Stop();
        long staticTime = sw.ElapsedMilliseconds;

        // Тест 2: Динамический пул (от 2 до 10 потоков)
        sw.Restart();
        using (var dynamicPool = new MyThreadPool(2, 10))
        {
            CountdownEvent countdown = new CountdownEvent(benchmarkJobs.Count);
            foreach (var j in benchmarkJobs)
                dynamicPool.Execute(() => { RunRealTest(j); countdown.Signal(); });
            countdown.Wait();
        }
        sw.Stop();
        long dynamicTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"\nРезультаты сравнения:");
        Console.WriteLine($"- Статический пул (2 потока): {staticTime} мс");
        Console.WriteLine($"- Динамический пул (до 10 потоков): {dynamicTime} мс");
        Console.WriteLine($"- Эффективность масштабирования: Ускорение в {(double)staticTime / dynamicTime:F2} раз");
    }

    static void RunRealTest(TestJob job)
    {
        ExecuteSingleTest(job).GetAwaiter().GetResult();
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

            foreach (var test in methods.Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null))
            {
                var attr = test.GetCustomAttribute<TestMethodAttribute>();
                var dataRows = test.GetCustomAttributes<DataRowAttribute>().ToList();
                var runs = dataRows.Any() ? dataRows.Select(d => d.Data).ToList() : new List<object[]> { null };

                foreach (var data in runs)
                {
                    jobs.Add(new TestJob
                    {
                        ClassType = type,
                        Method = test,
                        Setup = setup,
                        Teardown = teardown,
                        Data = data,
                        Priority = attr.Priority, // Из ЛР 1
                        Description = attr.Description // Из ЛР 1
                    });
                }
            }
        }
        return jobs;
    }

    static async Task<TestStatus> ExecuteSingleTest(TestJob job)
    {
        // Атрибут Ignore из ЛР 1
        if (job.Method.GetCustomAttribute<IgnoreAttribute>() != null)
        {
            LogResult(job, "Skipped", ConsoleColor.Yellow);
            return TestStatus.Skipped;
        }

        var instance = Activator.CreateInstance(job.ClassType);
        try
        {
            job.Setup?.Invoke(instance, null);

            object[] args = null;
            if (job.Data != null)
            {
                var targetParams = job.Method.GetParameters();
                args = job.Data.Select((v, i) => Convert.ChangeType(v, targetParams[i].ParameterType)).ToArray();
            }

            var res = job.Method.Invoke(instance, args);
            if (res is Task t) await t;

            LogResult(job, "OK", ConsoleColor.Green);
            return TestStatus.Passed;
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException ? ex.InnerException : ex;
            LogResult(job, $"Failed: {inner.Message}", ConsoleColor.Red);
            return TestStatus.Failed;
        }
        finally
        {
            job.Teardown?.Invoke(instance, null);
        }
    }

    static void LogResult(TestJob job, string message, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            Console.ForegroundColor = ConsoleColor.White;
            string desc = !string.IsNullOrEmpty(job.Description) ? $"({job.Description})" : "";
            string info = job.Data != null ? $" [{string.Join(", ", job.Data)}]" : "";

            Console.Write($"  [Thread {tid}] {job.Method.Name}{desc}{info} ... ");
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}

public enum TestStatus { Passed, Failed, Skipped }
public class TestJob
{
    public Type ClassType { get; set; }
    public MethodInfo Method { get; set; }
    public MethodInfo Setup { get; set; }
    public MethodInfo Teardown { get; set; }
    public object[] Data { get; set; }
    public int Priority { get; set; }
    public string Description { get; set; }
}