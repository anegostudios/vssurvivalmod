using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockMushroom : BlockPlant
    {
        WorldInteraction[] interactions = null;

        /// <summary>
        /// Code part indicating a non harvested, fully grown mushroom
        /// </summary>
        public static readonly string normalCodePart = "normal";

        /// <summary>
        /// Code part indicating a harvested mushroom
        /// </summary>
        public static readonly string harvestedCodePart = "harvested";


        public override void OnLoaded(ICoreAPI api)
        {
            if (LastCodePart() == "harvested") return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "mushromBlockInteractions", () =>
            {
                List<ItemStack> knifeStacklist = new List<ItemStack>();
                
                foreach (Item item in api.World.Items)
                {
                    if (item.Code == null) continue;

                    if (item.Tool == EnumTool.Knife)
                    {
                        knifeStacklist.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-mushroom-harvest",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = knifeStacklist.ToArray()
                    }
                };
            });
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                failureCode = "__ignore__";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            if (byPlayer != null)
            {
                EnumTool? tool = byPlayer.InventoryManager.ActiveTool;
                if (IsGrown() && tool == EnumTool.Knife)
                {
                    Block harvestedBlock = GetHarvestedBlock(world);
                    world.BlockAccessor.SetBlock(harvestedBlock.BlockId, pos);
                }
            }
        }

        public override BlockDropItemStack[] GetDropsForHandbook(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            return GetHandbookDropsFromBreakDrops(world, pos, byPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (IsGrown())
            {
                return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            }
            else
            {
                return null;
            }
        }
        

        public bool IsGrown()
        {
            return Code.Path.Contains(normalCodePart);
        }

        public Block GetNormalBlock(IWorldAccessor world)
        {
            AssetLocation newBlockCode = Code.CopyWithPath(Code.Path.Replace(harvestedCodePart, normalCodePart));
            return world.GetBlock(newBlockCode);
        }

        public Block GetHarvestedBlock(IWorldAccessor world)
        {
            AssetLocation newBlockCode = Code.CopyWithPath(Code.Path.Replace(normalCodePart, harvestedCodePart));
            return world.GetBlock(newBlockCode);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
