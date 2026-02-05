using System;

namespace MyTestFramework
{
    public class TestAssertionException : Exception
    {
        public TestAssertionException(string message) : base(message) { }
    }
}