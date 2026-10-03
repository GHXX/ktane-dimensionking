using System;

namespace DimensionKing {
    internal class VertexPressedEventArgs : EventArgs {
        public VertexPressedEventArgs(GeoObject.VertexObject vertexObject, int i) {
            VertexObject = vertexObject;
            this.i = i;
        }

        public readonly GeoObject.VertexObject VertexObject;
        public readonly int i;
    }
}
