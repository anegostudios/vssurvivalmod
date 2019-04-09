using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorUnstable : BlockBehavior
    {
        BlockFacing[] AttachedToFaces;

        public BlockBehaviorUnstable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            AttachedToFaces = new BlockFacing[] { BlockFacing.DOWN };

            if (properties["attachedToFaces"].Exists)
            {
                string[] faces = properties["attachedToFaces"].AsStringArray();
                AttachedToFaces = new BlockFacing[faces.Length];

                for (int i = 0; i < faces.Length; i++)
                {
                    AttachedToFaces[i] = BlockFacing.FromCode(faces[i]);
                }
            }
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled, ref string failureCode)
        {
            if (!IsAttached(world.BlockAccessor, blockSel.Position))
            {
                handled = EnumHandling.PreventSubsequent;
                failureCode = "requiresolidground";
                return false;
            }

            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            if (!IsAttached(world.BlockAccessor, pos))
            {
                handled = EnumHandling.PreventDefault;
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos, ref handled);
        }




        internal virtual bool IsAttached(IBlockAccessor blockAccessor, BlockPos pos)
        {
            for (int i = 0; i < AttachedToFaces.Length; i++)
            {
                BlockFacing face = AttachedToFaces[i];

                Block block = blockAccessor.GetBlock(pos.AddCopy(face));

                if (block.CanAttachBlockAt(blockAccessor, block, pos.AddCopy(face), face.GetOpposite()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
