using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.GameContent
{
    public class BlockQuern : BlockMPBase
    {

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            bool ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);

            if (ok)
            {
                if (!tryConnect(world, byPlayer, blockSel.Position, BlockFacing.UP))
                {
                    tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
                }
            }

            return ok;
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuern;

            if (beQuern != null && beQuern.CanGrind() && (blockSel.SelectionBoxIndex == 1 || beQuern.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
            {
                beQuern.SetPlayerGrinding(byPlayer, true);
                return true;
            }
            
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuern;

            if (beQuern != null && (blockSel.SelectionBoxIndex == 1 || beQuern.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
            {
                beQuern.IsGrinding(byPlayer);
                return beQuern.CanGrind();
            }

            return false;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuern;
            if (beQuern != null)
            {
                beQuern.SetPlayerGrinding(byPlayer, false);
            }

        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuern;
            if (beQuern != null)
            {
                beQuern.SetPlayerGrinding(byPlayer, false);
            }


            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (selection.SelectionBoxIndex == 0)
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-quern-addremoveitems",
                        MouseButton = EnumMouseButton.Right
                    }
                }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }
            else
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-quern-grind",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) => {
                            BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityQuern;
                            return beQuern != null && beQuern.CanGrind();
                        }
                    }
                }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == BlockFacing.UP || face == BlockFacing.DOWN;
        }
    }
}
