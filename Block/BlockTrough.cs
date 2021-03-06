﻿using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockTrough : Block
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            CanStep = false;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null) {
                BlockPos pos = blockSel.Position;
                
                BlockEntityTrough betr = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTrough;
                if (betr != null)
                {
                    bool ok = betr.OnInteract(byPlayer, blockSel);
                    if (ok && world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    }
                    return ok;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            bool flip = Math.Abs(angle) == 90 || Math.Abs(angle) == 270;

            if (flip)
            {
                string orient = Variant["side"];

                return CodeWithVariant("side", orient == "we" ? "ns" : "we");
            }

            return Code;
        }
        

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }
            return Code;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (LastCodePart(1) == "feet")
            {
                BlockFacing facing = BlockFacing.FromCode(LastCodePart()).Opposite;
                pos = pos.AddCopy(facing);
            }

            BlockEntityTrough betr = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTrough;
            if (betr != null)
            {
                StringBuilder dsc = new StringBuilder();
                betr.GetBlockInfo(forPlayer, dsc);
                return dsc.ToString();
            }


            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            return capi.BlockTextureAtlas.GetRandomColor(Textures["wood"].Baked.TextureSubId);
        }

        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            int texSubId = Textures["wood"].Baked.TextureSubId;
            return capi.BlockTextureAtlas.GetAverageColor(texSubId);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            BlockEntityTrough betr = world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityTrough;
            if (betr == null) return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            ItemStack[] stacks = betr.GetNonEmptyContentStacks();
            
            if (stacks == null || stacks.Length == 0)
            {
                List<ItemStack> allowedstacks = new List<ItemStack>();

                foreach (var val in betr.ContentConfig)
                {
                    allowedstacks.Add(val.Content.ResolvedItemstack);
                }

                stacks = allowedstacks.ToArray();
            }

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-trough-addfeed",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = stacks,
                    GetMatchingStacks = (wi, bs, es) => betr.IsFull ? null : wi.Itemstacks
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
