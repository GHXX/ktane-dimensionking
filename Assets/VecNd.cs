using System;
using System.Linq;
using UnityEngine;

namespace TheNCube
{
    struct VecNd
    {
        public double[] Components { get; private set; }

        public VecNd(params double[] coords)
        {
            this.Components = coords;
        }

        private static readonly float wLen = .2f;//1 / Mathf.Sqrt(3);
        //private static readonly Vector3[] nonXyzVectors = new[] { // TODO make N-Capable
        //    new Vector3(2 * wLen, 2 * wLen, wLen), // W
        //    new Vector3(wLen, 2 * wLen, 2 * wLen), // V
        //};

        private static Vector3[] _nonXyzVectors = new Vector3[0];

        private static Vector3[] GetNonXyzVectors(int amount)
        {
            if (_nonXyzVectors.Length < amount)
            {
                _nonXyzVectors = CoordGenerator(amount);
            }

            return _nonXyzVectors.Take(amount).ToArray();
        }

        private Vector3[] NonXyzVectors { get { return GetNonXyzVectors(this.Components.Length - 3); } }

        private static Vector3[] CoordGenerator(int amount)
        {
            var retvalue = new Vector3[amount];
            for (int i = 0; i < amount; i++)
            {
                int lengthfactor = i / 6 + 1;
                int mod6 = i % 6;
                if (i % 12 > 2)
                    lengthfactor *= -1;

                float scale345 = 1.1f;

                switch (mod6)
                {
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

        public Vector3 Project()
        {
            var retval = new Vector3((float)this.Components[0], (float)this.Components[1], (float)this.Components[2]);
            for (int dim = 0; dim < this.NonXyzVectors.Length; dim++)
            {
                retval += (float)this.Components[dim + 3] * this.NonXyzVectors[dim];
            }
            return retval;
        }

        public static VecNd operator *(VecNd p, double[] matrix)
        {
            var args = new double[p.Components.Length];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = 0;
                for (int j = 0; j < args.Length; j++)
                {
                    args[i] += matrix[i * args.Length + j] * p.Components[j];
                }
            }

            return new VecNd(args);
        }

        public bool ValueEquals(VecNd other)
        {
            if (other.Components.Length != this.Components.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Components.Length; i++)
            {
                if (this.Components[i] != other.Components[i])
                {
                    return false;
                }
            }

            return true;
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            throw new NotImplementedException("Use ValueEquals instead!");
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException("Dont use this!");
        }
    }
}
