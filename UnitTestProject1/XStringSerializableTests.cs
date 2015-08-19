using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Undefined.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class XStringSerializableTests
    {
        public struct Point : IXStringSerializable
        {
            public double X { get; set; }

            public double Y { get; set; }

            public string Serialize()
            {
                return X.ToString(CultureInfo.InvariantCulture) + " " + Y.ToString(CultureInfo.InvariantCulture);
            }

            public void Deserialize(string s)
            {
                if (s == null) throw new ArgumentNullException("s");
                var parts = s.Split();
                if (parts.Length < 2) throw new ArgumentException("Invalid value : " + s + " .");
                X = Convert.ToDouble(parts[0], CultureInfo.InvariantCulture);
                Y = Convert.ToDouble(parts[1], CultureInfo.InvariantCulture);
            }

            public override string ToString()
            {
                return X + "," + Y;
            }

            public Point(double x, double y)
                : this()
            {
                X = x;
                Y = y;
            }
        }

        public class Rectangle
        {
            [XAttribute("point1")]
            public Point Point1 { get; set; }

            [XAttribute("point2")]
            public Point Point2 { get; set; }

            [XAttribute("point3")]
            public Point? Point3 { get; set; }

            [XAttribute("border")]
            public Color BorderColor { get; set; }

            [XAttribute("fill")]
            public Color FillColor { get; set; }

            [XAttribute("fillMode")]
            public FillMode FillMode { get; set; }
        }

        public class ColorXSSSurrogate : IXStringSerializableSurrogate
        {
            public bool IsTypeSupported(Type t)
            {
                return t == typeof(Color);
            }

            public string Serialize(object obj)
            {
                var c = (Color)obj;
                return c.Name;
            }

            private static readonly Regex ARGBMatcher = new Regex("^[0-9a-fA-F]{1,8}$");

            public object Deserialize(string s, Type desiredType)
            {
                if (desiredType != typeof(Color)) throw new NotSupportedException();
                return ARGBMatcher.IsMatch(s) ? Color.FromArgb(int.Parse(s, NumberStyles.HexNumber)) : Color.FromName(s);
            }
        }


        [TestMethod]
        public void SerializeAPoint()
        {
            var s = new XSerializer(typeof(Point));
            var obj = new Point(123.45, 678.9012);
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (Point)s.Deserialize(doc, null);
            Assert.AreEqual(obj, obj1);
        }

        [TestMethod]
        public void SerializeARectangle()
        {
            var surrogates = new XSerializableSurrogateCollection
            {
                new ColorXSSSurrogate()
            };
            var s = new XSerializer(typeof(Rectangle), null, surrogates);
            var obj = new Rectangle
            {
                Point1 = new Point(123.45, 678.9012),
                Point2 = new Point(350, 850),
                BorderColor = Color.FromArgb(127, 100, 50, 100),
                FillColor = Color.CadetBlue,
                FillMode = FillMode.Alternate
            };
            var doc = s.GetSerializedDocument(obj);
            Trace.WriteLine(doc);
            var obj1 = (Rectangle)s.Deserialize(doc, null);
            Assert.AreEqual(obj.BorderColor, obj1.BorderColor);
            Assert.AreEqual(obj.FillColor, obj1.FillColor);
            Assert.AreEqual(obj.Point1, obj1.Point1);
            Assert.AreEqual(obj.Point2, obj1.Point2);
            Assert.AreEqual(obj.FillMode, obj1.FillMode);
        }
    }
}
