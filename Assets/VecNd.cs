using System;
using System.Linq;
using UnityEngine;

namespace DimensionKing {
    internal struct VecNd {
        public double[] Components { get; private set; }

        public VecNd(params double[] coords) {
            Components = coords;
        }

        private static readonly float wLen = .2f;//1 / Mathf.Sqrt(3);
        //private static readonly Vector3[] nonXyzVectors = new[] { // TODO make N-Capable
        //    new Vector3(2 * wLen, 2 * wLen, wLen), // W
        //    new Vector3(wLen, 2 * wLen, 2 * wLen), // V
        //};

        private static Vector3[] _nonXyzVectors = new Vector3[0];

        private static Vector3[] GetNonXyzVectors(int amount) {
            if (_nonXyzVectors.Length < amount) {
                _nonXyzVectors = CoordGenerator(amount);
            }

            return _nonXyzVectors.Take(amount).ToArray();
        }

        private Vector3[] NonXyzVectors { get { return GetNonXyzVectors(Components.Length - 3); } }

        private static Vector3[] CoordGenerator(int amount) {
            var retvalue = new Vector3[amount];
            for (int i = 0; i < amount; i++) {
                int lengthfactor = i / 6 + 1;
                int mod6 = i % 6;
                if (i % 12 > 2)
                    lengthfactor *= -1;

                float scale345 = 1.1f;

                switch (mod6) {
                    case 0:
                        retvalue[i] = new Vector3(2, 2, 1); break;
                    case 1:
                        retvalue[i] = new Vector3(1, 2, 2) * scale345; break;
                    case 2:
                        retvalue[i] = new Vector3(2, 1, 2) * scale345 * scale345; break;
                    case 3:
                        retvalue[i] = new Vector3(-2, 2, 1) * scale345 * scale345 * scale345; break;
                    case 4:
                        retvalue[i] = new Vector3(-1, 2, 2) * scale345 * scale345 * scale345 * scale345; break;
                    case 5:
                        retvalue[i] = new Vector3(-2, 1, 2) * scale345 * scale345 * scale345 * scale345 * scale345; break;
                }
                retvalue[i] *= wLen * lengthfactor;
            }
            return retvalue;
        }

        public Vector3 Project() {
            var retval = new Vector3(Components.Length > 0 ? (float)Components[0] : 0, Components.Length > 1 ? (float)Components[1] : 0, Components.Length > 2 ? (float)Components[2] : 0);
            for (int dim = 0; dim < NonXyzVectors.Length; dim++) {
                retval += (float)Components[dim + 3] * NonXyzVectors[dim];
            }
            return retval;
        }

        public static VecNd operator *(VecNd p, double[] matrix) {
            var args = new double[p.Components.Length];
            for (int i = 0; i < args.Length; i++) {
                args[i] = 0;
                for (int j = 0; j < args.Length; j++) {
                    args[i] += matrix[i * args.Length + j] * p.Components[j];
                }
            }

            return new VecNd(args);
        }

        public static VecNd operator *(VecNd v, float scale) {
            return new VecNd(v.Components.Select(x => x * scale).ToArray());
        }

        public static VecNd operator +(VecNd v1, VecNd v2) {
            if (v1.Components.Length != v2.Components.Length)
                throw new ArgumentException("The vectors are of different dimensions!");

            for (int i = 0; i < v2.Components.Length; i++) {
                v1.Components[i] += v2.Components[i];
            }

            return v1;
        }

        public bool ValueEquals(VecNd other) {
            if (other.Components == null || Components == null) // if either are null then return whether both are null
            {
                return (other.Components == null) && (Components == null);
            }

            if (other.Components.Length != Components.Length) {
                return false;
            }

            for (int i = 0; i < Components.Length; i++) {
                if (Components[i] != other.Components[i]) {
                    return false;
                }
            }

            return true;
        }

        // override object.Equals
        public override bool Equals(object obj) {
            throw new NotImplementedException("Use ValueEquals instead!");
        }

        public override int GetHashCode() {
            throw new NotImplementedException("Dont use this!");
        }
    }
}
