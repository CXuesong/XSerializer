using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Undefined.Serialization
{
    internal static class ExpressionExtensions
    {
        public static Expression AssignFrom(this Expression lhs, Expression rhs)
        {
            return Expression.Assign(lhs, rhs);
        }

        public static Expression EqualsTo(this Expression lhs, Expression rhs)
        {
            return Expression.Equal(lhs, rhs);
        }

        public static Expression NotEqualsTo(this Expression lhs, Expression rhs)
        {
            return Expression.NotEqual(lhs, rhs);
        }

        public static Expression Then(this Expression condition, Expression ifTrue)
        {
            return Expression.IfThen(condition, ifTrue);
        }

        public static Expression ThenElse(this Expression condition, Expression ifTrue, Expression ifFalse)
        {
            return Expression.IfThenElse(condition, ifTrue, ifFalse);
        }

        public static Expression Invert(this Expression lhs)
        {
            return Expression.Not(lhs);
        }

        public static Expression Cast<T>(this Expression lhs)
        {
            return Expression.Convert(lhs, typeof (T));
        }

        public static Expression Cast(this Expression lhs, Type destType)
        {
            return Expression.Convert(lhs, destType);
        }

        public static Expression Member(this Expression lhs, MemberInfo member)
        {
            return Expression.MakeMemberAccess(lhs, member);
        }

        public static Expression Member(this Expression lhs, string memberName)
        {
            return Expression.PropertyOrField(lhs, memberName);
        }


        public static Expression CallMember(this Expression lhs, MethodInfo method)
        {
            return Expression.Call(lhs, method);
        }

        public static Expression CallMember(this Expression lhs, MethodInfo method, params Expression[] args)
        {
            return Expression.Call(lhs, method, args);
        }

        public static Expression CallMember(this Expression lhs, string methodName, params Expression[] args)
        {
            //Debug.Print("{0}.{1}({2})", lhs, methodName, string.Join(", ", args.Select(a => a.Type)));
            return Expression.Call(lhs, methodName, null, args);
        }

        public static Expression CallMember(this Expression lhs, string methodName, Type T, params Expression[] args)
        {
            return Expression.Call(lhs, methodName, new[] {T}, args);
        }
    }
}
