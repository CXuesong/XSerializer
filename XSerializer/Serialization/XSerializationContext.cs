using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 为 XML 序列化过程提供附加信息。
    /// Provides contextual information during XML serialization process.
    /// </summary>
    public struct XSerializationContext
    {
        private object _Context;

        /// <summary>
        /// 由序列化调用方提供的附加信息。
        /// Get the additional context provided by the caller.
        /// </summary>
        public object Context
        {
            get { return _Context; }
        }

        public XSerializationContext(object context)
        {
            _Context = context;
        }
    }

    /// <summary>
    /// Provides more info to enable some features like
    /// cicular reference detection during serialization.
    /// </summary>
    internal class XSerializationState
    {
        private XSerializationContext _SerializationContext;

        public XSerializationContext SerializationContext
        {
            get { return _SerializationContext; }
        }

        private XSerializerBuilder _Builder;

        public XElement SerializeXElement(object obj)
        {
            return SerializeXElement(obj, null, null);
        }

        public XElement SerializeXElement(object obj, XName nameOverride)
        {
            return SerializeXElement(obj, nameOverride, null);
        }

        // typeScope is applied for child items, rather than obj itself.
        public XElement SerializeXElement(object obj, XName nameOverride, SerializationScope typeScope)
        {
            Debug.Assert(obj != null);
            var nominalType = obj.GetType();
            if (nameOverride == null)
            {
                // Now Reject Polymorph
                if (typeScope != null) nameOverride = typeScope.GetName(nominalType);
                if (nameOverride == null) nameOverride = _Builder.GlobalScope.GetName(nominalType);
                if (nameOverride == null)
                    throw new NotSupportedException(string.Format(Prompts.UnregisteredType, nominalType, _Builder.GlobalScope));
            }
            var s = _Builder.GetSerializer(nominalType);
            if (s == null && SerializationHelper.IsSimpleType(nominalType))
                return new XElement(nameOverride, obj);
            EnterObjectSerialization(obj);
            var e = new XElement(nameOverride);
            if (s == null)
                throw new NotSupportedException(string.Format(Prompts.UnregisteredType, nominalType, typeScope));
            s.Serialize(e, obj, this);
            ExitObjectSerialization(obj);
            return e;
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

        public XSerializationState(object contextObj, XSerializerBuilder builder)
        {
            referenceChain = new Stack();
            _Builder = builder;
            _SerializationContext = new XSerializationContext(contextObj);
        }
    }
}
