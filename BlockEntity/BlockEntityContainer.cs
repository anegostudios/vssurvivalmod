using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public abstract class BlockEntityContainer : BlockEntity, IBlockEntityContainer
    {
        public abstract InventoryBase Inventory { get; }
        public abstract string InventoryClassName { get; }

        IInventory IBlockEntityContainer.Inventory { get { return Inventory; } }

        RoomRegistry roomReg;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Inventory.LateInitialize(InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
            Inventory.Pos = Pos;
            Inventory.ResolveBlocksOrItems();
            Inventory.OnAcquireTransitionSpeed = Inventory_OnAcquireTransitionSpeed;
            if (api.Side == EnumAppSide.Client) {
                Inventory.OnInventoryOpened += Inventory_OnInventoryOpenedClient;
            }

            RegisterGameTickListener(OnTick, 10000);

            roomReg = Api.ModLoader.GetModSystem<RoomRegistry>();
            room = roomReg.GetRoomForPosition(Pos);
        }

        private void Inventory_OnInventoryOpenedClient(IPlayer player)
        {
            OnTick(1);
        }

        Room room;

        protected virtual void OnTick(float dt)
        {
            room = roomReg.GetRoomForPosition(Pos);
            if (Api.Side == EnumAppSide.Client)
            {
                // We don't have to do this client side. The item stack renderer already updates those states for us
                return;
            }

            if (room.AnyChunkUnloaded != 0)  return;

            foreach (ItemSlot slot in Inventory)
            {
                if (slot.Itemstack == null) continue;

                AssetLocation codeBefore = slot.Itemstack.Collectible.Code;
                slot.Itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, slot);

                if (slot.Itemstack?.Collectible.Code != codeBefore)
                {
                    MarkDirty(true);
                }
            }
        }
        

        protected virtual float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            float positionAwarePerishRate = Api != null && transType == EnumTransitionType.Perish ? GetPerishRate() : 1;
            if (transType == EnumTransitionType.Dry) positionAwarePerishRate = 0;

            return baseMul * positionAwarePerishRate;
        }


        public virtual float GetPerishRate()
        {
            BlockPos sealevelpos = Pos.Copy();
            sealevelpos.Y = Api.World.SeaLevel;

            ClimateCondition cond = Api.World.BlockAccessor.GetClimateAt(sealevelpos);
            if (cond == null) return 1;

            float soilTempWeight = 0f;

            if (room.ExitCount == 0)
            {
                soilTempWeight = 0.5f + 0.5f * (1 - GameMath.Clamp((float)room.NonCoolingWallCount / Math.Max(1, room.CoolingWallCount), 0, 1));
            }

            int lightlevel = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);

            // light level above 12 makes it additionally warmer, especially when part of a cellar
            float airTemp = cond.Temperature + GameMath.Clamp(lightlevel - 11, 0, 10) * (1f + 5 * soilTempWeight);


            // Lets say deep soil temperature is a constant 5°C
            float cellarTemp = 5;

            // How good of a cellar it is depends on how much rock or soil was used on he cellars walls
            float hereTemp = GameMath.Lerp(airTemp, cellarTemp, soilTempWeight);

            // For fairness lets say if its colder outside, use that temp instead
            hereTemp = Math.Min(hereTemp, airTemp);

            // Some neat curve to turn the temperature into a spoilage rate
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtYXgoMC4xLG1pbigyLjUsM14oeC8xOS0xLjIpKS0wLjEpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiLTIwIiwiNDAiLCIwIiwiMyJdLCJncmlkIjpbIjIuNSIsIjAuMjUiXX1d
            // max(0.1, min(2.5, 3^(x/15 - 1.2))-0.1)
            float rate = Math.Max(0.1f, Math.Min(2.4f, (float)Math.Pow(3, hereTemp / 19 - 1.2) - 0.1f));

            return rate;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            BlockContainer container = byItemStack?.Block as BlockContainer;
            if (container != null)
            {
                ItemStack[] stacks = container.GetContents(Api.World, byItemStack);
                for (int i = 0; stacks != null && i < stacks.Length; i++)
                {
                    Inventory[i].Itemstack = stacks[i]?.Clone();
                }

            }
        }

        public override void OnBlockBroken()
        {
            if (Api.World is IServerWorldAccessor)
            {
                Inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            base.OnBlockBroken();
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
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (Inventory != null)
            {
                ITreeAttribute invtree = new TreeAttribute();
                Inventory.ToTreeAttributes(invtree);
                tree["inventory"] = invtree;
            }
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                slot.Itemstack?.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;

                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                } else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping);
                }

                

                if (slot.Itemstack?.Collectible is ItemLootRandomizer)
                {
                    (slot.Itemstack.Collectible as ItemLootRandomizer).ResolveLoot(slot, worldForResolve);
                }

                if (slot.Itemstack?.Collectible is ItemStackRandomizer)
                {
                    (slot.Itemstack.Collectible as ItemStackRandomizer).Resolve(slot, worldForResolve);
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            room = roomReg.GetRoomForPosition(Pos);
            float rate = GetPerishRate();

            if (Inventory is InventoryGeneric)
            {
                InventoryGeneric inv = Inventory as InventoryGeneric;
                float rateMul;
                if (inv.TransitionableSpeedMulByType != null && inv.TransitionableSpeedMulByType.TryGetValue(EnumTransitionType.Perish, out rateMul))
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

    }
}
