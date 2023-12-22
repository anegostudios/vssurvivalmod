using System;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class CuboidWithBlock : CuboidWithMaterial
    {
        public int BlockId;

        public CuboidWithBlock(CuboidWithMaterial cwm, int blockId)
        {
            this.BlockId = blockId;
            this.Material = cwm.Material;
            Set(cwm.X1, cwm.Y1, cwm.Z1, cwm.X2, cwm.Y2, cwm.Z2);
        }
    }

    public class CuboidWithMaterial : Cuboidi
    {
        public byte Material;

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return X1;
                    case 1: return Y1;
                    case 2: return Z1;
                    case 3: return X2;
                    case 4: return Y2;
                    case 5: return Z2;
                }

                throw new ArgumentOutOfRangeException("Must be index 0..5");
            }
        }

        public Cuboidf ToCuboidf()
        {
            return new Cuboidf(X1 / 16f, Y1/ 16f, Z1 / 16f, X2 / 16f, Y2 / 16f, Z2 / 16f);
        }

        public bool ContainsOrTouches(CuboidWithMaterial neib, int axis)
        {
            switch (axis)
            {
                case 0: // X-Axis
                    return neib.Z2 <= Z2 && neib.Z1 >= Z1 && neib.Y2 <= Y2 && neib.Y1 >= Y1;
                case 1: // Y-Axis
                    return neib.X2 <= X2 && neib.X1 >= X1 && neib.Z2 <= Z2 && neib.Z1 >= Z1;
                case 2: // Z-Axis
                    return neib.X2 <= X2 && neib.X1 >= X1 && neib.Y2 <= Y2 && neib.Y1 >= Y1;
            }

            throw new ArgumentOutOfRangeException("axis must be 0, 1 or 2");
        }
    }
}
