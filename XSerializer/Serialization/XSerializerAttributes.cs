using System;
using System.Reflection;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 为可指定 XML 完全限定名的特性提供基础公共功能。
    /// </summary>
    public abstract class XNamedAttributeBase : Attribute
    {
        private readonly string _LocalName;

        private readonly string _Namespace;

        public string LocalName
        {
            get { return _LocalName; }
        }

        public string Namespace
        {
            get { return _Namespace; }
        }

        internal XName GetName()
        {
            return XName.Get(_LocalName, _Namespace);
        }

        protected XNamedAttributeBase(string localName, string namespaceUri)
        {
            _LocalName = localName;
            _Namespace = namespaceUri;
        }

        protected XNamedAttributeBase() : this(null, null)
        { }
    }

    /// <summary>
    /// 指示指定的成员应当被保存为XML元素。
    /// Specifies the member should be included in serialization as an XML element.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class XElementAttribute : XNamedAttributeBase
    {
        public XElementAttribute()
        { }

        public XElementAttribute(string localName)
            : base(localName, null)
        { }

        public XElementAttribute(string localName, string namespaceUri)
            : base(localName, namespaceUri)
        { }
    }

    /// <summary>
    /// 指示指定的成员应当被保存为XML属性。
    /// Specifies the member should be included in serialization as an XML attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XAttributeAttribute : XNamedAttributeBase
    {
        public XAttributeAttribute(string localName)
            : base(localName, null)
        { }

        public XAttributeAttribute(string localName, string namespaceUri)
            : base(localName, namespaceUri)
        { }

        public XAttributeAttribute()
        { }
    }

    /// <summary>
    /// 为指定的类型提供默认的 XML 完全限定名。
    /// Specifies the XML qualified name for the class or structure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class XTypeAttribute : XNamedAttributeBase
    {
        public XTypeAttribute()
        { }

        public XTypeAttribute(string localName)
            : base(localName, null)
        { }

        public XTypeAttribute(string localName, string namespaceUri)
            : base(localName, namespaceUri)
        { }
    }

    /// <summary>
    /// 指示应当将所有未识别的XML属性保存至此。
    /// Specifies all the unrecognized XML attributes should be contained in the field or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class XmlAnyAttributeAttribute : Attribute
    {
        public XmlAnyAttributeAttribute()
        {
        }
    }

    /// <summary>
    /// [RIW] 指示应当将所有未识别的XML元素保存至此。
    /// Specifies all the unrecognized XML elements should be contained in the field or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class XAnyElementAttribute : Attribute
    {
        public XAnyElementAttribute()
        {
        }
    }

    /// <summary>
    /// 控制枚举值在 XML 输出时的文本表示。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal class XEnumAttribute : Attribute
    {
        private readonly string _Name;

        public string Name
        {
            get { return _Name; }
        }

        public XEnumAttribute(string name)
        {
            _Name = name;
        }

        /// <summary>
        /// 获取一个枚举项的 XML 字符串表示。
        /// </summary>
        /// <param name="info">枚举项的信息。</param>
        public static string GetXEnumName(FieldInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            else
            {
                var attr = info.GetCustomAttribute<XEnumAttribute>();
                return attr == null ? info.Name : attr._Name;
            }
        }
    }


}
