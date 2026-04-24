using MyTestFramework;
using System.Collections.Generic;
using System.Threading;

namespace OrderTests
{
    [TestClass]
    public class AdvancvedTests
    {
        public static IEnumerable<object[]> GetTestData()
        {
            yield return new object[] { 10, 20, 30 };
            yield return new object[] { 5, 5, 10 };
            yield return new object[] { 1, 2, 99 };
        }

        [TestMethod("Тест с итератором")]
        [TestCaseSource(nameof(GetTestData))]
        [Category("Parameterized")]
        public void TestWithYield(int a, int b, int expected)
        {
            Assert.AreEqual(expected, a + b);
        }

        [TestMethod("Критический тест для фильтрации")]
        [Category("Critical")]
        public void CriticalSystemTest()
        {
            Thread.Sleep(100);
            Assert.IsTrue(true);
        }

        [TestMethod("Тест дерева выражений")]
        [Category("AdvancedAssert")]
        public void TestExpressionTree()
        {
            int actualValue = 42;
            int limit = 50;
            Assert.That(() => actualValue > limit);
        }

        [TestMethod("Простой тест")]
        [Category("Simple")]
        public void SimpleTest() => Assert.IsTrue(true);
    }
}