using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public abstract class BlockEntityItemPile : BlockEntity, IBlockShapeSupplier
    {
        public InventoryGeneric inventory;
        public object inventoryLock = new object(); // Because OnTesselation runs in another thread

        public abstract AssetLocation SoundLocation { get; }
        public abstract string BlockCode { get; }
        public abstract int MaxStackSize { get; }

        

        public virtual int TakeQuantity { get { return 1; } }

        public int OwnStackSize
        {
            get { return inventory[0]?.StackSize ?? 0; }
        }

        public int AtlasSize
        {
            get { return ((ICoreClientAPI)api).BlockTextureAtlas.Size; }
        }

        public BlockEntityItemPile()
        {
            inventory = new InventoryGeneric(1, BlockCode, null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.LateInitialize(BlockCode + "-" + pos.ToString(), api);
            inventory.ResolveBlocksOrItems();
        }

        public override void OnBlockBroken()
        {
            if (api.World is IServerWorldAccessor)
            {
                IItemSlot slot = inventory[0];
                while (slot.StackSize > 0)
                {
                    ItemStack split = slot.TakeOut(GameMath.Clamp(slot.StackSize, 1, System.Math.Max(1, slot.Itemstack.Collectible.MaxStackSize / 4)));
                    api.World.SpawnItemEntity(split, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (api != null)
            {
                inventory.Api = api;
                inventory.ResolveBlocksOrItems();
            }

            if (api is ICoreClientAPI)
            {
                api.World.BlockAccessor.MarkBlockDirty(pos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
        }



        public virtual bool OnPlayerInteract(IPlayer byPlayer)
        {
            BlockPos abovePos = pos.UpCopy();

            BlockEntity be = api.World.BlockAccessor.GetBlockEntity(abovePos);
            if (be is BlockEntityItemPile)
            {
                return ((BlockEntityItemPile)be).OnPlayerInteract(byPlayer);
            }

            bool sneaking = byPlayer.Entity.Controls.Sneak;

          
            IItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            bool equalStack = hotbarSlot.Itemstack != null && hotbarSlot.Itemstack.Equals(api.World, inventory[0].Itemstack, GlobalConstants.IgnoredStackAttributes);

            if (sneaking && !equalStack)
            {
                return false;
            }

            if (sneaking && equalStack && OwnStackSize >= MaxStackSize)
            {
                Block pileblock = api.World.BlockAccessor.GetBlock(pos);
                Block aboveblock = api.World.BlockAccessor.GetBlock(abovePos);

                if (aboveblock.IsReplacableBy(pileblock))
                {
                    if (api.World is IServerWorldAccessor)
                    {
                        api.World.BlockAccessor.SetBlock((ushort)pileblock.Id, abovePos);
                        BlockEntityItemPile bep = api.World.BlockAccessor.GetBlockEntity(abovePos) as BlockEntityItemPile;
                        if (bep != null) bep.TryPutItem(byPlayer);
                    }
                    return true;
                }

                return false;
            }

            lock (inventoryLock)
            {
                if (sneaking)
                {
                    return TryPutItem(byPlayer);
                }
                else
                {
                    return TryTakeItem(byPlayer);
                }
            }
        }


        public virtual bool TryPutItem(IPlayer player)
        {
            if (OwnStackSize >= MaxStackSize) return false;

            IItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (hotbarSlot.Itemstack == null) return false;

            IItemSlot invSlot = inventory[0];

            if (invSlot.Itemstack == null)
            {
                invSlot.Itemstack = hotbarSlot.Itemstack.Clone();
                invSlot.Itemstack.StackSize = 0;
                api.World.PlaySoundAt(SoundLocation, pos.X, pos.Y, pos.Z, null, false);
            }

            if (invSlot.Itemstack.Equals(api.World, hotbarSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                int q = GameMath.Min(hotbarSlot.StackSize, TakeQuantity, MaxStackSize - OwnStackSize);

                invSlot.Itemstack.StackSize += q;
                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(q);
                    hotbarSlot.OnItemSlotModified(null);
                }

                api.World.PlaySoundAt(SoundLocation, pos.X, pos.Y, pos.Z, player, false);

                MarkDirty();

                Cuboidf[] collBoxes = api.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(api.World.BlockAccessor, pos);
                if (collBoxes != null && collBoxes.Length > 0 && CollisionTester.AabbIntersect(collBoxes[0], pos.X, pos.Y, pos.Z, player.Entity.CollisionBox, player.Entity.LocalPos.XYZ))
                {
                    player.Entity.LocalPos.Y += collBoxes[0].Y2 - (player.Entity.LocalPos.Y - (int)player.Entity.LocalPos.Y);
                }


                return true;
            }

            return false;
        }

        public bool TryTakeItem(IPlayer player)
        {
            int q = GameMath.Min(TakeQuantity, OwnStackSize);

            if (inventory[0]?.Itemstack != null)
            {
                ItemStack stack = inventory[0].TakeOut(q);
                player.InventoryManager.TryGiveItemstack(stack);

                if (stack.StackSize > 0)
                {
                    api.World.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            if (OwnStackSize == 0)
            {
                api.World.BlockAccessor.SetBlock(0, pos);
            }

            api.World.PlaySoundAt(SoundLocation, pos.X, pos.Y, pos.Z, player, false);

            MarkDirty();

            return true;
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            ItemStack stack = inventory[0].Itemstack;
            if (stack == null) return null;

            return stack.StackSize + "x " + stack.GetName();
        }

        public abstract bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator);


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            ItemStack stack = inventory?[0]?.Itemstack;
            if (stack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                inventory[0].Itemstack = null;
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            ItemStack stack = inventory?[0]?.Itemstack;
            if (stack != null)
            {
                stack.Collectible.OnStoreCollectibleMappings(api.World, inventory[0], blockIdMapping, itemIdMapping);
            }
        }
    }
}
