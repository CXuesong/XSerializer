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
        /// ������ת��Ϊ�ʺ��� XML ��ȡ���ַ�����
        /// </summary>
        public static string ToXString(Complex value)
        {
            return value.Real.ToString(CultureInfo.InvariantCulture) + " " +
                   value.Imaginary.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// ��ö����ת��Ϊ��֮��Ӧ�� XML �еĳ����ַ�����
        /// </summary>
        /// <param name="value">Ҫת����ö��ֵ��</param>
        /// <exception cref="ArgumentOutOfRangeException">ָ����ö��ֵ�޷�����ö���������ҵ�ƥ���</exception>
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
    /// ָ���˶���������л�Ϊһ�μ򵥵��ı��������з����л���
    /// Specifies the object can be serialized as a simple String.
    /// </summary>
    public interface IXStringSerializable
    {
        string Serialize();

        void Deserialize(string v);
    }
}