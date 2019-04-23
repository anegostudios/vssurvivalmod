using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockQuern : Block
    {
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex == 1)
            {
                BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuern;
                if (beQuern != null && beQuern.CanGrind())
                {
                    beQuern.SetPlayerGrinding(byPlayer, true);
                    return true;
                }
            }
            
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityQuern beQuern = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuern;
            if (beQuern != null)
            {
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
                };
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
                };
            }
        }

    }
}
