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
        private ScopedTypeCollection registeredTypes = new ScopedTypeCollection("global");
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

        #region 可序列化类型的注册 | Declaration & Implementation of Type Serializers
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
            var serializer = new TypeSerializer(t, kind);
            //处理引用项。
            foreach (var attr in t.GetCustomAttributes<XIncludeAttribute>())
                GetOrDeclareSerializer(attr.Type);
            //此时此类型已经声明了。
            serializerDecls.Push(new SerializerDeclaration(t, serializer));
            return registeredTypes.Register(name, t, serializer);
        }
        /// <summary>
        /// Scan the specified type and build TypeSerializer for incoming serialzation.
        /// </summary>
        private void ImplementTypeSerializer(TypeSerializer serializer, Type t)
        {
            Debug.Assert(serializer != null && t != null);
            var kind = serializer.SerializableKind;
            switch (kind)
            {
                case TypeSerializableKind.Simple:
                    if (t != typeof(object))
                    {
                        serializer.RegisterXObjectCastings(
                            SerializationHelper.GetExplicitOperator(typeof(XAttribute), t),
                            SerializationHelper.GetExplicitOperator(typeof(XElement), t)
                            );
                    }
                    break;
                case TypeSerializableKind.Collection:
                case TypeSerializableKind.Complex:
                    if (kind == TypeSerializableKind.Collection)
                    {
                        if (t.IsArray)
                        {
                            serializer.RegisterAddItemMethod(null);
                        }
                        else if (typeof(IList).IsAssignableFrom(t))
                        {
                            var map = t.GetInterfaceMap(typeof(IList));
                            var addMethodIndex = Array.FindIndex(map.InterfaceMethods, m => m.Name == "Add");
                            Debug.Assert(addMethodIndex >= 0);
                            serializer.RegisterAddItemMethod(map.TargetMethods[addMethodIndex]);
                        }
                        else
                        {
                            // search for public Add function
                            // bug : cannot get "Add" method for IList<T>
                            var addMethod = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .FirstOrDefault(m =>
                                {
                                    if (m.Name != "Add") return false;
                                    var p = m.GetParameters();
                                    if (p.Length != 1) return false;
                                    return p[0].IsIn && p[0].ParameterType.IsAssignableFrom(t);
                                });
                            serializer.RegisterAddItemMethod(addMethod);
                        }
                    }
                    //序列化属性/字段。
                    var bindingFlags = BindingFlags.GetProperty | BindingFlags.GetField
                                       | BindingFlags.Public | BindingFlags.Instance;
                    {
                        var typeAttr = t.GetCustomAttribute<XTypeAttribute>();
                        if (typeAttr != null)
                            bindingFlags |= BindingFlags.NonPublic;
                    }
                    foreach (var member in t.GetMembers(bindingFlags))
                    {
                        MemberSerializer msInfo = null;
                        //杂项元素。
                        var anyElemAttr = member.GetCustomAttribute<XAnyElementAttribute>();
                        if (anyElemAttr != null)
                        {
                            if (kind == TypeSerializableKind.Collection)
                                throw new InvalidOperationException(string.Format(
                                    Prompts.CollectionPropertyElementNotSupported, member));
                            SerializationHelper.AssertKindOf(typeof(IEnumerable<XElement>),
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
                                var childTypes = new ScopedTypeCollection(member + " (" + member.DeclaringType + ")");
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
                    break;
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

        #endregion

        public XElement Serialize(object obj, XSerializationState state)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            Debug.Assert(rootType != null);
            if (!rootType.IsInstanceOfType(obj))
                throw new InvalidCastException(string.Format(Prompts.InvalidObjectType, obj.GetType(), rootType));
            var s = registeredTypes.GetSerializer(obj.GetType());
            return s.Serializer.SerializeXElement(rootName ?? s.Name, obj, null, state);
        }

        public object Deserialize(XElement e, XSerializationState state)
        {
            if (e == null) throw new ArgumentNullException("e");
            var s = registeredTypes.GetSerializer(rootType);
            var obj = s.Serializer.Deserialize(e, null, state);
            Debug.Assert(rootType.IsInstanceOfType(obj));
            return obj;
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
        private Type _Type;
        private Dictionary<XName, MemberSerializer> nameMemberDict = new Dictionary<XName, MemberSerializer>();
        // Complex / Collection
        private MemberInfo anyAttributeMember;
        // Complex
        private MemberInfo anyElementMember;
        // Collection
        private MethodBase addItemMethod;
        // Simple
        private MethodBase xElementExplicitOperator;
        private MethodBase xAttributeExplicitOperator;


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

        /// <summary>
        /// (Of simple types) Register explicit conversion operators
        /// used to convert XObject to corresponding value.
        /// </summary>
        public void RegisterXObjectCastings(MethodBase attr, MethodBase elem)
        {
            Debug.Assert(attr != null && elem != null);
            xAttributeExplicitOperator = attr;
            xElementExplicitOperator = elem;
        }

        public void RegisterAddItemMethod(MethodBase member)
        {
            // null : 集合无法添加项目。
            addItemMethod = member;
        }

        public TypeSerializer(Type type, TypeSerializableKind serializableKind)
        {
            _Type = type;
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
                    return xstr != null ? new XElement(name, xstr) : null;
                case TypeSerializableKind.Collection:
                case TypeSerializableKind.Complex:
                    var element = new XElement(name);
                    //Any
                    if (anyAttributeMember != null)
                    {
                        foreach (var attr in (IEnumerable)SerializationHelper.GetMemberValue(anyAttributeMember, obj))
                            element.Add(attr);
                    }
                    if (anyElementMember != null)
                    {
                        Debug.Assert(_SerializableKind != TypeSerializableKind.Collection);
                        foreach (var elem in (IEnumerable)SerializationHelper.GetMemberValue(anyElementMember, obj))
                            element.Add(elem);
                    }
                    //循环引用检测。
                    state.EnterObjectSerialization(obj);
                    try
                    {
                        //集合。
                        if (_SerializableKind == TypeSerializableKind.Collection)
                        {
                            //忽略掉为 null 的项目
                            foreach (var item in (IEnumerable)obj)
                            {
                                if (item == null) continue;
                                var itemSS = childItemTypes.GetSerializer(item.GetType());
                                element.Add(itemSS.Serializer.SerializeXElement(itemSS.Name, item, null, state));
                            }
                        }
                        //成员。
                        //注意 Add 函数允许 null 输入。
                        foreach (var entry in nameMemberDict)
                            element.Add(entry.Value.Serialize(obj, state));
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

        private MemberSerializer TryGetSerializer(XName name)
        {
            MemberSerializer s;
            if (nameMemberDict.TryGetValue(name, out s))
                return s;
            return null;
        }

        internal object Deserialize(XObject elementOrAttribute, ScopedTypeCollection childItemTypes, XSerializationState state)
        {
            Debug.Assert(elementOrAttribute is XElement || elementOrAttribute is XAttribute);
            switch (_SerializableKind)
            {
                case TypeSerializableKind.Simple:
                    if (_Type == typeof(object)) return new object();
                    if (elementOrAttribute is XAttribute)
                        return xAttributeExplicitOperator.Invoke(null, new object[] { elementOrAttribute });
                    return xElementExplicitOperator.Invoke(null, new object[] { elementOrAttribute });
                case TypeSerializableKind.XStringSerializable:
                    var attr = elementOrAttribute as XAttribute;
                    var str = attr != null
                        ? attr.Value
                        : ((XElement)elementOrAttribute).Value;
                    var xss = (IXStringSerializable)Activator.CreateInstance(_Type);
                    xss.Deserialize(str);
                    return xss;
                case TypeSerializableKind.Collection:
                case TypeSerializableKind.Complex:
                    object obj = null;
                    var element = (XElement)elementOrAttribute;
                    //集合。
                    if (_SerializableKind == TypeSerializableKind.Collection)
                    {
                        if (addItemMethod != null)
                        {
                            obj = Activator.CreateInstance(_Type);
                            InPlaceDeserialize(obj, elementOrAttribute, childItemTypes, state);
                        }
                        else
                        {
                            var itemType = SerializationHelper.GetCollectionItemType(_Type);
                            if (_Type.IsArray || _Type.IsInterface)
                            {
                                var surrogateType = typeof(List<>).MakeGenericType(itemType);
                                if (_Type.IsInterface)
                                    SerializationHelper.AssertKindOf(_Type, surrogateType);
                                // 需要进行代理。
                                var surrogateCollection = (IList)Activator.CreateInstance(surrogateType);
                                foreach (var item in element.Elements())
                                {
                                    var ts = childItemTypes.GetSerializer(item.Name);
                                    var itemObj = ts.Serializer.Deserialize(item, null, state);
                                    surrogateCollection.Add(itemObj);
                                }
                                if (_Type.IsArray)
                                {
                                    var array = Array.CreateInstance(itemType, surrogateCollection.Count);
                                    surrogateCollection.CopyTo(array, 0);
                                    obj = array;
                                }
                                else
                                {
                                    obj = surrogateCollection;
                                }
                            }
                            else
                            {
                                throw new NotSupportedException(string.Format(Prompts.CollectionCannotAddItem, _Type));
                                //obj = Activator.CreateInstance(_Type);
                                //var list = obj as IList;
                                //if (list != null && list.IsReadOnly) goto SURROGATE;
                            }
                        }
                    }
                    else
                    {
                        obj = Activator.CreateInstance(_Type);
                        //成员。
                        var anyElements = new List<XElement>();
                        foreach (var item in element.Elements())
                        {
                            var s = TryGetSerializer(item.Name);
                            if (s != null)
                                s.Deserialize(obj, item, state);
                            else
                                anyElements.Add(item);
                        }
                        if (anyElementMember != null)
                        {
                            SerializationHelper.SetMemberValue(anyElementMember, obj,
                                anyElements.Select(e => new XElement(e)).ToArray());
                        }
                    }
                    return obj;
            }
            Debug.Assert(false);
            return null;
        }

        internal bool SupportsInPlaceDeserialize(object currentValue)
        {
            switch (_SerializableKind)
            {
                case TypeSerializableKind.XStringSerializable:
                case TypeSerializableKind.Simple:
                    return false;
                case TypeSerializableKind.Complex:
                    return !_Type.IsValueType;
                case TypeSerializableKind.Collection:
                    if (addItemMethod == null) return false;
                    var list = currentValue as IList;
                    if (list != null) return !list.IsReadOnly;
                    return true;
            }
            Debug.Assert(false);
            return false;
        }

        internal object InPlaceDeserialize(object currentValue, XObject elementOrAttribute,
            ScopedTypeCollection childItemTypes,
            XSerializationState state)
        {
            Debug.Assert(currentValue != null);
            Debug.Assert(_SerializableKind == TypeSerializableKind.Collection ||
                         _SerializableKind == TypeSerializableKind.Complex);
            Debug.Assert(elementOrAttribute is XElement || elementOrAttribute is XAttribute);
            var element = (XElement)elementOrAttribute;
            //集合。
            if (_SerializableKind == TypeSerializableKind.Collection)
            {
                var itemType = SerializationHelper.GetCollectionItemType(_Type);
                Debug.Assert(addItemMethod != null);
                foreach (var item in element.Elements())
                {
                    var ts = childItemTypes.GetSerializer(item.Name);
                    var itemObj = ts.Serializer.Deserialize(item, null, state);
                    addItemMethod.Invoke(currentValue, new[] { itemObj });
                }
            }
            else
            {
                currentValue = Activator.CreateInstance(_Type);
                //成员。
                var anyElements = new List<XElement>();
                foreach (var item in element.Elements())
                {
                    var s = TryGetSerializer(item.Name);
                    if (s != null)
                        s.Deserialize(currentValue, item, state);
                    else
                        anyElements.Add(item);
                }
                if (anyElementMember != null)
                {
                    SerializationHelper.SetMemberValue(anyElementMember, currentValue,
                        anyElements.Select(e => new XElement(e)).ToArray());
                }
            }
            //成员。
            var anyAttributes = new List<XAttribute>();
            foreach (var item in element.Attributes())
            {
                var s = TryGetSerializer(item.Name);
                if (s != null)
                    s.Deserialize(currentValue, item, state);
                else
                    anyAttributes.Add(item);
            }
            if (anyAttributeMember != null)
            {
                SerializationHelper.SetMemberValue(anyAttributeMember, currentValue,
                    anyAttributes.Select(a => new XAttribute(a)).ToArray());
            }
            return currentValue;
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

        internal void Deserialize(object obj, XObject value, XSerializationState state)
        {
            Debug.Assert(_IsAttribute ? value is XAttribute : value is XElement);
            if (SerializationHelper.IsMemberReadOnly(_MemberInfo))
            {
                var v = SerializationHelper.GetMemberValue(_MemberInfo, obj);
                if (!_ValueSerializer.SupportsInPlaceDeserialize(v))
                    throw new NotSupportedException(string.Format(Prompts.InSituDeserializationNotSupported,
                        _MemberInfo + " (" + _MemberInfo.DeclaringType + ")", v));
                _ValueSerializer.InPlaceDeserialize(v, value, _RegisteredChildTypes, state);
            }
            else
            {
                var v = _ValueSerializer.Deserialize(value, _RegisteredChildTypes, state);
                SerializationHelper.SetMemberValue(_MemberInfo, obj, v);
            }
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
        private string scopeName;

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
            throw new NotSupportedException(string.Format(Prompts.UnregisteredType, t, scopeName));
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
            throw new NotSupportedException(string.Format(Prompts.UnregisteredType, name, scopeName));
        }

        public ScopedTypeCollection(string scopeName)
        {
            this.scopeName = scopeName;
        }
    }
}
