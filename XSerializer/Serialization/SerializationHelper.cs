using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
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