using System;
using System.Collections.Generic;
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
        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 10, idleTimeoutMs: 3000, taskMaxDurationMs: 5000);

        Console.WriteLine("=== ТЕСТИРОВАНИЕ С CUSTOM THREAD POOL ===");

        var assembly = typeof(ManagerTests).Assembly;
        var allTestJobs = DiscoverTests(assembly);

        var extendedJobs = new List<TestJob>();
        while (extendedJobs.Count < 60) extendedJobs.AddRange(allTestJobs);

        // --- МОДЕЛИРОВАНИЕ НАГРУЗКИ ---

        Console.WriteLine("\n>>> Единичные задачи (Проверка MinThreads)");
        for (int i = 0; i < 3; i++)
        {
            var job = extendedJobs[i];
            pool.Execute(() => RunRealTest(job));
            Thread.Sleep(300); // Небольшая пауза
        }

        Console.WriteLine("\n>>> Пиковая нагрузка (Проверка расширения до MaxThreads)");
        for (int i = 3; i < 43; i++)
        {
            var job = extendedJobs[i];
            pool.Execute(() => RunRealTest(job));
        }

        Console.WriteLine("\n>>> Зависающая задача (Проверка замены потока)");
        pool.Execute(() => {
            int tid = Thread.CurrentThread.ManagedThreadId;
            lock (_consoleLock) Console.WriteLine($"  [Thread {tid}] !!! ВНИМАНИЕ: Запущен бесконечный тест...");
            while (true) { Thread.Sleep(1000); }
        });

        Console.WriteLine("\n>>> Ожидание бездействия (Проверка адаптивного сжатия пула)...");
        Thread.Sleep(10000);

        Console.WriteLine("\n>>> Финальная проверка (Работа после сжатия)");
        for (int i = 43; i < extendedJobs.Count; i++)
        {
            var job = extendedJobs[i];
            pool.Execute(() => RunRealTest(job));
        }

        Thread.Sleep(5000);
        Console.WriteLine("\n=================================================");
        Console.WriteLine("ДЕМОНСТРАЦИЯ ЗАВЕРШЕНА.");
        //Console.WriteLine("Проверьте заголовок окна или логи пула выше на предмет создания/удаления потоков.");
    }

    // Обертка для запуска реального теста внутри пула
    static void RunRealTest(TestJob job)
    {
        ExecuteSingleTest(job).GetAwaiter().GetResult();
    }

    // --- (Рефлексия и Исполнение) ---

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
            var tests = methods.Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null);

            foreach (var test in tests)
            {
                var dataRows = test.GetCustomAttributes<DataRowAttribute>().ToList();
                var runs = dataRows.Any() ? dataRows.Select(d => d.Data).ToList() : new List<object[]> { null };
                foreach (var data in runs)
                {
                    jobs.Add(new TestJob { ClassType = type, Method = test, Setup = setup, Teardown = teardown, Data = data });
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

            var result = job.Method.Invoke(instance, convertedParams);
            if (result is Task t) await t;

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
            string info = job.Data != null ? $" [{string.Join(", ", job.Data)}]" : "";
            Console.Write($"  [Thread {tid}] {job.Method.Name}{info} ... ");
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}

// Модели данных для тестов
public enum TestStatus { Passed, Failed, Skipped }
public class TestJob
{
    public Type ClassType { get; set; }
    public MethodInfo Method { get; set; }
    public MethodInfo Setup { get; set; }
    public MethodInfo Teardown { get; set; }
    public object[] Data { get; set; }
}