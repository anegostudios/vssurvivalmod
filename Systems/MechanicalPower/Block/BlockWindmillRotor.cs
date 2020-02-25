using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockWindmillRotor : BlockMPBase
    {
        BlockFacing powerOutFacing;

        public override void OnLoaded(ICoreAPI api)
        {
            powerOutFacing = BlockFacing.FromCode(Variant["side"]).GetOpposite();

            base.OnLoaded(api);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }

        public override bool HasConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
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
                    if (block.HasConnectorAt(world, pos, face.GetOpposite()))
                    {
                        Block toPlaceBlock = world.GetBlock(new AssetLocation(FirstCodePart() + "-" + face.GetOpposite().Code));

                        world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

                        block.DidConnectAt(world, pos, face.GetOpposite());
                        WasPlaced(world, blockSel.Position, face);

                        return true;
                    }
                }
            }

            bool ok = base.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            if (ok)
            {
                WasPlaced(world, blockSel.Position, null);
            }
            return ok;
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
            if (be != null && be.SailLength >= 3) return new WorldInteraction[0];


            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-addsails",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("sail")), 4) }
                }
            };
        }

    }
}
