﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockTroughBase : Block
    {
        public ContentConfig[] contentConfigs;
        public WorldInteraction[] placeInteractionHelp;

        public Vec3i RootOffset = new Vec3i(0, 0, 0);

        protected string[] unsuitableEntityCodesBeginsWith = Array.Empty<string>();
        protected string[] unsuitableEntityCodesExact;
        protected string   unsuitableEntityFirstLetters = "";

        public void init()
        {
            CanStep = false; // Prevent creatures from walking on troughs

            contentConfigs = ObjectCacheUtil.GetOrCreate(api, "troughContentConfigs-" + Code, () =>
            {
                var cfgs = Attributes?["contentConfig"]?.AsObject<ContentConfig[]>();
                if (cfgs == null) return null;

                foreach (var val in cfgs)
                {
                    if (!val.Content.Code.Path.Contains('*'))
                    {
                        val.Content.Resolve(api.World, "troughcontentconfig");
                    }
                }

                return cfgs;
            });


            List<ItemStack> allowedstacks = new List<ItemStack>();
            foreach (var val in contentConfigs)
            {
                if (val.Content.Code.Path.Contains('*'))
                {
                    if (val.Content.Type == EnumItemClass.Block)
                    {
                        allowedstacks.AddRange(api.World.SearchBlocks(val.Content.Code).Select(block => new ItemStack(block, val.QuantityPerFillLevel)));
                    }
                    else
                    {
                        allowedstacks.AddRange(api.World.SearchItems(val.Content.Code).Select(item => new ItemStack(item, val.QuantityPerFillLevel)));
                    }
                }
                else
                {
                    if (val.Content.ResolvedItemstack == null) continue;

                    var stack = val.Content.ResolvedItemstack.Clone();
                    stack.StackSize = val.QuantityPerFillLevel;
                    allowedstacks.Add(stack);
                }
            }

            placeInteractionHelp = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-trough-addfeed",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = allowedstacks.ToArray(),
                    GetMatchingStacks = (wi, bs, es) => {
                        BlockEntityTrough betr = api.World.BlockAccessor.GetBlockEntity(bs.Position.AddCopy(RootOffset)) as BlockEntityTrough;
                        if (betr?.IsFull != false) return null;

                        ItemStack[] stacks = betr.GetNonEmptyContentStacks();
                        if (stacks != null && stacks.Length != 0) return [.. wi.Itemstacks.Where(stack => stack.Equals(api.World, stacks[0], GlobalConstants.IgnoredStackAttributes))];

                        return wi.Itemstacks;
                    }
                }
            };

            string[] codes = Attributes?["unsuitableFor"].AsArray<string>(Array.Empty<string>());
            if (codes.Length > 0) AiTaskBaseTargetable.InitializeTargetCodes(codes, ref unsuitableEntityCodesExact, ref unsuitableEntityCodesBeginsWith, ref unsuitableEntityFirstLetters); ;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return placeInteractionHelp.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        
        public virtual bool UnsuitableForEntity(string testPath)  // Similar code to AiTaskBaseTargetable.IsTargetEntity(testPath)
        {
            if (unsuitableEntityFirstLetters.IndexOf(testPath[0]) < 0) return false;   // early exit if we don't have the first letter

            for (int i = 0; i < unsuitableEntityCodesExact.Length; i++)
            {
                if (testPath == unsuitableEntityCodesExact[i]) return true;
            }

            for (int i = 0; i < unsuitableEntityCodesBeginsWith.Length; i++)
            {
                if (testPath.StartsWithFast(unsuitableEntityCodesBeginsWith[i])) return true;
            }

            return false;
        }
    }


    public class BlockTrough : BlockTroughBase
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            init();
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


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            return capi.BlockTextureAtlas.GetRandomColor(Textures["wood"].Baked.TextureSubId, rndIndex);
        }

        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            int texSubId = Textures["wood"].Baked.TextureSubId;
            return capi.BlockTextureAtlas.GetAverageColor(texSubId);
        }


    }
}
