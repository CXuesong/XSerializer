using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class AbstractTypeTests
    {
        public class ClassWithEnumerable
        {
            [XElement]
            public IEnumerable Collection;

            [XElement]
            public IEnumerable<string> StringCollection;
        }

        //[TestMethod]
        public void IEnumerableTest()
        {
            var s = new XSerializer(typeof(ClassWithEnumerable));
            var obj = new ClassWithEnumerable
            {
                Collection = new object[] {"abc", 123.45},
                StringCollection = new string[] {"abc", "def"}
            };
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (ClassWithEnumerable)s.Deserialize(doc, null);
            Assert.IsTrue(obj.Collection.Cast<object>().SequenceEqual(obj1.Collection.Cast<object>()));
            Assert.IsTrue(obj.StringCollection.SequenceEqual(obj1.StringCollection));
        }
    }
}
