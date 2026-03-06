using MyTestFramework;
using OrderProcessingSystem;
using System.Threading.Tasks;

namespace OrderTests
{
    [TestClass]
    public class ProductTests
    {
        [TestMethod("Проверка создания продукта")]
        [Timeout(1000)]
        public async Task TestProductCreation()
        {
            await Task.Delay(500); // Имитация работы
            var p = new Product { Name = "Phone", Price = 500 };
            Assert.IsNotNull(p);
        }

        [TestMethod("Параллельный тест свойств")]
        [DataRow("A")]
        [DataRow("B")]
        [DataRow("C")]
        public async Task TestParallelProperties(string label)
        {
            await Task.Delay(700); // Имитация долгой проверки
            Assert.IsTrue(label.Length == 1);
        }
    }
}