using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Undefined.Serialization
{
    /// <summary>
    /// 表示一个 XML 命名空间前缀以及与之对应的 URI。
    /// Represents an XML namespace prefix and its corresponding URI.
    /// </summary>
    public struct PrefixUriPair
    {
        private readonly string _Prefix;
        private readonly string _Uri;

        /// <summary>
        /// 表示 xsi 的命名空间前缀以及对应的 URI。
        /// Represents xsi namespace and its corresponding URI.
        /// </summary>
        public static readonly PrefixUriPair Xsi = new PrefixUriPair("xsi", SerializationHelper.Xsi.NamespaceName);

        public string Prefix
        {
            get { return _Prefix; }
        }

        public string Uri
        {
            get { return _Uri; }
        }

        public PrefixUriPair(string prefix, string uri)
        {
            if (prefix == null) throw new ArgumentNullException("prefix");
            if (string.IsNullOrEmpty(uri)) throw new ArgumentNullException("prefix");
            _Prefix = prefix;
            _Uri = uri;
        }
    }

    /// <summary>
    /// Maintins XML namespace uri and its corresponding prefix,
    /// used by <see cref="XSerializer"/>.
    /// </summary>
    public class XSerializerNamespaceCollection : ICollection<PrefixUriPair>
    {
        private IList<PrefixUriPair> list;

        public void Add(string prefix, string namespaceUri)
        {
            list.Add(new PrefixUriPair(prefix, namespaceUri));
        }

        public XSerializerNamespaceCollection()
        {
            list = new List<PrefixUriPair>();
        }

        public XSerializerNamespaceCollection(XSerializerNamespaceCollection other)
        {
            list = new List<PrefixUriPair>(other.list);
        }

        #region ICollection
        public void Add(PrefixUriPair item)
        {
            list.Add(item);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(PrefixUriPair item)
        {
            return list.Contains(item);
        }

        public void CopyTo(PrefixUriPair[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(PrefixUriPair item)
        {
            return list.Remove(item);
        }

        public IEnumerator<PrefixUriPair> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
        #endregion
    }
}
