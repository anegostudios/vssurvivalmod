using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockStoneCoffinLid : Block
    {
        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, int[] chunkExtIds, ushort[] chunkLightExt, int extIndex3d)
        {
            int nBlockId = chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
            var block = api.World.Blocks[nBlockId] as BlockStoneCoffinSection;

            if (block != null)
            {
                int temp = block.GetTemperature(api.World, pos.DownCopy());
                int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);
                for (int i = 0; i < sourceMesh.FlagsCount; i++)
                {
                    sourceMesh.Flags[i] &= ~0xff;
                    sourceMesh.Flags[i] |= extraGlow;
                }

                int[] incade = ColorUtil.getIncandescenceColor(temp);
                float ina = GameMath.Clamp(incade[3] / 255f, 0, 1);

                for (int i = 0; i < lightRgbsByCorner.Length; i++)
                {
                    int col = lightRgbsByCorner[i];

                    int r = col & 0xff;
                    int g = (col >> 8) & 0xff;
                    int b = (col >> 16) & 0xff;
                    int a = (col >> 24) & 0xff;

                    lightRgbsByCorner[i] = (GameMath.Mix(a, 0, System.Math.Min(1, 1.5f * ina)) << 24) | (GameMath.Mix(b, incade[2], ina) << 16) | (GameMath.Mix(g, incade[1], ina) << 8) | GameMath.Mix(r, incade[0], ina);
                }
            }
        }

    }
}
