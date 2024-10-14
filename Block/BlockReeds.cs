using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockReeds : BlockPlant
    {
        WorldInteraction[] interactions = null;
        string climateColorMapInt;
        string seasonColorMapInt;
        int maxWaterDepth;

        public override string ClimateColorMapForMap => climateColorMapInt;
        public override string SeasonColorMapForMap => seasonColorMapInt;

        string habitatBlockCode = null;
        public override string RemapToLiquidsLayer { get => habitatBlockCode; }

        public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict)
        {
            base.OnCollectTextures(api, textureDict);

            climateColorMapInt = Attributes["climateColorMapForMap"].AsString();
            seasonColorMapInt = Attributes["seasonColorMapForMap"].AsString();
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            maxWaterDepth = Attributes["maxWaterDepth"].AsInt(1);

            string hab = Variant["habitat"];
            if (hab == "water") habitatBlockCode = "water-still-7";
            else if (hab == "ice") habitatBlockCode = "lakeice";

            if (LastCodePart() == "harvested") return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "reedsBlockInteractions", () =>
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
                        ActionLangCode = "blockhelp-reeds-harvest",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = knifeStacklist.ToArray()
                    }
                };
            });
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            if (CanPlantStay(world.BlockAccessor, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(BlockId, blockSel.Position);
            }
            else
            {
                failureCode = "requirefertileground";
                return false;
            }

            return true;
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (Variant["state"] == "harvested") dt /= 2;
            else if (player.InventoryManager.ActiveTool != EnumTool.Knife)
            {
                dt /= 3;
            }
            else
            {
                float mul;
                if (itemslot.Itemstack.Collectible.MiningSpeed.TryGetValue(EnumBlockMaterial.Plant, out mul)) dt *= mul;
            }

            float resistance = RequiredMiningTier == 0 ? remainingResistance - dt : remainingResistance;

            if (counter % 5 == 0 || resistance <= 0)
            {
                double posx = blockSel.Position.X + blockSel.HitPosition.X;
                double posy = blockSel.Position.InternalY + blockSel.HitPosition.Y;
                double posz = blockSel.Position.Z + blockSel.HitPosition.Z;
                player.Entity.World.PlaySoundAt(resistance > 0 ? Sounds.GetHitSound(player) : Sounds.GetBreakSound(player), posx, posy, posz, player, true, 16, 1);
            }

            return resistance;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            AssetLocation loc = CodeWithVariants(new string[] { "habitat", "cover" }, new string[] { "land", "free" });
            Block block = world.GetBlock(loc);
            return new ItemStack(block);
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            Block harvestedBlock = api.World.GetBlock(CodeWithVariant("state", "harvested"));
            Block grownBlock = api.World.GetBlock(CodeWithVariant("state", "normal"));

            return grownBlock.Drops.Append(harvestedBlock.Drops);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                foreach (var bdrop in Drops)
                {
                    ItemStack drop = bdrop.GetNextItemStack();
                    if (drop != null)
                    {
                        world.SpawnItemEntity(drop, pos, null);
                    }
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, -0.5, byPlayer);
            }

            if (byPlayer != null && Variant["state"] == "normal" && (byPlayer.InventoryManager.ActiveTool == EnumTool.Knife || byPlayer.InventoryManager.ActiveTool == EnumTool.Sickle || byPlayer.InventoryManager.ActiveTool == EnumTool.Scythe))
            {
                world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariants(new string[] { "habitat", "state" }, new string[] { "land", "harvested" })).BlockId, pos);
                return;
            }

            SpawnBlockBrokenParticles(pos);
            world.BlockAccessor.SetBlock(0, pos);
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }

            int depth = 0;

            Block belowBlock = blockAccessor.GetBlockBelow(pos, depth + 1);
            while (belowBlock.LiquidCode == "water")
            {
                if (++depth > maxWaterDepth) return false;
                belowBlock = blockAccessor.GetBlockBelow(pos, depth + 1);
            }

            if (belowBlock.Fertility > 0)
            {
                return TryGen(blockAccessor, pos.DownCopy(depth));
            }

            return false;
        }

        private bool TryGen(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block placingBlock = blockAccessor.GetBlock(CodeWithVariant("habitat", "land"));
            if (placingBlock == null) return false;
            blockAccessor.SetBlock(placingBlock.BlockId, pos);
            return true;
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            return capi.World.ApplyColorMapOnRgba(ClimateColorMapForMap, SeasonColorMapForMap, capi.BlockTextureAtlas.GetRandomColor(Textures.Last().Value.Baked.TextureSubId, rndIndex), pos.X, pos.Y, pos.Z);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }

}
