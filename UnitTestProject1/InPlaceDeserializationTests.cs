using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class InPlaceDeserializationTests
    {
        public class Container
        {
            [XElement]
            public string Name { get; set; }

            public Container(string name)
            {
                Name = name;
            }
        }

        public class MyObject1
        {
            public MyObject1(Container container)
            {
                Container = container;
            }

            public Container Container { get; }

            [XElement]
            public string Name { get; set; }

            public override string ToString()
            {
                return $"{Name} ({Container.Name})";
            }
        }

        [TestMethod]
        public void ObjectWithoutEmptyConstructorTest()
        {
            var s = new XSerializer(typeof (MyObject1));
            var container = new Container("container");
            var obj = new MyObject1(container) {Name = "My Object 1"};
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (MyObject1) s.Deserialize(doc, null, new MyObject1(container));
            Trace.WriteLine(obj1);
        }


        public class MyObject2
        {
            [XElement]
            public readonly Container ReadonlyContainer = new Container("Default Container");
        }


        [TestMethod]
        public void ObjectWithReadonlyFieldTest()
        {
            const string containerName = "This Container";
            var s = new XSerializer(typeof(MyObject2));
            var container = new Container("container");
            var obj = new MyObject2();
            obj.ReadonlyContainer.Name = containerName;
            var doc = s.GetSerializedDocument(obj);
            var obj1 = (MyObject2)s.Deserialize(doc, null, new MyObject2());
            Assert.AreEqual(obj1.ReadonlyContainer.Name, containerName);
        }
    }
}
