using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.NoObf
{
    public class InerhitableRotatableCube
    {
        public float? X1;
        public float? Y1;
        public float? Z1;

        public float? X2;
        public float? Y2;
        public float? Z2;

        public float RotateX = 0;
        public float RotateY = 0;
        public float RotateZ = 0;

        public Cuboidf InheritedCopy(Cuboidf parent)
        {
            Cuboidf finalCube = new Cuboidf(
                X1 == null ? parent.X1 : (float)X1,
                Y1 == null ? parent.Y1 : (float)Y1,
                Z1 == null ? parent.Z1 : (float)Z1,
                X2 == null ? parent.X2 : (float)X2,
                Y2 == null ? parent.Y2 : (float)Y2,
                Z2 == null ? parent.Z2 : (float)Z2
            );

            return finalCube.RotatedCopy(RotateX, RotateY, RotateZ, new Vec3d(0.5, 0.5, 0.5));
        }

    }
}
