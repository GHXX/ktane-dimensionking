using System;
using System.Collections.Generic;
using System.Linq;
using TheNCube;
using UnityEngine;

namespace DimensionKing
{
    internal class GeoObject : MonoBehaviour
    {
        private readonly List<VertexObject> VertexLocations;
        private readonly List<EdgeObject> EdgeObjects;
        private readonly List<FaceObject> FaceObjects;

        private int dimensionCount = 0;


        public GeoObject(Transform baseVertex, Transform baseEdge, Transform baseFace)
        {
            this.VertexLocations = new List<VertexObject>() { new VertexObject(new VecNd()) { vertexTransform = baseVertex } };
            this.EdgeObjects = new List<EdgeObject>();
            this.FaceObjects = new List<FaceObject>();
        }

        //public void SetupAndResolveVertices() // link vertices to VertexLocations[].ModuleVertex create and delete as needed, same for edges and faces
        //{
        //    var dmko = module.transform.GetChild(0).transform; // dimensionKingObject
        //    if (!this.initialized)
        //    {
        //        for (int i = 0; i < dmko.childCount; i++) // resolve base-building-blocks to be able to duplicate them later
        //        {
        //            var child = dmko.GetChild(i);
        //            var meshKind = child.GetComponent<MeshFilter>().mesh;

        //            if (meshKind.vertexCount == 1) // this is a vertex
        //            {
        //                if (this.baseVertex == null)
        //                {
        //                    this.baseVertex = child;
        //                }
        //            }
        //            else if (meshKind.vertexCount == 1) // this is a edge
        //            {
        //                if (this.baseEdge == null)
        //                {
        //                    this.baseEdge = child;
        //                }
        //            }
        //            else // this is a face
        //            {
        //                if (this.baseFace == null)
        //                {
        //                    this.baseFace = child;
        //                }
        //            }
        //        }


        //        this.initialized = true;
        //    }
        //}

        public void LoadVerticesEdgesAndFaces(float[][] newVertexPositions, int[][] newEdgeVertexIds, int[][] newFaceVertexIds)
        {
            this.dimensionCount = newVertexPositions[0].Length;

            DestroyExessAndCreateRequired(this.VertexLocations, newVertexPositions.Length);
            for (int i = 0; i < newVertexPositions.Length; i++)
            {
                this.VertexLocations[i].position = new VecNd(newVertexPositions[i].Cast<double>().ToArray());
            }

            DestroyExessAndCreateRequired(this.EdgeObjects, newVertexPositions.Length);
            for (int i = 0; i < newEdgeVertexIds.Length; i++)
            {
                this.EdgeObjects[i].vertexObjects = newEdgeVertexIds[i].Select(x => this.VertexLocations[x]).ToArray();
                this.EdgeObjects[i].RecalculateMesh();
            }

            DestroyExessAndCreateRequired(this.FaceObjects, newVertexPositions.Length);
            for (int i = 0; i < newFaceVertexIds.Length; i++)
            {
                this.FaceObjects[i].vertexObjects = newFaceVertexIds[i].Select(x => this.VertexLocations[x]).ToArray();
            }

            RecalculateMeshes();
        }

        private void RecalculateMeshes()
        {
            for (int i = 0; i < this.EdgeObjects.Count; i++)
            {
                this.EdgeObjects[i].RecalculateMesh();
            }

            for (int i = 0; i < this.FaceObjects.Count; i++)
            {
                this.FaceObjects[i].RecalculateMesh(); // TODO could optimize by only recalculating normals for changed meshes
            }
        }

        /// <summary>
        /// Rotates the <see cref="GeoObject"/> along the axis normal to the face which is enclosed by axis A and axis b.
        /// </summary>
        /// <param name="axisIndexA"></param>
        /// <param name="axisIndexB"></param>
        /// <param name="progress">The progress of the current rotation</param>
        public void Rotate(int axisIndexA, int axisIndexB, float progress)
        {
            var angle = Helpers.GetRotationProgress(progress, 3);
            var matrix = new double[this.dimensionCount * this.dimensionCount];
            for (int i = 0; i < this.dimensionCount; i++)
                for (int j = 0; j < this.dimensionCount; j++)
                    matrix[i + this.dimensionCount * j] =
                        i == axisIndexA && j == axisIndexA ? Mathf.Cos(angle) :
                        i == axisIndexA && j == axisIndexB ? Mathf.Sin(angle) :
                        i == axisIndexB && j == axisIndexA ? -Mathf.Sin(angle) :
                        i == axisIndexB && j == axisIndexB ? Mathf.Cos(angle) :
                        i == j ? 1 : 0;

            for (int i = 0; i < this.VertexLocations.Count; i++)
            {
                this.VertexLocations[i].position *= matrix;
            }

            RecalculateMeshes();
        }

        private void DestroyExessAndCreateRequired<T>(List<T> collection, int newCount) where T : IDestroyable<T>
        {
            if (newCount < collection.Count) // if the new array shield have less items than the previous then destroy the excess ones
            {
                for (int i = collection.Count - 1; i >= newCount; i--)
                {
                    collection[i].Destroy();
                }
            }
            else if (newCount > collection.Count)
            {
                for (int i = collection.Count - 1; i >= newCount; i--)
                {
                    collection[i] = collection[0].CreateNewInstance();
                }
            }
        }

        internal class VertexObject : IDestroyable<VertexObject>
        {
            internal VecNd position;
            internal Transform vertexTransform;

            private Vector3 projectedCache = new VecNd().Project();
            private VecNd projectedCachedInput = new VecNd();
            internal Vector3 ProjectTo3D()
            {
                if (!this.position.ValueEquals(this.projectedCachedInput)) // TODO check if the cache code is actually useful in terms of cpu efficiency
                {
                    this.projectedCachedInput = this.position;
                    this.projectedCache = this.position.Project();
                }
                return this.projectedCache;
            }

            public VertexObject(VecNd position)
            {
                this.position = position;
            }

            private VertexObject(Transform t)
            {
                this.vertexTransform = t;
            }

            void IDestroyable<VertexObject>.Destroy()
            {
                Destroy(this.vertexTransform);
            }

            VertexObject IDestroyable<VertexObject>.CreateNewInstance()
            {
                return new VertexObject(Instantiate(this.vertexTransform));
            }
        }

        internal class EdgeObject : IDestroyable<EdgeObject>
        {
            internal VertexObject[] vertexObjects;
            internal Transform edgeTransform;
            internal MeshFilter edgeMesh;

            public EdgeObject(VertexObject[] vertexObjects)
            {
                if (vertexObjects.Length != 2)
                {
                    throw new ArgumentException("Every edge has to have two vertices!");
                }

                this.vertexObjects = vertexObjects;
            }

            private EdgeObject(MeshFilter mesh, Transform t)
            {
                this.edgeMesh = mesh;
                this.edgeTransform = t;
            }

            void IDestroyable<EdgeObject>.Destroy()
            {
                Destroy(this.edgeMesh);
            }

            EdgeObject IDestroyable<EdgeObject>.CreateNewInstance()
            {
                return new EdgeObject(Instantiate(this.edgeMesh), Instantiate(this.edgeTransform));
            }

            internal Vector3[] GetEdgeVertexPositions()
            {
                var retval = new Vector3[2];

                for (int i = 0; i < 2; i++)
                {
                    retval[i] = this.vertexObjects[i].ProjectTo3D();
                }

                return retval;
            }

            internal void RecalculateMesh()
            {
                var pos1 = this.vertexObjects[0].ProjectTo3D();
                var pos2 = this.vertexObjects[1].ProjectTo3D();

                var deltaVector = pos2 - pos1;
                var deltaVectorNormalized = deltaVector.normalized;
                this.edgeMesh.transform.localPosition = (pos1 + pos2) / 2;
                this.edgeMesh.transform.localScale = new Vector3(0.1f, deltaVector.magnitude, 0.1f);

                this.edgeMesh.transform.rotation = new Quaternion(deltaVectorNormalized.x, deltaVectorNormalized.y, deltaVectorNormalized.z, 0);
            }
        }

        internal class FaceObject : IDestroyable<FaceObject>
        {
            internal VertexObject[] vertexObjects;
            internal MeshFilter faceMesh;
            internal Transform faceTransform;

            public FaceObject(VertexObject[] vertexObjects)
            {
                if (vertexObjects.Length < 3)
                {
                    throw new ArgumentException("Every face has to have at least 3 vertices!");
                }

                this.vertexObjects = vertexObjects;
            }

            private FaceObject(MeshFilter mesh, Transform t)
            {
                this.faceMesh = mesh;
                this.faceTransform = t;
            }

            void IDestroyable<FaceObject>.Destroy()
            {
                Destroy(this.faceMesh);
            }

            FaceObject IDestroyable<FaceObject>.CreateNewInstance()
            {
                return new FaceObject(Instantiate(this.faceMesh), Instantiate(this.faceTransform));
            }

            internal Vector3[] GetFaceVertexPositions()
            {
                var retval = new Vector3[this.vertexObjects.Length];

                for (int i = 0; i < this.vertexObjects.Length; i++)
                {
                    retval[i] = this.vertexObjects[i].ProjectTo3D();
                }

                return retval;
            }

            internal void RecalculateMesh()
            {
                this.faceMesh.mesh.vertices = GetFaceVertexPositions();
                this.faceMesh.mesh.RecalculateNormals();
            }
        }

        internal interface IDestroyable<T>
        {
            void Destroy();
            T CreateNewInstance();
        }
    }
}