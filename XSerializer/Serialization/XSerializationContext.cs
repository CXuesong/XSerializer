using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private Stack referenceChain;

        /// <summary>
        /// Declares that an reference-type object is to be serialized.
        /// This method is used to check circular reference.
        /// </summary>
        public void EnterObjectSerialization(object obj)
        {
            Debug.Assert(!obj.GetType().IsValueType);
            // Detect circular reference.
            if (referenceChain.Contains(obj))
                throw new InvalidOperationException(string.Format(Prompts.CircularReferenceDetected, obj));
            referenceChain.Push(obj);
        }

        /// <summary>
        /// Declares that an reference-type object has been serialized.
        /// </summary>
        public void ExitObjectSerialization(object obj)
        {
            var top = referenceChain.Pop();
            Debug.Assert(top == obj);
        }

        public XSerializationState(object contextObj)
        {
            referenceChain = new Stack();
            _SerializationContext = new XSerializationContext(contextObj);
        }
    }
}
