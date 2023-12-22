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


        public ICoreAPI Api => api;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Variant["state"] == "harvested") return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "mushromBlockInteractions", () =>
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-mushroom-harvest",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = BlockUtil.GetKnifeStacks(api)
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

            /*if (byPlayer != null)
            {
                EnumTool? tool = byPlayer.InventoryManager.ActiveTool;
                if (IsGrown() && tool == EnumTool.Knife)
                {
                    Block harvestedBlock = GetHarvestedBlock(world);
                    world.BlockAccessor.SetBlock(harvestedBlock.BlockId, pos);
                }
            }*/
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            // Prevent the legacy mushrooms from removing/adding snow coverage
            //base.OnServerGameTick(world, pos, extra);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {

            if (IsGrown())
            {
                if (Attributes?.IsTrue("forageStatAffected") == true)
                {
                    dropQuantityMultiplier *= byPlayer?.Entity?.Stats.GetBlended("forageDropRate") ?? 1;
                }

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

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            BlockPos rootPos = onBlockFace.IsHorizontal ? pos.AddCopy(onBlockFace) : pos.AddCopy(onBlockFace.Opposite);

            var block = blockAccessor.GetBlock(rootPos);

            if (!block.HasBehavior<BehaviorMyceliumHost>())
            {
                rootPos.Down();
                block = blockAccessor.GetBlock(rootPos);
                if (!block.HasBehavior<BehaviorMyceliumHost>()) return false;
            }

            blockAccessor.SpawnBlockEntity("Mycelium", rootPos);
            (blockAccessor.GetBlockEntity(rootPos) as BlockEntityMycelium)?.OnGenerated(blockAccessor, worldGenRand, this);
            return true;
        }

    }
}
