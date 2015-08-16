using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class XElementSerializableTests
    {
        public class ClassWithDictionary
        {
            private Dictionary<int, string> _Dict = new Dictionary<int, string>();

            [XElement] 
            public Dictionary<int, string> Dict
            {
                get { return _Dict; }
            }
        }

        public class CustomTable : IXElementSerializable
        {
            //[XElement]
            public string[,] Content { get; set; }

            void IXElementSerializable.Serialize(XElement element)
            {
                if (element == null) throw new ArgumentNullException("element");
                foreach (var entry in Content)
                    element.Add(new XElement("data", entry));
            }

            void IXElementSerializable.Deserialize(XElement element)
            {
                if (element == null) throw new ArgumentNullException("element");
                var entries = element.Elements("data").ToArray();
                var c = new string[entries.Length/2, 2];
                for (int i = 0; i < entries.Length / 2; i++)
                {
                    c[i, 0] = (string) entries[i*2];
                    c[i, 1] = (string) entries[i*2 + 1];
                }
                Content = c;
            }
        }

        /// <summary>
        /// Provides a primitive example of how to serialize dictionaries.
        /// </summary>
        public class DictionaryXSerializableSurrogate<TKey, TValue> : IXElementSerializableSurrogate
        {
            public bool IsTypeSupported(Type t)
            {
                return typeof (IDictionary).IsAssignableFrom(t);
            }

            public void Serialize(object obj, XElement element)
            {
                if (obj == null) throw new ArgumentNullException("obj");
                if (element == null) throw new ArgumentNullException("element");
                var dict = (IDictionary)obj;
                foreach (DictionaryEntry p in dict)
                {
                    //此处假定键与值均为简单类型。
                    element.Add(new XElement("entry", new XAttribute("key", p.Key), p.Value));
                }
            }

            public object Deserialize(XElement element, Type desiredType, object existingObj)
            {
                if (!typeof (IDictionary).IsAssignableFrom(desiredType)) throw new NotSupportedException();
                if (element == null) throw new ArgumentNullException("element");
                var dict = (IDictionary)(existingObj ?? Activator.CreateInstance(desiredType));
                var keyExp = GetExplicitOperator(typeof(XAttribute), typeof(TKey));
                var valueExp = GetExplicitOperator(typeof(XElement), typeof(TValue));
                if (keyExp == null || valueExp == null) throw new NotSupportedException();
                foreach (var e in element.Elements("entry"))
                {
                    //此处假定键与值均为简单类型。 Assuming keys & values are both simple types.
                    dict.Add(keyExp.Invoke(null, new object[] {e.Attribute("key")}),
                        valueExp.Invoke(null, new object[] {e}));
                }
                return existingObj;
            }

            // I have no other choice but ...
            private static MethodBase GetExplicitOperator(Type source, Type dest)
            {
                Debug.Assert(source != null && dest != null);
                return source.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "op_Explicit" && m.ReturnType == dest);
            }
        }

        [TestMethod]
        public void DictionaryTest()
        {
            var surrogates = new XSerializableSurrogateCollection
            {
                new DictionaryXSerializableSurrogate<int,string>()
            };
            var s = new XSerializer(typeof (ClassWithDictionary), null, surrogates);
            var obj = new ClassWithDictionary();
            obj.Dict[10] = "alpha";
            obj.Dict[20] = "beta";
            obj.Dict[100] = "gamma";
            obj.Dict[150] = "theta";
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (ClassWithDictionary)s.Deserialize(doc, null);
            foreach (var key in obj.Dict.Keys)
                Assert.AreEqual(obj.Dict[key], obj1.Dict[key]);
        }

        [TestMethod]
        public void Dim2ArrayTest()
        {
            var s = new XSerializer(typeof(CustomTable));
            var obj = new CustomTable
            {
                Content = new[,]
                {
                    {"A", "Alice"},
                    {"B", "Bob"},
                    {"C", "Carola"}
                }
            };
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (CustomTable)s.Deserialize(doc, null);
            Assert.IsTrue(obj.Content.Cast<string>().SequenceEqual(obj1.Content.Cast<string>()));
        }
    }
}
