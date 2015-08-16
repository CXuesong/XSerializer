using System.Diagnostics;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class CallbackTests
    {
        public class MyClass
        {
            [XElement]
            public string myString;

            [OnSerializing]
            private void OnSerializing(StreamingContext context)
            {
                Trace.WriteLine($"OnSerializing, {context.State}, {context.Context}");
                Trace.WriteLine($"\tmyString = {myString}");
            }

            [OnSerialized]
            private void OnSerialized(StreamingContext context)
            {
                Trace.WriteLine($"OnSerialized, {context.State}, {context.Context}");
            }

            [OnDeserializing]
            private void OnDeserializing(StreamingContext context)
            {
                Trace.WriteLine($"OnSerializing, {context.State}, {context.Context}");
            }

            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                Trace.WriteLine($"OnDeserialized, {context.State}, {context.Context}");
                Trace.WriteLine($"\tmyString = {myString}");
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
        public void CallbackTest()
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
