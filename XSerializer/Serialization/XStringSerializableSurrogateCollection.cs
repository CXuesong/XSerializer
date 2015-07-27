using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Undefined.Serialization
{
    public class XStringSerializableSurrogateCollection : Collection<IXStringSerializableSurrogate>
    {
        protected override void InsertItem(int index, IXStringSerializableSurrogate item)
        {
            if (item == null) throw new ArgumentNullException("item");
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, IXStringSerializableSurrogate item)
        {
            if (item == null) throw new ArgumentNullException("item");
            base.SetItem(index, item);
        }

        public IXStringSerializableSurrogate FindSurrogate(Type desiredType)
        {
            if (desiredType == null) throw new ArgumentNullException("desiredType");
            return Items.FirstOrDefault(s => s.IsTypeSupported(desiredType));
        }
    }
}
