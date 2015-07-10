using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [XType(null, MyUri)]
    public class MyObject1
    {
        public const string MyUri = "http://www.yourcompany.org/schemas/undefined/1";
        public const string MyUri2 = "http://www.yourcompany.org/schemas/undefined/2";

        [XElement("field1", MyUri)]
        public int Field1 = 20;

        [XElement("field2", MyUri2)]
        public DateTime Field2 = DateTime.Now;

        [XElement()]
        public long? Field3 = null;

        [XElement()]
        public long? Field4 = 1234567;


        [XElement("property1", MyUri)]
        public double Property1 { get; set; }

        private List<string> _List1 = new List<string>();

        [XElement("list1", MyUri)]
        public List<string> List1
        {
            get { return _List1; }
        }

        private string[] _Array1{get; set; }
    }

    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void SerializationTest1()
        {
            var obj = new MyObject1() {Property1 = 123e45};
            //There are intentional spaces left in the strings.
            obj.List1.Add("越过长城，走向世界。    ");
            obj.List1.Add("\t\tAcross the Great Wall we can reach every corner in the world.");
            var s = new XSerializer(typeof(MyObject1));
            s.Namespaces = new XSerializerNamespaceCollection();
            s.Namespaces.Add("n2", MyObject1.MyUri2);
            Trace.Write(s.GetSerializedString(obj));
        }
    }
}
