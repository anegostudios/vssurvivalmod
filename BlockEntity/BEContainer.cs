using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{

    public abstract class BlockEntityContainer : BlockEntity, IBlockEntityContainer
    {
        public abstract InventoryBase Inventory { get; }
        public abstract string InventoryClassName { get; }
        IInventory IBlockEntityContainer.Inventory { get { return Inventory; } }

        protected InWorldContainer container;

        protected BlockEntityContainer()
        {
            container = new InWorldContainer(() => Inventory, "inventory");
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            var inventoryClassName = InventoryClassName + "-" + Pos;
            Inventory.LateInitialize(inventoryClassName, api);
            Inventory.Pos = Pos;

            container.Init(Api, ()=>Pos, ()=>MarkDirty(true));
            RegisterGameTickListener(OnTick, 10000);
        }

        protected virtual void OnTick(float dt)
        {
            container.OnTick(dt);
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            BlockContainer container = byItemStack?.Block as BlockContainer;
            if (container != null)
            {
                ItemStack[] stacks = container.GetContents(Api.World, byItemStack);

                if (stacks != null && stacks.Length > Inventory.Count)
                {
                    throw new InvalidOperationException(string.Format("OnBlockPlaced stack copy failed. Trying to set {0} stacks on an inventory with {1} slots", stacks.Length, Inventory.Count));
                }

                for (int i = 0; stacks != null && i < stacks.Length; i++)
                {
                    Inventory[i].Itemstack = stacks[i]?.Clone();
                }

            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (Api is ICoreServerAPI sapi)
            {
                if (!Inventory.Empty)
                {
                    StringBuilder sb = new($"{byPlayer?.PlayerName} broke container {Block.Code} at {Pos} dropped: ");
                    foreach (var slot in Inventory)
                    {
                        if (slot.Itemstack == null) continue;
                        sb.Append(slot.Itemstack.StackSize).Append("x ").Append(slot.Itemstack.Collectible?.Code).Append(", ");
                    }
                    sapi.Logger.Audit(sb.ToString());
                }
                Inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            base.OnBlockBroken(byPlayer);
        }

        public ItemStack[] GetNonEmptyContentStacks(bool cloned = true)
        {
            List<ItemStack> stacklist = new List<ItemStack>();
            foreach (var slot in Inventory)
            {
                if (slot.Empty) continue;
                stacklist.Add(cloned ? slot.Itemstack.Clone() : slot.Itemstack);
            }

            return stacklist.ToArray();
        }

        public ItemStack[] GetContentStacks(bool cloned = true)
        {
            List<ItemStack> stacklist = new List<ItemStack>();
            foreach (var slot in Inventory)
            {
                stacklist.Add(cloned ? slot.Itemstack?.Clone() : slot.Itemstack);
            }

            return stacklist.ToArray();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            container.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            container.ToTreeAttributes(tree);
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            container.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            container.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            container.ReloadRoom();
            float rate = container.GetPerishRate();

            if (Inventory is InventoryGeneric)
            {
                InventoryGeneric inv = Inventory as InventoryGeneric;
                if (inv.TransitionableSpeedMulByType != null && inv.TransitionableSpeedMulByType.TryGetValue(EnumTransitionType.Perish, out float rateMul))
                {
                    rate *= rateMul;
                }

                if (inv.PerishableFactorByFoodCategory != null)
                {
                    dsc.AppendLine(Lang.Get("Stored food perish speed:"));

                    foreach (var val in inv.PerishableFactorByFoodCategory)
                    {
                        string type = Lang.Get("foodcategory-" + val.Key.ToString().ToLowerInvariant());
                        dsc.AppendLine(Lang.Get("- {0}: {1}x", type, Math.Round(rate * val.Value, 2)));
                    }

                    if (inv.PerishableFactorByFoodCategory.Count != Enum.GetValues(typeof(EnumFoodCategory)).Length)
                    {
                        dsc.AppendLine(Lang.Get("- {0}: {1}x", Lang.Get("food_perish_speed_other"), Math.Round(rate, 2)));
                    }
                    return;
                }
            }

            dsc.AppendLine(Lang.Get("Stored food perish speed: {0}x", Math.Round(rate, 2)));
        }

        public virtual void DropContents(Vec3d atPos)
        {

        }

        public virtual void CheckInventoryClearedMidTick()
        {

        }
    }
}
