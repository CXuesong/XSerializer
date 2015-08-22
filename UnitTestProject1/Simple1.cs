using System;
using System.Collections;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [XType(null, MyUri, IncludeNonPublicMembers = true)]
    public class SimpleObject1
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
        [XCollectionItem(typeof(SimpleObject1), "embeded")]
        public ArrayList CompositeArray
        {
            get { return _CompositeArray; }
        }

        [XText]
        public string ExtraContent { get; set; }
    }

    [TestClass]
    public class Simple1
    {
        [TestMethod]
        public void SerializationTest()
        {
            var s = new XSerializer(typeof(SimpleObject1));
            var obj = new SimpleObject1 {Title = "Simple Object Test", ExtraContent = "Extra content here."};
            obj.CompositeArray.Add("Hello, world!");
            obj.CompositeArray.Add(12345.67e89);
            obj.CompositeArray.Add(new SimpleObject1 { Title = "Child Object" });
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (SimpleObject1)s.Deserialize(doc, null);
            Assert.AreEqual(obj.Title, obj1.Title);
            Assert.AreEqual(obj.ExtraContent, obj1.ExtraContent);
            Assert.AreEqual(obj.Time, obj1.Time);
            Assert.AreEqual(obj.CompositeArray.Count, obj1.CompositeArray.Count);
            for (var i = 0; i < obj.CompositeArray.Count; i++)
            {
                if (!(obj.CompositeArray[i] is SimpleObject1))
                    Assert.AreEqual(obj.CompositeArray[i], obj1.CompositeArray[i]);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CircularReferenceTest()
        {
            var s = new XSerializer(typeof(SimpleObject1));
            var obj = new SimpleObject1 { Title = "Simple Object Test" };
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
