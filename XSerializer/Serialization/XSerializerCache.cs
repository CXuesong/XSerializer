using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 用于在序列化过程中暂时保存经常使用到的信息。
    /// 注意，此类型的行为与 XmlSerializer 类似，
    /// 要求所有参与序列化类型均通过某些方式（如属性或字段的声明类型，
    /// 或 XCollectionItemAttribute）显式声明。
    /// Used to cache some useful information during serialization.
    /// Note that in order to get ready for later optimization,
    /// the behavior of this class may be like XmlSerializer,
    /// in the way that all types to be serialized should be declared
    /// by some means (e.g. by the type of property or field declaration, or
    /// by XCollectionItemAttribute).  And for the same reason,
    /// the dependencies between SerializerCache and the properties
    /// in TypeSerializer should be as few as possible.
    /// That is, to treat TypeSerializer as only an actuator.
    /// </summary>
    internal class XSerializerCache
    {
        private ScopedTypeCollection registeredTypes = new ScopedTypeCollection();
        private Type rootType;
        private XName rootName;

        /// <summary>
        /// 注册指定的类型作为XML文档的根节点。
        /// Register the specified type as XML root element in serailization.
        /// </summary>
        public void RegisterRootType(Type t)
        {
            if (t == null) throw new ArgumentNullException("t");
            if (rootType != null) throw new InvalidOperationException();
            var s = RegisterTypeCore(t);
            var rootAttr = t.GetCustomAttribute<XRootAttribute>();
            rootName = rootAttr == null ? s.Name : rootAttr.GetName(s.Name);
            rootType = t;
        }

        /// <summary>
        /// 注册指定的类型，以便在序列化中使用。
        /// Register the specified type, which can be used in serailization.
        /// </summary>
        /// <remarks>
        /// 如果指定的类型已经注册，则不会进行任何操作。
        /// Do nothing if the specified type has already been registered.
        /// </remarks>
        public void RegisterType(Type t)
        {
            RegisterTypeCore(t);
        }

        public ScopedTypeSerializer RegisterTypeCore(Type t)
        {
            var s = GetOrDeclareSerializer(t);
            while (serializerDecls.Count > 0)
            {
                var decl = serializerDecls.Pop();
                ImplementTypeSerializer(decl.Serializer, decl.Type);
            }
            return s;
        }

        public XElement Serialize(object obj, XSerializationState state)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            Debug.Assert(rootType != null);
            if (!rootType.IsInstanceOfType(obj))
                throw new InvalidCastException(string.Format(Prompts.InvalidObjectType, obj.GetType(), rootType));
            var s = registeredTypes.GetSerializer(obj.GetType());
            return s.Serializer.SerializeXElement(rootName ?? s.Name, obj, null, state);
        }

        /// <summary>
        /// Scan the specified type and build TypeSerializer for incoming serialzation.
        /// </summary>
        private void ImplementTypeSerializer(TypeSerializer serializer, Type t)
        {
            Debug.Assert(serializer != null && t != null);
            var kind = serializer.SerializableKind;
            if (kind == TypeSerializableKind.Complex || kind == TypeSerializableKind.Collection)
            {
                MemberSerializer msInfo = null;
                //序列化属性/字段。
                foreach (var member in t.GetMembers(BindingFlags.GetProperty | BindingFlags.GetField
                    | BindingFlags.Public | BindingFlags.Instance))
                {
                    //杂项元素。
                    var anyElemAttr = member.GetCustomAttribute<XAnyElementAttribute>();
                    if (anyElemAttr != null)
                    {
                        if (kind == TypeSerializableKind.Collection)
                            throw new InvalidOperationException(string.Format(
                                Prompts.CollectionPropertyElementNotSupported, member));
                        SerializationHelper.AssertKindOf(typeof (IEnumerable<XElement>),
                            SerializationHelper.GetMemberValueType(member));
                        serializer.RegisterAnyElementMember(member);
                    }
                    //杂项属性。
                    var anyAttrAttr = member.GetCustomAttribute<XmlAnyAttributeAttribute>();
                    if (anyAttrAttr != null)
                    {
                        if (anyElemAttr != null)
                            throw new InvalidOperationException(string.Format(
                                Prompts.InvalidAttributesCombination, member));
                        SerializationHelper.AssertKindOf(typeof(IEnumerable<XAttribute>),
                            SerializationHelper.GetMemberValueType(member));
                       serializer.RegisterAnyAttributeMember(member);
                    }
                    //元素。
                    var eattr = member.GetCustomAttribute<XElementAttribute>();
                    if (eattr != null)
                    {
                        if (kind == TypeSerializableKind.Collection)
                            throw new InvalidOperationException(string.Format(
                                Prompts.CollectionPropertyElementNotSupported, member));
                        if (anyElemAttr != null || anyAttrAttr != null)
                            throw new InvalidOperationException(string.Format(
                                Prompts.InvalidAttributesCombination, member));
                        msInfo = serializer.RegisterMember(SerializationHelper.GetName(member, eattr), member, false);
                    }
                    //属性。
                    var aattr = member.GetCustomAttribute<XAttributeAttribute>();
                    if (aattr != null)
                    {
                        if (eattr != null || anyElemAttr != null || anyAttrAttr != null)
                            throw new InvalidOperationException(string.Format(
                                Prompts.InvalidAttributesCombination, member));
                        msInfo = serializer.RegisterMember(SerializationHelper.GetName(member, aattr), member, true);
                    }
                    if (msInfo != null)
                    {
                        //注册返回类型。
                        var vType = SerializationHelper.GetMemberValueType(member);
                        var vSeri = GetOrDeclareSerializer(vType);
                        msInfo.RegisterValueSerializer(vSeri.Serializer);
                        if (vSeri.Serializer.SerializableKind == TypeSerializableKind.Collection)
                        {
                            // IEnumerable<T> => T
                            var viType = SerializationHelper.GetCollectionItemType(vType);
                            var viTypeRegistered = false;
                            var childTypes = new ScopedTypeCollection();
                            foreach (var colAttr in member.GetCustomAttributes<XCollectionItemAttribute>())
                            {
                                var thisType = colAttr.Type ?? viType;
                                var thisSS = GetOrDeclareSerializer(thisType);
                                childTypes.Register(colAttr.GetName(thisSS.Name), thisType, thisSS.Serializer);
                                //当前注册的就是基类类型。
                                if (thisType == viType) viTypeRegistered = true;
                            }
                            //确保基类类型被注册。
                            if (!viTypeRegistered)
                            {
                                var viSS = GetOrDeclareSerializer(viType);
                                childTypes.Register(viSS.Name, viType, viSS.Serializer);
                            }
                            msInfo.RegisterChildItemTypes(childTypes);
                        }
                    }
                }
            }
        }

        private struct SerializerDeclaration
        {
            public Type Type;
            public TypeSerializer Serializer;
            public SerializerDeclaration(Type type, TypeSerializer serializer)
            {
                Type = type;
                Serializer = serializer;
            }
        }
        private Stack<SerializerDeclaration> serializerDecls = new Stack<SerializerDeclaration>();

        private ScopedTypeSerializer GetOrDeclareSerializer(Type t)
        {
            Debug.Assert(t != null);
            var s = registeredTypes.GetSerializer(t, true);
            if (s != null) return s;
            //准备声明 ScopedTypeSerializer
            var name = SerializationHelper.GetName(t);
            TypeSerializableKind kind;
            if (SerializationHelper.IsSimpleType(t))
                kind = TypeSerializableKind.Simple;
            else if (typeof(IXStringSerializable).IsAssignableFrom(t))
                kind = TypeSerializableKind.XStringSerializable;
            else if (typeof(IEnumerable).IsAssignableFrom(t))
                kind = TypeSerializableKind.Collection;
            else
                kind = TypeSerializableKind.Complex;
            var serializer = new TypeSerializer(kind);
            //处理引用项。
            foreach (var attr in t.GetCustomAttributes<XIncludeAttribute>())
                GetOrDeclareSerializer(attr.Type);
            //此时此类型已经声明了。
            serializerDecls.Push(new SerializerDeclaration(t, serializer));
            return registeredTypes.Register(name, t, serializer);
        }
    }

    internal enum TypeSerializableKind
    {
        /// <summary>
        /// Including built-in types and their Nullable forms.
        /// </summary>
        Simple = 0,
        /// <summary>
        /// Implemeted IXStringSerializable.
        /// </summary>
        XStringSerializable,
        /// <summary>
        /// Must be represented by child elements.
        /// Hence cannot be stored in XML attributes.
        /// </summary>
        Complex,
        /// <summary>
        /// Child items will be serialized and stored as child elements,
        /// while the element can have attributes.
        /// </summary>
        Collection
    }

    /// <summary>
    /// 用于对某个指定的类型进行序列化与反序列化。
    /// Perform serilization and deserilization for a specific type.
    /// </summary>
    /// <remarks>
    /// Note that this class, along with <see cref="MemberSerializer"/>, 
    /// are only used as actuators. That is, they do nothing other than
    /// serialization &amp; deserialization, nor do they expose other
    /// descriptive information. This behavior is just like
    /// dynamically-generated assemblies, which is to be adopted as a
    /// future implementation pattern.
    /// </remarks>
    internal class TypeSerializer
    {
        private TypeSerializableKind _SerializableKind;
        //private XName _Name;
        //private Type _Type;
        private Dictionary<XName, MemberSerializer> nameMemberDict = new Dictionary<XName, MemberSerializer>();
        private MemberInfo anyAttributeMember;
        private MemberInfo anyElementMember;

        public TypeSerializableKind SerializableKind
        {
            get { return _SerializableKind; }
        }

        public MemberSerializer RegisterMember(XName name, MemberInfo memberInfo, bool isAttribute)
        {
            var info = new MemberSerializer(name, memberInfo, isAttribute);
            nameMemberDict.Add(name, info);
            return info;
        }

        public void RegisterAnyAttributeMember(MemberInfo member)
        {
            anyAttributeMember = member;
        }

        public void RegisterAnyElementMember(MemberInfo member)
        {
            anyElementMember = member;
        }

        public TypeSerializer(TypeSerializableKind serializableKind)
        {
            _SerializableKind = serializableKind;
        }

        #region 序列化的执行机构 | Serialization Actuators

        internal XAttribute SerializeXAttribute(XName name, object obj, XSerializationState state)
        {
            //如果 obj == null，那么此元素应当直接不存在。
            Debug.Assert(obj != null && state != null);
            //序列化简单类型。
            switch (SerializableKind)
            {
                case TypeSerializableKind.Simple:
                    return new XAttribute(name, obj);
                case TypeSerializableKind.XStringSerializable:
                    var xstr = ((IXStringSerializable)obj).Serialize();
                    if (xstr != null) return new XAttribute(name, xstr);
                    return null;
            }
            //无法序列化复杂类型和集合。
            throw new InvalidOperationException(string.Format(Prompts.CannotSerializeAsAttribute, obj.GetType().FullName));
        }

        /// <summary>
        /// Serialize the specified object into XElement.
        /// For collecion type, child items are also serilized, using childItemTypes param.
        /// </summary>
        internal XElement SerializeXElement(XName name, object obj, ScopedTypeCollection childItemTypes, XSerializationState state)
        {
            //如果 obj == null，那么此元素应当直接不存在。
            Debug.Assert(obj != null && state != null);
            switch (_SerializableKind)
            {
                //序列化简单类型。
                case TypeSerializableKind.Simple:
                    return new XElement(name, obj);
                case TypeSerializableKind.XStringSerializable:
                    var xstr = ((IXStringSerializable)obj).Serialize();
                    if (xstr != null) return new XElement(name, xstr);
                    return null;
                //序列化复杂类型。
                case TypeSerializableKind.Collection:
                case TypeSerializableKind.Complex:
                    var element = new XElement(name);
                    //Any
                    if (anyAttributeMember != null)
                    {
                        foreach (var attr in (IEnumerable)SerializationHelper.GetMemberValue(anyAttributeMember, obj))
                        {
                            element.Add(attr);
                        }
                    }
                    if (anyElementMember != null)
                    {
                        Debug.Assert(_SerializableKind != TypeSerializableKind.Collection);
                        foreach (var elem in (IEnumerable)SerializationHelper.GetMemberValue(anyElementMember, obj))
                        {
                            element.Add(elem);
                        }
                    }
                    //循环引用检测。
                    state.EnterObjectSerialization(obj);
                    try
                    {
                        //集合。
                        if (_SerializableKind == TypeSerializableKind.Collection)
                        {
                            //忽略掉为 null 的项目
                            foreach (var item in (IEnumerable) obj)
                            {
                                if (item == null) continue;
                                var itemSS = childItemTypes.GetSerializer(item.GetType());
                                element.Add(itemSS.Serializer.SerializeXElement(itemSS.Name, item, null, state));
                            }
                        }
                        //成员。
                        foreach (var entry in nameMemberDict)
                        {
                            //注意 Add 函数允许 null 输入。
                            element.Add(entry.Value.Serialize(obj, state));
                        }
                    }
                    finally
                    {
                        state.ExitObjectSerialization(obj);
                    }
                    return element; 
            }
            Debug.Assert(false);
            return null;
        }
        #endregion
    }

    /// <summary>
    /// 用于对类型中的某个成员进行序列化与反序列化。
    /// Perform serilization and deserilization for a specific member of a type.
    /// </summary>
    internal class MemberSerializer
    {
        private MemberInfo _MemberInfo;
        private XName _Name;
        private bool _IsAttribute;
        private ScopedTypeCollection _RegisteredChildTypes;
        private TypeSerializer _ValueSerializer;

        public MemberInfo MemberInfo
        {
            get { return _MemberInfo; }
        }

        public XName Name
        {
            get { return _Name; }
        }

        /// <summary>
        /// Specify whether this member should be serialized as attribute.
        /// If not, it will be serialized as a child element.
        /// </summary>
        public bool IsAttribute
        {
            get { return _IsAttribute; }
        }

        /// <summary>
        /// Register the serializer for the type of field/property.
        /// </summary>
        public void RegisterValueSerializer(TypeSerializer info)
        {
            _ValueSerializer = info;
        }

        /// <summary>
        /// (Of collection) Register child item types.
        /// </summary>
        public void RegisterChildItemTypes(ScopedTypeCollection types)
        {
            _RegisteredChildTypes = types;
        }

        internal MemberSerializer(XName name, MemberInfo memberInfo, bool isAttribute)
        {
            _Name = name;
            _MemberInfo = memberInfo;
            _IsAttribute = isAttribute;
        }

        public override string ToString()
        {
            return _MemberInfo.ToString();
        }

        /// <summary>
        /// 根据 MemberSerializer 的设置，序列化指定实例的字段/属性值。
        /// Serialize the one field / property according to current MemberSerializer
        /// of specified object into corresponding XElement or XAttribute.
        /// </summary>
        internal XObject Serialize(object obj, XSerializationState state)
        {
            var v = SerializationHelper.GetMemberValue(_MemberInfo, obj);
            if (v == null) return null;
            if (_IsAttribute)
                return _ValueSerializer.SerializeXAttribute(_Name, v, state);
            return _ValueSerializer.SerializeXElement(_Name, v, _RegisteredChildTypes, state);
        }
    }

    internal class ScopedTypeSerializer
    {
        private XName _Name;
        private TypeSerializer _Serializer;

        public XName Name
        {
            get { return _Name; }
        }

        public TypeSerializer Serializer
        {
            get { return _Serializer; }
        }

        public ScopedTypeSerializer(XName name, TypeSerializer serializer)
        {
            _Name = name;
            _Serializer = serializer;
        }
    }

    /// <summary>
    /// Maintains registered type serializers and their XNames in certain scope.
    /// Note the XNames here may be different from its corrsponding TypeSerializer.Name .
    /// (e.g. for collection items)
    /// </summary>
    internal class ScopedTypeCollection
    {
        private Dictionary<XName, ScopedTypeSerializer> nameDict;
        private Dictionary<Type, ScopedTypeSerializer> typeDict;

        public ScopedTypeSerializer Register(XName name, Type type, TypeSerializer serializer)
        {
            Debug.Assert(type != null && serializer != null);
            var scoped = new ScopedTypeSerializer(name, serializer);
            Register(type, scoped);
            return scoped;
        }

        public void Register(Type type, ScopedTypeSerializer scoped)
        {
            Debug.Assert(type != null && scoped != null);
            if (nameDict == null)
            {
                nameDict = new Dictionary<XName, ScopedTypeSerializer>();
                typeDict = new Dictionary<Type, ScopedTypeSerializer>();
            }
            nameDict.Add(scoped.Name, scoped);
            typeDict.Add(type, scoped);
        }

        public ScopedTypeSerializer GetSerializer(Type t)
        {
            return GetSerializer(t, false);
        }

        public ScopedTypeSerializer GetSerializer(Type t, bool noException)
        {
            if (typeDict == null)
            {
                if (noException) return null;
            }
            else
            {
                ScopedTypeSerializer s;
                if (typeDict.TryGetValue(t, out s))
                    return s;
                if (noException) return null;
            }
            throw new NotSupportedException(string.Format(Prompts.UnregisteredType, t));
        }

        public ScopedTypeSerializer GetSerializer(XName name)
        {
            if (nameDict == null) goto EXCEPTION;
            try
            {
                return nameDict[name];
            }
            catch (KeyNotFoundException ex)
            { }
            EXCEPTION:
            throw new NotSupportedException(string.Format(Prompts.UnregisteredType, name));
        }

        public ScopedTypeCollection()
        {
            
        }
    }
}
