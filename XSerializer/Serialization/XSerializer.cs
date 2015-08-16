using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 用于实现对象与 XML 之间的相互转换。
    /// Serializes and deserializes objects into and from XML documents.
    /// </summary>
    public class XSerializer
    {
        private static readonly XSerializerNamespaceCollection defaultNamespaces;
        private static readonly XSerializerParameters defaultParameters;

        private XSerializerBuilder builder;

        /// <summary>
        /// 将指定的对象序列化，并写入流中。
        /// Serialize an object and write it into a Stream.
        /// </summary>
        public void Serialize(Stream s)
        {
            Serialize(s, null, (XSerializerParameters) null);
        }

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
            var root = builder.Serialize(obj, parameters.Context, parameters.Namespaces ?? defaultNamespaces);
            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        }

        /// <summary>
        /// 从指定的流中反序列化对象。
        /// Deserialize an object from stream.
        /// </summary>
        public object Deserialize(Stream s)
        {
            return Deserialize(s, null, null);
        }

        /// <summary>
        /// 从指定的流中反序列化对象。
        /// Deserialize an object from stream.
        /// </summary>
        public object Deserialize(Stream s, object context)
        {
            return Deserialize(s, context, null);
        }

        /// <summary>
        /// 从指定的流中反序列化对象。
        /// Deserialize an object from stream.
        /// </summary>
        /// <param name="existingObject">已存在的对象引用。如果指定，则将进行就地反序列化。</param>
        public object Deserialize(Stream s, object context, object existingObject)
        {
            return Deserialize(XDocument.Load(s), context, existingObject);
        }

        /// <summary>
        /// 从指定的 XML 文档中反序列化对象。
        /// Deserialize an object from XDocument.
        /// </summary>
        /// <param name="existingObject">已存在的对象引用。如果指定，则将进行就地反序列化。</param>
        public object Deserialize(XDocument doc)
        {
            return Deserialize(doc, null, null);
        }

        /// <summary>
        /// 从指定的 XML 文档中反序列化对象。
        /// Deserialize an object from XDocument.
        /// </summary>
        public object Deserialize(XDocument doc, object context)
        {
            return Deserialize(doc, context, null);
        }

        /// <summary>
        /// 从指定的 XML 文档中反序列化对象。
        /// Deserialize an object from XDocument.
        /// </summary>
        /// <param name="existingObject">已存在的对象引用。如果指定，则将进行就地反序列化。</param>
        public object Deserialize(XDocument doc, object context, object existingObject)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (doc.Root == null) throw new ArgumentException(Prompts.EmptyXDocument, "doc");
            return builder.Deserialize(doc.Root, context, existingObject);
        }

        static XSerializer()
        {
            defaultNamespaces = new XSerializerNamespaceCollection {PrefixUriPair.Xsi};
            defaultParameters = new XSerializerParameters(defaultNamespaces, null);
        }

        public XSerializer(Type rootType)
            : this(rootType, null, null)
        { }

        public XSerializer(Type rootType, IEnumerable<Type> includedTypes) 
            : this(rootType, includedTypes, null)
        { }

        public XSerializer(Type rootType, IEnumerable<Type> includedTypes, XSerializableSurrogateCollection serializableSurrogates)
        {
            if (rootType == null) throw new ArgumentNullException("rootType");
            builder = new XSerializerBuilder(serializableSurrogates);
            builder.RegisterBuiltInTypes();
            builder.RegisterRootType(rootType);
            if (includedTypes == null) return;
            foreach (var t in includedTypes)
                builder.RegisterType(t);
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
