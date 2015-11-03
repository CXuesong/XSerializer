using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;
using System.Linq;
using System.Xml.Linq;

namespace UnitTestProject1.Bugs
{
    /// <summary>
    /// B003: 在序列化包含基元类型的列表时，XCollectionItemAttribute 的默认命名空间是 xsi 而不是空。
    /// When serializing collection with built-in types, the default Namespace for XCollectionItemAttribute
    ///     is xsi rather than empty.
    /// </summary>
    [TestClass]
    public class B003
    {
        public class MyClass
        {
            [XElement("list")] [XCollectionItem("item")] public List<int> List = new List<int>();
        }

        [TestMethod]
        public void B003TestMethod1()
        {
            var s = new XSerializer(typeof(MyClass));
            var obj = new MyClass();
            obj.List.AddRange(new[] { 10, 20, 30 });
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            Assert.IsFalse(doc.Elements().DescendantsAndSelf().Any(xe => xe.Name.Namespace != XNamespace.None));
            var obj1 = (MyClass)s.Deserialize(doc, null);
            Assert.AreEqual(obj1.List.Count, obj.List.Count);
        }
    }
}
