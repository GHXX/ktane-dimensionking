using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TheNCube
{
    struct VertexSelectableNDCompound
    {
        public KMSelectable selectable;
        public VecNd vectorN;

        public VertexSelectableNDCompound(KMSelectable selectable, VecNd vectorN)
        {
            this.selectable = selectable;
            this.vectorN = vectorN;
        }
    }
}
