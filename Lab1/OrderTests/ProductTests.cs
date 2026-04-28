using MyTestFramework;
using OrderProcessingSystem;
using System.Threading.Tasks;

namespace OrderTests
{
    [TestClass]
    [Author("Сидоров С.С.")]
    public class ProductTests
    {
        [TestMethod("Проверка создания продукта")]
        [Timeout(2000)]
        [Category("Smoke")]
        [Author("Сидоров С.С.")]
        [Priority(2)]
        public async Task TestProductCreation()
        {
            await Task.Delay(200);
            var p = new Product { Name = "Phone", Price = 500 };
            Assert.IsNotNull(p);
            Assert.AreEqual("Phone", p.Name);
            Assert.IsTrue(p.Price > 0);
        }

        [TestMethod("Параллельный тест свойств")]
        [DataRow("Alpha")]
        [DataRow("Beta")]
        [DataRow("Gamma")]
        [DataRow("Delta")]
        [DataRow("Epsilon")]
        [Category("Smoke")]
        [Author("Сидоров С.С.")]
        [Priority(3)]
        public async Task TestParallelProperties(string label)
        {
            await Task.Delay(300);
            Assert.IsTrue(label.Length > 0);
            Assert.IsNotNull(label);
        }

        [TestMethod("Проверка типов данных продукта")]
        [Category("Smoke")]
        [Author("Иванов И.И.")]
        [Priority(2)]
        public void TestProductTypes()
        {
            var p = new Product { Id = 1, Name = "Laptop", Price = 1500m };
            Assert.IsInstanceOf<Product>(p);
            Assert.IsNotNull(p.Name);
            Assert.IsTrue(p.Id > 0);
            Assert.IsGreaterThan(p.Price, 0);
        }

        [TestMethod("Проверка граничных значений цены")]
        [DataRow(0.01)]
        [DataRow(9999.99)]
        [DataRow(1.0)]
        [Category("Boundary")]
        [Author("Сидоров С.С.")]
        [Priority(3)]
        public void TestPriceBoundary(double price)
        {
            var p = new Product { Name = "Item", Price = (decimal)price };
            Assert.IsTrue(p.Price > 0);
        }

        [TestMethod("Тест нулевого Id")]
        [Category("Boundary")]
        [Author("Сидоров С.С.")]
        [Priority(3)]
        public void TestZeroId()
        {
            var p = new Product { Id = 0, Name = "NoId", Price = 1m };
            Assert.IsNotNull(p);
            Assert.AreEqual(0, p.Id);
        }
    }
}