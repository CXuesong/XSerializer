using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [XType(null, MyUri, IncludePrivateMembers = true)]
    public class SimpleObject
    {
        public const string MyUri = "http://www.yourcompany.org/schemas/undefined/simple";

        // All the members subjected to be serialized should be attributed.
        [XElement("title", MyUri)]
        public string Title { get; set; }

        // Private members can be serialized. (If permitted.)
        [XAttribute]
        private Guid guid = Guid.NewGuid();

        [XElement("now", MyUri)]
        public DateTime Time = DateTime.Now;

        private ArrayList _CompositeArray = new ArrayList();

        // Writable collecion exposed from readonly property is okay.
        // All the type used in the array should be declared explicitly.
        [XElement("array", MyUri)]
        [XCollectionItem(typeof(string))]
        [XCollectionItem(typeof(double))]
        [XCollectionItem(typeof(SimpleObject), "embeded")]
        public ArrayList CompositeArray
        {
            get { return _CompositeArray; }
        }
    }

    [TestClass]
    public class Simple
    {
        [TestMethod]
        public void SerializationTest()
        {
            var s = new XSerializer(typeof(SimpleObject));
            var obj = new SimpleObject { Title = "Simple Object Test" };
            obj.CompositeArray.Add("Hello, world!");
            obj.CompositeArray.Add(12345.67e89);
            obj.CompositeArray.Add(new SimpleObject { Title = "Child Object" });
            Trace.WriteLine(s.GetSerializedDocument(obj));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CircularReferenceTest()
        {
            var s = new XSerializer(typeof(SimpleObject));
            var obj = new SimpleObject { Title = "Simple Object Test" };
            obj.CompositeArray.Add(obj);
            try
            {
                s.GetSerializedDocument(obj);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                throw;
            }
        }
    }
}
