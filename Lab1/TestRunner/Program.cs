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

    static async Task Main(string[] args)
    {
        int maxParallelism = 5; // Настройте количество потоков здесь

        Console.WriteLine("=== ЛАБОРАТОРНАЯ РАБОТА №2: СРАВНЕНИЕ ЭФФЕКТИВНОСТИ ===\n");

        // 1. СТРОГО ПОСЛЕДОВАТЕЛЬНЫЙ ЗАПУСК
        var sw = Stopwatch.StartNew();
        await RunAllTests(parallel: false, maxParallelism: 1);
        sw.Stop();
        long sequentialTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"\n> Время последовательного выполнения: {sequentialTime} ms\n");
        Console.WriteLine(new string('=', 50));

        // 2. ПАРАЛЛЕЛЬНЫЙ ЗАПУСК
        sw.Restart();
        await RunAllTests(parallel: true, maxParallelism: maxParallelism);
        sw.Stop();
        long parallelTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"\n> Время параллельного выполнения ({maxParallelism} потока): {parallelTime} ms");
        Console.WriteLine($"> Ускорение: {(double)sequentialTime / parallelTime:F2}x");
    }

    static async Task RunAllTests(bool parallel, int maxParallelism)
    {
        var assembly = typeof(ManagerTests).Assembly;

        // ВЫЗОВ ТОГО САМОГО МЕТОДА, КОТОРОГО НЕ ХВАТАЛО
        var allTestJobs = DiscoverTests(assembly);

        int passed = 0, failed = 0, skipped = 0;

        if (!parallel)
        {
            Console.WriteLine($"Запуск {allTestJobs.Count} тестов ПОСЛЕДОВАТЕЛЬНО...");
            foreach (var job in allTestJobs)
            {
                var result = await ExecuteSingleTest(job);
                if (result == TestStatus.Passed) passed++;
                else if (result == TestStatus.Failed) failed++;
                else skipped++;
            }
        }
        else
        {
            Console.WriteLine($"Запуск {allTestJobs.Count} тестов ПАРАЛЛЕЛЬНО ({maxParallelism} потока)...");
            using var semaphore = new SemaphoreSlim(maxParallelism);

            var tasks = allTestJobs.Select(async job =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var result = await ExecuteSingleTest(job);
                    if (result == TestStatus.Passed) Interlocked.Increment(ref passed);
                    else if (result == TestStatus.Failed) Interlocked.Increment(ref failed);
                    else Interlocked.Increment(ref skipped);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
        }

        Console.WriteLine($"\nРезультат: Passed: {passed}, Failed: {failed}, Skipped: {skipped}");
    }

    // МЕТОД ДЛЯ СБОРА ТЕСТОВ (DISCOVERY)
    static List<TestJob> DiscoverTests(Assembly assembly)
    {
        var jobs = new List<TestJob>();
        var testClasses = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null);

        foreach (var type in testClasses)
        {
            var methods = type.GetMethods();
            var setup = methods.FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
            var teardown = methods.FirstOrDefault(m => m.GetCustomAttribute<TeardownAttribute>() != null);

            var tests = methods
                .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
                .OrderBy(m => m.GetCustomAttribute<TestMethodAttribute>().Priority);

            foreach (var test in tests)
            {
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
                        Data = data
                    });
                }
            }
        }
        return jobs;
    }

    static async Task<TestStatus> ExecuteSingleTest(TestJob job)
    {
        if (job.Method.GetCustomAttribute<IgnoreAttribute>() != null)
        {
            LogResult(job, "Skipped", ConsoleColor.Yellow);
            return TestStatus.Skipped;
        }

        var timeoutAttr = job.Method.GetCustomAttribute<TimeoutAttribute>();
        var instance = Activator.CreateInstance(job.ClassType);

        try
        {
            job.Setup?.Invoke(instance, null);

            object[] convertedParams = null;
            if (job.Data != null)
            {
                var methodParams = job.Method.GetParameters();
                convertedParams = new object[job.Data.Length];
                for (int i = 0; i < job.Data.Length; i++)
                    convertedParams[i] = Convert.ChangeType(job.Data[i], methodParams[i].ParameterType);
            }

            // Запуск теста с поддержкой тайм-аута
            var testTask = Task.Run(async () => {
                var res = job.Method.Invoke(instance, convertedParams);
                if (res is Task t) await t;
            });

            if (timeoutAttr != null)
            {
                if (await Task.WhenAny(testTask, Task.Delay(timeoutAttr.Milliseconds)) != testTask)
                {
                    LogResult(job, $"Timeout (> {timeoutAttr.Milliseconds}ms)", ConsoleColor.Red);
                    return TestStatus.Failed;
                }
            }

            await testTask;
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
            Console.ForegroundColor = ConsoleColor.White;
            string info = job.Data != null ? $" [{string.Join(", ", job.Data)}]" : "";
            Console.Write($"[{Thread.CurrentThread.ManagedThreadId}] {job.Method.Name}{info} ... ");
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}

// ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ
public enum TestStatus { Passed, Failed, Skipped }

public class TestJob
{
    public Type ClassType { get; set; }
    public MethodInfo Method { get; set; }
    public MethodInfo Setup { get; set; }
    public MethodInfo Teardown { get; set; }
    public object[] Data { get; set; }
}