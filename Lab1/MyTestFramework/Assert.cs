using System;
using System.Linq.Expressions;

namespace MyTestFramework
{
    public static class Assert
    {
        public static void AreEqual(object expected, object actual)
        {
            if (!Equals(expected, actual)) throw new TestAssertionException($"Expected <{expected}>, but got <{actual}>.");
        }

        public static void AreNotEqual(object val1, object val2)
        {
            if (Equals(val1, val2)) throw new TestAssertionException($"Values <{val1}> and <{val2}> are unexpectedly equal.");
        }

        public static void IsTrue(bool condition) { if (!condition) throw new TestAssertionException("Condition is False, expected True."); }

        public static void IsFalse(bool condition) { if (condition) throw new TestAssertionException("Condition is True, expected False."); }

        public static void IsNotNull(object obj) { if (obj == null) throw new TestAssertionException("Object is null."); }

        public static void IsNull(object obj) { if (obj != null) throw new TestAssertionException("Object is not null."); }

        public static void Contains(string substring, string fullString)
        {
            if (fullString == null || !fullString.Contains(substring)) throw new TestAssertionException($"String does not contain '{substring}'.");
        }

        public static void IsGreaterThan(decimal val, decimal threshold)
        {
            if (val <= threshold) throw new TestAssertionException($"{val} is not greater than {threshold}.");
        }

        public static void IsInstanceOf<T>(object obj)
        {
            if (!(obj is T)) throw new TestAssertionException($"Object is not of type {typeof(T).Name}.");
        }

        public static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception ex) { throw new TestAssertionException($"Expected exception {typeof(T).Name}, but {ex.GetType().Name} was thrown."); }
            throw new TestAssertionException($"No exception was thrown. Expected: {typeof(T).Name}.");
        }

        public static void That(Expression<Func<bool>> expression)
        {
            var func = expression.Compile();
            if (!func())
            {
                string details = AnalyzeExpression(expression.Body);
                throw new TestAssertionException($"Assertion failed!\nExpression: {expression}\nDetails: {details}");
            }
        }

        private static string AnalyzeExpression(Expression body)
        {
            try
            {
                if (body is BinaryExpression binary)
                {
                    object leftValue = GetValue(binary.Left);
                    object rightValue = GetValue(binary.Right);
                    string op = GetOperatorSymbol(binary.NodeType);

                    return $"Left side: [{leftValue}], Operator: [{op}], Right side: [{rightValue}]";
                }

                if (body is UnaryExpression unary)
                {
                    object val = GetValue(unary.Operand);
                    return $"Unary operator [{unary.NodeType}] on value [{val}]";
                }

                return $"Raw structure: {body}";
            }
            catch (Exception ex)
            {
                return $"[Could not parse expression details: {ex.Message}]";
            }
        }

        private static object GetValue(Expression member)
        {
            var objectMember = Expression.Convert(member, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            return getterLambda.Compile()();
        }

        private static string GetOperatorSymbol(ExpressionType type)
        {
            return type switch
            {
                ExpressionType.Equal => "==",
                ExpressionType.NotEqual => "!=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.AndAlso => "&&",
                ExpressionType.OrElse => "||",
                _ => type.ToString()
            };
        }
    }
}