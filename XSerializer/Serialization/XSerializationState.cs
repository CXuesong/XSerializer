using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// Provides more info to enable some features like
    /// cicular reference detection during serialization.
    /// </summary>
    internal class XSerializationState
    {
        private static readonly XName XsiType = SerializationHelper.Xsi + "type";

        private XSerializerBuilder _Builder;

        private StreamingContext _Context;

        public StreamingContext Context
        {
            get { return _Context; }
        }

        // typeScope is applied for child items, rather than obj itself.
        private XElement SerializeXElement(object obj, Type defaultType, XName name, SerializationScope typeScope)
        {
            Debug.Assert(obj != null && defaultType != null && name != null);
            var objType = obj.GetType();
            var objTypeName = _Builder.GlobalScope.GetName(objType);
            if (objTypeName == null)
                throw new NotSupportedException(string.Format(Prompts.UnregisteredType, objType, _Builder.GlobalScope));
            var s = _Builder.GetSerializer(objType);
            var e = new XElement(name);
            //如果名义类型和实际类型不同，则注明实际类型。
            //如果是 TypeScope 中未定义的类型，则使用对应的元素名，而不使用 xsi:type
            if (name != objTypeName && objType != defaultType)
            {
                //先将 XName 加入批注，到根节点中处理。
                e.AddAnnotation(objTypeName);
            }
            //一般不会调用到此处，除非 obj 是根部节点，或是集合中的一项。
            if (s == null)
                throw new NotSupportedException(string.Format(Prompts.UnregisteredType, objType, typeScope));
            EnterObjectSerialization(obj);
            s.Serialize(e, obj, this, typeScope);
            ExitObjectSerialization(obj);
            return e;
        }

        public void TestPoint(object a)
        {
            Debug.Print("{0}", a);
        }

        public XElement SerializeRoot(object obj, Type rootType, XName name, XSerializerNamespaceCollection namespaces)
        {
            Debug.Assert(rootType.IsInstanceOfType(obj));
            Debug.Assert(namespaces != null);
            var root = SerializeXElement(obj, rootType, name, null);
            //导入命名空间。
            foreach (var ns in namespaces)
                root.SetAttributeValue(XNamespace.Xmlns + ns.Prefix, ns.Uri);
            //处理导入的类型。
            var nsCounter = 0;
            foreach (var descendant in root.Descendants())
            {
                var actualTypeName = descendant.Annotation<XName>();
                if (actualTypeName != null)
                {
                    if (actualTypeName.Namespace == descendant.GetDefaultNamespace())
                    {
                        descendant.SetAttributeValue(SerializationHelper.Xsi + "type", actualTypeName.LocalName);
                    }
                    else
                    {
                        var prefix = descendant.GetPrefixOfNamespace(actualTypeName.Namespace);
                        if (prefix == null)
                        {
                            nsCounter++;
                            prefix = "nss" + nsCounter;
                            descendant.SetAttributeValue(XNamespace.Xmlns + prefix, actualTypeName.NamespaceName);
                        }
                        descendant.SetAttributeValue(SerializationHelper.Xsi + "type", prefix + ":" + actualTypeName.LocalName);
                    }
                    descendant.RemoveAnnotations<XName>();
                }
            }
            return root;
        }

        // Serialize Collection or Complex objects
        public XElement SerializeXProperty(object obj, Type defaultType, XName name, SerializationScope childItemsTypeScope)
        {
            //Debug.Print("SP : {0}\t{1}\t{2}", name, obj, childItemsTypeScope);
            if (obj != null) return SerializeXElement(obj, defaultType, name, childItemsTypeScope);
            return null;
        }

        public XElement SerializeXCollectionItem(object obj, Type defaultType, SerializationScope typeScope)
        {
            //Debug.Print("SC : {0}\t{1}", obj, typeScope);
            Debug.Assert(typeScope != null);
            var nominalType = obj.GetType();
            var objTypeName = typeScope.GetName(nominalType);
            if (objTypeName == null)
            {
                objTypeName = typeScope.GetName(defaultType);
                nominalType = defaultType;
            }
            return SerializeXElement(obj, nominalType, objTypeName, null);
        }

        public object DeserializeRoot(XElement e, Type rootType)
        {
            var obj = DeserializeXElement(e, null, rootType, null);
            if (!rootType.IsInstanceOfType(obj))
                throw new InvalidOperationException("Invalid root element : " + e.Name);
            return obj;
        }

        // param : obj is the current value of property / field.
        public object DeserializeXProperty(XElement e, object obj, Type defaultType, SerializationScope childItemsTypeScope)
        {
            //Debug.Print("DP : {0}\t{1}", e, childItemsTypeScope);
            return DeserializeXElement(e, obj, defaultType, childItemsTypeScope);
        }

        public object DeserializeXCollectionItem(XElement e, SerializationScope typeScope)
        {
            //Debug.Print("DC : {0}\t{1}", e, typeScope);
            Debug.Assert(typeScope != null);
            var t = typeScope.GetType(e.Name);
            if (t == null) throw new NotSupportedException(string.Format(Prompts.UnregisteredType, e.Name, typeScope));
            return DeserializeXElement(e, null, t, typeScope);
        }

        private object DeserializeXElement(XElement e, object obj, Type defaultType, SerializationScope typeScope)
        {
            Debug.Assert(e != null && defaultType != null);
            // 对于集合，从元素名推理对象类型。
            var xsiTypeName = (string)e.Attribute(XsiType);
            Type objType;
            if (xsiTypeName == null)
            {
                //未指定 xsi:type，则使用 defaultType
                objType = defaultType;
            }
            else
            {
                //指定了 xsi:type
                //解析 prefix:localName
                var parts = xsiTypeName.Split(':');
                XName xName;
                if (parts.Length <= 1) xName = e.GetDefaultNamespace() + xsiTypeName;
                else xName = e.GetNamespaceOfPrefix(parts[0]) + parts[1];
                objType = _Builder.GlobalScope.GetType(xName);
                if (objType == null)
                    throw new NotSupportedException(string.Format(Prompts.UnregisteredType, xsiTypeName, typeScope));
            }
            var s = _Builder.GetSerializer(objType);
            if (s == null)
                throw new NotSupportedException(string.Format(Prompts.UnregisteredType, objType, typeScope));
            return s.Deserialize(e, obj, this, typeScope);
        }

        /// <summary>
        /// Enumerates all elements that is not in specific set.
        /// </summary>
        public IEnumerable<XElement> EnumAnyElements(XElement e, HashSet<XName> knownNames)
        {
            return e.Elements().Where(ex => !knownNames.Contains(ex.Name));
        }

        private Stack referenceChain;

        /// <summary>
        /// Declares that an reference-type object is to be serialized.
        /// This method is used to check circular reference.
        /// </summary>
        private void EnterObjectSerialization(object obj)
        {
            // Detect circular reference.
            if (!obj.GetType().IsValueType && referenceChain.Contains(obj))
                throw new InvalidOperationException(string.Format(Prompts.CircularReferenceDetected, obj));
            referenceChain.Push(obj);
        }

        /// <summary>
        /// Declares that an reference-type object has been serialized.
        /// </summary>
        private void ExitObjectSerialization(object obj)
        {
            var top = referenceChain.Pop();
            Debug.Assert(obj.GetType().IsValueType || top == obj);
        }

        public XSerializationState(StreamingContext context, XSerializerBuilder builder)
        {
            referenceChain = new Stack();
            _Builder = builder;
            _Context = context;
        }
    }
}
