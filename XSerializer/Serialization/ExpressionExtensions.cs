﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Undefined.Serialization
{
    internal static class ExpressionExtensions
    {
        public static Expression AssignFrom(this Expression lhs, Expression rhs)
        {
            return Expression.Assign(lhs, rhs);
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

        public static Expression CallMember(this Expression lhs, MethodInfo method)
        {
            return Expression.Call(lhs, method);
        }

        public static Expression CallMember(this Expression lhs, string methodName, params Expression[] args)
        {
            return Expression.Call(lhs, methodName, null, args);
        }

        public static Expression CallMember(this Expression lhs, string methodName, Type T, params Expression[] args)
        {
            return Expression.Call(lhs, methodName, new[] {T}, args);
        }
    }
}