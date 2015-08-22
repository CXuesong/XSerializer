using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Undefined.Serialization
{
    public class XSerializableSurrogateCollection : Collection<IXSerializableSurrogate>
    {
        protected override void InsertItem(int index, IXSerializableSurrogate item)
        {
            if (item == null) throw new ArgumentNullException("item");
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, IXSerializableSurrogate item)
        {
            if (item == null) throw new ArgumentNullException("item");
            base.SetItem(index, item);
        }

        public TSurrogate FindSurrogate<TSurrogate>(Type desiredType) where TSurrogate : IXSerializableSurrogate
        {
            if (desiredType == null) throw new ArgumentNullException("desiredType");
            return Items.OfType<TSurrogate>().FirstOrDefault(s => s.IsTypeSupported(desiredType));
        }
    }

}
