using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockReeds : BlockPlant
    {
        WorldInteraction[] interactions = null;
        string climateColorMapInt;
        string seasonColorMapInt;

        public override string ClimateColorMapForMap => climateColorMapInt;
        public override string SeasonColorMapForMap => seasonColorMapInt;

        private int habitat = EnumReedsHabitat.Land;

        public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict)
        {
            base.OnCollectTextures(api, textureDict);

            climateColorMapInt = ClimateColorMap;
            seasonColorMapInt = SeasonColorMap;

            if (api.Side == EnumAppSide.Client && SeasonColorMap == null)
            {
                climateColorMapInt = (api as ICoreClientAPI).TesselatorManager.GetCachedShape(Shape.Base)?.Elements[2].ClimateColorMap;
                seasonColorMapInt = (api as ICoreClientAPI).TesselatorManager.GetCachedShape(Shape.Base)?.Elements[2].SeasonColorMap;
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            string habitat = Variant["habitat"];
            if (habitat == "water") this.habitat = EnumReedsHabitat.Water;
            else if (habitat == "ice") this.habitat = EnumReedsHabitat.Ice;
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

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            Block blockToPlace = this;

            bool inWater = block.IsLiquid() && block.LiquidLevel == 7 && block.LiquidCode.Contains("water");

            if (inWater)
            {
                blockToPlace = world.GetBlock(CodeWithVariant("habitat", "water"));
                if (blockToPlace == null) blockToPlace = this;
            }
            else
            {
                if (habitat != 0)
                {
                    failureCode = "requirefullwater";
                    return false;
                }
            }


            if (blockToPlace != null)
            {
                if (CanPlantStay(world.BlockAccessor, blockSel.Position))
                {
                    world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                }
                else
                {
                    failureCode = "requirefertileground";
                    return false;
                }

                return true;
            }

            return false;
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
                double posy = blockSel.Position.Y + blockSel.HitPosition.Y;
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
            bool isReed = Variant["type"] == "coopersreed";
            return new BlockDropItemStack[]
            {
                new BlockDropItemStack(new ItemStack(api.World.GetItem(new AssetLocation(isReed ? "cattailtops" : "papyrustops")))),
                new BlockDropItemStack(new ItemStack(api.World.GetItem(new AssetLocation(isReed ? "cattailroot" : "papyrusroot"))))
            };
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                bool isReed = Variant["type"] == "coopersreed";
                ItemStack drop = null;
                if (Variant["state"] == "normal")
                {
                    drop = new ItemStack(world.GetItem(new AssetLocation(isReed ? "cattailtops" : "papyrustops")));
                }
                else
                {
                    drop = new ItemStack(world.GetItem(new AssetLocation(isReed ? "cattailroot" : "papyrusroot")));
                }

                if (drop != null)
                {
                    world.SpawnItemEntity(drop, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (byPlayer != null && Variant["state"] == "normal" && (byPlayer.InventoryManager.ActiveTool == EnumTool.Knife || byPlayer.InventoryManager.ActiveTool == EnumTool.Sickle || byPlayer.InventoryManager.ActiveTool == EnumTool.Scythe))
            {
                world.BlockAccessor.SetBlock(world.GetBlock(this.habitat == EnumReedsHabitat.Ice ? CodeWithVariants(new string[] { "habitat", "state" }, new string[] { "water", "harvested" })  : CodeWithVariant("state", "harvested")).BlockId, pos);
                return;
            }

            if (habitat != 0)
            {
                world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("water-still-7")).BlockId, pos);
                world.BlockAccessor.GetBlock(pos).OnNeighbourBlockChange(world, pos, pos);
            }
            else
            {
                world.BlockAccessor.SetBlock(0, pos);
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }

            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (belowBlock.Fertility > 0)
            {
                if (block.LiquidCode == "water")
                {
                    return TryPlaceBlockInWater(blockAccessor, pos.UpCopy());
                }

                Block placingBlock = blockAccessor.GetBlock(CodeWithVariant("habitat", "land"));
                if (placingBlock == null) return false;
                blockAccessor.SetBlock(placingBlock.BlockId, pos);
                return true;
            }

            if (belowBlock.LiquidCode == "water")
            {
                return TryPlaceBlockInWater(blockAccessor, pos);
            }

            return false;
        }

        protected virtual bool TryPlaceBlockInWater(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 2, pos.Z);
            if (belowBlock.Fertility > 0)
            {
                blockAccessor.SetBlock(blockAccessor.GetBlock(CodeWithVariant("habitat", "water")).BlockId, pos.AddCopy(0, -1, 0));
                return true;
            }
            return false;
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            return capi.World.ApplyColorMapOnRgba(ClimateColorMapForMap, SeasonColorMapForMap, capi.BlockTextureAtlas.GetRandomColor(Textures.Last().Value.Baked.TextureSubId), pos.X, pos.Y, pos.Z);
            //return base.GetRandomColor(capi, pos, facing);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override bool ShouldMergeFace(int facingIndex, Block neighbourIce, int intraChunkIndex3d)
        {
            return BlockMaterial == neighbourIce.BlockMaterial;
        }

        #region ice variant

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            if (!GlobalConstants.MeltingFreezingEnabled) return false;
            if (habitat == EnumReedsHabitat.Land) return false;

            if (habitat == EnumReedsHabitat.Ice)  // ice -> water
            {
                ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
                if (conds == null) return false;

                float chance = GameMath.Clamp((conds.Temperature - 2f) / 20f, 0, 1);
                return offThreadRandom.NextDouble() < chance;
            }

            // water -> ice

            if (Variant["type"] == "papyrus") return false;  //TODO: currently we do not have an ice version of Papyrus

            if (offThreadRandom.NextDouble() < 0.6)
            {
                int rainY = world.BlockAccessor.GetRainMapHeightAt(pos);
                if (rainY <= pos.Y)
                {
                    for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                    {
                        BlockFacing facing = BlockFacing.HORIZONTALS[i];
                        if (world.BlockAccessor.GetBlock(pos.AddCopy(facing)).Replaceable < 6000)
                        {
                            ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
                            if (conds != null && conds.Temperature < -4)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            Block iceBlock = api.World.GetBlock(CodeWithVariant("habitat", habitat == EnumReedsHabitat.Water ? "ice" : "water"));
            world.BlockAccessor.SetBlock(iceBlock.Id, pos);
        }

        #endregion

    }

    public class EnumReedsHabitat
    {
        public const int Land = 0;
        public const int Water = 1;
        public const int Ice = 2;
    }

}
