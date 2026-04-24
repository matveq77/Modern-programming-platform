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

        // 1. Сбор всех тестов из сборки
        var assembly = typeof(ProductTests).Assembly;
        var allTestJobs = DiscoverTests(assembly);

        // 2. Инициализация пула потоков с ПОДПИСКОЙ НА СОБЫТИЯ (Требование ЛР4)
        using var pool = new MyThreadPool(minThreads: 2, maxThreads: 8, idleTimeoutMs: 3000);

        pool.ThreadCreated += (s, e) => LogSystem($"[POOL_EVENT] Создан поток {e.ThreadId}", ConsoleColor.Cyan);
        pool.ThreadRemoved += (s, e) => LogSystem($"[POOL_EVENT] Удален поток {e.ThreadId} (простой)", ConsoleColor.DarkYellow);
        pool.ThreadHanged += (s, e) => LogSystem($"[POOL_EVENT] ОБНАРУЖЕНО ЗАВИСАНИЕ в потоке {e.ThreadId}!", ConsoleColor.Magenta);
        // События задач можно раскомментировать для очень подробного лога:
        // pool.TaskStarted += (s, e) => LogSystem($"[TASK] Поток {e.ThreadId} взял задачу", ConsoleColor.DarkGray);

        // --- ДЕМОНСТРАЦИЯ 1: Фильтрация тестов (через делегаты) ---
        Console.WriteLine("\n>>> ШАГ 1: Запуск только критических тестов (Фильтрация по категории 'Critical')");
        ExecuteFiltered(allTestJobs, pool, job => job.Categories.Contains("Critical"));

        // --- ДЕМОНСТРАЦИЯ 2: Параметризованные тесты (yield return) ---
        Console.WriteLine("\n>>> ШАГ 2: Запуск параметризованных тестов (TestCaseSource + yield return)");
        ExecuteFiltered(allTestJobs, pool, job => job.Method.GetCustomAttribute<TestCaseSourceAttribute>() != null);

        // --- ДЕМОНСТРАЦИЯ 3: Expression Tree Assert (Дополнительно) ---
        Console.WriteLine("\n>>> ШАГ 3: Демонстрация детального разбора ошибки (Expression Trees)");
        pool.Execute(() => RunSingleTest(allTestJobs.FirstOrDefault(j => j.Method.Name == "TestExpressionFailure")));

        // Ожидаем завершения основных задач
        Thread.Sleep(2000);

        // --- ДЕМОНСТРАЦИЯ 4: Нагрузка и приоритеты (из прошлых ЛР) ---
        Console.WriteLine("\n>>> ШАГ 4: Запуск оставшейся массы тестов (с учетом приоритетов)");
        var remainingTests = allTestJobs.OrderBy(j => j.Priority).ToList();
        foreach (var job in remainingTests) pool.Execute(() => RunSingleTest(job));

        Console.WriteLine("\n>>> Мониторинг завершения и сжатия пула...");
        while (pool.TasksInQueue > 0 || pool.CurrentThreadCount > 2)
        {
            Console.Title = $"Threads: {pool.CurrentThreadCount} | Tasks: {pool.TasksInQueue}";
            Thread.Sleep(500);
        }

        Console.WriteLine("\n=== ВСЕ ТЕСТЫ И ДЕМОНСТРАЦИИ ЗАВЕРШЕНЫ ===");
        Console.ReadLine();
    }

    // Метод для запуска тестов с применением фильтра-делегата (Требование ЛР4)
    static void ExecuteFiltered(List<TestJob> jobs, MyThreadPool pool, Predicate<TestJob> filter)
    {
        var filtered = jobs.Where(j => filter(j)).ToList();
        foreach (var job in filtered)
        {
            pool.Execute(() => RunSingleTest(job));
        }
        // Даем немного времени для вывода логов перед следующим шагом
        Thread.Sleep(1000);
    }

    static void RunSingleTest(TestJob job)
    {
        if (job == null) return;
        if (job.Method.GetCustomAttribute<IgnoreAttribute>() != null) return;

        var instance = Activator.CreateInstance(job.ClassType);
        try
        {
            job.Setup?.Invoke(instance, null);

            // Вызов метода (с параметрами или без)
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

                // 1. Проверка на TestCaseSource (yield return) - ЛР4
                var sourceAttr = method.GetCustomAttribute<TestCaseSourceAttribute>();
                if (sourceAttr != null)
                {
                    var sourceMethod = type.GetMethod(sourceAttr.MethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (sourceMethod != null)
                    {
                        var dataSet = (IEnumerable<object[]>)sourceMethod.Invoke(null, null);
                        foreach (var data in dataSet)
                            jobs.Add(CreateJob(type, method, setup, teardown, data, attr, categories));
                        continue;
                    }
                }

                // 2. Проверка на DataRow (старая фишка)
                var dataRows = method.GetCustomAttributes<DataRowAttribute>().ToList();
                if (dataRows.Any())
                {
                    foreach (var row in dataRows)
                        jobs.Add(CreateJob(type, method, setup, teardown, row.Data, attr, categories));
                }
                else
                {
                    // Обычный тест
                    jobs.Add(CreateJob(type, method, setup, teardown, null, attr, categories));
                }
            }
        }
        return jobs;
    }

    static TestJob CreateJob(Type t, MethodInfo m, MethodInfo s, MethodInfo td, object[] d, TestMethodAttribute a, List<string> cats)
    {
        return new TestJob
        {
            ClassType = t,
            Method = m,
            Setup = s,
            Teardown = td,
            Data = d,
            Priority = a.Priority,
            Description = a.Description,
            Categories = cats
        };
    }

    static void LogResult(TestJob job, string status, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            Console.ForegroundColor = ConsoleColor.White;
            string dataInfo = job.Data != null ? $" [{string.Join(", ", job.Data)}]" : "";
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

// Обновленная модель работы для поддержки категорий
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