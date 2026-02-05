using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityBehaviorCoverable : BlockEntityBehavior
    {
        public ItemStack WallStack;
        protected ICoreClientAPI capi;
        public BlockEntityBehaviorCoverable(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            capi = api as ICoreClientAPI;
        }

        
        public void TryAddMaterial(IPlayer byPlayer, BlockSelection blockSel)
        {
            var hslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (!SuitableMaterial(hslot)) return;

            if (WallStack != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                Api.World.SpawnItemEntity(WallStack, Pos);
                WallStack = null;
            }

            var block = hslot.Itemstack.Block.GetInterface<ILookAwarePlacement>(Api.World, blockSel.Position)?.GetLookAwareBlockVariant(byPlayer, hslot.Itemstack, blockSel) ?? hslot.Itemstack.Block;
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) hslot.TakeOut(1);
            WallStack = new ItemStack(block);
            Blockentity.MarkDirty(true);
            Api.World.PlaySoundAt(block.Sounds.Place, Pos, 0.5f, byPlayer);
        }

        public static bool SuitableMaterial(ItemSlot hslot)
        {
            if (hslot.Empty) return false;
            var block = hslot.Itemstack.Block;
            if (block == null) return false;
            if (block.Attributes?["wallAxelable"].Exists == true) return block.Attributes.IsTrue("wallAxelable");
            return block.DrawType == EnumDrawType.Cube;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var oldStack = WallStack;

            WallStack = tree.GetItemstack("wallStack");
            WallStack?.ResolveBlockOrItem(worldAccessForResolve);

            if (worldAccessForResolve.Side == EnumAppSide.Server) return;

            if (((oldStack == null) != (WallStack == null)) || (WallStack != null && oldStack != null && WallStack.Equals(worldAccessForResolve, oldStack, GlobalConstants.IgnoredStackAttributes)))
            {
                Blockentity.MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetItemstack("wallStack", WallStack);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (WallStack == null) return;

            if (!WallStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings))
            {
                WallStack = null;
            }
            else
            {   
                WallStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(WallStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            WallStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(WallStack), blockIdMapping, itemIdMapping);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            if (WallStack != null)
            {
                Api.World.SpawnItemEntity(WallStack, Pos);
                WallStack = null;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (WallStack != null)
            {
                mesher.AddMeshData(capi.TesselatorManager.GetDefaultBlockMesh(WallStack.Block));
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}
