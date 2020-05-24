using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensionKing
{
    public static class SchlafliInterpreter // this code is a modfication of the python code suggested in this answer https://codegolf.stackexchange.com/a/167896
    {
        public static SchlafliStruct GetGeometryDataFromSchlafli(string[] schlafliInput)
        {
            var schlafliFloats = schlafliInput.Select(x => StringFractionToFloat(x)).ToArray();
            float[][] vertexPositions;
            List<int[][]> vertexIndexes;

            bool isOK = TryRegularPolytope(schlafliFloats, out vertexPositions, out vertexIndexes);
            //string st = vertexPositions.Select(x => x.Select(n => Math.Round(n, 4).ToString()).Join()).Join("\n");


            if (!isOK)
            {
                throw new SchlafliInterpreterException("Unable to generate geometry object.");
            }

            // center the vertices
            for (int dim = 0; dim < vertexPositions[0].Length; dim++) // loop all dimensions
            {
                float min = vertexPositions[0][dim];
                float max = min;

                for (int vertexIndex = 0; vertexIndex < vertexPositions.Length; vertexIndex++)
                {
                    var pos = vertexPositions[vertexIndex][dim];
                    min = Math.Min(pos, min);
                    max = Math.Max(pos, max);
                }

                float delta = (min + max) / 2;

                for (int vi = 0; vi < vertexPositions.Length; vi++)
                {
                    vertexPositions[vi][dim] -= delta;
                }

            }

            // normalize vertices
            float magnitude = vertexPositions.Max(x => new VecNd(x.Select(y => (double)y).ToArray()).Project().magnitude);

            for (int i = 0; i < vertexPositions.Length; i++)
            {
                for (int dim2 = 0; dim2 < vertexPositions[0].Length; dim2++)
                {
                    vertexPositions[i][dim2] /= magnitude;
                }
            }

            var s = new SchlafliStruct(vertexPositions, vertexIndexes[0], vertexIndexes[1]);

            return s;
        }

        private static bool TryRegularPolytope(float[] schlafliFloats, out float[][] vertexPositions, out List<int[][]> edgesEtc)
        {
            var dim = schlafliFloats.Length + 1;
            if (dim == 1)
            {
                vertexPositions = new[] { new[] { 0f }, new[] { 1f } };
                edgesEtc = new List<int[][]>();

                return true;
            }

            var gens = CalculateSymmetryGenerators(schlafliFloats);

            float[][] facetVerts;
            List<int[][]> facetEdgesEtc;
            bool ok = TryRegularPolytope(schlafliFloats.Take(schlafliFloats.Length - 1).ToArray(), out facetVerts, out facetEdgesEtc);
            if (!ok)
            {
                vertexPositions = facetVerts;
                edgesEtc = facetEdgesEtc;
                return false;
            }

            var verts = facetVerts.Select(facetVert => facetVert.Concat(new[] { 0f }).ToArray()).ToList();

            var vert2index = new Dictionary<string, int>();
            for (int i = 0; i < verts.Count; i++)
            {
                var vert = verts[i];
                var v2k = Vert2key(vert);
                vert2index.Add(v2k, i);
            }

            var multiplicationTable = new List<int[]>();
            var iVert = 0;
            while (iVert < verts.Count)
            {
                multiplicationTable.Add(new int[gens.Length]);
                for (int iGen = 0; iGen < gens.Length; iGen++)
                {
                    var newVert = MxvHomo(gens[iGen], verts[iVert]);
                    var newVertKey = Vert2key(newVert);

                    if (!vert2index.ContainsKey(newVertKey))
                    {
                        vert2index.Add(newVertKey, verts.Count);
                        verts.Add(newVert);
                    }
                    multiplicationTable[iVert][iGen] = vert2index[newVertKey];
                }

                iVert++;
            }

            facetEdgesEtc.Add(new[] { Enumerable.Range(0, facetVerts.Length).ToArray() });
            var edgesEtc2 = new List<int[][]>();

            foreach (var facetElementsOfSomeDimension in facetEdgesEtc)
            {
                var elts = new List<int[]>(facetElementsOfSomeDimension.Select(x => (int[])x.Clone()).ToArray());
                var elt2index = Enumerable.Range(0, elts.Count).ToDictionary(i => elts[i].Join(","), i => i);
                var iElt = 0;
                while (iElt < elts.Count)
                {
                    for (int iGen = 0; iGen < gens.Length; iGen++)
                    {
                        // TODO make sure the cast doesnt actually break it all
                        var newElt = elts[iElt].Select(iVert2 => (int)multiplicationTable[iVert2][iGen]).OrderBy(x => x).ToArray(); // ivert2 is ambiguous to ivert in the pyscript
                        if (!elt2index.ContainsKey(newElt.Join(",")))
                        {
                            elt2index.Add(newElt.Join(","), elts.Count);
                            elts.Add(newElt);
                        }
                    }
                    iElt++;
                }
                edgesEtc2.Add(elts.ToArray());
            }

            vertexPositions = verts.ToArray();
            edgesEtc = edgesEtc2;
            return true;
        }

        private static float[] MxvHomo(float[][] m, float[] v)
        {
            return m.Select(row => Dot(row, v.Concat(new[] { 1f }).ToArray())).ToArray();
        }

        private static float Dot(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new NotImplementedException();

            return Enumerable.Range(0, a.Length).Sum(i => a[i] * b[i]);
        }

        private static string Vert2key(float[] vert) // TODO fix this bullshit code
        {
            return string.Join(" ", vert.Select(x => Math.Round(x + 0.123f, 5).ToString()).ToArray());
        }

        private static float[][][] CalculateSymmetryGenerators(float[] schlafli) // might be int[][][]
        {
            var dim = schlafli.Length + 1;
            if (dim == 1) return new[] { new[] { new[] { -1f, 1f } } }; // one generator, reflect about x=0.5
            var facetGenerators = CalculateSymmetryGenerators(schlafli.Take(schlafli.Length - 1).ToArray());
            var generators = facetGenerators.Select(x => ExpandHomo(x)).ToList();


            float s;
            float c;
            SinAndCosHalfDihedralAngle(schlafli, out s, out c);

            var v = new float[dim];
            v[dim - 2] = -s;
            v[dim - 1] = c;

            generators.Add(MakeHomo(HouseholderReflection(v)));

            return generators.ToArray();
        }

        private static float[][] HouseholderReflection(float[] v)
        {
            return Mmm(Identity(v.Length), Sxm(2, Outer(v, v)));
        }

        private static float[][] Mmm(int[][] v1, float[][] v2)
        {
            if (v1.Length != v2.Length)
            {
                throw new NotImplementedException(); // need to do math.min of v1 len and v2 len in the forloop as max
            }

            return Enumerable.Range(0, v1.Length).Select(i => Vmv(v1[i], v2[i])).ToArray();
        }

        private static float[] Vmv(int[] v1, float[] v2)
        {
            if (v1.Length != v2.Length)
            {
                throw new NotImplementedException(); // need to do math.min of v1 len and v2 len in the forloop as max
            }

            return Enumerable.Range(0, v1.Length).Select(i => v1[i] - v2[i]).ToArray();
        }

        private static float[][] Sxm(int s, float[][] m)
        {
            return m.Select(row => Sxv(s, row)).ToArray();
        }

        private static float[][] Outer(float[] a, float[] b)
        {
            return a.Select(x => Sxv(x, b)).ToArray();
        }

        private static float[] Sxv(float s, float[] v)
        {
            return v.Select(x => s * x).ToArray();
        }

        private static int[][] Identity(int dim)
        {
            var matrix = new int[dim][];
            for (int i = 0; i < dim; i++)
            {
                var m2 = new int[dim];
                m2[i] = 1;
                matrix[i] = m2;
            }

            return matrix;
        }

        private static float[][] MakeHomo(float[][] m)
        {
            return m.Select(row => row.Concat(new[] { 0f }).ToArray()).ToArray();
        }

        private static void SinAndCosHalfDihedralAngle(float[] schlafli, out float s, out float c)
        {
            var ss = 0d;
            for (int i = 0; i < schlafli.Length; i++)
            {
                var q = schlafli[i];
                ss = Math.Pow(Math.Cos(Math.PI / q), 2) / (1 - ss);
            }

            if (Math.Abs(1 - ss) < 1e-9) // prevent glitch in planar tiling cases
            {
                ss = 1;
            }

            s = (float)Math.Sqrt(ss);
            c = (float)Math.Sqrt(1 - ss);
        }

        private static float[][] ExpandHomo(float[][] m)
        {
            var m2 = new List<float[]>();
            for (int i = 0; i < m.Length; i++)
            {
                var row = m[i].ToList();
                row.Insert(row.Count - 1, 0);
                m2.Add(row.ToArray());
            }

            var m3 = new float[m.Length + 2];
            m3[m3.Length - 2] = 1;

            m2.Add(m3);
            return m2.ToArray();
        }

        private static float StringFractionToFloat(string str)
        {
            if (str.Contains('/'))
            {
                var splitted = str.Split('/');
                return float.Parse(splitted[0]) / float.Parse(splitted[1]);
            }
            else
            {
                return float.Parse(str);
            }
        }

        public struct SchlafliStruct
        {
            public float[][] VertexLocations { get; private set; }
            public int[][] EdgeVertexIndexes { get; private set; }
            public int[][] FaceVertexIndexes { get; private set; }

            public SchlafliStruct(float[][] vertexLocations, int[][] edgeVertexIndexes, int[][] faceVertexIndexes)
            {
                this.VertexLocations = vertexLocations;
                this.EdgeVertexIndexes = edgeVertexIndexes;
                this.FaceVertexIndexes = faceVertexIndexes;
            }
        }
    }
}
