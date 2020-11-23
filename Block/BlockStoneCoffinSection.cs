using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    // Steelmaking
    // 1. Craft stone coffing
    // 2. Combine both halves into one whole
    // 3. Add coal, add ingots, add coal, till full
    // 4. 

    public class BlockStoneCoffinSection : Block
    {
        public BlockFacing Orientation => BlockFacing.FromCode(Variant["side"]);

        public bool ControllerBlock => EntityClass != null;

        public bool IsCompleteCoffin(BlockPos pos)
        {
            var otherblock = api.World.BlockAccessor.GetBlock(pos.AddCopy(Orientation.Opposite)) as BlockStoneCoffinSection;
            return otherblock != null && otherblock.Orientation == Orientation.Opposite;
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if (blockFace == BlockFacing.UP && block.FirstCodePart() == "stonecoffinlid") return true;

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;
            if (!ControllerBlock)
            {
                pos = GetControllerBlockPositionOrNull(blockSel.Position);
            }
            
            if (pos == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            BlockEntityStoneCoffin besc = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityStoneCoffin;
            if (besc != null)
            {
                bool placed = besc.Interact(byPlayer);
                (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);

        }

        BlockPos GetControllerBlockPositionOrNull(BlockPos pos)
        {
            if (!ControllerBlock && IsCompleteCoffin(pos)) return pos.AddCopy(Orientation.Opposite);
            return null;
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockPos npos;
            if ((npos = GetControllerBlockPositionOrNull(pos)) != null)
            {
                return api.World.BlockAccessor.GetBlock(npos).GetPlacedBlockInfo(world, npos, forPlayer);
            }

            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            BlockPos npos;
            if ((npos = GetControllerBlockPositionOrNull(selection.Position)) != null)
            {
                BlockSelection nsele = selection.Clone();
                nsele.Position = npos;

                return api.World.BlockAccessor.GetBlock(npos).GetPlacedBlockInteractionHelp(world, nsele, forPlayer);
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }


        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            BlockPos npos;
            if ((npos = GetControllerBlockPositionOrNull(pos)) != null)
            {
                world.BlockAccessor.BreakBlock(npos, byPlayer, dropQuantityMultiplier);
            }

            if (ControllerBlock && IsCompleteCoffin(pos))
            {
                world.BlockAccessor.BreakBlock(pos.AddCopy(Orientation.Opposite), byPlayer, dropQuantityMultiplier);
            }
        }

    }

}
