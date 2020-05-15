using TheNCube;
using UnityEngine;

namespace Assets
{
    internal class GeoObject
    {
        VertexObject[] VertexLocations;
        FaceObject[] FaceObjects;

        

        public GeoObject()
        {

        }










        internal class VertexObject
        {
            PointND Position;
            KMSelectable ModuleVertex;
        }
        
        internal class FaceObject
        {
            internal int[] verticeIds;
            internal VertexObject[] vertexObjects;
            internal MeshFilter face;
        }
    }
}