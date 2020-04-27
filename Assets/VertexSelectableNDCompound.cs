using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TheNCube
{
    struct VertexSelectableNDCompound
    {
        public KMSelectable selectable;
        public PointND vectorN;

        public VertexSelectableNDCompound(KMSelectable selectable, PointND vectorN)
        {
            this.selectable = selectable;
            this.vectorN = vectorN;
        }
    }
}
