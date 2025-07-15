using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockCreativeRotor : BlockMPBase, IMPPowered
    {
        BlockFacing powerOutFacing;

        public override void OnLoaded(ICoreAPI api)
        {
            powerOutFacing = BlockFacing.FromCode(Variant["side"]).Opposite;

            base.OnLoaded(api);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == powerOutFacing;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);
                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null)
                {
                    if (block.HasMechPowerConnectorAt(world, pos, face.Opposite))
                    {
                        //Prevent rotor back-to-back placement
                        if (block is IMPPowered) return false;

                        Block toPlaceBlock = world.GetBlock(new AssetLocation(FirstCodePart() + "-" + face.Opposite.Code));
                        world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

                        block.DidConnectAt(world, pos, face.Opposite);
                        WasPlaced(world, blockSel.Position, face);

                        return true;
                    }
                }
            }

            bool ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            if (ok)
            {
                WasPlaced(world, blockSel.Position, null);
            }
            return ok;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEBehaviorMPCreativeRotor be = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorMPCreativeRotor>();
            if (be != null) return be.OnInteract(byPlayer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-creativerotor-power",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = System.Array.Empty<ItemStack>()
                }
            };
        }
    }
}
