using UnityEngine;

namespace TheUltracube
{
    struct Point5D
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }
        public double W { get; private set; }
        public double V { get; private set; }

        public Point5D(double x, double y, double z, double w, double v)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            V = v;
        }

        private static readonly float wLen = .2f;//1 / Mathf.Sqrt(3);
        private static readonly Vector3 wVec = new Vector3(2 * wLen, 2 * wLen, wLen);
        private static readonly Vector3 vVec = new Vector3(wLen, 2 * wLen, 2 * wLen);

        public Vector3 Project()
        {
            return new Vector3((float) X, (float) Y, (float) Z) + (float) W * wVec + (float) V * vVec;
        }

        public static Point5D operator *(Point5D p, double[] matrix)
        {
            return new Point5D
            (
                matrix[0] * p.X + matrix[1] * p.Y + matrix[2] * p.Z + matrix[3] * p.W + matrix[4] * p.V,
                matrix[5] * p.X + matrix[6] * p.Y + matrix[7] * p.Z + matrix[8] * p.W + matrix[9] * p.V,
                matrix[10] * p.X + matrix[11] * p.Y + matrix[12] * p.Z + matrix[13] * p.W + matrix[14] * p.V,
                matrix[15] * p.X + matrix[16] * p.Y + matrix[17] * p.Z + matrix[18] * p.W + matrix[19] * p.V,
                matrix[20] * p.X + matrix[21] * p.Y + matrix[22] * p.Z + matrix[23] * p.W + matrix[24] * p.V
            );
        }
    }
}
