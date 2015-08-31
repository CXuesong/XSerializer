using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class AnyElementTests
    {

        public class MyClass
        {
            [XAnyAttribute]
            public IList<XAttribute> attr = new List<XAttribute>();

            [XAnyElement]
            public readonly IList<XElement> elem = new List<XElement>();

            [XElement]
            public string myString;

            public MyClass()
            {
                
            }

            public MyClass(string str)
            {
                myString = str;
            }
        }

        [TestMethod]
        public void AnyElementTest()
        {
            var s = new XSerializer(typeof(MyClass));
            var obj = new MyClass("AnyElement / AnyAttribute Test")
            {
                attr = new[] {new XAttribute("a1", "value1"), new XAttribute("a2", DateTime.UtcNow)}
            };
            obj.elem.Add(new XElement("customElement", "content1"));
            obj.elem.Add(new XElement("customElement", 12345));
            obj.elem.Add(new XElement("customElement", Guid.NewGuid()));
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (MyClass)s.Deserialize(doc, null);
            Assert.AreEqual(obj.myString, obj1.myString);
            Assert.AreEqual(obj.attr.Count, obj1.attr.Count);
            Assert.AreEqual(obj.elem.Count, obj1.elem.Count);
        }
    }
}
