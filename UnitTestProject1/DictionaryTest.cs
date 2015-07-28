using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class DictionaryTest
    {
        public class ClassWithDictionary
        {
            private Dictionary<int, string> _Dict = new Dictionary<int, string>();

            [XElement] 
            public Dictionary<int, string> Dict
            {
                get { return _Dict; }
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            var s = new XSerializer(typeof(ClassWithDictionary));
            var obj = new ClassWithDictionary();
            obj.Dict[10] = "alpha";
            obj.Dict[20] = "beta";
            obj.Dict[100] = "gamma";
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (ClassWithDictionary)s.Deserialize(doc, null);
            foreach (var key in obj.Dict.Keys)
                Assert.AreEqual(obj.Dict[key], obj1.Dict[key]);
        }
    }
}
