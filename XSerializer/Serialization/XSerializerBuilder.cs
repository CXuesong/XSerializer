using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
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
            if (SerializationHelper.IsSimpleType(t) || typeof(IXStringSerializable).IsAssignableFrom(t))
                return;
            var serializer = new TypeSerializer(t.ToString());
            //处理引用项。
            foreach (var attr in t.GetCustomAttributes<XIncludeAttribute>())
                DeclareType(attr.Type);
            //此时此类型已经声明了。
            serializerDecls.Push(new SerializerDeclaration(t, serializer));
            Register(t, serializer);
        }

        private Expression ForEach(ParameterExpression i, Expression enumerable, E body)
        {
            var localEnum = E.Variable(enumerable.Type, "myEnumerable");
            return E.Block(new[] { localEnum }, E.Assign(localEnum, enumerable), ForEach(i, localEnum, body));
        }

        private Expression ForEach(ParameterExpression i, ParameterExpression enumerable, E body)
        {
            var enumerator = enumerable.Cast(SerializationHelper.GetIEnumerable(enumerable.Type))
                .CallMember("GetEnumerator");
            var localEnum = E.Variable(enumerator.Type, "myEnumerator");
            var labelExitFor = E.Label("exitForEach");
            return E.Block(new[] { localEnum, i },
                localEnum.AssignFrom(enumerator),
                E.Loop(E.IfThenElse(localEnum.CallMember(IEnumerator_MoveNext),
                    E.Block(i.AssignFrom(localEnum.Member("Current")), body),
                    E.Break(labelExitFor)), labelExitFor));
        }

        private static MethodInfo IEnumerator_MoveNext = typeof(IEnumerator).GetMethod("MoveNext");
        private static MethodInfo IXStringSerializable_Serialize = typeof(IXStringSerializable).GetMethod("Serialize");
        private static MethodInfo IXStringSerializable_Deserialize = typeof(IXStringSerializable).GetMethod("Deserialize");

        private static ConstructorInfo GetTypeConstructor(Type t)
        {
            if (t.IsArray) return null;
            var constr = t.GetConstructor(Type.EmptyTypes);
            if (constr != null) return constr;
            //为接口寻找合适的实现类型。
            if (typeof(IEnumerable).IsAssignableFrom(t))
            {
                var viType = SerializationHelper.GetCollectionItemType(t);
                // General Lists
                var listType = viType == typeof(object) ? typeof(ArrayList) : typeof(List<>).MakeGenericType(viType);
                if (t.IsAssignableFrom(listType)) return listType.GetConstructor((Type.EmptyTypes));
            }
            return null;
        }

        // (RUNTIME) Construct or reuse existing object.
        private static Expression BuildObjectConstructor(Type t, Expression existingObject)
        {
            //寻找合适的构造函数。
            var tConstructor = GetTypeConstructor(t);
            if (tConstructor == null) 
                throw new NotSupportedException(string.Format(Prompts.UnableToDeserialize, t));
            //使用默认的构造函数。
            var localObj = E.Parameter(t, "constructingObj");
            return E.Block(t, new[] {localObj},
                localObj.AssignFrom(existingObject.Cast(t)),
                SerializationHelper.IsNullable(t)
                    ? E.Condition(localObj.EqualsTo(E.Constant(null)), E.New(tConstructor).Cast(t), localObj)
                    : E.New(tConstructor).Cast(t));
        }


        private static Expression BuildCollection(Type t, Expression existingCollection, Func<ParameterExpression, E> addItemsBlockBuilder)
        {
            var localCollection = E.Parameter(t, "collection");
            var viType = SerializationHelper.GetCollectionItemType(t);
            if (t.IsArray)
            {
                if (t.GetArrayRank() > 1)
                    throw new NotSupportedException(string.Format(Prompts.UnableToDeserialize, t));
                var localSurrogate = E.Parameter(typeof(List<>).MakeGenericType(viType), "surrogate");
                //对数组使用代理集合（List<T>）。
                return E.Block(t, new[] {localCollection, localSurrogate},
                    localSurrogate.AssignFrom(E.New(localSurrogate.Type)),
                    addItemsBlockBuilder(localSurrogate),
                    //将集合转换回数组。
                    localCollection.AssignFrom(E.NewArrayBounds(viType, localSurrogate.Member("Count"))),
                    localSurrogate.CallMember("CopyTo", localCollection),
                    localCollection);
            }
            //直接添加。
            return E.Block(t, new[] { localCollection },
                localCollection.AssignFrom(BuildObjectConstructor(t, existingCollection.Cast(t))),
                addItemsBlockBuilder(localCollection),
                localCollection);
        }

        // newValueGenerator : Expression (Expression currentValue)
        private Expression BuildMemberAssigner(E obj, MemberInfo member, Func<E, E> newValueGenerator)
        {
            var memberType = SerializationHelper.GetMemberValueType(member);
            var localCurrentValue = E.Parameter(memberType, "current" + member.Name);
            var localNewValue = E.Parameter(memberType, "new" + member.Name);
            var assignMemberExpr = SerializationHelper.IsMemberReadOnly(member)
                ? E.Throw(E.Constant(
                    new NotSupportedException(string.Format(Prompts.UnableToDeserialize,
                        member))))
                : obj.Member(member).AssignFrom(localNewValue);
            return E.Block(memberType, new[] {localCurrentValue, localNewValue},
                localCurrentValue.AssignFrom(obj.Member(member)),
                localNewValue.AssignFrom(newValueGenerator(localCurrentValue)),
                (SerializationHelper.IsNullable(memberType)
                    ? localCurrentValue.NotEqualsTo(localNewValue).IfTrue(assignMemberExpr)
                    : assignMemberExpr),
                localNewValue);
        }

        /// <summary>
        /// Scan the specified type and build TypeSerializer for incoming serialzation.
        /// </summary>
        private void ImplementTypeSerializer(TypeSerializer serializer, Type t)
        {
            Debug.Assert(serializer != null && t != null);
            var kind = SerializationHelper.GetSerializationKind(t);
            Debug.Assert(kind != TypeSerializationKind.Simple && kind != TypeSerializationKind.XStringSerializable);
            //void serialize(XName, object, XSerializationState, SerializationScope)
            // Arguments
            var argElement = E.Variable(typeof(XElement), "element");
            var argObj = E.Variable(typeof(object), "obj");
            var argState = E.Variable(typeof(XSerializationState), "state");
            var argTypeScope = E.Variable(typeof(SerializationScope), "typeScope");
            // Expressions
            var exprs = new List<Expression>();
            // Locals
            var localObj = E.Variable(t, "localObj");
            var locals = new[] { localObj };
            //object deserialize(XObject, object inplaceExisting, XSerializationState, SerializationScope)
            // In-place deserialize
            var exprd = new List<Expression>();
            // Arguments
            // Locals
            var localNewValueElem = E.Parameter(typeof(XElement), "newValueElement");
            var localNewValueAttr = E.Parameter(typeof(XElement), "newValueAttribute");
            var localNewValueStr = E.Parameter(typeof(string), "newValueStr");
            var locald = new[] { localObj, localNewValueElem, localNewValueAttr, localNewValueStr };
            //序列化集合内容。 Serialize collection items.
#if DEBUG
            exprs.Add(E.Invoke((Expression<Action<object>>)(obj => Debug.Assert(t.IsInstanceOfType(obj))), argObj));
#endif
            exprs.Add(localObj.AssignFrom(argObj.Cast(t)));
            // 处理回调函数。 Invoke callbacks.
            var onDeserializingCallbacks = new List<MethodInfo>();
            foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic 
                | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<OnSerializingAttribute>() != null)
                    exprs.Add(localObj.CallMember(method, argState.Member("Context")));
                if (method.GetCustomAttribute<OnDeserializingAttribute>() != null)
                    //注意，此时 argObj == null
                    onDeserializingCallbacks.Add(method);
            }
            if (kind == TypeSerializationKind.Collection)
            {
                //处理集合类型中包含的项目。
                var viType = SerializationHelper.GetCollectionItemType(t);
                DeclareType(viType);
                // Serialize
                var localEachItem = E.Variable(viType, "eachItem");
                var tempExpr = argElement.CallMember("Add",
                    argState.CallMember("SerializeXCollectionItem",
                        localEachItem.Cast<object>(), E.Constant(viType), argTypeScope));
                exprs.Add(ForEach(localEachItem, localObj,
                   SerializationHelper.IsNullable(viType)
                        ? localEachItem.NotEqualsTo(E.Constant(null)).IfTrue(tempExpr)
                        : tempExpr
                    ));
                // Deserialize
                exprd.Add(localObj.AssignFrom(BuildCollection(t, argObj, destList =>
                {
                    var localEachElement = E.Variable(typeof(XElement), "eachElement");
                    if (SerializationHelper.IsDictionary(t))
                    {
                        //is a dictionary
                        //TODO: Build appropriate logic to construct KeyValuePair
                        // or using IDictionary.Add
                        throw new NotSupportedException("IDictionary is currently not supported.");
                    }
                    var coreExpr = ForEach(localEachElement, argElement.CallMember("Elements"),
                        destList.CallMember(SerializationHelper.FindCollectionAddMethod(t),
                            argState.CallMember("DeserializeXCollectionItem",
                                localEachElement, argTypeScope).Cast(viType)));
                    if (onDeserializingCallbacks.Count > 0 && t.IsAssignableFrom(destList.Type))
                    {
                        //集合刚刚初始化完毕后，调用回调函数，然后再添加项目。
                        return E.Block(E.Block(onDeserializingCallbacks.Select(
                            m => localObj.CallMember(m, argState.Member("Context")))), coreExpr);
                    }
                    return coreExpr;
                })));
            }
            else
            {
                //kind == TypeSerializationKind.Complex
                exprd.Add(localObj.AssignFrom(BuildObjectConstructor(t, argObj)));
                exprd.AddRange(onDeserializingCallbacks.Select(m => localObj.CallMember(m, argState.Member("Context"))));
            }
            //序列化属性/字段。 Serialize property / field.
            var bindingFlags = BindingFlags.GetProperty | BindingFlags.GetField
                               | BindingFlags.Public | BindingFlags.Instance;
            {
                var typeAttr = t.GetCustomAttribute<XTypeAttribute>();
                if (typeAttr != null)
                    bindingFlags |= BindingFlags.NonPublic;
            }
            //用于保存可被识别的属性和元素列表，以便于生成未识别的元素列表。
            var knownAttributes = new HashSet<XName>();
            var knownElements = new HashSet<XName>();
            foreach (var member in t.GetMembers(bindingFlags))
            {
                //exprd.Add(argState.CallMember("TestPoint", E.Constant(member.Name).Cast<object>()));
                if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                    continue;
                var memberType = SerializationHelper.GetMemberValueType(member);
                //杂项元素。
                var anyElemAttr = member.GetCustomAttribute<XAnyElementAttribute>();
                if (anyElemAttr != null)
                {
                    if (kind == TypeSerializationKind.Collection)
                        throw new InvalidOperationException(string.Format(
                            Prompts.CollectionPropertyElementNotSupported, member));
                    SerializationHelper.AssertKindOf(typeof(IEnumerable<XElement>),
                        SerializationHelper.GetMemberValueType(member));
                    var localEachElem = E.Variable(typeof(object), "eachElem");
                    exprs.Add(ForEach(localEachElem, localObj.Member(member),
                        E.Call(argElement, "Add", null, new Expression[] { localEachElem })));
                    // Deserialize
                    exprd.Add(BuildMemberAssigner(localObj, member, current =>
                        BuildCollection(memberType, current,
                            destList =>
                            {
                                var localEachElement = E.Variable(typeof (XElement), "eachElement");
                                var anyElements = E.Invoke(
                                    (Expression<Func<XElement, HashSet<XName>, IEnumerable<XElement>>>)
                                        ((e, k) => e.Elements().Where(ex => !k.Contains(ex.Name))),
                                    argElement, E.Constant(knownElements));
                                return ForEach(localEachElement, anyElements,
                                    destList.CallMember(SerializationHelper.FindCollectionAddMethod(memberType), localEachElement));
                            })));
                }
                //杂项属性。
                var anyAttrAttr = member.GetCustomAttribute<XAnyAttributeAttribute>();
                if (anyAttrAttr != null)
                {
                    if (anyElemAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    SerializationHelper.AssertKindOf(typeof(IEnumerable<XAttribute>),
                        SerializationHelper.GetMemberValueType(member));
                    var localEachAttr = E.Variable(typeof(object), "eachAttr");
                    exprs.Add(ForEach(localEachAttr, localObj.Member(member),
                        E.Call(argElement, "Add", null, new Expression[] { localEachAttr })));
                    // Deserialize
                    exprd.Add(BuildMemberAssigner(localObj, member, current =>
                        BuildCollection(memberType, current,
                            destList =>
                            {
                                var localEachAttribute = E.Variable(typeof (XAttribute), "eachAttribute");
                                var anyAttributes = E.Invoke(
                                    (Expression<Func<XElement, HashSet<XName>, IEnumerable<XAttribute>>>)
                                        ((e, k) => e.Attributes().Where(a =>
                                            a.Name.Namespace != XNamespace.Xmlns && !k.Contains(a.Name))),
                                    argElement, E.Constant(knownAttributes));
                                return ForEach(localEachAttribute, anyAttributes,
                                    destList.CallMember(SerializationHelper.FindCollectionAddMethod(memberType),
                                        localEachAttribute));
                            })));
                }
                //元素。
                var eattr = member.GetCustomAttribute<XElementAttribute>();
                if (eattr != null)
                {
                    if (anyElemAttr != null || anyAttrAttr != null)
                        throw new InvalidOperationException(string.Format(
                            Prompts.InvalidAttributesCombination, member));
                    var memberKind = SerializationHelper.GetSerializationKind(memberType);
                    var memberNameExpr = E.Constant(SerializationHelper.GetName(member, eattr));
                    var localCurrentValue = E.Parameter(memberType, "currentValue");
                    knownElements.Add((XName) memberNameExpr.Value);
                    exprd.Add(localNewValueElem.AssignFrom(argElement.CallMember("Element", memberNameExpr)));
                    switch (memberKind)
                    {
                        case TypeSerializationKind.Simple:
                            exprs.Add(argElement.CallMember("SetElementValue", memberNameExpr,
                                localObj.Member(member).Cast<object>()));
                            exprd.Add(localNewValueElem.NotEqualsTo(E.Constant(null)).IfTrue(
                                localObj.Member(member).AssignFrom(localNewValueElem.Cast(memberType))));
                            break;
                        case TypeSerializationKind.XStringSerializable:
                            exprs.Add(argElement.CallMember("SetElementValue", memberNameExpr,
                                localObj.Member(member).CallMember(IXStringSerializable_Serialize)));
                            //Deserialize
                            exprd.Add(localNewValueStr.AssignFrom(localNewValueElem.Cast<string>()));
                            exprd.Add(localNewValueElem.NotEqualsTo(E.Constant(null)).IfTrue(
                                BuildMemberAssigner(localObj, member, current =>
                                    BuildObjectConstructor(memberType, localCurrentValue))
                                    .CallMember(IXStringSerializable_Deserialize, localNewValueStr)));
                            break;
                        case TypeSerializationKind.Collection:
                        case TypeSerializationKind.Complex:
                            DeclareType(memberType);
                            if (t.IsValueType && SerializationHelper.IsMemberReadOnly(member)) 
                                throw new NotSupportedException(string.Format(Prompts.StrcutureReadonly, t));
                            SerializationScope scope = null;
                            if (memberKind == TypeSerializationKind.Collection)
                            {
                                //注册集合项目的类型。
                                //Register types of ollection items.
                                // IEnumerable<T> => T
                                var viType = SerializationHelper.GetCollectionItemType(memberType);
                                var viTypeRegistered = false;
                                scope = new SerializationScope(member + " (" + member.DeclaringType + ")");
                                foreach (var colAttr in member.GetCustomAttributes<XCollectionItemAttribute>())
                                {
                                    var thisType = colAttr.Type ?? viType;
                                    scope.AddType(colAttr.GetName(SerializationHelper.GetName(thisType)), thisType);
                                    DeclareType(t);
                                    if (thisType == viType) viTypeRegistered = true;
                                }
                                if (!viTypeRegistered)
                                {
                                    scope.AddType(SerializationHelper.GetName(viType), viType);
                                    DeclareType(viType);
                                }
                            }
                            exprs.Add(argElement.CallMember("Add",
                                argState.CallMember("SerializeXProperty", localObj.Member(member).Cast<object>(), E.Constant(memberType),
                                    E.Constant(SerializationHelper.GetName(member, eattr)),
                                    E.Constant(scope, typeof(SerializationScope)))));
                            //Deserialize
                            exprd.Add(localNewValueElem.NotEqualsTo(E.Constant(null)).IfTrue(
                                BuildMemberAssigner(localObj, member, current => 
                                    argState.CallMember("DeserializeXProperty",
                                        localNewValueElem,
                                        current.Cast<object>(), E.Constant(memberType),
                                        E.Constant(scope, typeof (SerializationScope))).Cast(memberType)
                                    )));
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
                    var memberNameExpr = E.Constant(SerializationHelper.GetName(member, aattr));
                    var localCurrentValue = E.Parameter(memberType, "currentValue");
                    knownAttributes.Add((XName)memberNameExpr.Value);
                    exprd.Add(localNewValueAttr.AssignFrom(argElement.CallMember("Element", memberNameExpr)));
                    switch (SerializationHelper.GetSerializationKind(memberType))
                    {
                        case TypeSerializationKind.Simple:
                            exprs.Add(argElement.CallMember("SetAttributeValue", memberNameExpr,
                                localObj.Member(member).Cast<object>()));
                            exprd.Add(localNewValueAttr.NotEqualsTo(E.Constant(null)).IfTrue(
                                localObj.Member(member).AssignFrom(localNewValueAttr.Cast(memberType))));
                            break;
                        case TypeSerializationKind.XStringSerializable:
                            exprs.Add(argElement.CallMember("SetAttributeValue", memberNameExpr,
                                localObj.Member(member).CallMember(IXStringSerializable_Serialize)));
                            //Deserialize
                            exprd.Add(localNewValueStr.AssignFrom(localNewValueAttr.Cast<string>()));
                            exprd.Add(localNewValueStr.NotEqualsTo(E.Constant(null)).IfTrue(
                                BuildMemberAssigner(localObj, member, current =>
                                    BuildObjectConstructor(memberType, localCurrentValue))
                                    .CallMember(IXStringSerializable_Deserialize, localNewValueStr)));
                            break;
                        case TypeSerializationKind.Collection:
                        case TypeSerializationKind.Complex:
                            //无法序列化复杂类型和集合。
                            throw new InvalidOperationException(string.Format(Prompts.CannotSerializeAsAttribute, member));
                    }
                }
            }
            // 处理回调函数。 Invoke callbacks.
            foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<OnSerializedAttribute>() != null)
                    exprs.Add(localObj.CallMember(method, argState.Member("Context")));
                if (method.GetCustomAttribute<OnDeserializedAttribute>() != null)
                    exprd.Add(localObj.CallMember(method, argState.Member("Context")));
            }
            // 别忘了反序列化的返回值。
            exprd.Add(localObj);
            // 编译。
            var serializerLambda = E.Lambda(E.Block(typeof(void), locals, exprs), argElement, argObj, argState, argTypeScope);
            serializer.RegisterSerializeXElementAction(serializerLambda.Compile());
            var deserializerLambda = E.Lambda(E.Block(typeof(object), locald, exprd), argElement, argObj, argState, argTypeScope);
            serializer.RegisterDeserializeXElementAction(deserializerLambda.Compile());
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
            var state = new XSerializationState(new StreamingContext(StreamingContextStates.Persistence, context), this);
            return state.SerializeRoot(obj, rootType, rootName ?? _GlobalScope.GetName(rootType));
        }

        public object Deserialize(XElement e, object context)
        {
            if (e == null) throw new ArgumentNullException("e");
            var state = new XSerializationState(new StreamingContext(StreamingContextStates.Persistence, context), this);
            var obj = state.DeserializeRoot(e, rootType);
            Debug.Assert(rootType.IsInstanceOfType(obj));
            return obj;
        }
    }

    internal enum TypeSerializationKind
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
        private string _Name;
        // Complex / Collection
        //<XElement, T, XSerializationState>
        private Action<XElement, object, XSerializationState, SerializationScope> serialzeXElementAction;
        private Func<XElement, object, XSerializationState, SerializationScope, object> deserialzeXElementAction;


        public void RegisterSerializeXElementAction(Delegate d)
        {
            Debug.Assert(d != null);
            serialzeXElementAction =
                (Action<XElement, object, XSerializationState, SerializationScope>) d;
        }

        public void RegisterDeserializeXElementAction(Delegate d)
        {
            Debug.Assert(d != null);
            deserialzeXElementAction =
                (Func<XElement, object, XSerializationState, SerializationScope, object>)d;
        }

        public TypeSerializer(string name)
        {
            _Name = name;
        }

        public override string ToString()
        {
            return _Name;
        }

        internal void Serialize(XElement e, object obj, XSerializationState state, SerializationScope typeScope)
        {
            Debug.Assert(e != null && obj != null && state != null);
            serialzeXElementAction(e, obj, state, typeScope);
        }

        internal object Deserialize(XElement e, object obj, XSerializationState state, SerializationScope typeScope)
        {
            Debug.Assert(e != null && state != null);
            if (e.Name.LocalName == "compositeArray")
            {
                
            }
            return deserialzeXElementAction(e, obj, state, typeScope);
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

        public Type GetType(XName n)
        {
            Type t;
            if (nameTypeDict.TryGetValue(n, out t))
                return t;
            return null;
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
