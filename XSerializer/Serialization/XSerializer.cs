﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private static XSerializerParameters defaultParameters;

        private XSerializerCache cache;

        /// <summary>
        /// 将指定的对象序列化，并写入流中。
        /// Serialize an object and write it into a Stream.
        /// </summary>
        public void Serialize(Stream s, object obj)
        {
            Serialize(s, obj, (XSerializerParameters) null);
        }

        /// <summary>
        /// 将指定的对象序列化，并写入流中。
        /// Serialize an object and write it into a Stream.
        /// </summary>
        public void Serialize(Stream s, object obj, XSerializerNamespaceCollection namespaces)
        {
            Serialize(s, obj, new XSerializerParameters(namespaces));
        }

        /// <summary>
        /// 将指定的对象序列化，并写入流中。
        /// Serialize an object and write it into a Stream.
        /// </summary>
        public void Serialize(Stream s, object obj, XSerializerParameters parameters)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (obj == null) throw new ArgumentNullException("obj");
            if (parameters == null) parameters = defaultParameters;
            GetSerializedDocument(obj, parameters).Save(s, parameters.CompactFormat
                ? SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces
                : SaveOptions.None);
        }

        /// <summary>
        /// 将指定的对象序列化，并获取序列化后的 XML 文档。
        /// Serialize an object into XDocument.
        /// </summary>
        public XDocument GetSerializedDocument(object obj)
        {
            return GetSerializedDocument(obj, null);
        }

        /// <summary>
        /// 将指定的对象序列化，并获取序列化后的 XML 文档。
        /// Serialize an object into XDocument.
        /// </summary>
        public XDocument GetSerializedDocument(object obj, XSerializerParameters parameters)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            if (parameters == null) parameters = defaultParameters;
            var state = new XSerializationState(parameters.Context);
            var root = cache.Serialize(obj, state);
            foreach (var ns in parameters.Namespaces ?? defaultNamespaces)
                root.SetAttributeValue(XNamespace.Xmlns + ns.Prefix, ns.Uri);
            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
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
            //return DeserializeCore(doc.Root);
            return null;
        }

        static XSerializer()
        {
            defaultNamespaces = new XSerializerNamespaceCollection();
            //defaultNamespaces.Add()
            defaultParameters = new XSerializerParameters(defaultNamespaces, null);
        }

        public XSerializer(Type rootType)
            : this(rootType, null)
        { }

        public XSerializer(Type rootType, IEnumerable<Type> includedTypes)
        {
            if (rootType == null) throw new ArgumentNullException("rootType");
            cache = new XSerializerCache();
            cache.RegisterRootType(rootType);
            if (includedTypes != null)
                foreach (var t in includedTypes)
                    cache.RegisterType(t);
        }
    }

    /// <summary>
    /// 为序列化过程指定参数。
    /// Specifies parameters that control serialization or deserialization.
    /// </summary>
    public class XSerializerParameters
    {
        /// <summary>
        /// 指定在根节点处应当导入的命名空间。
        /// Gets / sets namespaces that should be imported at root element.
        /// </summary>
        /// <value>
        /// 可以为<c>null</c>。
        /// This property can be <c>null</c>。
        /// </value>
        public XSerializerNamespaceCollection Namespaces { get; set; }

        /// <summary>
        /// 为序列化过程指定用户上下文。
        /// Get / sets user-defined context for serialization or deserialization.
        /// </summary>
        public object Context { get; set; }

        /// <summary>
        /// 序列化输出时，是否使用紧凑的 XML 格式。
        /// Gets / sets whether to use compact format when generating XML string.
        /// </summary>
        public bool CompactFormat { get; set; }

        public XSerializerParameters()
        { }

        public XSerializerParameters(XSerializerNamespaceCollection namespaces) : this(namespaces, null)
        { }

        public XSerializerParameters(object context) : this(null, context)
        { }

        public XSerializerParameters(XSerializerNamespaceCollection namespaces, object context)
        {
            Namespaces = namespaces;
            Context = context;
        }
    }
}
