using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace DimensionKing {
    internal class GeoObject : ScriptableObject {
        private List<VertexObject> VertexLocations;

        internal event EventHandler<VertexPressedEventArgs> OnVertexClicked;

        internal VecNd[] OriginalVertexLocations;
        internal VecNd[] GetVertexLocations { get { return VertexLocations.Select(x => x.position).ToArray(); } }
        internal void SetVertexLocations(VecNd[] newLocations) {
            if (newLocations.Length != VertexLocations.Count) {
                throw new ArgumentException("The length of the passed vertice array is not equal to the expected length!");
            }

            for (int i = 0; i < newLocations.Length; i++) {
                VertexLocations[i].position = newLocations[i];
            }

            RecalculateMeshes();
        }

        private List<EdgeObject> EdgeObjects;
        private List<FaceObject> FaceObjects;

        public int dimensionCount = 0;


        public GeoObject() { }

        public void LoadVerticesEdgesAndFaces(float[][] newVertexPositions, int[][] newEdgeVertexIds, int[][] newFaceVertexIds) {
            dimensionCount = newVertexPositions[0].Length;

            DestroyExessAndCreateRequired(VertexLocations, newVertexPositions.Length);
            for (int i = 0; i < newVertexPositions.Length; i++) {
                VertexLocations[i].position = new VecNd(newVertexPositions[i].Select(x => (double)x).ToArray());
            }

            OriginalVertexLocations = VertexLocations.Select(x => x.position).ToArray();

            DestroyExessAndCreateRequired(EdgeObjects, newEdgeVertexIds.Length);
            for (int i = 0; i < newEdgeVertexIds.Length; i++) {
                EdgeObjects[i].vertexObjects = newEdgeVertexIds[i].Select(x => VertexLocations[x]).ToArray();
            }

            DestroyExessAndCreateRequired(FaceObjects, newFaceVertexIds.Length);
            for (int i = 0; i < newFaceVertexIds.Length; i++) {
                FaceObjects[i].vertexObjects = newFaceVertexIds[i].Select(x => VertexLocations[x]).ToArray();
            }

            var dk = VertexLocations[0].vertexTransform.parent.parent.parent;
            var kmsel = dk.GetComponent<KMSelectable>();

            var arr = new KMSelectable[VertexLocations.Count];
            for (int i = 0; i < arr.Length; i++) {
                arr[i] = VertexLocations[i].vertexTransform.GetComponent<KMSelectable>();
                //arr[i].Highlight = arr[0].Highlight;
                VertexLocations[i].vertexTransform.GetComponent<KMSelectable>().OnInteract = OnVertexClickedInternal(VertexLocations[i], i);
            }
            kmsel.ChildRowLength = arr.Length;
            kmsel.Children = arr;
            kmsel.UpdateChildren();

            RecalculateMeshes();
        }

        internal void SetBaseObjects(Transform baseVertex, Transform baseEdge, Transform baseFace) {
            VertexLocations = new List<VertexObject>() { new VertexObject(new VecNd()) { vertexTransform = baseVertex } };
            EdgeObjects = new List<EdgeObject>() {
                new EdgeObject(new[] { VertexLocations[0], VertexLocations[0] })
                {
                    edgeMesh = baseEdge.GetComponent<MeshFilter>(), edgeTransform = baseEdge
                }
            };

            FaceObjects = new List<FaceObject>() {
                new FaceObject(new[] { VertexLocations[0], VertexLocations[0], VertexLocations[0], VertexLocations[0] })
                {
                    faceMesh = baseFace.GetComponent<MeshFilter>(), faceTransform = baseFace
                }
            };
        }

        internal IEnumerator PhaseToNewObjectAndSetMaterialColor(float[][] newVertexPositions, int[][] newEdgeVertexIds, int[][] newFaceVertexIds, Color newVertexColor) {
            var returnDurationA = 2f;
            var returnElapsedA = 0f;

            var currVertPositions = GetVertexLocations;

            var zeroVecNd = new VecNd(new double[currVertPositions[0].Components.Length]);
            var newVertPositionsA = Enumerable.Range(0, currVertPositions.Length).Select(x => zeroVecNd).ToArray();


            while (returnElapsedA < returnDurationA) {
                float currDistance = Helpers.GetRotationProgress(returnElapsedA / returnDurationA, 3);

                var newPos = Enumerable.Range(0, currVertPositions.Length)
                    .Select(i => newVertPositionsA[i] * currDistance + currVertPositions[i] * (1 - currDistance)).ToArray();

                SetVertexLocations(newPos);

                yield return null;
                returnElapsedA += Time.deltaTime;
            }

            yield return null;

            LoadVerticesEdgesAndFaces(newVertexPositions.Select(x => new float[x.Length]).ToArray(), newEdgeVertexIds, newFaceVertexIds);

            IList<VertexObject> verts = GetVertexObjects();
            for (int i = 0; i < verts.Count; i++) {
                verts[i].GetTransform().GetComponent<MeshRenderer>().material.color = newVertexColor;
            }

            yield return null;

            var returnDurationB = 2f;
            var returnElapsedB = 0f;

            var newVecNdVertexPositionsB = newVertexPositions.Select(x => new VecNd(x.Select(a => (double)a).ToArray())).ToArray();
            var zeroVecNdB = new VecNd(new double[newVecNdVertexPositionsB[0].Components.Length]);
            var oldVertPositionsB = Enumerable.Range(0, newVecNdVertexPositionsB.Length).Select(x => zeroVecNdB).ToArray();

            while (returnElapsedB < returnDurationB) {
                float currDistance = Helpers.GetRotationProgress(returnElapsedB / returnDurationB, 3);

                var newPos = Enumerable.Range(0, oldVertPositionsB.Length)
                    .Select(i => newVecNdVertexPositionsB[i] * currDistance + oldVertPositionsB[i] * (1 - currDistance)).ToArray();

                SetVertexLocations(newPos);

                yield return null;
                returnElapsedB += Time.deltaTime;
            }

            yield return null;
        }


        private void RecalculateMeshes() {
            var min = VertexLocations[0].UpdatePosition();
            var max = min;

            for (int i = 0; i < VertexLocations.Count; i++) {
                var newPos = VertexLocations[i].UpdatePosition();
                min = Vector3.Min(newPos, min);
                max = Vector3.Max(newPos, max);
            }

            //this.VertexLocations[0].GetTransform().parent.localPosition = (min + max) / 2;

            for (int i = 0; i < EdgeObjects.Count; i++) {
                EdgeObjects[i].RecalculateMesh();
            }

            for (int i = 0; i < FaceObjects.Count; i++) {
                FaceObjects[i].RecalculateMesh(); // TODO could optimize by only recalculating normals for changed meshes
            }
        }

        /// <summary>
        /// Rotates the <see cref="GeoObject"/> along the axis normal to the face which is enclosed by axis A and axis b.
        /// </summary>
        /// <param name="axisIndexA"></param>
        /// <param name="axisIndexB"></param>
        /// <param name="angle">How much to rotate it by.</param>
        public void Rotate(int axisIndexA, int axisIndexB, float angle) {
            var matrix = new double[dimensionCount * dimensionCount];
            for (int i = 0; i < dimensionCount; i++)
                for (int j = 0; j < dimensionCount; j++)
                    matrix[i + dimensionCount * j] =
                        i == axisIndexA && j == axisIndexA ? Mathf.Cos(angle) :
                        i == axisIndexA && j == axisIndexB ? Mathf.Sin(angle) :
                        i == axisIndexB && j == axisIndexA ? -Mathf.Sin(angle) :
                        i == axisIndexB && j == axisIndexB ? Mathf.Cos(angle) :
                        i == j ? 1 : 0;

            for (int i = 0; i < VertexLocations.Count; i++) {
                VertexLocations[i].position *= matrix;
            }
            RecalculateMeshes();
        }

        internal ReadOnlyCollection<VertexObject> GetVertexObjects() {
            return VertexLocations.AsReadOnly();
        }

        private void DestroyExessAndCreateRequired<T>(List<T> collection, int newCount) where T : IDestroyable<T> {
            if (newCount < collection.Count) // if the new array shield have less items than the previous then destroy the excess ones
            {
                int delCount = collection.Count - newCount;

                for (int i = 0; i < delCount; i++) {
                    if (collection.Count - 1 == 0) {
                        collection[0].GetTransform().GetComponent<MeshRenderer>().enabled = false; // if its the last one, disable it instead of deleting it
                    } else {
                        collection[collection.Count - 1].Destroy();
                        collection.RemoveAt(collection.Count - 1);
                    }
                }
            } else if (newCount > collection.Count) {
                if (collection.Count == 1) {
                    collection[0].GetTransform().GetComponent<MeshRenderer>().enabled = true; // reenable the previously disabled meshrenderer
                }

                int addCount = newCount - collection.Count;
                for (int i = 0; i < addCount; i++) {
                    var baseTransform = collection[0].GetTransform();

                    var clone = collection[0].CreateNewInstance();
                    var cloneTransform = clone.GetTransform();

                    var basename = baseTransform.name;
                    cloneTransform.name = basename.Substring(0, basename.Length - 1) + (i + 1);
                    cloneTransform.parent = baseTransform.parent;
                    cloneTransform.localScale = baseTransform.localScale;
                    cloneTransform.localPosition = baseTransform.localPosition;

                    collection.Add(clone);

                }
            }
        }

        internal class VertexObject : IDestroyable<VertexObject> {
            internal VecNd position;
            internal Transform vertexTransform;

            internal Vector3 ProjectTo3D() {
                return position.Project();
            }

            public VertexObject(VecNd position) {
                this.position = position;
            }

            private VertexObject(Transform t) {
                vertexTransform = t;
            }

            void IDestroyable<VertexObject>.Destroy() {
                Destroy(vertexTransform.gameObject);
            }

            VertexObject IDestroyable<VertexObject>.CreateNewInstance() {
                return new VertexObject(Instantiate(vertexTransform));
            }

            public Transform GetTransform() {
                return vertexTransform;
            }

            internal Vector3 UpdatePosition() {
                var pos = ProjectTo3D();
                vertexTransform.localPosition = pos;

                return pos;
            }

            internal KMSelectable GetKMSelectable() {
                return vertexTransform.GetComponent<KMSelectable>();
            }
        }

        private KMSelectable.OnInteractHandler OnVertexClickedInternal(VertexObject vertex, int i) {
            return delegate {
                if (OnVertexClicked != null) {
                    OnVertexClicked.Invoke(this, new VertexPressedEventArgs(vertex, i));
                }
                return false;
            };
        }

        internal class EdgeObject : IDestroyable<EdgeObject> {
            internal VertexObject[] vertexObjects;
            internal Transform edgeTransform;
            internal MeshFilter edgeMesh;

            public EdgeObject(VertexObject[] vertexObjects) {
                if (vertexObjects.Length != 2) {
                    throw new ArgumentException("Every edge has to have two vertices!");
                }

                this.vertexObjects = vertexObjects;
            }

            private EdgeObject(MeshFilter mesh, Transform t) {
                edgeMesh = mesh;
                edgeTransform = t;
            }

            void IDestroyable<EdgeObject>.Destroy() {
                Destroy(edgeMesh.gameObject);
            }

            EdgeObject IDestroyable<EdgeObject>.CreateNewInstance() {
                var t = Instantiate(edgeTransform);
                return new EdgeObject(t.GetComponent<MeshFilter>(), t);
            }

            internal Vector3[] GetEdgeVertexPositions() {
                var retval = new Vector3[2];

                for (int i = 0; i < 2; i++) {
                    retval[i] = vertexObjects[i].ProjectTo3D();
                }

                return retval;
            }

            internal void RecalculateMesh() {
                var pos1 = vertexObjects[0].ProjectTo3D();
                var pos2 = vertexObjects[1].ProjectTo3D();

                var deltaVector = pos2 - pos1;
                var deltaVectorNormalized = deltaVector.normalized;
                edgeMesh.transform.localPosition = (pos1 + pos2) / 2;
                edgeMesh.transform.localScale = new Vector3(0.1f, deltaVector.magnitude / 2f, 0.1f);

                edgeMesh.transform.localRotation = Quaternion.FromToRotation(Vector3.up, pos2 - pos1);
            }
            public Transform GetTransform() {
                return edgeTransform;
            }
        }

        internal class FaceObject : IDestroyable<FaceObject> {
            internal VertexObject[] vertexObjects;
            internal MeshFilter faceMesh;
            internal Transform faceTransform;

            public FaceObject(VertexObject[] vertexObjects) {
                if (vertexObjects.Length < 3) {
                    throw new ArgumentException("Every face has to have at least 3 vertices!");
                }

                this.vertexObjects = vertexObjects;
            }

            private FaceObject(MeshFilter mesh, Transform t) {
                faceMesh = mesh;
                faceTransform = t;
            }

            void IDestroyable<FaceObject>.Destroy() {
                Destroy(faceMesh.gameObject);
            }

            FaceObject IDestroyable<FaceObject>.CreateNewInstance() {
                var t = Instantiate(faceTransform);
                return new FaceObject(t.GetComponent<MeshFilter>(), t);
            }

            internal Vector3[] GetFaceVertexPositions() {
                var retval = new Vector3[vertexObjects.Length];

                for (int i = 0; i < vertexObjects.Length; i++) {
                    retval[i] = vertexObjects[i].ProjectTo3D();
                }

                return retval;
            }

            internal void RecalculateMesh() {
                faceMesh.mesh.Clear();
                var vertices = GetFaceVertexPositions();
                faceMesh.mesh.vertices = vertices;

                int[] triangleIndices;

                switch (vertices.Length) {
                    case 3: triangleIndices = new[] { 0, 1, 2 }; break;
                    case 4:
                        triangleIndices = new[] {
                        0, 1, 2,
                        1, 2, 3
                    }; break;
                    case 5:
                        triangleIndices = new[] {
                        0, 1, 2,
                        0, 2, 3,
                        0, 3, 4
                    }; break;
                    case 6:
                        triangleIndices = new[] {
                        0, 1, 2,
                        2, 3, 5,
                        3, 4, 5,
                        5, 0, 2,
                    }; break;
                    default: throw new NotImplementedException();
                }

                faceMesh.mesh.triangles = triangleIndices;
                faceMesh.transform.localRotation = Quaternion.Euler(0, 0, 0);

                faceMesh.mesh.RecalculateNormals();
            }
            public Transform GetTransform() {
                return faceTransform;
            }
        }

        internal interface IDestroyable<T> {
            void Destroy();
            T CreateNewInstance();

            Transform GetTransform();
        }

        internal void Reset() {
            SetVertexLocations(OriginalVertexLocations);
        }
    }
}