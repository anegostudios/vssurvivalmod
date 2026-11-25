using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumShelvableLayout
    {
        Quadrants,
        Halves,
        SingleCenter,
        Stacking4
    }

    public interface IShelvable
    {
        public EnumShelvableLayout? GetShelvableType(ItemStack stack) => EnumShelvableLayout.Quadrants;
        public ModelTransform? GetOnShelfTransform(ItemStack stack) => null;
    }

    public class BlockEntityShelf : BlockEntity
    {
        private const int OldShelfSlotCount = 8;
        private const int OldShelfSlotsPerSurface = 4;
        private const string OldShelfInventoryTreeName = "inventory";
        private const string OldShelfInventoryId = "shelf-0";
        private const string ShelfDisplayCategory = "shelf";

        private const float OldShelfLeftX = 4f;
        private const float OldShelfRightX = 12f;
        private const float OldShelfBackZ = 4f;
        private const float OldShelfFrontZ = 10f;
        private const float OldShelfCenterX = 8f;
        private const float OldShelfCenterZ = 6.5f;

        private bool migratedOldInventory;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            SyncDisplayAngle(api.World);

            if (migratedOldInventory && api.Side == EnumAppSide.Server)
            {
                MarkDirty(true);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            SyncDisplayAngle(Api?.World);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            SyncDisplayAngle(worldAccessForResolve);
            TryMigrateOldInventory(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SyncDisplayAngle();
            base.ToTreeAttributes(tree);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            BlockSelection blockSel = forPlayer.CurrentBlockSelection;
            BEBehaviorDisplay display = GetBehavior<BEBehaviorDisplay>();

            if (blockSel != null && blockSel.Position.Equals(Pos) && display != null)
            {
                var slotLoc = BlockBehaviorDisplay.decodeSlotid(blockSel.SelectionBoxId);
                if (slotLoc?.IsPlacedItem == true)
                {
                    ItemSlot? slot = display.InventoryInv[slotLoc.EncodedLocation];
                    if (slot?.Itemstack?.Collectible != null)
                    {
                        var obj = slot.Itemstack.Collectible;
                        float ripenRate = GameMath.Clamp((1 - display.GetPerishRate() - 0.5f) * 3, 0, 1);
                        var transitionableProps = obj.GetTransitionableProperties(Api.World, slot.Itemstack, forPlayer.Entity);
                        if (transitionableProps?.Length > 0) sb.Append(PerishableInfoCompact(Api, slot!, ripenRate));
                        else sb.AppendLine(obj.GetCollectibleInterface<IContainedCustomName>()?.GetContainedInfo(slot) ?? slot.GetStackName() ?? Lang.Get("unknown"));
                        return;
                    }
                }
            }

            base.GetBlockInfo(forPlayer, sb);

            if (blockSel == null || !blockSel.Position.Equals(Pos) || display == null) return;

            float shelfRipenRate = GameMath.Clamp((1 - display.GetPerishRate() - 0.5f) * 3, 0, 1);
            if (shelfRipenRate > 0)
            {
                sb.AppendLine(Lang.Get("Suitable spot for food ripening."));
            }
        }
        // The block still stores the wall side in its code. Display uses meshAngleRad so the shelf mesh,
        // placement grid and displayed item meshes all rotate from the same value.
        private void SyncDisplayAngle(IWorldAccessor? worldForResolve = null)
        {
            if (Block == null) return;

            var display = GetBehavior<BEBehaviorDisplay>();
            display?.SetMeshAngleRad(GetShelfAngleRad(), worldForResolve);
        }

        private void TryMigrateOldInventory(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            ITreeAttribute? oldInvTree = tree.GetTreeAttribute(OldShelfInventoryTreeName);
            BEBehaviorDisplay display = GetBehavior<BEBehaviorDisplay>();
            BlockBehaviorDisplay? displayBlock = Block?.GetBehavior<BlockBehaviorDisplay>();
            if (oldInvTree == null || display == null || displayBlock == null || !display.InventoryInv.Empty) return;

            InventoryGeneric oldInv = new InventoryGeneric(OldShelfSlotCount, OldShelfInventoryId, worldForResolve.Api, (id, inv) => new ItemSlotDisplay(inv, ShelfDisplayCategory));
            oldInv.Pos = Pos;
            oldInv.FromTreeAttributes(oldInvTree);
            oldInv.ResolveBlocksOrItems();

            List<(string slotId, ItemStack stack)> migratedStacks = new List<(string slotId, ItemStack stack)>();
            for (int i = 0; i < oldInv.Count; i++)
            {
                if (oldInv[i].Empty) continue;

                if (GetShelvableLayout(oldInv[i].Itemstack) is not EnumShelvableLayout layout) return;

                int surfaceIndex = i >= OldShelfSlotsPerSurface ? 1 : 0;
                PlacementSurface surface = displayBlock.PlacementSurfaces[surfaceIndex];
                if (BlockBehaviorDisplay.GetDisplayableAttributes(oldInv[i], surface) == null) return;

                string slotId = GetMigratedSlotId(i, surfaceIndex, layout);
                if (migratedStacks.Exists(val => val.slotId == slotId)) return;

                migratedStacks.Add((slotId, oldInv[i].Itemstack.Clone()));
            }

            if (migratedStacks.Count == 0) return;

            for (int i = 0; i < migratedStacks.Count; i++)
            {
                display.InventoryInv.Allocate(migratedStacks[i].slotId);
                display.InventoryInv[migratedStacks[i].slotId].Itemstack = migratedStacks[i].stack;
            }

            migratedOldInventory = true;
            display.MarkDisplayDirty(worldForResolve);
        }

        private static string GetMigratedSlotId(int oldSlotId, int surfaceIndex, EnumShelvableLayout layout)
        {
            float x = (oldSlotId % OldShelfSlotsPerSurface) >= 2 ? OldShelfRightX : OldShelfLeftX;
            float z = (oldSlotId % 2) == 0 ? OldShelfBackZ : OldShelfFrontZ;

            if (layout == EnumShelvableLayout.SingleCenter)
            {
                x = OldShelfCenterX;
                z = OldShelfCenterZ;
            }
            else if (layout == EnumShelvableLayout.Halves)
            {
                z = OldShelfCenterZ;
            }

            return new SlotLocation(x, 0, z)
            {
                PlacementSurfaceIndex = surfaceIndex
            }.EncodedLocation;
        }

        private float GetShelfAngleRad()
        {
            return Block.LastCodePart() switch
            {
                "east" => 270 * GameMath.DEG2RAD,
                "west" => 90 * GameMath.DEG2RAD,
                "south" => 180 * GameMath.DEG2RAD,
                _ => 0
            };
        }

        public static EnumShelvableLayout? GetShelvableLayout(ItemStack? stack)
        {
            if (stack == null) return null;

            var attr = stack.Collectible?.Attributes;
            var layout = stack.Collectible?.GetCollectibleInterface<IShelvable>()?.GetShelvableType(stack);

            layout ??= attr?["shelvable"].AsString() switch
            {
                "Quadrants" => EnumShelvableLayout.Quadrants,
                "Halves" => EnumShelvableLayout.Halves,
                "SingleCenter" => EnumShelvableLayout.SingleCenter,
                _ => null
            };

            layout ??= attr?["shelvable"].AsBool() == true ? EnumShelvableLayout.Quadrants : null;

            return layout;
        }

        public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            if (contentSlot.Empty) return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[]? transitionStates = contentSlot.Itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);
            if (transitionStates == null) return dsc.ToString();

            bool nowSpoiling = false;
            bool appendLine = false;
            for (int i = 0; i < transitionStates.Length; i++)
            {
                TransitionState state = transitionStates[i];

                TransitionableProperties prop = state.Props;
                float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                if (perishRate <= 0) continue;

                float transitionLevel = state.TransitionLevel;
                float freshHoursLeft = state.FreshHoursLeft / perishRate;

                switch (prop.Type)
                {
                    case EnumTransitionType.Perish:

                        appendLine = true;

                        if (transitionLevel > 0)
                        {
                            nowSpoiling = true;
                            dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                        }
                        else
                        {
                            double hoursPerday = Api.World.Calendar.HoursPerDay;

                            if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                            {
                                dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                            }
                            else if (freshHoursLeft > hoursPerday)
                            {
                                dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                            }
                            else
                            {
                                dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                            }
                        }
                        break;

                    case EnumTransitionType.Ripen:
                        if (nowSpoiling) break;

                        appendLine = true;

                        if (transitionLevel > 0)
                        {
                            dsc.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                        }
                        else
                        {
                            double hoursPerday = Api.World.Calendar.HoursPerDay;

                            if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                            {
                                dsc.Append(", " + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                            }
                            else if (freshHoursLeft > hoursPerday)
                            {
                                dsc.Append(", " + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                            }
                            else
                            {
                                dsc.Append(", " + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                            }
                        }
                        break;
                }
            }

            if (appendLine) dsc.AppendLine();

            return dsc.ToString();
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            var display = GetBehavior<BEBehaviorDisplay>();
            display.OnLoadCollectibleMappings(world, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            var display = GetBehavior<BEBehaviorDisplay>();
            display.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        }
    }
}
