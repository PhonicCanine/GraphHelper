using System;
using Microsoft.Graph;
using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Collections;
using System.Reflection;

namespace GraphHelper
{
    public static class GraphHelper { 

        public class UnsupportedOperationException : Exception
        {
            ExpressionType t { get; set; }
            public override string Message => t.ToString() + " is not a supported Graph filter operation.";
            public UnsupportedOperationException(ExpressionType t)
            {
                this.t = t;
            }
        }

        public class MethodNotSupportedException : Exception
        {
            string details { get; set; }
            public override string Message => $"{details}\n{base.Message}";
            public MethodNotSupportedException(string details)
            {
                this.details = details;
            }
        }
        
        private static Expression reducePredicate(Expression ex)
        {
            if (ex.CanReduce)
            {
                return reducePredicate(ex.Reduce());
            }
            return ex;
        }

        private static string compileUnaryExpression<T>(UnaryExpression ex)
        {
            switch (ex.NodeType)
            {
                case ExpressionType.Not:
                    return "not (" + compileExpression<T>(ex.Operand) + ")";
                case ExpressionType.Convert:
                    return compileExpression<T>(ex.Operand);
                default:
                    throw new UnsupportedOperationException(ex.NodeType);
            }
        }

        private static string compileBinaryExpression<T>(BinaryExpression ex)
        {
            switch (ex.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return "(" + compileExpression<T>(ex.Left) + " and " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return "(" + compileExpression<T>(ex.Left) + " or " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.GreaterThan:
                    return "(" + compileExpression<T>(ex.Left) + " gt " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.LessThan:
                    return "(" + compileExpression<T>(ex.Left) + " lt " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.LessThanOrEqual:
                    return "(" + compileExpression<T>(ex.Left) + " le " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.GreaterThanOrEqual:
                    return "(" + compileExpression<T>(ex.Left) + " ge " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.Equal:
                    return "(" + compileExpression<T>(ex.Left) + " eq " + compileExpression<T>(ex.Right) + ")";
                case ExpressionType.NotEqual:
                    return "(" + compileExpression<T>(ex.Left) + " ne " + compileExpression<T>(ex.Right) + ")";
            }
            throw new UnsupportedOperationException(ex.NodeType);
        }

        private static bool expressionContainsGraphType<T>(Expression e)
        {
            switch (e)
            {
                case BinaryExpression ex:
                    return expressionContainsGraphType<T>(ex.Left) || expressionContainsGraphType<T>(ex.Right) || e.Type.Equals(typeof(T));
                case MethodCallExpression ex:
                    return expressionContainsGraphType<T>(ex.Object) || ex.Arguments.Aggregate(false, (f, expression) => f |= expressionContainsGraphType<T>(expression));
                case MemberExpression ex:
                    return expressionContainsGraphType<T>(ex.Expression);
            }
            return e.Type.Equals(typeof(T));
        }

        private static string compileCallExpression<T>(MethodCallExpression ex)
        {
            switch (ex.Method.Name)
            {
                case "StartsWith":
                    return $"startswith({compileExpression<T>(ex.Object)},{compileExpression<T>(ex.Arguments.First())})";
                default:
                    if (expressionContainsGraphType<T>(ex))
                    {
                        throw new MethodNotSupportedException("StartsWith is the only method that can be used with Graph types.");
                    }
                    else
                    {
                        var result = Expression.Lambda(ex).Compile().DynamicInvoke();
                        return compileExpression<T>(Expression.Constant(result));
                    }
                    
            }
        }

        private static string compileConstantExpression(ConstantExpression ex)
        {
            switch (ex.Value)
            {
                case string s:
                    return "'" + s.Replace("'", "''").Replace("%27", "%27%27") + "'";
                case bool b:
                    return b ? "true" : "false";
                default:
                    return ((dynamic)ex.Value).ToString();
            }
        }

        public class UnsupportedMemberException : Exception
        {
            string fieldName { get; set; }

            public override string Message => $"{fieldName} is not supported\n{base.Message}";
            public UnsupportedMemberException(string fieldName)
            {
                this.fieldName = fieldName;
            }
        }

        private static string compileParameterExpression<T>(MemberExpression px)
        {
            string mem = px.Member.Name;

            Type sType = typeof(T);
            if (sType.GetProperty(mem) == null && sType.GetField(mem) == null)
                throw new UnsupportedMemberException(mem);

            string field = mem;
            field = field.ToString().ToLower()[0] + field.Substring(1);
            return field;
        }

        private static string compileExpression<T>(Expression ex)
        {
            switch (ex)
            {
                case BinaryExpression e:
                    return compileBinaryExpression<T>(e);
                case UnaryExpression e:
                    return compileUnaryExpression<T>(e);
                case LambdaExpression e:
                    return compileExpression<T>(e.Body);
                case MethodCallExpression e:
                    return compileCallExpression<T>(e);
                case ConstantExpression e:
                    return compileConstantExpression(e);
                case MemberExpression e:
                    return compileParameterExpression<T>(e);
                default:
                    throw new UnsupportedOperationException(ex.NodeType);
            }
        }

        public static string compilePredicate<T>(Expression<Predicate<T>> expression)
        {
            var exp = reducePredicate(expression);
            string s = compileExpression<T>(exp);
            return s;
        }

        public static IGraphServiceUsersCollectionRequest Where(this IGraphServiceUsersCollectionRequest a, Expression<Predicate<User>> predicate)
        {

            string p = compilePredicate(predicate);

            return a.Filter(p);
        }

        public static string CompileRequest(Expression<Predicate<User>> predicate)
        {

            string p = compilePredicate(predicate);

            return p;
        }
    }
}
