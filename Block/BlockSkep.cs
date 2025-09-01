using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockSkep : Block
    {
        float beemobSpawnChance = 0.4f;

        public bool IsEmpty()
        {
            return Variant["type"] == "empty";
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            if (byItemStack?.Attributes.GetBool("harvestable") == true) GetBlockEntity<BlockEntityBeehive>(blockPos).Harvestable = true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var collObj = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible;
            if (collObj is ItemClosedBeenade or ItemOpenedBeenade) return false;

            if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative && collObj?.FirstCodePart() == "honeycomb")
            {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBeehive beh && !beh.Harvestable)
                {
                    beh.Harvestable = true;
                    beh.MarkDirty(true);
                }
                return true;
            }

            if (byPlayer.InventoryManager.TryGiveItemstack(new(world.BlockAccessor.GetBlock(CodeWithVariant("side", "east")))))
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position);
                world.PlaySoundAt(new AssetLocation("sounds/block/planks"), blockSel.Position, -0.5, byPlayer, false);

                return true;
            }

            return false;
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            beemobSpawnChance = Attributes?["beemobSpawnChance"].AsFloat(0.4f) ?? 0.4f;
        }

        

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            if (world.Side == EnumAppSide.Server && !IsEmpty() && world.Rand.NextDouble() < beemobSpawnChance)     // Only test the chance and spawn the entity on the server side
            {
                if (world.ClassRegistry.CreateEntity(world.GetEntityType("beemob")) is Entity entity)
                {
                    entity.ServerPos.X = pos.X + 0.5f;
                    entity.ServerPos.Y = pos.Y + 0.5f;
                    entity.ServerPos.Z = pos.Z + 0.5f;
                    entity.ServerPos.Yaw = (float)world.Rand.NextDouble() * 2 * GameMath.PI;
                    entity.Pos.SetFrom(entity.ServerPos);

                    entity.Attributes.SetString("origin", "brokenbeehive");
                    world.SpawnEntity(entity);
                }
            }
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            bool harvestable = handbookStack.Attributes.GetBool("harvestable");
            ItemStack[]? stacks = harvestable ? getHarvestableDrops(api.World, forPlayer.Entity.Pos.XYZ.AsBlockPos, forPlayer) : GetDrops(api.World, forPlayer.Entity.Pos.XYZ.AsBlockPos, forPlayer);
            if (stacks == null) return [];

            return [.. stacks.Select(stack => new BlockDropItemStack(stack))];
        }

        public override ItemStack[]? GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (IsEmpty() || GetBlockEntity<BlockEntityBeehive>(pos)?.Harvestable != true)
            {
                return [new(world.BlockAccessor.GetBlock(CodeWithVariant("side", "east")))];
            }

            return getHarvestableDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        private ItemStack[]? getHarvestableDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Drops == null) return null;
            List<ItemStack> todrop = [];

            for (int i = 0; i < Drops.Length; i++)
            {
                if (Drops[i].Tool != null && (byPlayer == null || Drops[i].Tool != byPlayer.InventoryManager.ActiveTool)) continue;

                ItemStack stack = Drops[i].GetNextItemStack(dropQuantityMultiplier);
                if (stack == null) continue;

                todrop.Add(stack);
                if (Drops[i].LastDrop) break;
            }

            return [.. todrop];
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack.Attributes.GetBool("harvestable")) return Lang.GetMatching(Code.Domain + AssetLocation.LocationSeparator + "block-" + CodeWithVariant("type", "harvestable").Path);

            return base.GetHeldItemName(itemStack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);

            if (GetBlockEntity<BlockEntityBeehive>(pos)?.Harvestable == true) stack.Attributes.SetBool("harvestable", true);

            return stack;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            WorldInteraction[] wi = 
            [
                new() {
                    ActionLangCode = Variant["type"] == "populated" ? "blockhelp-skep-putinbagslot" : "blockhelp-skep-pickup",
                    MouseButton = EnumMouseButton.Right
                }
            ];

            if (GetBlockEntity<BlockEntityBeehive>(selection.Position)?.Harvestable == true)
            {
                wi =
                [
                    ..wi,
                    new() {
                        ActionLangCode = "blockhelp-skep-harvest",
                        MouseButton = EnumMouseButton.Left
                    }
                ];
            }

            return [ ..wi, ..base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)];
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            var orientation = Variant["side"];
            var key = "beehive-" + Variant["material"] + "-harvestablemesh-" + orientation;

            MeshData mesh;
            if (!api.ObjectCache.ContainsKey(key))
            {
                Block fullSkep = capi.World.GetBlock(CodeWithVariant("type", "populated"));

                capi.Tesselator.TesselateShape(
                    fullSkep,
                    API.Common.Shape.TryGet(api, "shapes/block/beehive/skep-harvestable.json"),
                    out mesh,
                    new Vec3f(0, BlockFacing.FromCode(orientation).HorizontalAngleIndex * 90 - 90, 0)
                );
                api.ObjectCache[key] = mesh;
            }
            else mesh = (MeshData)api.ObjectCache[key];

            if (itemstack.Attributes.GetBool("harvestable")) renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(mesh);

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
    }
}
