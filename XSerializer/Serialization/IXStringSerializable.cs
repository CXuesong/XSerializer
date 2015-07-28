using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Undefined.Serialization
{
    /// <summary>
    /// 指明此对象可以序列化为一段简单的文本，并从中反序列化。
    /// Specifies the object can be serialized as a simple String.
    /// </summary>
    public interface IXStringSerializable
    {
        string Serialize();
        /// <summary>
        /// Deserialize from string used in Xml.
        /// </summary>
        /// <exception cref="ArgumentNullException"><param name="s" /> is <c>null</c>.</exception>
        void Deserialize(string s);
    }

    public interface IXSerializableSurrogate
    {
        bool IsTypeSupported(Type t);
    }

    /// <summary>
    /// 用于为无法实现 <see cref="IXStringSerializable"/> 的类型指定代理实现，
    /// 以实现指定类型与字符串之间的转换。
    /// Used to enable the convert between specific type and string,
    /// where the type cannot implement <see cref="IXStringSerializable"/>
    /// e.g. for System.xxx types.
    /// </summary>
    public interface IXStringSerializableSurrogate : IXSerializableSurrogate
    {
        string Serialize(object obj);
        /// <summary>
        /// Deserialize from string used in Xml.
        /// </summary>
        /// <exception cref="ArgumentNullException"><param name="s" /> or <param name="desiredType" /> is <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Specified <see cref="Type"/> is not supported.</exception>
        object Deserialize(string s, Type desiredType);
    }

    /// <summary>
    /// An <see cref="IXStringSerializableSurrogate"/> implementation that can
    /// convert between string and Enum types.
    /// </summary>
    public class EnumXStringSerializableSurrogate : IXStringSerializableSurrogate
    {
        public static readonly EnumXStringSerializableSurrogate Defualt = new EnumXStringSerializableSurrogate();

        public bool IsTypeSupported(Type t)
        {
            return t.IsEnum;
        }

        public string Serialize(object obj)
        {
            if (obj == null) return null;
            var value = (Enum) obj;
            Enum[] factors = null;
            var vType = value.GetType();
            if (vType.GetCustomAttribute<FlagsAttribute>() != null)
            {
                //Flags
                //Factors = FactorEnum(value)
                throw new NotImplementedException("Enum flags is not supported yet.");
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

        public object Deserialize(string s, Type desiredType)
        {
            if (String.IsNullOrWhiteSpace(s)) return null;
            if (desiredType.IsGenericType && desiredType.GetGenericTypeDefinition() == typeof (Nullable<>))
                desiredType = desiredType.GenericTypeArguments[0];
            string[] subExpressions;
            long underlyingValue = 0;
            if (desiredType.GetCustomAttribute<FlagsAttribute>() != null)
            {
                //按位组合的枚举，在 Xml 中以被空白分隔的表达式存在
                subExpressions = s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                subExpressions = new[] { s };
            }
            //按 XSDExpressionAttribute 确定值
            foreach (var eachExpression in subExpressions)
            {
                foreach (var eachField in desiredType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (XEnumAttribute.GetXEnumName(eachField) == eachExpression)
                    {
                        underlyingValue = underlyingValue | Convert.ToInt64(eachField.GetValue(null));
                        break;
                    }
                }
            }
            return Enum.ToObject(desiredType, underlyingValue);
        }
    }

    /// <summary>
    /// A demo <see cref="IXStringSerializableSurrogate"/> implementation that can
    /// convert between string and Uri.
    /// </summary>
    public class UriXStringSerializableSurrogate : IXStringSerializableSurrogate
    {
        public static readonly UriXStringSerializableSurrogate Defualt = new UriXStringSerializableSurrogate();

        public bool IsTypeSupported(Type t)
        {
            return t == typeof (Uri);
        }

        public string Serialize(object obj)
        {
            if (obj == null) return null;
            return obj.ToString();
        }

        public object Deserialize(string s, Type desiredType)
        {
            SerializationHelper.AssertKindOf(typeof (Uri), desiredType);
            return new Uri(s);
        }
    }
}