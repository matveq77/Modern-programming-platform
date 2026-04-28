using MyTestFramework;
using OrderProcessingSystem;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrderTests
{
    [TestClass]
    [Author("Петров П.П.")]
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
            yield return new object[] { 25, 750 };
            yield return new object[] { 75, 250 };
        }

        [TestMethod("Тест скидок через итератор (yield return)")]
        [TestCaseSource(nameof(GetDiscountData))]
        [Category("Critical")]
        [Category("Parameterized")]
        [Author("Петров П.П.")]
        [Priority(1)]
        public void TestDiscountsIterated(int percent, int expected)
        {
            _manager.AddProduct(new Product { Name = "Item", Price = 1000m });
            Assert.AreEqual((decimal)expected, _manager.CalculateTotal((decimal)percent));
        }

        [TestMethod("Тест расчета итоговой суммы со скидкой")]
        [DataRow(0, 1000)]
        [DataRow(10, 900)]
        [DataRow(100, 0)]
        [Category("Critical")]
        [Author("Петров П.П.")]
        [Priority(1)]
        public void TestDiscounts(int percent, int expected)
        {
            _manager.AddProduct(new Product { Name = "Item", Price = 1000m });
            Assert.AreEqual((decimal)expected, _manager.CalculateTotal((decimal)percent));
        }

        [TestMethod("Тест граничных значений количества товаров")]
        [DataRow(1)]
        [DataRow(10)]
        [DataRow(50)]
        [Category("Boundary")]
        [Author("Иванов И.И.")]
        [Priority(2)]
        public void TestProductCountLimits(int count)
        {
            for (int i = 0; i < count; i++)
                _manager.AddProduct(new Product { Name = $"P{i}", Price = 1 });
            Assert.IsTrue(_manager.ProductCount == count);
        }

        [TestMethod("Проверка поиска и свойств товара")]
        [Category("Smoke")]
        [Author("Петров П.П.")]
        [Priority(2)]
        public void TestProductDetails()
        {
            var p = new Product { Id = 77, Name = "Gaming Console", Price = 500m };
            _manager.AddProduct(p);
            Assert.Contains("Gaming", p.Name);
            Assert.IsInstanceOf<Product>(p);
        }

        [TestMethod("Проверка защиты от некорректных данных")]
        [Category("Critical")]
        [Author("Иванов И.И.")]
        [Priority(1)]
        public void TestSecurityAndValidation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                _manager.AddProduct(new Product { Name = "Bad", Price = -500 });
            });
        }

        [TestMethod("Асинхронное сохранение заказа")]
        [Category("Async")]
        [Author("Петров П.П.")]
        [Priority(2)]
        public async Task TestDatabaseSaveAsync()
        {
            _manager.AddProduct(new Product { Name = "Server", Price = 2000 });
            bool isSaved = await _manager.SaveOrderAsync();
            Assert.IsTrue(isSaved);
        }

        [TestMethod("Специально проваленный тест: неверная сумма")]
        [Category("FailDemo")]
        [Author("Сидоров С.С.")]
        [Priority(10)]
        public void TestFailedAssertion()
        {
            _manager.AddProduct(new Product { Name = "FailItem", Price = 100 });
            Assert.AreEqual(500m, _manager.CalculateTotal(0));
        }

        [TestMethod("Тест с непредвиденным исключением")]
        [Category("FailDemo")]
        [Author("Сидоров С.С.")]
        [Priority(10)]
        public void TestUnexpectedError()
        {
            Product p = null;
            var name = p.Name;
        }

        [TestMethod("Timeout: успешное завершение в срок")]
        [Timeout(2000)]
        [Category("Timeout")]
        [Author("Иванов И.И.")]
        [Priority(3)]
        public async Task TimeoutSuccessTest()
        {
            await Task.Delay(300);
            Assert.IsTrue(true);
        }

        [TestMethod("Timeout: превышение времени ожидания")]
        [Timeout(300)]
        [Category("Timeout")]
        [Author("Иванов И.И.")]
        [Priority(3)]
        public async Task TimeoutFailTest()
        {
            await Task.Delay(1000);
            Assert.IsTrue(true);
        }

        [TestMethod("Нагрузочный тест (имитация работы)")]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [Category("Load")]
        [Author("Петров П.П.")]
        [Priority(4)]
        public async Task HeavyWorkTest(int iteration)
        {
            await Task.Delay(200 + iteration * 50);
            _manager.AddProduct(new Product { Name = $"Product{iteration}", Price = iteration * 10m });
            Assert.IsTrue(_manager.ProductCount > 0);
        }
    }
}