using MyTestFramework;
using OrderProcessingSystem;
using System;
using System.Threading.Tasks;

namespace OrderTests
{
    [TestClass]
    public class ManagerTests
    {
        private OrderManager _manager;

        [Setup]
        public void BeforeEach()
        {
            _manager = new OrderManager();
        }

        // ПАРАМЕТРИЗОВАННЫЕ ТЕСТЫ (DataRow)
        [TestMethod("Тест расчета итоговой суммы со скидкой")]
        [DataRow(0, 1000)]    // Без скидки
        [DataRow(10, 900)]   // 10%
        [DataRow(100, 0)]    // 100% скидка
        public void TestDiscounts(int percent, int expected)
        {
            _manager.AddProduct(new Product { Name = "Item", Price = 1000m });
            Assert.AreEqual((decimal)expected, _manager.CalculateTotal((decimal)percent));
        }

        [TestMethod("Тест веса/количества (Граничные значения)")]
        [DataRow(1)]
        [DataRow(50)]
        [DataRow(99)]
        public void TestProductCountLimits(int count)
        {
            for (int i = 0; i < count; i++)
                _manager.AddProduct(new Product { Name = $"P{i}", Price = 1 });

            Assert.IsTrue(_manager.ProductCount == count);
            Assert.IsNotNull(_manager.GetProductByName("P0"));
        }

        // ЛОГИКА И СТРОКИ
        [TestMethod("Проверка поиска и свойств товара")]
        public void TestProductDetails()
        {
            var p = new Product { Id = 77, Name = "Gaming Console", Price = 500m };
            _manager.AddProduct(p);

            Assert.Contains("Gaming", p.Name);              
            Assert.IsInstanceOf<Product>(p);                
            Assert.AreNotEqual("Office", p.Name);           
            Assert.IsGreaterThan(p.Price, 100m);            
        }

        // ОШИБКИ И ИСКЛЮЧЕНИЯ
        [TestMethod("Проверка защиты от некорректных данных")]
        public void TestSecurityAndValidation()
        {
            // Тест на Null
            Product nullProduct = null;
            Assert.IsNull(nullProduct);

            // Тест на исключение (Отрицательная цена)
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                _manager.AddProduct(new Product { Name = "Bad", Price = -500 });
            });

            // Тест на исключение (Пустое имя)
            Assert.Throws<ArgumentException>(() => {
                _manager.AddProduct(new Product { Name = "", Price = 100 });
            });
        }

        // АСИНХРОННОСТЬ И КОНТЕКСТ
        [TestMethod("Асинхронное сохранение в БД (имитация)")]
        public async Task TestDatabaseSaveAsync()
        {
            _manager.AddProduct(new Product { Name = "Server", Price = 2000 });
            bool isSaved = await _manager.SaveOrderAsync();

            Assert.IsTrue(isSaved);
            Assert.IsFalse(_manager.ProductCount == 0);
        }

        [TestMethod("Проверка очистки корзины")]
        public void TestClearManager()
        {
            _manager.AddProduct(new Product { Name = "Temp", Price = 10 });
            _manager = new OrderManager(); // Эмуляция очистки
            Assert.AreEqual(0, _manager.ProductCount);
        }

        [Teardown]
        public void AfterEach()
        {
            _manager = null;
        }

        // ТЕСТЫ, КОТОРЫЕ НЕ ДОЛЖНЫ ПРОЙТИ

        [TestMethod("Специально проваленный тест: неверная сумма")]
        public void TestFailedAssertion()
        {
            _manager.AddProduct(new Product { Name = "FailItem", Price = 100 });
            //ПРОВАЛ.
            Assert.AreEqual(500m, _manager.CalculateTotal(0));
        }

        [TestMethod("Тест с ошибкой в логике (Exception)")]
        public void TestUnexpectedError()
        {
            //Обращение к null
            Product p = null;
            var name = p.Name; //NullReferenceException
        }

        [TestMethod("Параметризованный тест с ошибками")]
        [DataRow(10, 900)] // Пройдет
        [DataRow(50, 100)] // ПРОВАЛ
        public void TestDataRowFailure(int discount, int expected)
        {
            _manager.AddProduct(new Product { Name = "Promo", Price = 1000m });
            Assert.AreEqual((decimal)expected, _manager.CalculateTotal((decimal)discount));
        }
    }
}