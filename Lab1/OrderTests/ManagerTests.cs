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

        [Teardown]
        public void AfterEach()
        {
            _manager = null;
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

        [TestMethod]
        public async Task LongRunningTest()
        {
            await Task.Delay(1000);
            Assert.IsTrue(true);
        }

        // Тест с тайм-аутом: должен УСПЕТЬ
        [TestMethod]
        [Timeout(2000)]
        public async Task TimeoutSuccessTest()
        {
            await Task.Delay(500);
            Assert.IsTrue(true);
        }

        // Тест с тайм-аутом: должен ПРЕРВАТЬСЯ
        [TestMethod]
        [Timeout(500)]
        public async Task TimeoutFailTest()
        {
            await Task.Delay(2000); // Спит дольше, чем разрешено
            Assert.IsTrue(true);
        }

        [TestMethod("Тяжелый тест имитации загрузки")]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(9)]
        [DataRow(10)]
        public async Task HeavyWorkTest(int iteration)
        {
            await Task.Delay(500);
            Assert.IsTrue(true);
        }

        [TestMethod("Тест с критическим временем выполнения")]
        [Timeout(800)]
        public async Task TimeoutSuccess()
        {
            await Task.Delay(600);
            Assert.IsTrue(true);
        }

        [TestMethod("Тест, который будет прерван по тайм-ауту")]
        [Timeout(300)]
        public async Task TimeoutFailure()
        {
            await Task.Delay(1000);
            Assert.IsTrue(true);
        }
    }
}