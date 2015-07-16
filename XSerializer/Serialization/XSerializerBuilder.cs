using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using E = System.Linq.Expressions.Expression;

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
    internal class XSerializerBuilder
    {
        private Dictionary<Type, TypeSerializer> typeDict;
        private SerializationScope _GlobalScope;
        private Type rootType;
        private XName rootName;

        internal SerializationScope GlobalScope
        {
            get { return _GlobalScope; }
        }

        /// <summary>
        /// 注册指定的类型作为XML文档的根节点。
        /// Register the specified type as XML root element in serailization.
        /// </summary>
        public void RegisterRootType(Type t)
        {
            if (t == null) throw new ArgumentNullException("t");
            if (rootType != null) throw new InvalidOperationException();
            RegisterType(t);
            var rootAttr = t.GetCustomAttribute<XRootAttribute>();
            rootName = _GlobalScope.GetName(t);
            if (rootAttr != null) rootName = rootAttr.GetName(rootName);
            rootType = t;
        }

        /// <summary>
        /// 注册指定的类型，以便在序列化中使用。
        /// Register the specified type, which can be used in serailization.
        /// </summary>
        /// <remarks>
        /// 如果指定的类型已经注册，则不会进行任何操作。
        /// Does nothing if the specified type has already been registered.
        /// </remarks>
        public void RegisterType(Type t)
        {
            DeclareType(t);
            while (serializerDecls.Count > 0)
            {
                var decl = serializerDecls.Pop();
                ImplementTypeSerializer(decl.Serializer, decl.Type);
            }
        }

        #region 可序列化类型的注册 | Declaration & Implementation of Type Serializers

        private static MethodInfo IEnumerable_GetEnumerator = typeof(IEnumerable).GetMethod("GetEnumerator");
        private static PropertyInfo IEnumerator_Current = typeof(IEnumerator).GetProperty("Current");
        private static MethodInfo IEnumerator_MoveNext = typeof(IEnumerator).GetMethod("MoveNext");

        
        private void Register(Type type, TypeSerializer serializer)
        {
            Debug.Assert(type != null && serializer != null);
            typeDict.Add(type, serializer);
        }

        public TypeSerializer GetSerializer(Type t)
        {
            return GetSerializer(t, false);
        }

        public TypeSerializer GetSerializer(Type t, bool noException)
        {
            TypeSerializer s;
            if (typeDict.TryGetValue(t, out s))
                return s;
            return null;
        }

        private void DeclareType(Type t)
        {
            Debug.Assert(t != null);
            if (_GlobalScope.GetName(t) != null) return;
            //将此类型加入全局作用域。
            GlobalScope.AddType(SerializationHelper.GetName(t), t);
            if (SerializationHelper.IsSimpleType(t) || typeof (IXStringSerializable).IsAssignableFrom(t))
                return;
            TypeSerializableKind kind;
            if (typeof(IEnumerable).IsAssignableFrom(t))
                kind = TypeSerializableKind.Collection;
            else
                kind = TypeSerializableKind.Complex;
            var serializer = new TypeSerializer(t, kind);
            //处理引用项。
            foreach (var attr in t.GetCustomAttributes<XIncludeAttribute>())
                DeclareType(attr.Type);
            //此时此类型已经声明了。
            serializerDecls.Push(new SerializerDeclaration(t, serializer));
            Register(t, serializer);
        }

        private void BuildCollectionSerializer(Type t)
        {
            //void (XName, object, XSerializationState)
            var expr = new List<Expression>();
            var argElement = E.Variable(typeof(XElement), "element");
            var argObj = E.Variable(typeof(object), "obj");
            var argState = E.Variable(typeof(XSerializationState), "state");
            //if (kind == TypeSerializableKind.Collection)
            var localEnum = E.Variable(typeof(IEnumerator), "myEnum");
            var labelExitFor1 = E.Label("exitFor1");
            var localCurrent = E.Variable(typeof(object), "current");
            expr.Add(E.Assign(localEnum, E.Call(argObj, IEnumerable_GetEnumerator)));

        }

        private Expression ForEach(ParameterExpression i, Expression enumerable, E body)
        {
            var localEnum = E.Variable(enumerable.Type, "myEnumerable");
            return E.Block(new[] {localEnum}, E.Assign(localEnum, enumerable), ForEach(i, localEnum, body));
        }

        private Expression ForEach(ParameterExpression i, ParameterExpression enumerable, E body)
        {
            var localEnum = E.Variable(typeof(IEnumerator), "myEnumerator");
            var labelExitFor = E.Label("exitForEach");
            Debug.Assert(i.Type == typeof(object));
            return E.Block(new[] {localEnum, i},
                E.Assign(localEnum, E.Call(enumerable, IEnumerable_GetEnumerator)),
                E.Loop(E.IfThenElse(E.Call(localEnum, IEnumerator_MoveNext),
                    E.Block(E.Assign(i, E.Property(localEnum, IEnumerator_Current)), body),
                    E.Break(labelExitFor)), labelExitFor));
        }

        private static ConstructorInfo XElement_Constructor2 =
            typeof(XElement).GetConstructor(new[] { typeof(XName), typeof(object) });
        private static ConstructorInfo XAttribute_Constructor2 =
            typeof(XAttribute).GetConstructor(new[] { typeof(XName), typeof(object) });
        //private static MethodInfo XElement_SetAttributeValue =
        //    typeof(XElement).GetMethod("SetAttributeValue", new[] { typeof(XName), typeof(object) });
        private static MethodInfo IXStringSerializable_Serialize = typeof(IXStringSerializable).GetMethod("Serialize");
        private static MethodInfo IXStringSerializable_Deserialize = typeof(IXStringSerializable).GetMethod("Deserialize");

        /// <summary>
        /// Scan the specified type and build TypeSerializer for incoming serialzation.
        /// </summary>
        private void ImplementTypeSerializer(TypeSerializer serializer, Type t)
        {
            Debug.Assert(serializer != null && t != null);
            var kind = serializer.SerializableKind;
            Debug.Assert(kind != TypeSerializableKind.Simple && kind != TypeSerializableKind.XStringSerializable);
            //void (XName, object, XSerializationState)
            // Arguments
            var argElement = E.Variable(typeof(XElement), "element");
            var argObj = E.Variable(t, "obj");
            var argState = E.Variable(typeof(XSerializationState), "state");
            // Locals
            var localElement = E.Parameter(typeof(XElement), "localElement");
            var locals = new[] {localElement};
            // Expressions
            var expr = new List<Expression>();
            //序列化集合内容。 Serialize collection items.
            //expr.Add(E.IfThen(E.TypeIs(argObj, t).Invert(),
            //    E.Throw(E.Constant(new ArgumentException("Invalid argument type.")))));
            if (kind == TypeSerializableKind.Collection)
            {
                var localEachItem = E.Variable(typeof(object), "eachItem");
                expr.Add(ForEach(localEachItem, argObj,
                    E.IfThen(E.NotEqual(localEachItem, E.Constant(null)),
                        argElement.CallMember("Add", argState.CallMember("SerializeXElement", localEachItem)))));
                var viType = SerializationHelper.GetCollectionItemType(t);
                DeclareType(viType);
                //serializer.RegisterAddItemMethod(null);
            }
            //序列化属性/字段。 Serialize property / field.
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
                    var localEachElem = E.Variable(typeof(object), "eachElem");
                    expr.Add(ForEach(localEachElem,argObj.Member(member),
                        E.Call(argElement, "Add", null, new Expression[] { localEachElem })));
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
                    var localEachAttr = E.Variable(typeof(object), "eachAttr");
                    expr.Add(ForEach(localEachAttr, argObj.Member(member),
                        E.Call(argElement, "Add", null, new Expression[] { localEachAttr })));
                }
                //元素。
                var eattr = member.GetCustomAttribute<XElementAttribute>();
                if (eattr != null)
                {
                    if (anyElemAttr != null || anyAttrAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    var memberType = SerializationHelper.GetMemberValueType(member);
                    var memberKind = SerializationHelper.GetSerializationKind(memberType);
                    switch (memberKind)
                    {
                        case TypeSerializableKind.Simple:
                            expr.Add(localElement.AssignFrom(E.New(XElement_Constructor2,
                                E.Constant(SerializationHelper.GetName(member, eattr)),
                                argObj.Member(member).Cast<object>())));
                            break;
                        case TypeSerializableKind.XStringSerializable:
                            expr.Add(localElement.AssignFrom(E.New(XElement_Constructor2,
                                E.Constant(SerializationHelper.GetName(member, eattr)),
                                argObj.Member(member).CallMember(IXStringSerializable_Serialize))));
                            break;
                        case TypeSerializableKind.Collection:
                        case TypeSerializableKind.Complex:
                            DeclareType(memberType);
                            SerializationScope scope = null;
                            if (memberKind == TypeSerializableKind.Collection)
                            {
                                //注册集合项目的类型。
                                //Register types of ollection items.
                                // IEnumerable<T> => T
                                var viType = SerializationHelper.GetCollectionItemType(memberType);
                                scope = new SerializationScope(member + " (" + member.DeclaringType + ")");
                                foreach (var colAttr in member.GetCustomAttributes<XCollectionItemAttribute>())
                                {
                                    var thisType = colAttr.Type ?? viType;
                                    scope.AddType(colAttr.GetName(SerializationHelper.GetName(thisType)), thisType);
                                    DeclareType(t);
                                }
                            }
                            expr.Add(localElement.AssignFrom(E.New(XElement_Constructor2,
                                E.Constant(SerializationHelper.GetName(member, eattr)), E.Constant(null))));
                            expr.Add(argElement.CallMember("Add",
                                argState.CallMember("SerializeXElement", argObj.Member(member),
                                    E.Constant(SerializationHelper.GetName(member, eattr)),
                                    E.Constant(scope))));
                            break;
                    }
                }
                //属性。
                var aattr = member.GetCustomAttribute<XAttributeAttribute>();
                if (aattr != null)
                {
                    if (eattr != null || anyElemAttr != null || anyAttrAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    var memberType = SerializationHelper.GetMemberValueType(member);
                    switch (SerializationHelper.GetSerializationKind(memberType))
                    {
                        case TypeSerializableKind.Simple:
                            expr.Add(argElement.CallMember("SetAttributeValue",
                                E.Constant(SerializationHelper.GetName(member, aattr)),
                                argObj.Member(member).Cast<object>()));
                            break;
                        case TypeSerializableKind.XStringSerializable:
                            expr.Add(argElement.CallMember("SetAttributeValue",
                                E.Constant(SerializationHelper.GetName(member, aattr)),
                                argObj.Member(member).CallMember(IXStringSerializable_Serialize)));
                            break;
                        case TypeSerializableKind.Collection:
                        case TypeSerializableKind.Complex:
                            //无法序列化复杂类型和集合。
                            throw new InvalidOperationException(string.Format(Prompts.CannotSerializeAsAttribute, member));
                    }
                }
            }
            var block = E.Block(typeof (void), locals, expr);
            serializer.RegisterSerializeXElementAction(E.Lambda(block, argElement, argObj, argState).Compile());
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

        public XSerializerBuilder()
        {
            typeDict = new Dictionary<Type, TypeSerializer>();
            _GlobalScope = new SerializationScope("global");
            foreach (var t in SerializationHelper.SimpleTypes)
                _GlobalScope.AddType(SerializationHelper.GetName(t), t);
        }

        #endregion

        public XElement Serialize(object obj, object context)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            Debug.Assert(rootType != null);
            if (!rootType.IsInstanceOfType(obj))
                throw new InvalidCastException(string.Format(Prompts.InvalidObjectType, obj.GetType(), rootType));
            var state = new XSerializationState(context, this);
            return state.SerializeXElement(obj, rootName ?? GlobalScope.GetName(rootType), null);
        }

        public object Deserialize(XElement e, XSerializationState state)
        {
            //if (e == null) throw new ArgumentNullException("e");
            //var s = registeredTypes.GetSerializer(rootType);
            //var obj = s.Serializer.Deserialize(e, null, state);
            //Debug.Assert(rootType.IsInstanceOfType(obj));
            //return obj;
            return null;
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
        // Complex / Collection
        //<XElement, T, XSerializationState>
        private Delegate serialzeXElementAction;


        public TypeSerializableKind SerializableKind
        {
            get { return _SerializableKind; }
        }

        public void RegisterSerializeXElementAction(Delegate d)
        {
            Debug.Assert(d != null);
            serialzeXElementAction = d;
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

        internal void Serialize(XElement e, object obj, XSerializationState state)
        {
            serialzeXElementAction.DynamicInvoke(e, obj, state);
        }

        private MemberSerializer TryGetSerializer(XName name)
        {
            MemberSerializer s;
            if (nameMemberDict.TryGetValue(name, out s))
                return s;
            return null;
        }

        internal object Deserialize(XObject elementOrAttribute, SerializationScope childItemTypes, XSerializationState state)
        {
            //Debug.Assert(elementOrAttribute is XElement || elementOrAttribute is XAttribute);
            //switch (_SerializableKind)
            //{
            //    case TypeSerializableKind.Simple:
            //        if (_Type == typeof(object)) return new object();
            //        if (elementOrAttribute is XAttribute)
            //            return xAttributeExplicitOperator.Invoke(null, new object[] { elementOrAttribute });
            //        return xElementExplicitOperator.Invoke(null, new object[] { elementOrAttribute });
            //    case TypeSerializableKind.XStringSerializable:
            //        var attr = elementOrAttribute as XAttribute;
            //        var str = attr != null
            //            ? attr.Value
            //            : ((XElement)elementOrAttribute).Value;
            //        var xss = (IXStringSerializable)Activator.CreateInstance(_Type);
            //        xss.Deserialize(str);
            //        return xss;
            //    case TypeSerializableKind.Collection:
            //    case TypeSerializableKind.Complex:
            //        object obj = null;
            //        var element = (XElement)elementOrAttribute;
            //        //集合。
            //        if (_SerializableKind == TypeSerializableKind.Collection)
            //        {
            //            if (addItemMethod != null)
            //            {
            //                obj = Activator.CreateInstance(_Type);
            //                InPlaceDeserialize(obj, elementOrAttribute, childItemTypes, state);
            //            }
            //            else
            //            {
            //                var itemType = SerializationHelper.GetCollectionItemType(_Type);
            //                if (_Type.IsArray || _Type.IsInterface)
            //                {
            //                    var surrogateType = typeof(List<>).MakeGenericType(itemType);
            //                    if (_Type.IsInterface)
            //                        SerializationHelper.AssertKindOf(_Type, surrogateType);
            //                    // 需要进行代理。
            //                    var surrogateCollection = (IList)Activator.CreateInstance(surrogateType);
            //                    foreach (var item in element.Elements())
            //                    {
            //                        var ts = childItemTypes.GetSerializer(item.Name);
            //                        var itemObj = ts.Serializer.Deserialize(item, null, state);
            //                        surrogateCollection.Add(itemObj);
            //                    }
            //                    if (_Type.IsArray)
            //                    {
            //                        var array = Array.CreateInstance(itemType, surrogateCollection.Count);
            //                        surrogateCollection.CopyTo(array, 0);
            //                        obj = array;
            //                    }
            //                    else
            //                    {
            //                        obj = surrogateCollection;
            //                    }
            //                }
            //                else
            //                {
            //                    throw new NotSupportedException(string.Format(Prompts.CollectionCannotAddItem, _Type));
            //                    //obj = Activator.CreateInstance(_Type);
            //                    //var list = obj as IList;
            //                    //if (list != null && list.IsReadOnly) goto SURROGATE;
            //                }
            //            }
            //        }
            //        else
            //        {
            //            obj = Activator.CreateInstance(_Type);
            //            //成员。
            //            var anyElements = new List<XElement>();
            //            foreach (var item in element.Elements())
            //            {
            //                var s = TryGetSerializer(item.Name);
            //                if (s != null)
            //                    s.Deserialize(obj, item, state);
            //                else
            //                    anyElements.Add(item);
            //            }
            //            if (anyElementMember != null)
            //            {
            //                SerializationHelper.SetMemberValue(anyElementMember, obj,
            //                    anyElements.Select(e => new XElement(e)).ToArray());
            //            }
            //        }
            //        return obj;
            //}
            //Debug.Assert(false);
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
            SerializationScope childItemTypes,
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
                    //var ts = childItemTypes.GetSerializer(item.Name);
                    //var itemObj = ts.Serializer.Deserialize(item, null, state);
                    //addItemMethod.Invoke(currentValue, new[] { itemObj });
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
        private SerializationScope _RegisteredChildTypes;
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
        public void RegisterChildItemTypes(SerializationScope types)
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

    /// <summary>
    /// Maintains registered type serializers and their XNames in certain scope.
    /// Note the XNames here may be different from its corrsponding TypeSerializer.Name .
    /// (e.g. for collection items)
    /// </summary>
    internal class SerializationScope
    {
        private Dictionary<XName, Type> nameTypeDict = new Dictionary<XName, Type>();
        private Dictionary<Type, XName> typeNameDict = new Dictionary<Type, XName>();
        private string _ScopeName;

        public void AddType(XName name, Type type)
        {
            Debug.Assert(type != null && name != null);
            nameTypeDict.Add(name, type);
            typeNameDict.Add(type, name);
        }

        public XName GetName(Type t)
        {
            XName n;
            if (typeNameDict.TryGetValue(t, out n))
                return n;
            return null;
        }

        public Type GetType(XName n, bool noException)
        {
            Type t;
            if (nameTypeDict.TryGetValue(n, out t))
                return t;
            if (noException) return null;
            throw new NotSupportedException(string.Format(Prompts.UnregisteredType, n, _ScopeName));
        }

        public override string ToString()
        {
            return _ScopeName;
        }

        public SerializationScope(string scopeName)
        {
            _ScopeName = scopeName;
        }
    }
}
