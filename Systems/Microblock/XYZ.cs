using System;

#nullable disable

namespace Vintagestory.GameContent
{
    public struct XYZ : IEquatable<XYZ>
    {
        public int X;
        public int Y;
        public int Z;

        public XYZ(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(XYZ other)
        {
            return other.X == X && other.Y == Y && other.Z == Z;
        }

        /// <summary>
        /// Returns the n-th coordinate
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public int this[int index]
        {
            get { return ((2 - index) / 2) * X + (index % 2) * Y + (index / 2) * Z; }   // branch-free code to result in X if index is 0, Y if index is 1, Z if index is 2
            set { if (index == 0) X = value; else if (index == 1) Y = value; else Z = value; }
        }

    }
}
