using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [XType(null, MyUri1)]
    public class MyObject1
    {
        public const string MyUri1 = "http://www.yourcompany.org/schemas/undefined/1";
        public const string MyUri2 = "http://www.yourcompany.org/schemas/undefined/2";

        [XElement("myDouble", MyUri1)]
        public int Field1 = 20;

        [XElement("now", MyUri2)]
        public DateTime Field2 = DateTime.Now;

        [XElement()]
        public long? Field3 = null;

        [XElement("nullableInt")]
        public long? Field4 = 1234567;

        [XAttribute("myGuid")]
        public Guid Field5 = Guid.NewGuid();

        [XElement("property1", MyUri1)]
        public double Property1 { get; set; }

        private List<string> _List1 = new List<string>();

        [XElement("list1", MyUri1)]
        public List<string> List1
        {
            get { return _List1; }
        }

        [XElement("compositeArray", MyUri1)]
        [XCollectionItem(typeof(string))]
        [XCollectionItem(typeof(double))]
        [XCollectionItem(typeof(MyObject1), "embededObject")]
        public object[] Array1 { get; set; }

        [XElement(null, MyUri1)]
        public MyObject1 AnotherObject;

        private string[] _Array1{get; set; }
    }

    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void SerializationTest1()
        {
            const int repetitions = 50;
            var s = new XSerializer(typeof(MyObject1));
            var ns = new XSerializerNamespaceCollection { { "n1", MyObject1.MyUri1 }, { "n2", MyObject1.MyUri2 } };
            var p = new XSerializerParameters(ns);
            var obj = new MyObject1()
            {
                Property1 = 123e45,
                Array1 = new object[]
                {
                    "abc",
                    123.4567,
                    "def",
                    new MyObject1()
                },
                AnotherObject = new MyObject1()
            };
            //There are intentional spaces left in the strings.
            obj.List1.Add("越过长城，走向世界。    ");
            obj.List1.Add("\t\tAcross the Great Wall we can reach every corner in the world.");
            Trace.WriteLine(s.GetSerializedDocument(obj, p));
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < repetitions; i++)
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                s.GetSerializedDocument(obj, p).ToString();
            }
            Trace.Write("Elapsed ms : ");
            Trace.WriteLine(sw.Elapsed.TotalMilliseconds / repetitions);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CircularSerializationTest()
        {
            var s = new XSerializer(typeof(MyObject1));
            var obj = new MyObject1();
            obj.AnotherObject = obj;    // circular reference
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
