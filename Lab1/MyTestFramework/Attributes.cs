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

    [AttributeUsage(AttributeTargets.Method)]
    public class TimeoutAttribute : Attribute
    {
        public int Milliseconds { get; }
        public TimeoutAttribute(int milliseconds) => Milliseconds = milliseconds;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestCaseSourceAttribute : Attribute
    {
        public string MethodName { get; }
        public TestCaseSourceAttribute(string methodName) => MethodName = methodName;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class CategoryAttribute : Attribute
    {
        public string Name { get; }
        public CategoryAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class AuthorAttribute : Attribute
    {
        public string Name { get; }
        public AuthorAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class PriorityAttribute : Attribute
    {
        public int Level { get; }
        public PriorityAttribute(int level) => Level = level;
    }
}