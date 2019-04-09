using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ChestLabelRenderer : BlockEntitySignRenderer
    {
        public ChestLabelRenderer(BlockPos pos, ICoreClientAPI api) : base(pos, api)
        {

            Block block = api.World.BlockAccessor.GetBlock(pos);
            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            if (facing == null) return;

            translateY = 0.25f;

            switch (facing.Index)
            {
                case 0: // N
                    translateX = 0.5f;
                    translateZ = 15/16f + 0.001f;
                    rotY = 180;
                    break;
                case 1: // E
                    translateX = 1 / 16f - 0.001f;
                    translateZ = 0.5f;
                    rotY = 90;
                    break;
                case 2: // S
                    translateX = 0.5f;
                    translateZ = 1/ 16f - 0.001f;
                    rotY = 0;
                    break;
                case 3: // W
                    translateX = 15/ 16f + 0.001f;
                    translateZ = 0.5f;
                    rotY = 270;
                    break;
            }
        }


        public override void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            base.OnRenderFrame(deltaTime, stage);
        }
    }
}
