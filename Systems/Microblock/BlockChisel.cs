﻿using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockChisel : BlockMicroBlock, IWrenchOrientable
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-chisel-removedeco",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = BlockUtil.GetKnifeStacks(api),
                    GetMatchingStacks = (wi, bs, es) => {
                        var bec = GetBlockEntity<BlockEntityChisel>(bs.Position);
                        if (bec?.DecorIds != null && bec.DecorIds[bs.Face.Index] != 0)
                        {
                            return wi.Itemstacks;
                        }
                        return null;
                    }
                }
            };
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if ((inSlot.Itemstack.Attributes["materials"] as StringArrayAttribute)?.value.Length > 1 || (inSlot.Itemstack.Attributes["materials"] as IntArrayAttribute)?.value.Length > 1)
            {
                dsc.AppendLine(Lang.Get("<font color=\"lightblue\">Multimaterial chiseled block</font>"));
            }
        }

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            var bechisel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (byEntity.Controls.CtrlKey)
            {
                int rot = bechisel.DecorRotations;
                int bitshift = blockSel.Face.Index * 3;
                int facerot = rot >> bitshift & DecorBits.maskRotationData;
                rot &= ~(DecorBits.maskRotationData << bitshift);
                rot += ((facerot + 1) & DecorBits.maskRotationData) << bitshift;
                bechisel.DecorRotations = rot;
            }
            else bechisel.RotateModel(dir > 0 ? 90 : -90, null);

            bechisel.MarkDirty(true);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var bechisel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bechisel?.Interact(byPlayer, blockSel) == true) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChisel bec && (api as ICoreClientAPI)?.World.Player.InventoryManager.ActiveTool == EnumTool.Chisel)
            {
                ((ICoreClientAPI)api).Network.SendBlockEntityPacket(pos, 1011);
                return null;
            }

            return base.OnPickBlock(world, pos);
        }

        public override bool TryToRemoveSoilFirst(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            return false;
        }

        public override bool IsSoilNonSoilMix(BlockEntityMicroBlock be)
        {
            return false;
        }

        public override bool IsSoilNonSoilMix(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return false;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
