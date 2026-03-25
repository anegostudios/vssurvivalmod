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
            return face == powerOutFacing;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing face = GetFacingForPlacement(world, blockSel.Position, true, out bool invalid);
            if (invalid) return false;

            bool ok = face != null;
            if (!ok) ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);

            if (ok)
            {
                WasPlaced(world, blockSel.Position, face);      // face may be null here if we called the base method
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
                if (block != null)
                {
                    if (block.HasMechPowerConnectorAt(world, pos, face.Opposite, this))
                    {
                        //Prevent rotor back-to-back placement
                        if (block is IMPPowered)
                        {
                            invalid = true;
                            return null;
                        }

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
