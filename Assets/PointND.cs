using UnityEngine;

namespace TheUltracube
{
    struct PointND
    {
        public double[] Coordinates { get; private set; }

        public PointND(params double[] coords)
        {
            this.Coordinates = coords;
        }

        private static readonly float wLen = .2f;//1 / Mathf.Sqrt(3);
        private static readonly Vector3[] nonXyzVectors = new[] { // TODO make N-Capable
            new Vector3(2 * wLen, 2 * wLen, wLen),
            new Vector3(wLen, 2 * wLen, 2 * wLen)
        };

        public Vector3 Project()
        {
            var retval = new Vector3((float)this.Coordinates[0], (float)this.Coordinates[1], (float)this.Coordinates[2]);
            for (int dim = 0; dim < nonXyzVectors.Length; dim++)
            {
                retval += (float)this.Coordinates[dim + 3] * nonXyzVectors[dim];
            }
            return retval;
        }

        public static PointND operator *(PointND p, double[] matrix)
        {
            var args = new double[p.Coordinates.Length];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = 0;
                for (int j = 0; j < args.Length; j++)
                {
                    args[i] += matrix[i * args.Length + j] * p.Coordinates[j];
                }
            }

            return new PointND(args);
        }
    }
}
