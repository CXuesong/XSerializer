using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;
using Undefined.Serialization;

namespace Undefined.Serialization
{
    internal static class SerializationHelper
    {

        /// <summary>
        /// 将复数转换为适合于 XML 存取的字符串。
        /// </summary>
        public static string ToXString(Complex value)
        {
            return value.Real.ToString(CultureInfo.InvariantCulture) + " " +
                   value.Imaginary.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 将枚举项转化为与之对应的 XML 中的常数字符串。
        /// </summary>
        /// <param name="value">要转换的枚举值。</param>
        /// <exception cref="ArgumentOutOfRangeException">指定的枚举值无法在其枚举类型中找到匹配项。</exception>
        public static string ToXString(Enum value)
        {
            if (value == null) throw new ArgumentNullException("value");
            {
                Enum[] factors = null;
                var vType = value.GetType();
                if (vType.GetCustomAttribute<FlagsAttribute>() != null)
                {
                    //Flags
                    //Factors = FactorEnum(value)
                    throw new NotSupportedException();
                }
                factors = new[] { value };
                return String.Join(" ", factors.Select(eachFactor =>
                {
                    var eachName = Enum.GetName(vType, eachFactor);
                    return eachName == null
                        ? Convert.ToString(Convert.ToUInt64(eachFactor))
                        : XEnumAttribute.GetXEnumName(vType.GetField(eachName));
                }));
            }
        }

        public static string ToXString(object value)
        {
            var ixss = value as IXStringSerializable;
            if (ixss != null) return ixss.Serialize();
            if (value is Complex) return ToXString((Complex)value);
            if (value is Enum) return ToXString((Enum)value);
            return null;
        }

        /// <summary>
        /// 获取与指定类型对应的 XML 元素名称。
        /// </summary>
        /// <returns>如果类型未使用 XTypeAttribute 特性，则会为类型生成一个元素名称。</returns>
        public static XName GetName(Type t)
        {
            Debug.Assert(t != null);
            var attr = t.GetCustomAttribute<XTypeAttribute>();
            if (attr != null) return attr.GetName(attr.LocalName == null ? GetNameDirect(t) : null);
            return GetNameDirect(t);
        }

        private static string GetNameDirect(Type t)
        {
            var rawName = t.Name;
            if (t.IsArray)
                return "ArrayOf" + GetNameDirect(GetCollectionItemType(t));
            if (t.IsGenericType)
            {
                // List`1[String] --> ListOfString
                var pos = rawName.IndexOf('`');
                if (pos > 0) rawName = rawName.Substring(0, pos);
                rawName += "Of" + string.Join(null, from a in t.GenericTypeArguments select GetNameDirect(a));
            }
            return rawName;
        }

        public static XName GetName(MemberInfo m, XNamedAttributeBase attr = null)
        {
            Debug.Assert(m != null);
            if (attr == null) attr = m.GetCustomAttribute<XElementAttribute>();
            if (attr == null) m.GetCustomAttribute<XAttributeAttribute>();
            if (attr != null) return XName.Get(attr.LocalName ?? m.Name, attr.Namespace ?? "");
            return m.Name;
        }
        public static Type GetMemberValueType(MemberInfo member)
        {
            var f = member as FieldInfo;
            if (f != null) return f.FieldType;
            var p = member as PropertyInfo;
            if (p != null) return p.PropertyType;
            throw new NotSupportedException();
        }

        public static object GetMemberValue(MemberInfo member, object obj)
        {
            var f = member as FieldInfo;
            if (f != null) return f.GetValue(obj);
            var p = member as PropertyInfo;
            if (p != null) return p.GetValue(obj);
            throw new NotSupportedException();
        }

        /// <summary>
        /// 获取指定集合类型在声明时所使用的元素类型。
        /// </summary>
        public static Type GetCollectionItemType(Type collectionType)
        {
            Debug.Assert(collectionType != null && typeof (IEnumerable).IsAssignableFrom(collectionType));
            var ienum = collectionType.GetInterfaces().FirstOrDefault(t => t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof (IEnumerable<>));
            if (ienum == null) return typeof (object);
            return ienum.GenericTypeArguments[0];
        }

        /// <summary>
        /// 判断指定的类型与期望类型是否相同，或者可进行扩大转换。
        /// </summary>
        public static void AssertKindOf(Type desired, Type actual)
        {
            Debug.Assert(desired != null && actual != null);
            if (!desired.IsAssignableFrom(actual))
                throw new InvalidCastException(String.Format(Prompts.InvalidObjectType, actual, desired));
        }

        /// <summary>
        /// 判断指定的类型是否为可直接序列化的简单类型。
        /// </summary>
        public static bool IsSimpleType(Type t)
        {
            //对于 Nullable 的处理。
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsSimpleType(t.GenericTypeArguments[0]);
            return t == typeof (Object) || t == typeof (String) || t == typeof (Byte) || t == typeof (SByte)
                   || t == typeof (Int16) || t == typeof (UInt16) || t == typeof (Int32) || t == typeof (UInt32)
                   || t == typeof (Int64) || t == typeof (UInt64) || t == typeof (Single) || t == typeof (Double)
                   || t == typeof (IntPtr) || t == typeof (UIntPtr)
                   || t == typeof (DateTime) || t == typeof (TimeSpan) || t == typeof (DateTimeOffset)
                   || t == typeof (Guid);
        }
    }

    /// <summary>
    /// 指明此对象可以序列化为一段简单的文本，并从中反序列化。
    /// Specifies the object can be serialized as a simple String.
    /// </summary>
    public interface IXStringSerializable
    {
        string Serialize();

        void Deserialize(string v);
    }
}