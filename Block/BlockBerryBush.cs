using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBerryBush : BlockPlant
    {
        MeshData[] prunedmeshes;

        public string State => Variant["state"];
        public string Type => Variant["type"];

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = ObjectCacheUtil.GetOrCreate(api, "berryBushInteractions", () =>
            {
                List<ItemStack> toolStacklist = new List<ItemStack>();

                foreach (Item item in api.World.Items)
                {
                    if (item.Tool == EnumTool.Shears)
                    {
                        toolStacklist.Add(new ItemStack(item));
                    }
                }

                var sstacks = toolStacklist.ToArray();

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-berrybush-prune",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var bebush = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBerryBush;

                            if (bebush != null && !bebush.Pruned) return sstacks;
                            else return null;
                        }
                    }
                };
            });
        }

        public MeshData GetPrunedMesh(BlockPos pos)
        {
            if (api == null) return null;
            if (prunedmeshes == null) genPrunedMeshes();

            int rnd = RandomizeAxes == EnumRandomizeAxes.XYZ ? GameMath.MurmurHash3Mod(pos.X, pos.Y, pos.Z, prunedmeshes.Length) : GameMath.MurmurHash3Mod(pos.X, 0, pos.Z, prunedmeshes.Length);

            return prunedmeshes[rnd];
        }

        private void genPrunedMeshes()
        {
            var capi = api as ICoreClientAPI;

            prunedmeshes = new MeshData[Shape.BakedAlternates.Length];

            var selems = new string[] { "Berries", "branchesN", "branchesS", "Leaves" };
            if (State == "empty") selems = selems.Remove("Berries");

            for (int i = 0; i < Shape.BakedAlternates.Length; i++)
            {
                var cshape = Shape.BakedAlternates[i];
                var shape = capi.TesselatorManager.GetCachedShape(cshape.Base);
                capi.Tesselator.TesselateShape(this, shape, out prunedmeshes[i], this.Shape.RotateXYZCopy, null, selems);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityBerryBush bebush = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBerryBush;

            if ((!bebush?.Pruned ?? false) && byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible?.Tool == EnumTool.Shears)
            {
                bebush.Prune();
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block belowBlock = blockAccessor.GetBlock(pos.DownCopy());
            if (belowBlock.Fertility > 0) return true;
            if (!(belowBlock is BlockBerryBush)) return false;

            Block belowbelowBlock = blockAccessor.GetBlock(pos.DownCopy(2));
            return belowbelowBlock.Fertility > 0 && this.Attributes?.IsTrue("stackable") == true && belowBlock.Attributes?.IsTrue("stackable") == true;
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (Textures == null || Textures.Count == 0) return 0;
            BakedCompositeTexture tex = Textures?.First().Value?.Baked;
            if (tex == null) return 0;

            int color = capi.BlockTextureAtlas.GetRandomColor(tex.TextureSubId, rndIndex);
            color = capi.World.ApplyColorMapOnRgba("climatePlantTint", "seasonalFoliage", color, pos.X, pos.Y, pos.Z);
            return color;
        }


        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            int color = base.GetColorWithoutTint(capi, pos);

            return capi.World.ApplyColorMapOnRgba("climatePlantTint", "seasonalFoliage", color, pos.X, pos.Y, pos.Z);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            foreach (var drop in drops)
            {
                if (drop.Collectible is BlockBerryBush) continue;

                float dropRate = 1;

                if (Attributes?.IsTrue("forageStatAffected") == true)
                {
                    dropRate *= byPlayer?.Entity.Stats.GetBlended("forageDropRate") ?? 1;
                }

                drop.StackSize = GameMath.RoundRandom(api.World.Rand, drop.StackSize * dropRate);
            }

            return drops;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(interactions);
        }
    }
}
