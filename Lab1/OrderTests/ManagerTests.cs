using MyTestFramework;
using OrderProcessingSystem;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrderTests
{
    [TestClass]
    public class ManagerTests
    {
        private OrderManager _manager;

        [Setup]
        public void BeforeEach() => _manager = new OrderManager();

        [Teardown]
        public void AfterEach() => _manager = null;

        public static IEnumerable<object[]> GetDiscountData()
        {
            yield return new object[] { 0, 1000 };
            yield return new object[] { 10, 900 };
            yield return new object[] { 50, 500 };
        }

        [TestMethod("Тест скидок через итератор")]
        [TestCaseSource(nameof(GetDiscountData))]
        [Category("Critical")]
        public void TestDiscountsIterated(int percent, int expected)
        {
            _manager.AddProduct(new Product { Name = "Item", Price = 1000m });
            Assert.AreEqual((decimal)expected, _manager.CalculateTotal((decimal)percent));
        }

        [TestMethod("Тест расчета итоговой суммы со скидкой")]
        [DataRow(0, 1000)]
        [DataRow(10, 900)]
        [DataRow(100, 0)]
        public void TestDiscounts(int percent, int expected)
        {
            _manager.AddProduct(new Product { Name = "Item", Price = 1000m });
            Assert.AreEqual((decimal)expected, _manager.CalculateTotal((decimal)percent));
        }

        [TestMethod("Тест веса/количества (Граничные значения)")]
        [DataRow(1)]
        [DataRow(50)]
        public void TestProductCountLimits(int count)
        {
            for (int i = 0; i < count; i++)
                _manager.AddProduct(new Product { Name = $"P{i}", Price = 1 });
            Assert.IsTrue(_manager.ProductCount == count);
        }

        [TestMethod("Проверка поиска и свойств товара")]
        public void TestProductDetails()
        {
            var p = new Product { Id = 77, Name = "Gaming Console", Price = 500m };
            _manager.AddProduct(p);
            Assert.Contains("Gaming", p.Name);
            Assert.IsInstanceOf<Product>(p);
        }

        [TestMethod("Проверка защиты от некорректных данных")]
        public void TestSecurityAndValidation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                _manager.AddProduct(new Product { Name = "Bad", Price = -500 });
            });
        }

        [TestMethod("Асинхронное сохранение в БД (имитация)")]
        public async Task TestDatabaseSaveAsync()
        {
            _manager.AddProduct(new Product { Name = "Server", Price = 2000 });
            bool isSaved = await _manager.SaveOrderAsync();
            Assert.IsTrue(isSaved);
        }

        [TestMethod("Специально проваленный тест: неверная сумма")]
        public void TestFailedAssertion()
        {
            _manager.AddProduct(new Product { Name = "FailItem", Price = 100 });
            Assert.AreEqual(500m, _manager.CalculateTotal(0));
        }

        [TestMethod("Тест с ошибкой в логике (Exception)")]
        public void TestUnexpectedError()
        {
            Product p = null;
            var name = p.Name;
        }

        [TestMethod]
        [Timeout(2000)]
        public async Task TimeoutSuccessTest()
        {
            await Task.Delay(500);
            Assert.IsTrue(true);
        }

        [TestMethod]
        [Timeout(500)]
        public async Task TimeoutFailTest()
        {
            await Task.Delay(1000);
            Assert.IsTrue(true);
        }

        [TestMethod("Тяжелый тест имитации загрузки")]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public async Task HeavyWorkTest(int iteration)
        {
            await Task.Delay(500);
            Assert.IsTrue(true);
        }
    }
}