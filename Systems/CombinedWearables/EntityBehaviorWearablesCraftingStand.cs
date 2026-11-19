using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorWearablesCraftingStand : EntityBehaviorContainer, ICustomInteractionHelpPositioning, ILayeredContainer
    {
        public EntityBehaviorWearablesCraftingStand(Entity entity) : base(entity) { }

        public WearableBitSlotConfig[]? WearableSlots { get; set; }

        public string[]? Layers { get; set; }

        public override InventoryBase? Inventory => inventory;

        public override string InventoryClassName => "wearablesInv";

        public virtual bool TransparentCenter => false;

        public virtual Dictionary<string, HashSet<EnumModifierKey>> ActionsModifiers { get; set; } = [];

        public virtual HashSet<string> ActionsRequiringTools { get; set; } = [
            "Combine",
            "CycleVariant",
            "Detach",
            "Repair"
        ];


        string[] ILayeredContainer.GetLayerCodes() => Layers ?? throw new InvalidOperationException("Trying to access 'EntityBehaviorStackableAccessories' layers before it is loaded");

        string[] ILayeredContainer.GetSlotCodes() => WearableSlots?.Select(config => config.Code).ToArray() ?? throw new InvalidOperationException("Trying to access 'EntityBehaviorStackableAccessories' slots configs before it is loaded");

        IInventory ILayeredContainer.Inventory => inventory ?? throw new InvalidOperationException("Trying to access 'EntityBehaviorStackableAccessories' inventory before it is loaded");

        WearableBitSlotConfig[] ILayeredContainer.GetSlotConfigs() => WearableSlots ?? throw new InvalidOperationException("Trying to access 'EntityBehaviorStackableAccessories' slots configs before it is loaded");



        protected int stackDepth { get; set; } = 0;

        protected InventoryGeneric? inventory { get; set; }

        protected AssetLocation? combinedItemType { get; set; }

        protected Dictionary<AssetLocation, string[]> combinedItemTypesBySlots { get; set; } = [];

        protected Dictionary<string, float> durabilityColors { get; set; } = [];



        public override bool TryEarlyLoadCollectibleMappings(IWorldAccessor worldForCollectibleResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports, EntityProperties entityProperties, JsonObject behaviorConfig)
        {
            loadSlotConfigs(behaviorConfig, out WearableBitSlotConfig[] wearableSlots, out string[] layers, out int stackDepth);

            inventory = new InventoryGeneric(wearableSlots.Length, InventoryClassName + "-EarlyLoad-" + entity.EntityId, entity.Api, (id, inv) => new ItemSlotWearable(inv, wearableSlots[id].ForCategoryCodes));
            loadInv();
            if (!inventory.Empty)
            {
                return base.TryEarlyLoadCollectibleMappings(worldForCollectibleResolve, oldBlockIdMapping, oldItemIdMapping, resolveImports, entityProperties, behaviorConfig);
            }
            return true;
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            Api = entity.World.Api;

            string? combinedItemTypeStr = attributes["combinedItemType"]?.AsString();
            if (combinedItemTypeStr != null)
            {
                combinedItemType = combinedItemTypeStr;
            }
            Dictionary<string, string[]>? combinedItemTypesBySlotsByStr = attributes["combinedItemTypesBySlots"]?.AsObject<Dictionary<string, string[]>>();
            if (combinedItemTypesBySlotsByStr != null)
            {
                combinedItemTypesBySlots = combinedItemTypesBySlotsByStr.ToDictionary(entry => new AssetLocation(entry.Key), entry => entry.Value);
            }
            durabilityColors = attributes["durabilityColors"]?.AsObject(durabilityColors) ?? durabilityColors;

            loadSlotConfigs(attributes);

            if (WearableSlots == null) throw new InvalidOperationException("Failed to initialize WearableSlots in EntityBehaviorWearablesCraftingStand.Initialize");

            inventory = new InventoryGeneric(WearableSlots.Length, InventoryClassName + "-" + entity.EntityId, entity.Api, (id, inv) => new ItemSlotWearable(inv, WearableSlots[id].ForCategoryCodes));
            loadInv();

            redistibuteItemsOnStackDepthChange();

            entity.WatchedAttributes.RegisterModifiedListener("wearablesInv", onWearablesModified);
            Dictionary<string, string[]> modifiers = attributes["modifiers"].AsObject<Dictionary<string, string[]>>() ?? [];
            ActionsRequiringTools = attributes["actionsRequiringTools"].AsObject(ActionsRequiringTools) ?? ActionsRequiringTools;

            ActionsModifiers = modifiers.ToDictionary(entry => entry.Key, entry => entry.Value.Select(value => Enum.Parse<EnumModifierKey>(value, ignoreCase: true)).ToHashSet());

            base.Initialize(properties, attributes);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode == EnumInteractMode.Attack || inventory == null)
            {
                base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
                return;
            }

            List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots = getInteractedSlots(byEntity);
            bool interactedSlotsEmpty = interactedSlots.Count == 0 || !interactedSlots.Any(entry => !entry.slot.Empty);
            getOuterOccupiedSlot(interactedSlots, out ItemSlot? outerOccupiedSlot, out WearableBitSlotConfig? outerOccupiedSlotConfig);

            // NONE
            if (itemslot.Empty && interactedSlotsEmpty)
            {
                base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
                return;
            }

            handled = EnumHandling.PreventSubsequent;

            // COMBINE
            bool correctModifier = checkModifiers("Combine", byEntity, out bool requiresTool);
            if (correctModifier && !requiresTool)
            {
                combine(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                return;
            }
            else
            {
                IArmorCombinationTool? combinationTool = itemslot.Itemstack?.Collectible.GetCollectibleInterface<IArmorCombinationTool>();
                if (correctModifier && combinationTool != null && combinationTool.CanCombine(itemslot, inventory.Where(slot => !slot.Empty), byEntity, this))
                {
                    combine(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                    return;
                }
            }

            // REPAIR
            correctModifier = checkModifiers("Repair", byEntity, out requiresTool);
            IArmorRepairTool? repairTool = itemslot.Itemstack?.Collectible.GetCollectibleInterface<IArmorRepairTool>();
            if (correctModifier && repairTool != null && repairTool.CanRepair(itemslot, interactedSlots.Select(entry => entry.slot), byEntity, this))
            {
                repair(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                return;
            }

            // FLIP SHAPE
            correctModifier = checkModifiers("CycleVariant", byEntity, out requiresTool);
            if (correctModifier && !requiresTool && outerOccupiedSlotConfig != null)
            {
                cycleVariant(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                return;
            }
            else
            {
                IArmorVariantFlipperTool? flipperTool = itemslot.Itemstack?.Collectible.GetCollectibleInterface<IArmorVariantFlipperTool>();
                if (correctModifier && flipperTool != null && outerOccupiedSlotConfig != null && flipperTool.CanFlipVariant(itemslot, interactedSlots[^1].slot, byEntity, outerOccupiedSlotConfig, this))
                {
                    cycleVariant(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                    return;
                }
            }

            // UNCOMBINE
            correctModifier = checkModifiers("Uncombine", byEntity, out requiresTool);
            if (correctModifier && itemslot.Itemstack?.Collectible.GetCollectibleInterface<ICustomArmor>() != null)
            {
                uncombine(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                return;
            }

            // DETACH PIECE
            correctModifier = checkModifiers("Detach", byEntity, out requiresTool);
            if (correctModifier && !requiresTool)
            {
                detach(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                return;
            }
            else
            {
                IArmorDisassemblyTool? disassemblyTool = itemslot.Itemstack?.Collectible.GetCollectibleInterface<IArmorDisassemblyTool>();
                if (correctModifier && disassemblyTool != null && outerOccupiedSlot != null && outerOccupiedSlotConfig != null && disassemblyTool.CanDisassemble(itemslot, outerOccupiedSlot, byEntity, outerOccupiedSlotConfig, this))
                {
                    detach(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                    return;
                }
            }

            // ATTACH PIECE
            correctModifier = checkModifiers("Attach", byEntity, out requiresTool);
            if (correctModifier && !itemslot.Empty)
            {
                attach(byEntity, itemslot, hitPosition, mode, requiresTool, interactedSlots);
                return;
            }

            // PASS THROUGH
            handled = EnumHandling.PassThrough;
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (inventory != null)
            {
                for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
                {
                    ItemSlot slot = inventory[slotIndex];
                    IAttachedInteractions? attachedInteractions = slot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedInteractions>();
                    attachedInteractions?.OnEntityDespawn(slot, slotIndex, entity, despawn);
                }
            }

            base.OnEntityDespawn(despawn);
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            if (inventory == null) return;

            for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
            {
                ItemSlot? slot = inventory[slotIndex];
                IAttachedInteractions? attachedInteractions = slot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedInteractions>();
                attachedInteractions?.OnReceivedClientPacket(slot, slotIndex, entity, player, packetid, data, ref handled, storeInv);

                if (handled == EnumHandling.PreventSubsequent)
                {
                    break;
                }
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (inventory != null)
            {
                for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
                {
                    ItemSlot slot = inventory[slotIndex];
                    IAttachedInteractions? attachedInteractions = slot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedInteractions>();
                    attachedInteractions?.OnEntityDeath(slot, slotIndex, entity, damageSourceForDeath);
                }
            }

            base.OnEntityDeath(damageSourceForDeath);
        }

        public override bool TryGiveItemStack(ItemStack itemstack, ref EnumHandling handling)
        {
            if (inventory == null) return false;

            int index = 0;
            DummySlot sourceslot = new(itemstack);
            foreach (ItemSlot? slot in inventory)
            {
                ItemSlot? targetSlot = getSlotFromSelectionBoxIndex(index);
                if (targetSlot != null && tryAttach(sourceslot, index, null))
                {
                    handling = EnumHandling.PreventDefault;
                    return true;
                }

                index++;
            }

            return base.TryGiveItemStack(itemstack, ref handling);
        }

        public override string PropertyName() => "wearablescraftingstand";

        public virtual Vec3d? GetInteractionHelpPosition()
        {
            ICoreClientAPI? capi = entity.Api as ICoreClientAPI;

            if (capi?.World.Player.CurrentEntitySelection == null) return null;

            int selebox = capi.World.Player.CurrentEntitySelection.SelectionBoxIndex - 1;

            if (selebox < 0) return null;

            return entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.GetCenterPosOfBox(selebox)?.Add(0, 0.2, 0);
        }

        public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            WorldInteraction[]? baseInteractions = base.GetInteractionHelp(world, es, player, ref handled);

            WorldInteraction[] armorInteractions = getArmorPiecesInteractions(world, es, player);

            if (baseInteractions != null && baseInteractions.Length > 0)
            {
                armorInteractions = armorInteractions.Concat(baseInteractions).ToArray();
            }

            return armorInteractions;
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);

            if ((Api as ICoreClientAPI)?.World?.Player?.Entity is not EntityPlayer player) return;

            List<(ItemSlot slot, WearableBitSlotConfig config)> slots = getInteractedSlots(player);

            if (slots.Count <= 0) return;

            infotext.AppendLine(Lang.Get("armorcraftingstand-selectionboxcontent"));

            foreach ((ItemSlot slot, WearableBitSlotConfig config) in slots)
            {
                string layer = Lang.Get($"armor-layer-{config.Layer}");

                if (slot.Itemstack != null)
                {
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
                    int remainingDurability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);

                    string durabilityText = "";

                    if (maxDurability != 0 && remainingDurability != maxDurability)
                    {
                        string? color = getDurabiltiyFractionColor((float)remainingDurability / maxDurability);
                        string durability;
                        if (color == null)
                        {
                            durability = $"{remainingDurability}/{maxDurability}";
                        }
                        else
                        {
                            durability = $"<font color=\"#{color}\">{remainingDurability}/{maxDurability}</font>";
                        }

                        durabilityText = Lang.Get("armorcraftingstand-layerdurability", durability);
                    }

                    infotext.AppendLine(Lang.Get("armorcraftingstand-layercontent", layer, slot.Itemstack.Collectible.GetHeldItemName(slot.Itemstack), durabilityText));
                }
                else
                {
                    infotext.AppendLine(Lang.Get("armorcraftingstand-layernocontent", layer));
                }
            }

            List<ProtectionModifiers> bitsModifiers = [];
            foreach ((ItemSlot slot, WearableBitSlotConfig config) in slots)
            {
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Collectible?.GetCollectibleBehavior<CollectibleBehaviorArmorBitStats>(true) is not CollectibleBehaviorArmorBitStats bitProtectionProvider) continue;

                ProtectionModifiers? bitModifiers = bitProtectionProvider.GetArmorBitProtectionModifiers(slot.Itemstack, config.Code, config.Layer);

                if (bitModifiers == null) continue;

                bitsModifiers.Add(bitModifiers);
            }

            if (bitsModifiers.Count > 0)
            {
                ProtectionModifiers aggregatedModifiers = ProtectionModifiers.Add(bitsModifiers.ToArray());

                infotext.AppendLine();
                aggregatedModifiers.GetDescription(infotext, player.Api);
            }
        }

        public virtual bool GetFirstSlotBy(string slotCode, string layerCode, [NotNullWhen(true)] out ItemSlot? slot, [NotNullWhen(true)] out WearableBitSlotConfig? config)
        {
            slot = null;
            config = null;

            if (WearableSlots == null || inventory == null) return false;

            for (int slotIndex = 0; slotIndex < WearableSlots.Length; slotIndex++)
            {
                WearableBitSlotConfig slotConfig = WearableSlots[slotIndex];

                if (slotConfig.Code == slotCode && slotConfig.Layer == layerCode)
                {
                    config = slotConfig;
                    slot = inventory[slotIndex];
                    return true;
                }
            }

            return false;
        }

        public virtual bool CanAttach(ItemSlot itemslot, int selectionBoxIndex, EntityAgent? byEntity)
        {
            if (itemslot.Itemstack == null) return false;

            if (WearableSlots == null || inventory == null || Layers == null) return false;

            IAttachableToEntity? iatta = IAttachableToEntity.FromCollectible(itemslot.Itemstack.Collectible);
            if (iatta == null || !iatta.IsAttachable(entity, itemslot.Itemstack)) return false;

            int selectionBoxSlotIndex = getSlotIndexFromSelectionBoxIndex(selectionBoxIndex);

            ItemSlot? targetSlot = getSuitableSlot(itemslot, selectionBoxSlotIndex, out int slotIndex, this);

            return targetSlot != null;
        }



        protected virtual void loadSlotConfigs(JsonObject attributes)
        {
            loadSlotConfigs(attributes, out WearableBitSlotConfig[] wearableSlots, out string[] layers, out int stackDepth);

            WearableSlots = wearableSlots;
            Layers = layers;
            this.stackDepth = stackDepth;
        }

        protected virtual void loadSlotConfigs(JsonObject attributes, out WearableBitSlotConfig[] wearableSlots, out string[] layers, out int stackDepth)
        {
            wearableSlots = attributes["wearableSlots"].AsObject<WearableBitSlotConfig[]>() ?? [];
            layers = attributes["layers"].AsObject<string[]>() ?? [];
            int defaultBitCoverage = attributes["defaultBitCoverage"]?.AsInt(1) ?? 1;
            Dictionary<string, int> bitCoverageBySlotCode = attributes["bitCoverageBySlotCode"]?.AsObject<Dictionary<string, int>>([]) ?? [];
            stackDepth = layers.Length;

            List<WearableBitSlotConfig> configs = [];
            for (int i = 0; i < wearableSlots.Length; i++)
            {
                for (int j = 0; j < stackDepth; j++)
                {
                    configs.Add((WearableBitSlotConfig)wearableSlots[i].Clone());
                }
            }
            wearableSlots = configs.ToArray();

            for (int i = 0; i < wearableSlots.Length; i++)
            {
                if (!bitCoverageBySlotCode.TryGetValue(wearableSlots[i].Code, out int coverage))
                {
                    coverage = defaultBitCoverage;
                }

                wearableSlots[i].Coverage = coverage;
                wearableSlots[i].Layer = layers[i % stackDepth];
            }
        }

        protected virtual void redistibuteItemsOnStackDepthChange()
        {
            if (inventory == null || WearableSlots == null) return;

            int previousStackDepth = entity.WatchedAttributes.GetAsInt("previousStackDepth", stackDepth);

            if (previousStackDepth == stackDepth) return;

            entity.WatchedAttributes.SetInt("previousStackDepth", stackDepth);

            int totalNumberOfSlots = WearableSlots.Length;
            int previousTotal = totalNumberOfSlots * previousStackDepth / stackDepth;
            Dictionary<int, ItemStack> contentsByNewSlotIndex = [];
            for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
            {
                ItemSlot slot = inventory[slotIndex];
                if (slot.Empty) continue;

                int oldLayer = slotIndex % previousStackDepth;
                int oldSlot = slotIndex / previousStackDepth;

                if (oldLayer >= stackDepth)
                {
                    Api.World.SpawnItemEntity(slot.TakeOutWhole(), entity.Pos.AsBlockPos);
                    continue;
                }

                int newIndex = oldSlot * stackDepth + oldLayer;

                contentsByNewSlotIndex.Add(newIndex, slot.TakeOutWhole());
                slot.MarkDirty();
            }

            foreach ((int slotIndex, ItemStack stack) in contentsByNewSlotIndex)
            {
                inventory[slotIndex].Itemstack = stack;
                inventory[slotIndex].MarkDirty();
            }
        }

        protected virtual ItemStack? combineBits(bool consumecombineditems, string slotCode, EntityAgent byEntity)
        {
            if (WearableSlots == null || inventory == null || Layers == null) return null;

            AssetLocation? resultItemCode = combinedItemType;
            string[]? slotsToProcess = null;
            foreach ((AssetLocation itemCode, string[] slots) in combinedItemTypesBySlots)
            {
                if (!slots.Contains(slotCode)) continue;

                resultItemCode = itemCode;
                slotsToProcess = slots;
            }

            if (resultItemCode == null) return null;

            List<int> slotIndexes = [];
            for (int slotIndex = 0; slotIndex < WearableSlots.Length; slotIndex++)
            {
                string pieceSlotCode = WearableSlots[slotIndex].Code;
                if (slotsToProcess == null || slotsToProcess.Contains(pieceSlotCode))
                {
                    slotIndexes.Add(slotIndex);
                }
            }

            JsonItemStack jsonStack = new()
            {
                Code = resultItemCode,
                Type = EnumItemClass.Item
            };

            jsonStack.Resolve(Api.World, "EntityBehaviorStackableAccessories.CombinePieces");

            ItemStack? carrier = jsonStack.ResolvedItemstack;

            ICombinedWearable? combined = carrier?.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (carrier == null || combined == null) return null;

            combined.CombineBits(carrier, this, byEntity, slotIndexes, consumecombineditems);

            return carrier;
        }

        protected virtual int getSelectionBoxIndex(EntityAgent byEntity)
        {
            return (byEntity as EntityPlayer)?.EntitySelection?.SelectionBoxIndex - 1 ?? -1;
        }

        protected virtual bool checkModifiers(string action, EntityAgent byEntity, out bool requiresTool)
        {
            requiresTool = false;

            if (!ActionsModifiers.TryGetValue(action, out HashSet<EnumModifierKey>? modifiers)) return true;

            requiresTool = ActionsRequiringTools.Contains(action);

            if (modifiers.Contains(EnumModifierKey.CTRL) && !byEntity.Controls.CtrlKey) return false;

            if (modifiers.Contains(EnumModifierKey.SHIFT) && !byEntity.Controls.ShiftKey) return false;

            // if (modifiers.Contains(EnumModifierKey.ALT) && !byEntity.Controls. ??? ) return false; // Not supported, because is missing from entity controls

            return true;
        }

        protected virtual List<(ItemSlot slot, WearableBitSlotConfig config)> getInteractedSlots(EntityAgent byEntity)
        {
            if (inventory == null || WearableSlots == null) return [];

            int seleBox = getSelectionBoxIndex(byEntity);
            if (seleBox < 0) return [];

            int slotIndex = getSlotIndexFromSelectionBoxIndex(seleBox);
            slotIndex = (slotIndex / stackDepth) * stackDepth;

            List<(ItemSlot slot, WearableBitSlotConfig config)> slots = [];
            for (int i = slotIndex; i < slotIndex + stackDepth; i++)
            {
                slots.Add((inventory[i], WearableSlots[i]));
            }

            return slots;
        }

        protected virtual void cycleVariant(EntityAgent byEntity, ItemSlot toolSlot, Vec3d hitPosition, EnumInteractMode mode, bool useTool, List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots)
        {
            getOuterOccupiedSlot(interactedSlots, out ItemSlot? outerOccupiedSlot, out WearableBitSlotConfig? config);
            if (outerOccupiedSlot == null || config == null) return;

            if (useTool)
            {
                IArmorVariantFlipperTool? tool = toolSlot.Itemstack?.Collectible?.GetCollectibleInterface<IArmorVariantFlipperTool>();
                if (tool == null) return;
                tool.FlipVariant(toolSlot, outerOccupiedSlot, byEntity, config, this);
            }
            else
            {
                CollectibleBehaviorArmorShapeVariantFlipperTool.FlipBitVariant(toolSlot, outerOccupiedSlot, byEntity, config, this);
            }

            onAttachmentToggled(byEntity, toolSlot);
        }

        protected virtual void attach(EntityAgent byEntity, ItemSlot toolSlot, Vec3d hitPosition, EnumInteractMode mode, bool useTool, List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots)
        {
            int selectionBox = getSelectionBoxIndex(byEntity);
            if (tryAttach(toolSlot, selectionBox, byEntity))
            {
                onAttachmentToggled(byEntity, toolSlot);
            }
        }

        protected virtual void detach(EntityAgent byEntity, ItemSlot toolSlot, Vec3d hitPosition, EnumInteractMode mode, bool useTool, List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots)
        {
            int selectionBox = getSelectionBoxIndex(byEntity);

            if (useTool)
            {
                IArmorDisassemblyTool? disassemblyTool = toolSlot.Itemstack?.Collectible.GetCollectibleInterface<IArmorDisassemblyTool>();

                getOuterOccupiedSlot(interactedSlots, out ItemSlot? outerOccupiedSlot, out WearableBitSlotConfig? config);

                if (disassemblyTool == null || outerOccupiedSlot == null || config == null) return;

                disassemblyTool.OnBeforeDisassembled(toolSlot, outerOccupiedSlot, byEntity, config, this);
            }

            if (tryRemoveAttachment(byEntity, selectionBox))
            {
                onAttachmentToggled(byEntity, toolSlot);
            }
        }

        protected virtual void combine(EntityAgent byEntity, ItemSlot toolSlot, Vec3d hitPosition, EnumInteractMode mode, bool useTool, List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots)
        {
            IArmorCombinationTool? combinationTool = toolSlot.Itemstack?.Collectible.GetCollectibleInterface<IArmorCombinationTool>();

            if (combinationTool == null || inventory == null || WearableSlots == null) return;

            combinationTool.OnBeforeCombined(toolSlot, inventory.Where(slot => !slot.Empty), byEntity, this);

            int selectionBox = getSelectionBoxIndex(byEntity);
            int slotIndex = getSlotIndexFromSelectionBoxIndex(selectionBox);
            WearableSlotConfig slotConfig = WearableSlots[slotIndex];
            bool consumecombineditems = combinationTool.ShouldConsumeArmorbits(toolSlot, inventory, byEntity, this);
            ItemStack? combined = combineBits(consumecombineditems, slotConfig.Code, byEntity);

            if (combined == null) return;

            DummySlot slotForCombined = new(combined);

            combinationTool?.OnAfterCombined(toolSlot, slotForCombined, byEntity, this);

            if (slotForCombined.Itemstack == null) return;

            if (!byEntity.TryGiveItemStack(slotForCombined.Itemstack))
            {
                Api.World.SpawnItemEntity(slotForCombined.Itemstack, byEntity.Pos.AsBlockPos);
            }
        }

        protected virtual void uncombine(EntityAgent byEntity, ItemSlot toolSlot, Vec3d hitPosition, EnumInteractMode mode, bool useTool, List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots)
        {
            ICombinedWearable? combinedBehavior = toolSlot.Itemstack?.Collectible.GetCollectibleInterface<ICombinedWearable>();
            if (combinedBehavior == null || toolSlot.Itemstack == null) return;

            combinedBehavior.UncombineBits(toolSlot.Itemstack, this, entity);

            dropAllInvalidBits(byEntity);

            toolSlot.TakeOutWhole();
            toolSlot.MarkDirty();
        }

        protected virtual void repair(EntityAgent byEntity, ItemSlot toolSlot, Vec3d hitPosition, EnumInteractMode mode, bool useTool, List<(ItemSlot slot, WearableBitSlotConfig config)> interactedSlots)
        {
            IArmorRepairTool? repairTool = toolSlot.Itemstack?.Collectible.GetCollectibleInterface<IArmorRepairTool>();

            if (repairTool == null) return;

            repairTool.Repair(toolSlot, interactedSlots.Select(entry => entry.slot), byEntity, this);
        }

        protected virtual void onWearablesModified()
        {
            loadInv();
            entity.MarkShapeModified();
        }

        protected virtual void onAttachmentToggled(EntityAgent byEntity, ItemSlot toolSlot)
        {
            SoundAttributes sound = toolSlot.Itemstack?.Block?.Sounds.Place ?? GlobalConstants.DefaultBuildSound;
            Api.World.PlaySoundAt(sound, entity, (byEntity as EntityPlayer)?.Player);
            entity.MarkShapeModified();
            entity.World.BlockAccessor.GetChunkAtBlockPos(entity.Pos.AsBlockPos).MarkModified();
        }

        protected virtual bool tryRemoveAttachment(EntityAgent byEntity, int selectionBoxIndex)
        {
            if (WearableSlots == null || inventory == null || Layers == null) return false;

            int slotIndex = getSlotIndexFromSelectionBoxIndex(selectionBoxIndex);
            slotIndex = (slotIndex / stackDepth) * stackDepth;
            ItemSlot slot = inventory[slotIndex + stackDepth - 1];
            if (slot.Empty)
            {
                for (int i = slotIndex + stackDepth - 1; i >= slotIndex; i--)
                {
                    if (slot.Empty)
                    {
                        slot = inventory[i];
                    }
                }
            }

            if (tryDetachAndGiveBit(slot, slotIndex, byEntity))
            {
                dropAllInvalidBits(byEntity);

                CollectibleBehaviorArmorShapeVariantFlipperTool.FlipAllInvalid(this);

                storeInv();
                return true;
            }

            return false;
        }

        protected virtual bool tryDetachAndGiveBit(ItemSlot bitSlot, int slotIndex, EntityAgent toEntity)
        {
            if (bitSlot.Empty) return false;

            IAttachedInteractions? attachedInteractable = bitSlot.Itemstack.Collectible.GetCollectibleInterface<IAttachedInteractions>();
            if (attachedInteractable?.OnTryDetach(bitSlot, slotIndex, entity) == false)
            {
                return false;
            }

            bool wasEmptyAlready = bitSlot.StackSize == 0;

            IAttachedListener? attached = bitSlot.Itemstack.Collectible.GetCollectibleInterface<IAttachedListener>();
            attached?.OnDetached(bitSlot, slotIndex, entity, toEntity);

            CollectibleBehaviorArmorTextureVariantFlipperTool.Unflip(bitSlot);
            CollectibleBehaviorArmorShapeVariantFlipperTool.Unflip(bitSlot);

            ItemStack bitStack = bitSlot.TakeOutWhole();

            if (Api.Side == EnumAppSide.Server && !wasEmptyAlready)
            {
                Api.World.Logger.Audit("{0} removed from a {1} at {2}, slot {4}: {3}", toEntity.GetName(), entity.Code.ToShortString(), entity.Pos.AsBlockPos, bitStack.ToString(), slotIndex);
            }

            bool playerInCreativeMode = toEntity is EntityPlayer player && player.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;

            if (!playerInCreativeMode && !wasEmptyAlready)
            {
                if (!toEntity.TryGiveItemStack(bitStack))
                {
                    toEntity.Api?.World.SpawnItemEntity(bitStack, toEntity.Pos.AsBlockPos);
                }
            }

            bitSlot.Itemstack = null;

            return true;
        }

        protected virtual bool tryAttach(ItemSlot itemslot, int selectionBoxIndex, EntityAgent? byEntity)
        {
            if (itemslot.Itemstack == null) return false;
            if (selectionBoxIndex < 0) return false;
            if (WearableSlots == null || inventory == null || Layers == null) return false;

            IAttachableToEntity? iatta = IAttachableToEntity.FromCollectible(itemslot.Itemstack.Collectible);
            if (iatta == null || !iatta.IsAttachable(entity, itemslot.Itemstack)) return false;

            int selectionBoxSlotIndex = getSlotIndexFromSelectionBoxIndex(selectionBoxIndex);

            ItemSlot? targetSlot = getSuitableSlot(itemslot, selectionBoxSlotIndex, out int slotIndex, this);

            if (targetSlot == null) return false;

            WearableBitSlotConfig slotConfig = WearableSlots[slotIndex];

            string? code = iatta.GetCategoryCode(itemslot.Itemstack);

            EntityBehaviorSeatable? ebhs = entity.GetBehavior<EntityBehaviorSeatable>();
            if (ebhs != null)
            {
                int seat = ebhs.SeatConfigs.IndexOf(x => x.SelectionBox == slotConfig.AttachmentPointCode);
                if (seat > -1 && ebhs.Seats[seat].Passenger != null)
                {
                    if (Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(this, "alreadyoccupied", Lang.Get("mount-interact-alreadyoccupied"));
                    }
                    return false;
                }
            }

            if (!slotConfig.CanHold(code)) return false;
            if (!targetSlot.Empty) return false;

            if (iatta.RequiresBehindSlots > 0)
            {
                if (slotConfig.BehindSlots.Length < iatta.RequiresBehindSlots)
                {
                    (entity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "notenoughspace", Lang.Get("mount-interact-requiresbehindslots", iatta.RequiresBehindSlots));
                    return false;
                }

                int behindSlotIndex = WearableSlots.IndexOf(sc => sc.Code == slotConfig.BehindSlots[0]);
                if (behindSlotIndex >= 0)
                {
                    ItemSlot slot = inventory[behindSlotIndex];
                    if (!slot.Empty)
                    {
                        (entity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "alreadyoccupied", Lang.Get("mount-interact-alreadyoccupiedbehind", iatta.RequiresBehindSlots + 1));
                        return false;
                    }
                }
            }

            int inFrontSlotIndex = WearableSlots.IndexOf(sc => sc.BehindSlots?.Contains(slotConfig.Code) == true);
            if (inFrontSlotIndex >= 0)
            {
                ItemSlot slot = inventory[inFrontSlotIndex];
                if (!slot.Empty)
                {
                    IAttachableToEntity? inFrontiatta = IAttachableToEntity.FromCollectible(slot.Itemstack.Collectible);
                    if (inFrontiatta?.RequiresBehindSlots > 0)
                    {
                        (entity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "alreadyoccupied", Lang.Get("mount-interact-alreadyoccupiedinfront", iatta.RequiresBehindSlots));
                        return false;
                    }
                }
            }


            EntityBehaviorOwnable? ebho = entity.GetBehavior<EntityBehaviorOwnable>();
            if (ebho != null && !ebho.IsOwner(byEntity))
            {
                (entity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "requiersownership", Lang.Get("mount-interact-requiresownership"));
                return false;
            }

            // Cannot attach something where a player already sits on
            EntityBehaviorSeatable? bhs = entity.GetBehavior<EntityBehaviorSeatable>();
            if (bhs?.Seats.FirstOrDefault(s => s.Config.APName == slotConfig.AttachmentPointCode)?.Passenger != null) return false;

            IAttachedInteractions? iai = itemslot.Itemstack.Collectible.GetCollectibleInterface<IAttachedInteractions>();
            if (iai?.OnTryAttach(itemslot, slotIndex, entity) == false) return false;

            IAttachedListener? ial = itemslot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedListener>();

            if (entity.World.Side == EnumAppSide.Server)
            {
                string auditLog = String.Format("{0} attached to a {1} at {2}, slot {4}: {3}", byEntity?.GetName(), entity.Code.ToShortString(), entity.Pos.AsBlockPos, itemslot.Itemstack?.ToString(), slotIndex);

                bool moved = false;
                if (byEntity is EntityPlayer player && player.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative && itemslot.Itemstack != null)
                {
                    ItemStack bitStack = itemslot.Itemstack.Clone();
                    bitStack.StackSize = 1;
                    DummySlot bitSlot = new(bitStack);

                    moved = bitSlot.TryPutInto(entity.World, targetSlot) > 0;
                }
                else
                {
                    moved = itemslot.TryPutInto(entity.World, targetSlot) > 0;
                }
                
                if (moved)
                {
                    Api.World.Logger.Audit(auditLog);
                    ial?.OnAttached(targetSlot, slotIndex, entity, byEntity);

                    string svt = slotConfig.Code;
                    string svt2 = targetSlot.Itemstack?.Attributes?.GetString("shapeVariantType", "") ?? ""; 
                    string tvt2 = targetSlot.Itemstack?.Attributes?.GetString("textureVariantType", "") ?? "";

                    if (svt2 != svt || svt2 == "" || tvt2 == "" || svt != tvt2)
                    {
                        CollectibleBehaviorArmorTextureVariantFlipperTool.Unflip(targetSlot);
                        CollectibleBehaviorArmorShapeVariantFlipperTool.Unflip(targetSlot);
                    }

                    CollectibleBehaviorArmorShapeVariantFlipperTool.FlipAllInvalid(this, skipSlot: itemslot);

                    CollectibleBehaviorArmorTextureVariantFlipperTool.TryFlipBitVariantToDefault(targetSlot, byEntity, slotConfig, this);

                    storeInv();
                }

                return moved;
            }

            CollectibleBehaviorArmorShapeVariantFlipperTool.FlipAllInvalid(this, skipSlot: itemslot);

            return true;
        }

        protected virtual ItemSlot? getSlotFromSelectionBoxIndex(int boxIndex)
        {
            int index = getSlotIndexFromSelectionBoxIndex(boxIndex);
            if (index == -1) return null;
            return inventory?[index];
        }

        protected virtual int getSlotIndexFromSelectionBoxIndex(int boxIndex)
        {
            if (boxIndex < 0) return -1;

            EntityBehaviorSelectionBoxes? selectionBehavior = entity.GetBehavior<EntityBehaviorSelectionBoxes>();
            ArgumentNullException.ThrowIfNull(selectionBehavior);

            // Allow throwing if the index is out of bounds, since that shouldn't be happening
            string apCode = selectionBehavior.selectionBoxes[boxIndex].AttachPoint.Code;

            return WearableSlots.IndexOf(elem => elem.AttachmentPointCode == apCode);
        }

        protected virtual WorldInteraction[] getArmorPiecesInteractions(IClientWorldAccessor world, EntitySelection selection, IClientPlayer player)
        {
            List<WorldInteraction> interactions = [];

            foreach ((string action, HashSet<EnumModifierKey> modifiers) in ActionsModifiers)
            {
                string requiresTool = ActionsRequiringTools.Contains(action) ? "withtool" : "withouttool";

                WorldInteraction interaction = new()
                {
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = $"armorcraftingstand-action-{requiresTool}-{action.ToLower()}",
                    HotKeyCodes = modifiers.Select(modifier => modifier.ToString().ToLower()).ToArray()
                };

                interactions.Add(interaction);
            }

            return interactions.ToArray();
        }

        protected virtual string? getDurabiltiyFractionColor(float fraction)
        {
            float currentThreshold = 0;
            string? currentColor = null;

            foreach ((string? color, float threshold) in durabilityColors)
            {
                if (fraction < threshold && threshold > currentThreshold)
                {
                    currentThreshold = fraction;
                    currentColor = color;
                }
            }

            return currentColor;
        }

        protected static void getOuterOccupiedSlot(List<(ItemSlot slot, WearableBitSlotConfig config)> slots, out ItemSlot? outerOccupiedSlot, out WearableBitSlotConfig? config)
        {
            outerOccupiedSlot = null;
            config = null;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (!slots[i].slot.Empty)
                {
                    outerOccupiedSlot = slots[i].slot;
                    config = slots[i].config;
                    break;
                }
            }
        }

        protected virtual void dropAllInvalidBits(EntityAgent byEntity)
        {
            HashSet<ItemSlot> dropSlots = getSlotsWithInvalidBits();
            HashSet<ItemSlot> skippedSlots = [];
            while (dropSlots.Any())
            {
                foreach (ItemSlot pieceSlot in dropSlots)
                {
                    if (pieceSlot.Itemstack == null) continue;

                    if (tryDetachAndGiveBit(pieceSlot, pieceSlot.Inventory.GetSlotId(pieceSlot), byEntity))
                    {
                        pieceSlot.MarkDirty();
                    }
                    else
                    {
                        skippedSlots.Add(pieceSlot);
                    }
                }

                dropSlots = getSlotsWithInvalidBits().Except(skippedSlots).ToHashSet();
                skippedSlots.Clear();
            }
        }

        protected virtual HashSet<ItemSlot> getSlotsWithInvalidBits()
        {
            HashSet<ItemSlot> slotsToDrop = [];

            if (Inventory == null) return slotsToDrop;

            bool foundPieceToDrop = true;
            while (foundPieceToDrop)
            {
                foundPieceToDrop = false;

                foreach (ItemSlot slot in Inventory.Where(slot => !slotsToDrop.Contains(slot)))
                {
                    ItemStack? piece = slot.Itemstack;
                    if (piece == null) continue;

                    int slotIndex = Inventory.GetSlotId(slot);

                    slot.Itemstack = null;
                    int layer = getSuitableLayer(piece, slotIndex, this);
                    slot.Itemstack = piece;

                    if (layer >= 0) continue;

                    slotsToDrop.Add(slot);
                    foundPieceToDrop = true;
                }
            }

            return slotsToDrop;
        }

        protected virtual ItemSlot? getSuitableSlot(ItemSlot itemslot, int selectionBoxIndexSlotIndex, out int slotIndex, ILayeredContainer container)
        {
            slotIndex = selectionBoxIndexSlotIndex;

            if (itemslot.Itemstack == null) return null;

            slotIndex -= selectionBoxIndexSlotIndex % container.GetLayerCodes().Length;

            int layerIndex = getSuitableLayer(itemslot.Itemstack, slotIndex, container);

            if (layerIndex < 0) return null;

            slotIndex += layerIndex;

            return container.Inventory[slotIndex];
        }

        protected virtual int getSuitableLayer(ItemStack piece, int slotIndex, ILayeredContainer container)
        {
            string[] slotCodes = container.GetSlotCodes();
            string[] inventoryLayers = container.GetLayerCodes();
            WearableBitSlotConfig[] configs = container.GetSlotConfigs();
            int stackDepth = inventoryLayers.Length;
            string slotCode = slotCodes[slotIndex];

            JsonObject? attachableToEntityAttributes = piece.ItemAttributes?["attachableToEntity"];
            if (attachableToEntityAttributes == null) return -1;

            string?[]? allowedSlots = attachableToEntityAttributes["allowedSlots"]?.AsArray<string>();
            string?[]? skipSlots = attachableToEntityAttributes["skipSlots"]?.AsArray<string>();
            if (allowedSlots != null && allowedSlots.Length > 0 && !allowedSlots.Contains(slotCode)) return -1;
            if (skipSlots != null && skipSlots.Length > 0 && skipSlots.Contains(slotCode)) return -1;

            int[]? layers = null;
            string?[]? layerCodes = attachableToEntityAttributes["layers"]?.AsArray<string>();

            if (layerCodes != null)
            {
                layers = layerCodes
                    .OfType<string>()
                    .Select(code => inventoryLayers.IndexOf(code))
                    .Where(index => index >= 0)
                    .ToArray();
            }

            bool requiresPreviousLayer = attachableToEntityAttributes["requiresPreviousLayer"]?.AsBool(defaultValue: true) ?? true;
            bool requiresAnyLayer = attachableToEntityAttributes["requiresAnyLayer"]?.AsBool(defaultValue: false) ?? false;
            requiresPreviousLayer = attachableToEntityAttributes["requiresPreviousLayerBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsBool(defaultValue: requiresPreviousLayer) ?? requiresPreviousLayer;
            requiresAnyLayer = attachableToEntityAttributes["requiresAnyLayerBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsBool(defaultValue: requiresAnyLayer) ?? requiresAnyLayer;
            string?[] extraOccupiedSlots = attachableToEntityAttributes["extraOccupiedSlotsBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsArray<string>() ?? [];
            string?[] requiredSlotsBySlotCode = attachableToEntityAttributes["requiredSlotsBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsArray<string>() ?? [];

            string? previousLayerWildcardStr = attachableToEntityAttributes["previousLayerRequirement"]?.AsString();
            string? previousLayerWildcardBySlotStr = attachableToEntityAttributes["previousLayerRequirementBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsString();
            if (previousLayerWildcardBySlotStr != null)
            {
                previousLayerWildcardStr = previousLayerWildcardBySlotStr;
            }
            AssetLocation? previousLayerWildcard = null;
            if (previousLayerWildcardStr != null) previousLayerWildcard = previousLayerWildcardStr;

            HashSet<int> slotsToOccupy = getInventoryIndexesToOccupy(slotCodes, inventoryLayers, extraOccupiedSlots.OfType<string>().Append(slotCode).ToArray());
            HashSet<int> occupiedSlots = getOccupiedInventoryIndexes(slotCodes, container.Inventory, inventoryLayers);

            Dictionary<int, HashSet<int>> requiredSlotsByLayers = [];
            string[] requiredSlotCodes = requiredSlotsBySlotCode.OfType<string>().ToArray();
            foreach (string layer in inventoryLayers)
            {
                int layerIndex = inventoryLayers.IndexOf(layer);
                requiredSlotsByLayers[layerIndex] = [];

                if (requiredSlotCodes.Length > 0)
                {
                    for (int inventorySlotIndex = 0; inventorySlotIndex < configs.Length; inventorySlotIndex++)
                    {
                        WearableBitSlotConfig config = configs[inventorySlotIndex];
                        if (config.Layer == layer && requiredSlotCodes.Contains(config.Code))
                        {
                            requiredSlotsByLayers[layerIndex].Add(inventorySlotIndex);
                        }
                    }
                }
            }


            ItemSlot? targetSlot = null;

            int layerStartIndex = (slotIndex / stackDepth) * stackDepth;
            int minimumLayer = 0;
            int maximumLayer = stackDepth - 1;
            if (layers != null && layers.Length > 0)
            {
                minimumLayer = layers.Min();
                maximumLayer = layers.Max();
            }

            int resultingLayer = -1;

            for (int layer = minimumLayer; layer <= maximumLayer; layer++)
            {
                if (requiresPreviousLayer && layer > 0)
                {
                    bool allOccupied = true;
                    foreach (int extraSlotIndex in slotsToOccupy)
                    {
                        if (!occupiedSlots.Contains(extraSlotIndex + layer - 1))
                        {
                            allOccupied = false;
                            break;
                        }
                    }

                    if (!allOccupied)
                    {
                        continue;
                    }
                }

                if (requiresAnyLayer && layer > 0)
                {
                    bool allOccupied = true;
                    foreach (int extraSlotIndex in slotsToOccupy)
                    {
                        bool someOccupied = false;
                        for (int previousLayer = 0; previousLayer < layer; previousLayer++)
                        {
                            if (occupiedSlots.Contains(extraSlotIndex + layer - previousLayer))
                            {
                                someOccupied = true;
                                break;
                            }
                        }
                        if (!someOccupied)
                        {
                            allOccupied = false;
                            break;
                        }
                    }

                    if (!allOccupied)
                    {
                        continue;
                    }
                }

                if (previousLayerWildcard != null && layer > 0)
                {
                    bool allOccupied = true;
                    foreach (int extraSlotIndex in slotsToOccupy)
                    {
                        ItemSlot previousLayerSlot = container.Inventory[extraSlotIndex + layer - 1];

                        if (previousLayerSlot.Empty || previousLayerSlot.Itemstack?.Collectible?.Code == null) continue;

                        if (!WildcardUtil.Match(previousLayerWildcard, previousLayerSlot.Itemstack.Collectible.Code))
                        {
                            allOccupied = false;
                            break;
                        }
                    }

                    if (!allOccupied)
                    {
                        continue;
                    }
                }

                bool missingRequired = false;
                foreach (int requiredSlotIndex in requiredSlotsByLayers[layer])
                {
                    if (!occupiedSlots.Contains(requiredSlotIndex))
                    {
                        missingRequired = true;
                    }
                }
                if (missingRequired) continue;

                bool allEmpty = true;
                foreach (int extraSlotIndex in slotsToOccupy)
                {
                    if (occupiedSlots.Contains(extraSlotIndex + layer))
                    {
                        allEmpty = false;
                        break;
                    }
                }

                if (!allEmpty)
                {
                    continue;
                }

                slotIndex = layerStartIndex + layer;
                targetSlot = container.Inventory[slotIndex];

                resultingLayer = slotIndex % stackDepth;

                break;
            }

            return resultingLayer;
        }

        protected virtual HashSet<int> getOccupiedInventoryIndexes(string[] slotCodes, IInventory inventory, string[] layers)
        {
            int stackDepth = layers.Length;
            HashSet<int> occupiedSlots = [];
            for (int inventoryIndex = 0; inventoryIndex < inventory.Count; inventoryIndex++)
            {
                int slotIndex = (inventoryIndex / stackDepth) * stackDepth;
                int slotLayer = inventoryIndex % stackDepth;
                string slotCode = slotCodes[inventoryIndex];

                ItemStack? carrier = inventory[inventoryIndex].Itemstack;

                if (carrier == null) continue;

                occupiedSlots.Add(inventoryIndex);

                IEnumerable<string> extraOccupiedSlots = getBitExtraSlots(carrier, slotCode);

                foreach (string extraOccupiedSlot in extraOccupiedSlots)
                {
                    for (int extraSlotIndex = slotLayer; extraSlotIndex < slotCodes.Length; extraSlotIndex += stackDepth)
                    {
                        if (slotCodes[extraSlotIndex] == extraOccupiedSlot)
                        {
                            occupiedSlots.Add(extraSlotIndex);
                            break;
                        }
                    }
                }
            }

            return occupiedSlots;
        }

        protected virtual HashSet<int> getInventoryIndexesToOccupy(string[] inventorySlots, string[] layers, string[] slotCodes)
        {
            int stackDepth = layers.Length;
            HashSet<int> occupiedSlots = [];

            for (int extraSlotIndex = 0; extraSlotIndex < inventorySlots.Length; extraSlotIndex += stackDepth)
            {
                if (slotCodes.Contains(inventorySlots[extraSlotIndex]))
                {
                    occupiedSlots.Add(extraSlotIndex);
                }
            }

            return occupiedSlots;
        }

        protected virtual IEnumerable<string> getBitExtraSlots(ItemStack carrier, string slotCode)
        {
            return carrier.ItemAttributes?["attachableToEntity"]?["extraOccupiedSlotsBySlotCode"]?.FirstByWildcardKey(slotCode)
                ?.AsArray<string>()
                ?.OfType<string>()
                .Where(code => code != slotCode)
                .Distinct() ?? [];
        }


        protected override Shape? addGearToShape(Shape entityShape, ItemSlot gearSlot, string slotCode, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements, Dictionary<string, StepParentElementTo>? overrideStepParent = null)
        {
            ItemStack? stack = gearSlot.Itemstack;
            IWearableBitAttachedShape? bitGearShape = stack?.Collectible.GetCollectibleInterface<IWearableBitAttachedShape>();
            ITextureAtlasAPI? entityTextureAtlas = (Api as ICoreClientAPI)?.EntityTextureAtlas;

            if (WearableSlots == null ||
                inventory == null ||
                Layers == null ||
                stack == null ||
                entityShape == null ||
                entity == null ||
                bitGearShape == null ||
                entityTextureAtlas == null)
            {
                return entityShape;
            }

            if (!shapeIsCloned)
            {
                // Make a full copy so we don't mess up the original
                Shape newShape = entityShape.Clone();
                entityShape = newShape;
                shapeIsCloned = true;
            }

            int slotIndex = gearSlot.Inventory.IndexOf((slot) => slot == gearSlot);
            overrideStepParent = WearableSlots[slotIndex].StepParentTo;
            slotCode = WearableSlots[slotIndex].Code;
            int layer = slotIndex % stackDepth;
            string layerCode = Layers[layer];

            IDictionary<string, CompositeTexture>? collectedTextures = entity.Properties.Client.Textures;
            WearableBitEntry bit = new()
            {
                Stack = stack,
                SlotCode = slotCode,
                LayerCode = layerCode,
                LayerIndex = layer
            };

            bitGearShape.AddGearToShape(entityTextureAtlas, entityShape, bit, shapePathForLogging, ref willDeleteElements, collectedTextures);

            return entityShape;
        }
    }


    public class EntityBehaviorCraftingStandSelectionBoxes : EntityBehaviorSelectionBoxes
    {
        public EntityBehaviorCraftingStandSelectionBoxes(Entity entity) : base(entity)
        {
        }

        protected override int getHitIndex(Ray pickingray)
        {
            EntityPlayer? player = entity.Api.World.GetNearestEntity(pickingray.origin + entity.Pos.XYZ, 2, 2, entity => entity is EntityPlayer) as EntityPlayer;

            if (player == null) return -1;

            IWearableBitAttachedShape? attached = player.ActiveHandItemSlot.Itemstack?.Collectible?.GetCollectibleInterface<IWearableBitAttachedShape>();

            int foundIndex = -1;
            double foundDistance = double.MaxValue;

            mvMatInv ??= new Matrixf();
            var rayLengthSq = pickingray.Length * pickingray.Length;
            Vec4d rayOrigin = new Vec4d(pickingray.origin.X, pickingray.origin.Y, pickingray.origin.Z, 1);
            Vec4d rayDirection = new Vec4d(pickingray.dir.X, pickingray.dir.Y, pickingray.dir.Z, 0);
            for (int i = 0; i < selectionBoxes.Length; i++)
            {
                var apap = selectionBoxes[i];

                mvmat.Identity();
                applyBoxTransform(mvmat, apap);
                mvMatInv.Set(mvmat.Values).Invert();

                // Here's the big trick: Instead of transforming the box into world space, we inverse transform the picking ray into the box's local space.
                // This way, we can use a simple AABB intersection test in the box's local space.
                var obbSpaceOrigin = mvMatInv.TransformVector(rayOrigin);
                var obbSpaceDirection = mvMatInv.TransformVector(rayDirection);
                Ray obbSpaceRay = new Ray(obbSpaceOrigin.XYZ, obbSpaceDirection.XYZ);

                if (AABBIntersectionTest.Testintersection(standardbox, obbSpaceRay, ref hitPositionOBBSpace) && (attached == null || testSlot(i, player)))
                {
                    var tf = mvmat.TransformVector(new Vec4d(hitPositionOBBSpace.X, hitPositionOBBSpace.Y, hitPositionOBBSpace.Z, 1));

                    double dist = (tf.XYZ - pickingray.origin).LengthSq();
                    if (foundIndex >= 0 && foundDistance < dist || rayLengthSq < dist) continue;

                    hitPositionAABBSpace = tf.XYZ;
                    foundDistance = dist;
                    foundIndex = i;
                }
            }

            return foundIndex;
        }

        protected virtual bool testSlot(int index, EntityPlayer player)
        {
            EntityBehaviorWearablesCraftingStand? stand = entity.GetBehavior<EntityBehaviorWearablesCraftingStand>();
            if (stand == null) return true;

            if (player.ActiveHandItemSlot.Itemstack == null) return true;

            bool canAttach = stand.CanAttach(player.ActiveHandItemSlot, index, player);

            return canAttach;
        }
    }
}
