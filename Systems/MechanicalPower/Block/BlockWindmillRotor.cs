using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockWindmillRotor : BlockMPBase, IMPPowered
    {
        protected BlockFacing powerOutFacing;

        public override void OnLoaded(ICoreAPI api)
        {
            powerOutFacing = BlockFacing.FromCode(Variant["side"]).Opposite;
            base.OnLoaded(api);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return face == powerOutFacing && HasCompatibleConnector(forBlock);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockAxle.TryGetHorizontalEndPlacement(world, blockSel, out BlockAxle clickedAxle, out BlockPos axlePos, out BlockFacing axleEnd, out BlockSelection placement);

            if (!CanPlaceBlock(world, byPlayer, placement, ref failureCode))
            {
                return false;
            }

            if (axleEnd != null)
            {
                if (!HasCompatibleConnector(clickedAxle))
                {
                    failureCode = "incompatibleconnector";
                    return false;
                }

                Block toPlaceBlock = world.GetBlock(CodeWithVariant("side", axleEnd.Code));
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, placement.Position, itemstack);

                clickedAxle.DidConnectAt(world, axlePos, axleEnd);
                WasPlaced(world, placement.Position, axleEnd.Opposite);
                return true;
            }

            // The block is placed here rather than through doPlace that way it receives the held stack.
            BlockFacing face = GetFacingForPlacement(world, placement.Position, false, out bool invalid);
            if (invalid) return false;

            bool ok = face != null;
            if (ok)
            {
                Block toPlaceBlock = world.GetBlock(CodeWithVariant("side", face.Opposite.Code));
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, placement.Position, itemstack);
                BlockPos neighbourPos = placement.Position.AddCopy(face);
                (world.BlockAccessor.GetBlock(neighbourPos) as IMechanicalPowerBlock)?.DidConnectAt(world, neighbourPos, face.Opposite);
            }
            else ok = base.TryPlaceBlock(world, byPlayer, itemstack, placement, ref failureCode);

            if (ok)
            {
                WasPlaced(world, placement.Position, face);      // face may be null here if we called the base method
            }
            return ok;
        }

        protected virtual BlockFacing GetFacingForPlacement(IWorldAccessor world, BlockPos position, bool doPlace, out bool invalid)
        {
            invalid = false;
            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                BlockPos pos = position.AddCopy(face);
                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                // Prevent rotor back-to-back placement, regardless of connector type.
                if (block is IMPPowered)
                {
                    if (!block.HasMechPowerConnectorAt(world, pos, face.Opposite, null)) continue;

                    invalid = true;
                    return null;
                }

                if (block != null && HasCompatibleConnector(block as BlockMPBase))
                {
                    if (block.HasMechPowerConnectorAt(world, pos, face.Opposite, this))
                    {
                        if (doPlace)
                        {
                            Block toPlaceBlock = world.GetBlock(CodeWithVariant("side", face.Opposite.Code));
                            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, position);
                            block.DidConnectAt(world, pos, face.Opposite);
                        }

                        return face;
                    }
                }
            }

            return null;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEBehaviorWindmillRotor be = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorWindmillRotor>();
            if (be != null) return be.OnInteract(byPlayer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            BEBehaviorWindmillRotor be = world.BlockAccessor.GetBlockEntity(selection.Position)?.GetBehavior<BEBehaviorWindmillRotor>();
            if (be != null && be.SailLength >= 3) return System.Array.Empty<WorldInteraction>();

            return
            [
                ..base.GetPlacedBlockInteractionHelp(world, selection, forPlayer),
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-addsails",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("sail")), 4) }
                }
            ];
        }


        public BlockFacing GetFacing()
        {
            return BlockFacing.FromCode(Variant["side"]);
        }
    }
}
