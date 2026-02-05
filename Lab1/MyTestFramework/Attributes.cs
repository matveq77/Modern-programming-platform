using System;

namespace MyTestFramework
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TestClassAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestMethodAttribute : Attribute
    {
        public string Description { get; set; }
        public int Priority { get; set; } = 0;

        public TestMethodAttribute(string description = "") => Description = description;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class IgnoreAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DataRowAttribute : Attribute
    {
        public object[] Data { get; }
        public DataRowAttribute(params object[] data) => Data = data;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SetupAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TeardownAttribute : Attribute { }
}