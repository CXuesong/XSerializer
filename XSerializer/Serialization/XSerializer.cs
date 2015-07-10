using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 用于实现对象与 XML 之间的相互转换。
    /// Serializes and deserializes objects into and from XML documents.
    /// </summary>
    public class XSerializer
    {
        private static XSerializerNamespaceCollection defaultNamespaces;

        /// <summary>
        /// 获取/设置在序列化过程中需要引用的 XML 命名空间。
        /// Gets/Sets XML namespaces used in serialization.
        /// </summary>
        public XSerializerNamespaceCollection Namespaces { get; set; }

        /// <summary>
        /// 将指定的对象序列化，并写入流中。
        /// Serialize an object and write it into a Stream.
        /// </summary>
        public void Serialize(Stream s, object obj)
        {
            Serialize(s, obj, false);
        }

        /// <summary>
        /// 将指定的对象序列化，并写入流中。
        /// Serialize an object and write it into a Stream.
        /// </summary>
        public void Serialize(Stream s, object obj, bool compact)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (obj == null) throw new ArgumentNullException("obj");
            GetSerializedDocument(obj).Save(s, compact
                ? SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces
                : SaveOptions.None);
        }

        /// <summary>
        /// 将指定的对象序列化，并获取序列化后的 XML 文档。
        /// Serialize an object into XDocument.
        /// </summary>
        public XDocument GetSerializedDocument(object obj)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            if (!_RootType.IsInstanceOfType(obj))
                throw new ArgumentException(string.Format(Prompts.InvalidObjectType, obj.GetType(), _RootType), "obj");
            var root = SerializeXElement(obj, null);
            foreach (var ns in Namespaces ?? defaultNamespaces)
                root.SetAttributeValue(XNamespace.Xmlns + ns.Prefix, ns.Uri);
            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        }

        /// <summary>
        /// 将指定的对象序列化，并获取包含序列化后 XML 文档的字符串。
        /// Serialize an object into String, which contains XML content.
        /// </summary>
        public string GetSerializedString(object obj)
        {
            return GetSerializedDocument(obj).ToString(SaveOptions.OmitDuplicateNamespaces);
        }

        /// <summary>
        /// 为复数、枚举等类型提供辅助转换支持，将其转换为适合于 XML 值的字符串。
        /// </summary>
        /// <param name="value">要尝试进行转换的值。此值不为<c>null</c>。</param>
        /// <returns>一个字符串，包含了反序列化此对象所需的全部信息。
        /// 如果不能序列化为简单的字符串，则返回<c>null</c>。</returns>
        protected virtual string GetXString(object value)
        {
            return SerializationHelper.ToXString(value);
        }

        public object Deserialize(Stream s)
        {
            return Deserialize(XDocument.Load(s));
        }

        public object Deserialize(XDocument doc)
        {
            return DeserializeCore(doc.Root);
        }

        static XSerializer()
        {
            defaultNamespaces = new XSerializerNamespaceCollection();
            //defaultNamespaces.Add()
        }

        #region 核心函数
        /// <summary>
        /// 将指定对象的内容（子元素和属性）存入指定的元素。
        /// </summary>
        protected XElement SerializeXElement(object obj, XName nameOverride)
        {
            //如果 obj == null，那么此元素应当直接不存在。
            Debug.Assert(obj != null);
            var element = new XElement(nameOverride ?? GetName(obj.GetType()));
            //序列化简单类型。
            if (IsSimpleType(obj.GetType()))
            {
                element.SetValue(obj);
                return element;
            }
            var xstr = GetXString(obj);
            if (xstr != null)
            {
                element.SetValue(xstr);
                return element;
            }
            //序列化集合子项目。
            var enumerable = obj as IEnumerable;
            if (enumerable != null)
            {
                //忽略掉为 null 的项目
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    //使用类型名作为元素名，支持多态性。
                    element.Add(SerializeXElement(item, null));
                }
            }
            //序列化属性/字段。
            foreach (var member in obj.GetType()
                .GetMembers(BindingFlags.GetProperty | BindingFlags.GetField
                | BindingFlags.Public | BindingFlags.Instance))
            {
                //杂项元素。
                var anyElemAttr = member.GetCustomAttribute<XAnyElementAttribute>();
                if (anyElemAttr != null)
                {
                    if (enumerable != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.CollectionPropertyElementNotSupported, member));
                    var value = GetMemberValue(member, obj);
                    if (value == null) continue;
                    foreach (var e in (IEnumerable)value)
                        element.Add(new XElement((XElement)e));
#if RELEASE
                    continue;
#endif
                }
                //杂项属性。
                var anyAttrAttr = member.GetCustomAttribute<XmlAnyAttributeAttribute>();
                if (anyAttrAttr != null)
                {
                    if (anyElemAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    var value = GetMemberValue(member, obj);
                    if (value == null) continue;
                    foreach (var e in (IEnumerable)value)
                        element.Add(new XAttribute((XAttribute)e));
#if RELEASE
                    continue;
#endif
                }
                var eattr = member.GetCustomAttribute<XElementAttribute>();
                if (eattr != null)
                {
                    if (enumerable != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.CollectionPropertyElementNotSupported, member));
                    if (anyElemAttr != null || anyAttrAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    var value = GetMemberValue(member, obj);
                    if (value == null) continue;
                    element.Add(SerializeXElement(value, GetName(member, eattr)));
#if RELEASE
                    continue;
#endif
                }
                var aattr = member.GetCustomAttribute<XAttributeAttribute>();
                if (aattr != null)
                {
                    if (eattr != null || anyElemAttr != null || anyAttrAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    var value = GetMemberValue(member, obj);
                    if (value == null) continue;
                    element.Add(SerializeXAttribute(value, GetName(member, aattr)));
                }
            }
            return element;
        }

        protected XAttribute SerializeXAttribute(object obj, XName name)
        {
            //如果 obj == null，那么此元素应当直接不存在。
            Debug.Assert(obj != null);
            //序列化简单类型。
            if (IsSimpleType(obj.GetType())) return new XAttribute(name, obj);
            var xstr = GetXString(obj);
            if (xstr != null) return new XAttribute(name, xstr);
            //无法序列化子属性/字段。
            throw new InvalidOperationException(string.Format(Prompts.CannotSerializeAsAttribute, obj.GetType().FullName));
        }
        public object DeserializeCore(XElement e)
        {
            return null;
        }
        #endregion

        #region 辅助函数
        private XName GetName(MemberInfo m, XNamedAttributeBase attr = null)
        {
            Debug.Assert(m != null);
            if (attr == null) attr = m.GetCustomAttribute<XNamedAttributeBase>();
            if (attr != null) return XName.Get(attr.LocalName ?? m.Name, attr.Namespace ?? "");
            return m.Name;
        }

        private XName GetName(Type t)
        {
            Debug.Assert(t != null);
            var attr = t.GetCustomAttribute<XTypeAttribute>();
            if (attr != null) return XName.Get(attr.LocalName ?? t.Name, attr.Namespace ?? "");
            return t.Name;
        }

        private object GetMemberValue(MemberInfo member, object obj)
        {
            var f = member as FieldInfo;
            if (f != null) return f.GetValue(obj);
            var p = member as PropertyInfo;
            if (p != null) return p.GetValue(obj);
            throw new NotSupportedException();
        }

        /// <summary>
        /// 判断指定的类型是否为可直接序列化的简单类型。
        /// </summary>
        private bool IsSimpleType(Type t)
        {
            //对于 Nullable 的处理。
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsSimpleType(t.GenericTypeArguments[0]);
            return t == typeof(String) || t == typeof(Byte) || t == typeof(SByte)
                   || t == typeof(Int16) || t == typeof(UInt16) || t == typeof(Int32) || t == typeof(UInt32)
                   || t == typeof(Int64) || t == typeof(UInt64) || t == typeof(Single) || t == typeof(Double)
                   || t == typeof(IntPtr) || t == typeof(UIntPtr)
                   || t == typeof(DateTime) || t == typeof(TimeSpan);
        }

        #endregion

        private Type _RootType;

        public XSerializer(Type rootType)
            : this(rootType, null)
        {
        }

        public XSerializer(Type rootType, Type[] includedTypes)
        {
            if (rootType == null) throw new ArgumentNullException("rootType");
            _RootType = rootType;
        }
    }
}
