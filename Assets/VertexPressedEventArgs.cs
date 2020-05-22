using System;

namespace DimensionKing
{
    class VertexPressedEventArgs : EventArgs
    {
        public VertexPressedEventArgs(GeoObject.VertexObject vertexObject, int i)
        {
            this.VertexObject = vertexObject;
            this.i = i;
        }

        public readonly GeoObject.VertexObject VertexObject;
        public readonly int i;
    }
}
