using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    internal static class SerializationHelper
    {

        public static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// 将复数转换为适合于 XML 存取的字符串。
        /// </summary>
        public static string ToXString(Complex value)
        {
            return value.Real.ToString(CultureInfo.InvariantCulture) + " " +
                   value.Imaginary.ToString(CultureInfo.InvariantCulture);
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
            XName n;
            if (simpleTypeNameDict.TryGetValue(t, out n)) return n;
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
        public static bool IsMemberReadOnly(MemberInfo member)
        {
            var f = member as FieldInfo;
            if (f != null) return f.IsLiteral || f.IsInitOnly;
            var p = member as PropertyInfo;
            if (p != null) return p.SetMethod == null;
            throw new NotSupportedException();
        }

        public static void SetMemberValue(MemberInfo member, object obj, object value)
        {
            var f = member as FieldInfo;
            if (f != null)
            {
                f.SetValue(obj, value);
                return;
            }
            var p = member as PropertyInfo;
            if (p != null)
            {
                p.SetValue(obj, value);
                return;
            }
            throw new NotSupportedException();
        }

        /// <summary>
        /// 获取指定集合类型在声明时所使用的元素类型。
        /// </summary>
        public static Type GetCollectionItemType(Type collectionType)
        {
            Debug.Assert(collectionType != null && typeof(IEnumerable).IsAssignableFrom(collectionType));
            var ienum = collectionType.GetInterfaces().FirstOrDefault(t => t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (ienum == null) return typeof(object);
            return ienum.GenericTypeArguments[0];
        }

        public static MethodInfo FindCollectionAddMethod(Type collectionType)
        {
            var viType = GetCollectionItemType(collectionType);
            return collectionType.GetMethod("Add", new[] {viType}) ??
                   GetICollection(collectionType).GetMethod("Add", new[] {viType});
        }

        public static bool IsDictionary(Type t)
        {
            return typeof(IDictionary).IsAssignableFrom(t);
        }

        /// <summary>
        /// 判断指定的类型是否可以为 null。
        /// 这包括所有的引用类型，以及 <see cref="Nullable&lt;T&gt;"/>。
        /// </summary>
        public static bool IsNullableType(Type t)
        {
            return !t.IsValueType || t.IsGenericType && t.GetGenericTypeDefinition() == typeof (Nullable<>);
        }

        public static Type GetIDictionary(Type dictionaryType)
        {
            Debug.Assert(IsDictionary(dictionaryType));
            return GetGenericInterface(dictionaryType, typeof(IDictionary<,>)) ?? typeof(IDictionary);
        }

        public static Type GetIEnumerable(Type collectionType)
        {
            Debug.Assert(typeof(IEnumerable).IsAssignableFrom(collectionType));
            return GetGenericInterface(collectionType, typeof(IEnumerable<>)) ?? typeof(IEnumerable);
        }

        public static Type GetICollection(Type collectionType)
        {
            //Debug.Assert(typeof(ICollection).IsAssignableFrom(collectionType));
            // ICollecion<T> does not derive from ICollection
            return GetGenericInterface(collectionType, typeof (ICollection<>)) ??
                   (typeof (ICollection).IsAssignableFrom(collectionType) ? typeof (ICollection) : null);
        }

        public static Type GetGenericInterface(Type type, Type genericInterfaceType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                return type;
            return type.GetInterfaces()
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == genericInterfaceType);
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

        private static Dictionary<Type, XName> simpleTypeNameDict;
        /// <summary>
        /// 获取可直接序列化的简单类型列表。
        /// Gets a list of types that can be serialized directly.
        /// </summary>
        public static Dictionary<Type, XName> SimpleTypes
        {
            get
            {
                if (simpleTypeNameDict == null)
                {
                    simpleTypeNameDict = new Dictionary<Type, XName>
                    {
                        //Object can be inherited.
                        //{typeof (Object), Xsi + "Object"},
                        {typeof (String), Xsi + "string"},
                        {typeof (Byte), Xsi + "unsignedByte"},
                        {typeof (SByte), Xsi + "byte"},
                        {typeof (Int16), Xsi + "short"},
                        {typeof (UInt16), Xsi + "unsignedShort"},
                        {typeof (Int32), Xsi + "int"},
                        {typeof (UInt32), Xsi + "unsignedInt"},
                        {typeof (Int64), Xsi + "long"},
                        {typeof (UInt64), Xsi + "unsignedLong"},
                        {typeof (Single), Xsi + "float"},
                        {typeof (Double), Xsi + "double"},
                        {typeof (IntPtr), Xsi + "integer"},
                        {typeof (UIntPtr), Xsi + "UIntPtr"},
                        {typeof (DateTime), Xsi + "dateTime"},
                        {typeof (TimeSpan), Xsi + "duration"},
                        {typeof (DateTimeOffset), "DateTimeOffset"},
                        {typeof (Guid), Xsi + "guid"}
                    };
                }
                return simpleTypeNameDict;
            }
        }

        public static MethodBase GetExplicitOperator(Type source, Type dest)
        {
            Debug.Assert(source != null && dest != null);
            return source.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "op_Explicit" && m.ReturnType == dest);
        }

        /// <summary>
        /// 判断指定的类型是否为可直接序列化的简单类型。
        /// </summary>
        public static bool IsSimpleType(Type t)
        {
            //对于 Nullable 的处理。
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsSimpleType(t.GenericTypeArguments[0]);
            return SimpleTypes.ContainsKey(t);
        }

        public static TypeSerializationKind GetSerializationKind(Type t)
        {
            if (IsSimpleType(t))
                return TypeSerializationKind.Simple;
            if (typeof(IXStringSerializable).IsAssignableFrom(t))
                return TypeSerializationKind.XStringSerializable;
            if (typeof(IEnumerable).IsAssignableFrom(t))
                return TypeSerializationKind.Collection;
            return TypeSerializationKind.Complex;
        }
    }
}