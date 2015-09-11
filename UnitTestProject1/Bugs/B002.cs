using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Runtime.Serialization;
using System.Diagnostics;
using Undefined.Serialization;

namespace UnitTestProject1.Bugs
{
    /// <summary>
    /// B002 : 当集合类型具有 OnDeserializing 回调函数时，反序列化时会引发 NullReferenceException。
    /// NullReferenceException during deserialization of a collection with OnDeserializing callback.
    /// </summary>
    [TestClass]
    public class B002
    {
        public class MyList : ArrayList
        {
            [OnDeserializing]
            protected void OnDeserializing(StreamingContext context)
            {
                Trace.WriteLine("MyList.OnDeserializing");
            }
        }

        public class MyObject
        {
            [XElement]
            public MyList List { get; } = new MyList();
        }

        [TestMethod]
        public void B002TestMethod1()
        {
            var s = new XSerializer(typeof(MyObject));
            var obj = new MyObject();
            obj.List.AddRange(new[] { 10, 20, 30 });
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (MyObject)s.Deserialize(doc, null);
            Assert.AreEqual(obj1.List.Count, obj.List.Count);
        }
    }
}
