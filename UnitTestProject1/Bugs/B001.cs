using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Runtime.Serialization;
using System.Diagnostics;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class B001
    {
        public class MyList : ArrayList
        {
            [OnSerializing]
            private void OnSerializing(StreamingContext context)
            {

            }
        }

        [TestMethod]
        public void B001TestMethod1()
        {
            var s = new XSerializer(typeof(MyList));
            var list = new MyList { 1, 2, 3 };
            var doc = s.GetSerializedDocument(list);
            Trace.WriteLine(doc);
            var list1 = (MyList)s.Deserialize(doc, null);
            Assert.AreEqual(list1.Count, list.Count);
        }
    }
}
