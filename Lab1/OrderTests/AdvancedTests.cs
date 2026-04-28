using MyTestFramework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrderTests
{
    [TestClass]
    [Author("Иванов И.И.")]
    public class AdvancedTests
    {
        public static IEnumerable<object[]> GetAdditionData()
        {
            yield return new object[] { 10, 20, 30 };
            yield return new object[] { 5, 5, 10 };
            yield return new object[] { 0, 0, 0 };
            yield return new object[] { -1, 1, 0 };
            yield return new object[] { 100, 200, 300 };
        }

        public static IEnumerable<object[]> GetStringData()
        {
            yield return new object[] { "hello", "world", "hello world" };
            yield return new object[] { "foo", "bar", "foo bar" };
            yield return new object[] { "abc", "def", "abc def" };
        }

        public static IEnumerable<object[]> GetRangeData()
        {
            for (int i = 1; i <= 10; i++)
                yield return new object[] { i, i * i };
        }

        [TestMethod("Параметризованный тест сложения (yield return)")]
        [TestCaseSource(nameof(GetAdditionData))]
        [Category("Parameterized")]
        [Author("Иванов И.И.")]
        [Priority(1)]
        public void TestAdditionWithYield(int a, int b, int expected)
        {
            Assert.AreEqual(expected, a + b);
        }

        [TestMethod("Параметризованный тест строк (yield return)")]
        [TestCaseSource(nameof(GetStringData))]
        [Category("Parameterized")]
        [Author("Иванов И.И.")]
        [Priority(2)]
        public void TestStringConcatWithYield(string a, string b, string expected)
        {
            Assert.AreEqual(expected, a + " " + b);
        }

        [TestMethod("Параметризованный тест квадратов (yield return)")]
        [TestCaseSource(nameof(GetRangeData))]
        [Category("Parameterized")]
        [Author("Петров П.П.")]
        [Priority(3)]
        public void TestSquaresWithYield(int n, int expected)
        {
            Assert.AreEqual(expected, n * n);
        }

        [TestMethod("Критический системный тест")]
        [Category("Critical")]
        [Author("Иванов И.И.")]
        [Priority(1)]
        public void CriticalSystemTest()
        {
            Thread.Sleep(80);
            Assert.IsTrue(true);
        }

        [TestMethod("Критический тест производительности")]
        [Category("Critical")]
        [Author("Петров П.П.")]
        [Priority(1)]
        public async Task CriticalPerformanceTest()
        {
            await Task.Delay(100);
            Assert.IsTrue(true);
        }

        [TestMethod("Тест дерева выражений (провальный — демонстрация разбора)")]
        [Category("AdvancedAssert")]
        [Author("Иванов И.И.")]
        [Priority(5)]
        public void TestExpressionTreeFailing()
        {
            int actualValue = 42;
            int limit = 50;
            Assert.That(() => actualValue > limit);
        }

        [TestMethod("Тест дерева выражений (успешный)")]
        [Category("AdvancedAssert")]
        [Author("Иванов И.И.")]
        [Priority(5)]
        public void TestExpressionTreePassing()
        {
            int x = 10;
            int y = 5;
            Assert.That(() => x > y);
        }

        [TestMethod("Тест дерева выражений — равенство")]
        [Category("AdvancedAssert")]
        [Author("Петров П.П.")]
        [Priority(5)]
        public void TestExpressionTreeEquality()
        {
            int a = 7;
            int b = 8;
            Assert.That(() => a == b);
        }

        [TestMethod("Простой тест категории Simple")]
        [Category("Simple")]
        [Author("Сидоров С.С.")]
        [Priority(10)]
        public void SimpleTest() => Assert.IsTrue(true);

        [TestMethod("Тест категории Simple — IsNotNull")]
        [Category("Simple")]
        [Author("Сидоров С.С.")]
        [Priority(10)]
        public void SimpleNotNullTest()
        {
            var obj = new object();
            Assert.IsNotNull(obj);
        }

        [TestMethod("Тест фильтрации по автору Сидоров")]
        [Category("AuthorFilter")]
        [Author("Сидоров С.С.")]
        [Priority(4)]
        public void SidorovAuthorTest() => Assert.IsTrue(1 + 1 == 2);

        [TestMethod("Тест фильтрации по автору Петров")]
        [Category("AuthorFilter")]
        [Author("Петров П.П.")]
        [Priority(4)]
        public void PetrovAuthorTest() => Assert.AreNotEqual(0, 42);

        [TestMethod("Тест фильтрации по приоритету 1")]
        [Category("PriorityFilter")]
        [Author("Иванов И.И.")]
        [Priority(1)]
        public void HighPriorityTest() => Assert.IsTrue(true);

        [TestMethod("Тест фильтрации по приоритету 5")]
        [Category("PriorityFilter")]
        [Author("Иванов И.И.")]
        [Priority(5)]
        public void MediumPriorityTest() => Assert.IsTrue(true);

        [TestMethod("Тест фильтрации по приоритету 10")]
        [Category("PriorityFilter")]
        [Author("Сидоров С.С.")]
        [Priority(10)]
        public void LowPriorityTest() => Assert.IsTrue(true);
    }
}