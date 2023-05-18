using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public abstract class BlockEntityContainer : BlockEntity, IBlockEntityContainer
    {
        public abstract InventoryBase Inventory { get; }
        public abstract string InventoryClassName { get; }

        IInventory IBlockEntityContainer.Inventory { get { return Inventory; } }

        RoomRegistry roomReg;
        protected Room room;

        /// <summary>
        /// On the server, we calculate the temperature only once each tick, to save repeating the same costly calculation.  A value -999 or less signifies not fresh and requires re-calculation
        /// </summary>
        protected float temperatureCached = -1000f;

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
        }

        private void Inventory_OnInventoryOpenedClient(IPlayer player)
        {
            OnTick(1);
        }

        protected virtual void OnTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                // We don't have to do this client side. The item stack renderer already updates those states for us
                return;
            }

            temperatureCached = -1000f;     // reset the cached temperature; it will be updated by the first perishable in the loop below, if there is one
            if (!HasTransitionables()) return;   // Skip the room check if this container currently has no transitionables

            room = roomReg.GetRoomForPosition(Pos);
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
            temperatureCached = -1000f;      // reset the cached temperature in case any code needs to call GetPerishRate() between ticks of this entity
        }

        protected virtual bool HasTransitionables()
        {
            foreach (ItemSlot slot in Inventory)
            {
                ItemStack stack = slot.Itemstack;
                if (stack == null) continue;

                var props = stack.Collectible.GetTransitionableProperties(Api.World, stack, null);
                if (props != null && props.Length > 0) return true;
            }
            return false;
        }

        protected virtual float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            float positionAwarePerishRate = Api != null && transType == EnumTransitionType.Perish ? GetPerishRate() : 1;
            if (transType == EnumTransitionType.Dry) positionAwarePerishRate = 0.25f;

            return baseMul * positionAwarePerishRate;
        }


        public virtual float GetPerishRate()
        {
            BlockPos sealevelpos = Pos.Copy();
            sealevelpos.Y = Api.World.SeaLevel;

            float temperature = temperatureCached;
            if (temperature < -999f)
            {
                temperature = Api.World.BlockAccessor.GetClimateAt(sealevelpos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, Api.World.Calendar.TotalDays).Temperature;
                if (Api.Side == EnumAppSide.Server) temperatureCached = temperature;   // Cache the temperature for the remainder of this tick
            }

            if (room == null)
            {
                room = roomReg.GetRoomForPosition(Pos);
            }

            float soilTempWeight = 0f;
            float skyLightProportion = (float)room.SkylightCount / Math.Max(1, room.SkylightCount + room.NonSkylightCount);   // avoid any risk of divide by zero

            if (room.IsSmallRoom)
            {
                soilTempWeight = 1f;
                // If there's too much skylight, it's less cellar-like
                soilTempWeight -= 0.4f * skyLightProportion;
                // If non-cooling blocks exceed cooling blocks, it's less cellar-like
                soilTempWeight -= 0.5f * GameMath.Clamp((float)room.NonCoolingWallCount / Math.Max(1, room.CoolingWallCount), 0f, 1f);
            }

            int lightlevel = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);

            // light level above 12 makes it additionally warmer, especially when part of a cellar or a greenhouse
            float lightImportance = 0.1f;
            // light in small fully enclosed rooms has a big impact
            if (room.IsSmallRoom) lightImportance += 0.3f * soilTempWeight + 1.75f * skyLightProportion;
            // light in large most enclosed rooms (e.g. houses, greenhouses) has medium impact
            else if (room.ExitCount <= 0.1f * (room.CoolingWallCount + room.NonCoolingWallCount)) lightImportance += 1.25f * skyLightProportion;
            // light outside rooms (e.g. chests on world surface) has low impact but still warms them above base air temperature
            else lightImportance += 0.5f * skyLightProportion;
            lightImportance = GameMath.Clamp(lightImportance, 0f, 1.5f);
            float airTemp = temperature + GameMath.Clamp(lightlevel - 11, 0, 10) * lightImportance;


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
            if (Api.World is IServerWorldAccessor)
            {
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

        public virtual void DropContents(Vec3d atPos)
        {
            
        }
    }
}
