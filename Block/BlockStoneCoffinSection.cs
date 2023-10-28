using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            int temp = GetTemperature(api.World, pos);
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
                int g = (col>>8) & 0xff;
                int b = (col>>16) & 0xff;
                int a = (col>>24) & 0xff;

                lightRgbsByCorner[i] = (GameMath.Mix(a, 0, System.Math.Min(1, 1.5f * ina)) << 24) | (GameMath.Mix(b, incade[2], ina) << 16) | (GameMath.Mix(g, incade[1], ina) << 8) | GameMath.Mix(r, incade[0], ina);
            }

        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if (blockFace == BlockFacing.UP && block.FirstCodePart() == "stonecoffinlid") return true;

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        /// <summary>
        /// This is called from BehaviorHorizontalOrientable.TryPlaceBlock() - called in the Block with the suggested orientation so this can override the suggested orientation
        /// </summary>
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            BlockPos pos = blockSel.Position;
            Block blockToPlace = this;

            // sneak overrides any placement AI  [##TODO roll this out to other blocks with placement AI, e.g. Chute, MP]
            if (byPlayer?.Entity.Controls.ShiftKey == false)
            {
                // see if any neighbours are incomplete stone coffins and facing this - if so, snap to face them
                foreach (BlockFacing face in BlockFacing.HORIZONTALS)
                {
                    if (world.BlockAccessor.GetBlock(pos.AddCopy(face)) is BlockStoneCoffinSection neib)
                    {
                        if (neib.Orientation == face)
                        {
                            blockToPlace = api.World.GetBlock(CodeWithVariant("side", face.Opposite.Code));
                            break;
                        }
                    }
                }
            }

            world.BlockAccessor.SetBlock(blockToPlace.BlockId, pos);
            return true;
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
                besc.Interact(byPlayer, !ControllerBlock);
                (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public int GetTemperature(IWorldAccessor world, BlockPos pos)
        {
            if (!ControllerBlock)
            {
                pos = GetControllerBlockPositionOrNull(pos);
            }

            if (pos == null) return 0;

            BlockEntityStoneCoffin besc = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityStoneCoffin;
            if (besc != null)
            {
                return besc.CoffinTemperature;
            }

            return 0;
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
