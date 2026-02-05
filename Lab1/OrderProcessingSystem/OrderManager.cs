using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderProcessingSystem
{

    public class OrderManager
    {
        private List<Product> _products = new List<Product>();

        public void AddProduct(Product product)
        {
            if (string.IsNullOrEmpty(product.Name)) throw new ArgumentException("Name cannot be empty");
            if (product.Price < 0) throw new ArgumentOutOfRangeException("Price cannot be negative");
            _products.Add(product);
        }

        public decimal CalculateTotal(decimal discountPercentage)
        {
            var total = _products.Sum(p => p.Price);
            return total - (total * (discountPercentage / 100));
        }

        public async Task<bool> SaveOrderAsync()
        {
            await Task.Delay(50);
            return _products.Count > 0;
        }

        public Product GetProductByName(string name) => _products.FirstOrDefault(p => p.Name == name);
        public int ProductCount => _products.Count;
    }
}