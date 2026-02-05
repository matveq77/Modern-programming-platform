using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyTestFramework;
using OrderTests;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Tests running");

        var assembly = typeof(ManagerTests).Assembly;
        var testClasses = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null);

        int passed = 0, failed = 0, skipped = 0;

        foreach (var type in testClasses)
        {
            Console.WriteLine($"\nClass execution: {type.Name}");
            var methods = type.GetMethods();

            var setup = methods.FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
            var teardown = methods.FirstOrDefault(m => m.GetCustomAttribute<TeardownAttribute>() != null);

            // Сортировка по Priority
            var tests = methods
                .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
                .OrderBy(m => m.GetCustomAttribute<TestMethodAttribute>().Priority);

            foreach (var test in tests)
            {
                // Проверка на наличие отдельного атрибута [Ignore]
                if (test.GetCustomAttribute<IgnoreAttribute>() != null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($" -> {test.Name} ... Skipped");
                    Console.ResetColor();
                    skipped++;
                    continue;
                }

                var instance = Activator.CreateInstance(type);
                var dataRows = test.GetCustomAttributes<DataRowAttribute>().ToList();
                var testRuns = dataRows.Any() ? dataRows.Select(d => d.Data).ToList() : new List<object[]> { null };

                foreach (var paramsData in testRuns)
                {
                    string info = paramsData != null ? $" [{string.Join(", ", paramsData)}]" : "";
                    Console.Write($" -> {test.Name}{info} ... ");

                    try
                    {
                        setup?.Invoke(instance, null);

                        object[] convertedParams = null;
                        if (paramsData != null)
                        {
                            var methodParams = test.GetParameters();
                            convertedParams = new object[paramsData.Length];
                            for (int i = 0; i < paramsData.Length; i++)
                                convertedParams[i] = Convert.ChangeType(paramsData[i], methodParams[i].ParameterType);
                        }

                        var result = test.Invoke(instance, convertedParams);
                        if (result is Task task) await task;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("OK");
                        passed++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        var inner = ex is TargetInvocationException ? ex.InnerException : ex;
                        Console.WriteLine(inner is TestAssertionException ? $"Failed: {inner.Message}" : $"Error: {inner.Message}");
                        failed++;
                    }
                    finally
                    {
                        teardown?.Invoke(instance, null);
                        Console.ResetColor();
                    }
                }
            }
        }
        Console.WriteLine("\n" + new string('-', 30));
        Console.WriteLine($"Total: Passed: {passed}, Failed: {failed}, Skipped: {skipped}");
    }
}