using System;

namespace Vintagestory.GameContent
{
    struct XYZ : IEquatable<XYZ>
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
    }
}
