using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class Simple2
    {
        public class SimpleObject2
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
            var s = new XSerializer(typeof(SimpleObject2));
            var obj = new SimpleObject2();
            obj.Dict[10] = "alpha";
            obj.Dict[20] = "beta";
            obj.Dict[100] = "gamma";
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (SimpleObject2)s.Deserialize(doc, null);
            foreach (var key in obj.Dict.Keys)
                Assert.AreEqual(obj.Dict[key], obj1.Dict[key]);
        }
    }
}
