using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class CallbackTest
    {
        public class MyClass
        {
            [XElement]
            public string myString;

            [OnSerializing]
            private void OnSerializing(StreamingContext context)
            {
                Trace.WriteLine(string.Format("OnSerializing, {0}, {1}", context.State, context.Context));
                Trace.WriteLine(string.Format("\tmyString = {0}", myString));
            }

            [OnSerialized]
            private void OnSerialized(StreamingContext context)
            {
                Trace.WriteLine(string.Format("OnSerialized, {0}, {1}", context.State, context.Context));
            }

            [OnDeserializing]
            private void OnDeserializing(StreamingContext context)
            {
                Trace.WriteLine(string.Format("OnSerializing, {0}, {1}", context.State, context.Context));
            }

            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                Trace.WriteLine(string.Format("OnDeserialized, {0}, {1}", context.State, context.Context));
                Trace.WriteLine(string.Format("\tmyString = {0}", myString));
            }

            public MyClass()
            {

            }

            public MyClass(string str)
            {
                myString = str;
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            var s = new XSerializer(typeof(MyClass));
            var obj = new MyClass("Callback Test");
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (MyClass)s.Deserialize(doc, null);
            Assert.AreEqual(obj.myString, obj1.myString);
        }
    }
}
