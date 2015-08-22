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
        public string LocalName { get; }

        public string Namespace { get; }

        internal XName GetName()
        {
            return XName.Get(LocalName, Namespace);
        }

        internal XName GetName(string defaultLocalName)
        {
            return XName.Get(LocalName ?? defaultLocalName, Namespace);
        }

        internal XName GetName(XName defaultName)
        {
            return XName.Get(LocalName ?? defaultName.LocalName, Namespace ?? defaultName.NamespaceName);
        }

        /// <param name="localName">
        /// XML元素或属性的本地名称。如果为<c>null</c>，则表示使用类型名称或成员名称。
        /// Local attribute or element name. <c>null</c> if type or member name is used.
        /// </param>
        /// <param name="namespaceUri">
        /// XML元素或属性的命名空间。可为<c>null</c>。
        /// Namespace of the attribute or element. Can be <c>null</c>。
        /// </param>
        protected XNamedAttributeBase(string localName, string namespaceUri)
        {
            LocalName = localName;
            Namespace = namespaceUri ?? string.Empty;
        }

        protected XNamedAttributeBase() : this(null, null)
        { }
    }

    /// <summary>
    /// 当指定的类型作为XML根节点时，控制根节点的特性。
    /// Controls XML serialization of the attributed target as XML root element.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class XRootAttribute : XNamedAttributeBase
    {
        public XRootAttribute()
        { }

        public XRootAttribute(string localName)
            : base(localName, null)
        { }

        public XRootAttribute(string localName, string namespaceUri)
            : base(localName, namespaceUri)
        { }
    }

    /// <summary>
    /// 指示指定的成员应当被保存为XML元素。
    /// Specifies the member should be included in serialization as an XML element.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
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
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
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
    /// 为当前集合的子级指定派生类型和/或这些类型的元素名称。此特性可以多次使用。
    /// Specifies the derived type and/or XML element name of child items (of the collection field/property).
    /// This attribute may be used multiple times.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class XCollectionItemAttribute : XNamedAttributeBase
    {
        /// <summary>
        /// Specifies the derived type of the item.
        /// If it's <c>null</c>, then the attribute is applied to the type of
        /// the field/property that applied this attribute.
        /// </summary>
        public Type Type { get; set;}

        public XCollectionItemAttribute()
        { }

        public XCollectionItemAttribute(string localName)
            : this(null, localName, null)
        { }

        public XCollectionItemAttribute(string localName, string namespaceUri)
            : this(null, localName, namespaceUri)
        { }

        public XCollectionItemAttribute(Type type)
            : this(type, null, null)
        { }

        public XCollectionItemAttribute(Type type, string localName)
            : this(type, localName, null)
        { }

        public XCollectionItemAttribute(Type type, string localName, string namespaceUri)
            : base(localName, namespaceUri)
        {
            Type = type;
        }
    }

    /// <summary>
    /// 控制指定的类型在序列化时的 XML 名称和行为。
    /// Specifies the XML qualified name & behavior for the class or structure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class XTypeAttribute : XNamedAttributeBase
    {

        /// <summary>
        /// 指示序列化时是否应当检查此类型的非共有成员。
        /// Specifies whether to check non-public members in the attributed type while serializing.
        /// </summary>
        public bool IncludeNonPublicMembers { get; set; }

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
    /// 允许序列化过程中识别指定类型的对象。
    /// Allows the specified type to be recognized during serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class XIncludeAttribute : Attribute
    {
        public XIncludeAttribute(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            Type = type;
        }

        public Type Type { get; }
    }

    /// <summary>
    /// 指示应当将所有未识别的XML属性保存至此。
    /// Specifies all the unrecognized XML attributes should be contained in the field or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class XAnyAttributeAttribute : Attribute
    {
    }

    /// <summary>
    /// 指示应当将所有未识别的XML元素保存至此。
    /// Specifies all the unrecognized XML elements should be contained in the field or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class XAnyElementAttribute : Attribute
    {
    }

    /// <summary>
    /// 指示应当将此成员序列化为父级元素中包含的文本。
    /// Specifies the attributed field or property should be serialized as xml text contained in the parent element.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class XTextAttribute : Attribute
    {
    }

    /// <summary>
    /// 控制枚举值在 XML 输出时的文本表示。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class XEnumAttribute : Attribute
    {
        public string Name { get; }

        public XEnumAttribute(string name)
        {
            Name = name;
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
            var attr = info.GetCustomAttribute<XEnumAttribute>();
            return attr == null ? info.Name : attr.Name;
        }
    }


}
