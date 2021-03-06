﻿using System;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 指明此对象可以序列化为一个XML树，并从中反序列化。
    /// Specifies the object can be serialized as XML tree.
    /// </summary>
    public interface IXElementSerializable
    {
        /// <summary>
        /// Serialize into specified XElement.
        /// </summary>
        /// <exception cref="ArgumentNullException"><param name="element" /> is <c>null</c>.</exception>
        void Serialize(XElement element);

        /// <summary>
        /// Deserialize from specified XElement.
        /// </summary>
        /// <exception cref="ArgumentNullException"><param name="element" /> is <c>null</c>.</exception>
        void Deserialize(XElement element);
    }

    /// <summary>
    /// 用于为无法实现 <see cref="IXElementSerializable"/> 的类型指定代理实现，
    /// 以实现指定类型与字符串之间的转换。
    /// Used to enable the convert between specific type and string,
    /// where the type cannot implement <see cref="IXStringSerializable"/>
    /// e.g. for System.xxx types.
    /// </summary>
    public interface IXElementSerializableSurrogate : IXSerializableSurrogate
    {
        void Serialize(object obj, XElement element);
        /// <summary>
        /// Deserialize from string used in Xml.
        /// </summary>
        /// <exception cref="ArgumentNullException"><param name="element" /> or <param name="desiredType" /> is <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Specified <see cref="Type"/> is not supported.</exception>
        /// <returns><param name="existingObj" /> if in-place deserialization is supported.
        /// Otherwise it should return a new instance of <param name="desiredType" />
        /// containing deserialized content.</returns>
        object Deserialize(XElement element, Type desiredType, object existingObj);
    }

    /// <summary>
    /// 可以使用两个委托来分别表示代理序列化/反序列化过程。
    /// Enables serialization for specified type with two delegates.
    /// </summary>
    /// <typeparam name="T">要支持序列化的对象类型。</typeparam>
    public sealed class DelegateXElementSerializableSurrogate<T> : IXElementSerializableSurrogate
    {
        private readonly Action<T, XElement> serializer;
        private readonly Func<XElement, T, T> deserializer;

        public DelegateXElementSerializableSurrogate(Action<T, XElement> serializer, Func<XElement, T, T> deserializer)
        {
            if (serializer == null) throw new ArgumentNullException("serializer");
            if (deserializer == null) throw new ArgumentNullException("deserializer");
            this.serializer = serializer;
            this.deserializer = deserializer;
        }

        public bool IsTypeSupported(Type t)
        {
            return t == typeof (T);
        }

        public void Serialize(object obj, XElement element)
        {
            serializer((T) obj, element);
        }

        public object Deserialize(XElement element, Type desiredType, object existingObj)
        {
            if (desiredType != typeof (T)) throw new NotSupportedException();
            return deserializer(element, (T) existingObj);
        }
       
    }
}
