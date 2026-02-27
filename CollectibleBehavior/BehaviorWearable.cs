using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent;

public class CollectibleBehaviorWearable : CollectibleBehaviorWearableAttachment, IWearable, IWearableStatsSupplier
{
    private StatModifiers? StatModifers;
    private ProtectionModifiers? ProtectionModifiers;
    private AssetLocation[]? FootStepSounds;
    private EnumCharacterDressType DressType;
    private ICoreAPI api;

    public CollectibleBehaviorWearable(CollectibleObject collObj) : base(collObj)
    {
    }

    public override string GetMeshCacheKey(ItemStack stack, string slotType)
    {
        return slotType + "-wearableModelRef-" + stack.Collectible.Code.ToString();
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        this.api = api;

        string strdress = collObj.Attributes["clothescategory"].AsString();
        Enum.TryParse(strdress, true, out EnumCharacterDressType dt);
        DressType = dt;


        JsonObject jsonObj = collObj.Attributes?["footStepSound"];
        if (jsonObj?.Exists == true)
        {
            string soundloc = jsonObj.AsString(null);
            if (soundloc != null)
            {
                AssetLocation loc = AssetLocation.Create(soundloc, collObj.Code.Domain).WithPathPrefixOnce("sounds/");

                if (soundloc.EndsWith('*'))
                {
                    loc.Path = loc.Path.TrimEnd('*');
                    FootStepSounds = api.Assets.GetLocations(loc.Path, loc.Domain).ToArray();
                }
                else
                {
                    FootStepSounds = new AssetLocation[] { loc };
                }
            }
        }

        jsonObj = collObj.Attributes?["statModifiers"];
        if (jsonObj?.Exists == true)
        {
            try
            {
                StatModifers = jsonObj.AsObject<StatModifiers>();
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading statModifiers for item/block {0}. Will ignore.", collObj.Code);
                api.World.Logger.Error(e);
                StatModifers = null;
            }
        }

        ProtectionModifiers defMods = null;
        jsonObj = collObj.Attributes?["defaultProtLoss"];
        if (jsonObj?.Exists == true)
        {
            try
            {
                defMods = jsonObj.AsObject<ProtectionModifiers>();
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading defaultProtLoss for item/block {0}. Will ignore.", collObj.Code);
                api.World.Logger.Error(e);
            }
        }

        jsonObj = collObj.Attributes?["protectionModifiers"];
        if (jsonObj?.Exists == true)
        {
            try
            {
                ProtectionModifiers = jsonObj.AsObject<ProtectionModifiers>();
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading protectionModifiers for item/block {0}. Will ignore.", collObj.Code);
                api.World.Logger.Error(e);
                ProtectionModifiers = null;
            }
        }


        if (ProtectionModifiers != null && ProtectionModifiers.PerTierFlatDamageReductionLoss == null)
        {
            ProtectionModifiers.PerTierFlatDamageReductionLoss = defMods?.PerTierFlatDamageReductionLoss;
        }
        if (ProtectionModifiers != null && ProtectionModifiers.PerTierRelativeProtectionLoss == null)
        {
            ProtectionModifiers.PerTierRelativeProtectionLoss = defMods?.PerTierRelativeProtectionLoss;
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);

        Dictionary<string, MultiTextureMeshRef> armorMeshrefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "armorMeshRefs");
        if (armorMeshrefs == null) return;

        foreach (var val in armorMeshrefs.Values)
        {
            val?.Dispose();
        }

        api.ObjectCache.Remove("armorMeshRefs");
    }

    public override void OnHandbookRecipeRender(ICoreClientAPI capi, IRecipeBase recipe, ItemSlot dummyslot, double x, double y, double z, double size, ref EnumHandling handling)
    {
        bool isRepairRecipe = recipe.Name.Path.Contains("repair");

        if (isRepairRecipe)
        {
            int prevDura = dummyslot.Itemstack.Collectible.GetRemainingDurability(dummyslot.Itemstack);
            dummyslot.Itemstack.Collectible.SetDurability(dummyslot.Itemstack, 0);

            capi.Render.RenderItemstackToGui(
                dummyslot,
                x,
                y,
                z, (float)size * 0.58f, ColorUtil.WhiteArgb,
                true, false, true
            );

            dummyslot.Itemstack.Collectible.SetDurability(dummyslot.Itemstack, prevDura);
            handling = EnumHandling.PreventSubsequent;
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (byEntity.Controls.ShiftKey) return;

        if (blockSel != null)
        {
            // Don't dress one-self if looking at a display or mannequin
            var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            var pos = blockSel.Position;
            if (block.GetBehavior<BlockBehaviorMultiblock>()?.ControllerPositionRel is { } relPos)
            {
                pos = pos.AddCopy(relPos);
            }
            var be = api.World.BlockAccessor.GetBlockEntity(pos);
            if (be?.Behaviors.Any(bh => bh is BEBehaviorMannequin or BEBehaviorDisplay) == true) return;
        }

        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null) return;

        IInventory inv = byPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (inv == null) return;
        if (GetDressType(slot) == EnumCharacterDressType.Unknown) return;

        if (inv[(int)GetDressType(slot)].TryFlipWith(slot))
        {
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventSubsequent;
        }
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        // Remove the descText from the base result so far, so that we can append it at the very end instead; this keeps all tabular data neatly together
        // (a better solution would be for GetHeldItemInfo to call a GetHeldItemTabularInfo() method to generate all the tabular data, which can be overridden, prior to adding the descText, but hey, backward compatibility)

        string descTextToAppend = "";
        string descText = collObj.GetItemDescText();
        if (descText.Length > 1)   // Only do this if the descText has material content
        {
            int descIndex = dsc.ToString().IndexOfOrdinal(descText);
            if (descIndex > 0)
            {
                if (descIndex > 0) descIndex--;   // remove the newline as well
                else descTextToAppend = "\n";
                descTextToAppend += dsc.ToString(descIndex, dsc.Length - descIndex);
                dsc.Remove(descIndex, dsc.Length - descIndex);   // Remove the descText and everything subsequent, we will append it (descTextToAppend) at the end of this method instead
            }
        }

        EnumCharacterDressType dressType = GetDressType(inSlot);
        if ((api as ICoreClientAPI).Settings.Bool["extendedDebugInfo"])
        {
            if (dressType == EnumCharacterDressType.Unknown)
            {
                dsc.AppendLine(Lang.Get("Cloth Category: Unknown"));
            }
            else
            {
                dsc.AppendLine(Lang.Get("Cloth Category: {0}", Lang.Get("clothcategory-" + dressType.ToString().ToLowerInvariant())));
            }
        }

        ProtectionModifiers protectionModifiers = GetProtectionModifiers(inSlot);
        if (protectionModifiers != null)
        {
            if (protectionModifiers.FlatDamageReduction != 0)
            {
                dsc.AppendLine(Lang.Get("Flat damage reduction: {0} hp", protectionModifiers.FlatDamageReduction));
            }

            if (protectionModifiers.RelativeProtection != 0)
            {
                dsc.AppendLine(Lang.Get("Percent protection: {0}%", (int)(100 * protectionModifiers.RelativeProtection)));
            }

            dsc.AppendLine(Lang.Get("Protection tier: {0}", (int)(protectionModifiers.ProtectionTier)));
        }

        StatModifiers statModifiers = GetStatModifiers(inSlot);
        if (statModifiers != null)
        {
            if (protectionModifiers != null) dsc.AppendLine();

            if (statModifiers.healingeffectivness != 0)
            {
                dsc.AppendLine(Lang.Get("Healing effectivness: {0}%", (int)(100 * statModifiers.healingeffectivness)));
            }

            if (statModifiers.hungerrate != 0)
            {
                dsc.AppendLine(Lang.Get("Hunger rate: {1}{0}%", (int)(100 * statModifiers.hungerrate), statModifiers.hungerrate > 0 ? "+" : ""));
            }

            if (statModifiers.rangedWeaponsAcc != 0)
            {
                dsc.AppendLine(Lang.Get("Ranged Weapon Accuracy: {1}{0}%", (int)(100 * statModifiers.rangedWeaponsAcc), statModifiers.rangedWeaponsAcc > 0 ? "+" : ""));
            }

            if (statModifiers.rangedWeaponsSpeed != 0)
            {
                dsc.AppendLine(Lang.Get("Ranged Weapon Charge Time: {1}{0}%", -(int)(100 * statModifiers.rangedWeaponsSpeed), -statModifiers.rangedWeaponsSpeed > 0 ? "+" : ""));
            }

            if (statModifiers.walkSpeed != 0)
            {
                dsc.AppendLine(Lang.Get("Walk speed: {1}{0}%", (int)(100 * statModifiers.walkSpeed), statModifiers.walkSpeed > 0 ? "+" : ""));
            }
        }


        if (protectionModifiers?.HighDamageTierResistant == true)
        {
            dsc.AppendLine("<font color=\"#86aad0\">" + Lang.Get("High damage tier resistant") + "</font> " + Lang.Get("When damaged by a higher tier attack, the loss of protection is only half as much."));
        }

        if (collObj.Variant["category"] == "head")
        {
            var rainProt = collObj.Attributes["rainProtectionPerc"].AsFloat(0);
            if (rainProt > 0)
            {
                dsc.AppendLine(Lang.Get("Protection from rain: {0}%", (int)(rainProt * 100)));
            }
        }

        // Condition: Useless (0-10%)
        // Condition: Heavily Tattered (10-20%)
        // Condition: Slightly Tattered (20-30%)
        // Condition: Heavily Worn (30-40%)
        // Condition: Worn (40-50%)
        // Condition: Good (50-100%)

        // Condition: 0-40%
        // Warmth: +1.5°C


        float maxWarmth = GetMaxWarmth(inSlot);

        if (maxWarmth != 0)
        {
            if (!(inSlot is ItemSlotCreative))
            {
                ensureConditionExists(inSlot);
                float condition = inSlot.Itemstack.Attributes.GetFloat("condition", 1);
                string condStr;

                if (condition > 0.5)
                {
                    condStr = Lang.Get("clothingcondition-good", (int)(condition * 100));
                }
                else if (condition > 0.4)
                {
                    condStr = Lang.Get("clothingcondition-worn", (int)(condition * 100));
                }
                else if (condition > 0.3)
                {
                    condStr = Lang.Get("clothingcondition-heavilyworn", (int)(condition * 100));
                }
                else if (condition > 0.2)
                {
                    condStr = Lang.Get("clothingcondition-tattered", (int)(condition * 100));
                }
                else if (condition > 0.1)
                {
                    condStr = Lang.Get("clothingcondition-heavilytattered", (int)(condition * 100));
                }
                else
                {
                    condStr = Lang.Get("clothingcondition-terrible", (int)(condition * 100));
                }

                dsc.Append(Lang.Get("Condition:") + " ");
                float warmth = GetWarmth(inSlot);

                string color = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, condition * 200)]);

                if (warmth < 0.05)
                {
                    dsc.AppendLine("<font color=\"" + color + "\">" + condStr + "</font>, <font color=\"#ff8484\">" + Lang.Get("+{0:0.#}°C", warmth) + "</font>");
                }
                else
                {
                    dsc.AppendLine("<font color=\"" + color + "\">" + condStr + "</font>, <font color=\"#84ff84\">" + Lang.Get("+{0:0.#}°C", warmth) + "</font>");
                }
            }

            dsc.AppendLine();
            dsc.AppendLine(Lang.Get("clothing-maxwarmth", maxWarmth));
        }

        dsc.Append(descTextToAppend);
    }

    public virtual float GetWarmth(ItemSlot inslot)
    {
        ensureConditionExists(inslot);
        float maxWarmth = GetMaxWarmth(inslot);
        float condition = inslot.Itemstack.Attributes.GetFloat("condition", 1);
        return Math.Min(maxWarmth, condition * 2 * maxWarmth);
    }

    public virtual void ChangeCondition(ItemSlot slot, float changeVal)
    {
        if (changeVal == 0) return;

        ensureConditionExists(slot);
        slot.Itemstack.Attributes.SetFloat("condition", GameMath.Clamp(slot.Itemstack.Attributes.GetFloat("condition", 1) + changeVal, 0, 1));
        slot.MarkDirty();
    }

    public override bool RequiresTransitionableTicking(IWorldAccessor world, ItemStack itemstack, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        return !itemstack.Attributes.HasAttribute("condition");
    }

    protected virtual void ensureConditionExists(ItemSlot slot, bool markdirty = true)
    {
        // Prevent derp in the handbook
        if (slot is DummySlot) return;

        if (!slot.Itemstack.Attributes.HasAttribute("condition") && api.Side == EnumAppSide.Server)
        {
            if (GetMaxWarmth(slot) != 0)
            {
                if (slot is ItemSlotTrade)
                {
                    slot.Itemstack.Attributes.SetFloat("condition", (float)api.World.Rand.NextDouble() * 0.25f + 0.75f);
                }
                else
                {
                    slot.Itemstack.Attributes.SetFloat("condition", (float)api.World.Rand.NextDouble() * 0.4f);
                }

                if (markdirty) slot.MarkDirty();
            }
        }
    }

    public override void OnCreatedByCrafting(ItemSlot[] inSlots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling)
    {
        base.OnCreatedByCrafting(inSlots, outputSlot, byRecipe, ref bhHandling);

        // Prevent derp in the handbook
        if (outputSlot is DummySlot) return;

        ensureConditionExists(outputSlot);
        outputSlot.Itemstack.Attributes.SetFloat("condition", 1);

        if (byRecipe.Name.Path.Contains("repair"))
        {
            CalculateRepairValue(inSlots, outputSlot, out float repairValue, out int matCostPerMatType);

            int curDur = outputSlot.Itemstack.Collectible.GetRemainingDurability(outputSlot.Itemstack);
            int maxDur = collObj.GetMaxDurability(outputSlot.Itemstack);

            outputSlot.Itemstack.Collectible.SetDurability(outputSlot.Itemstack, Math.Min(maxDur, (int)(curDur + maxDur * repairValue)));
            bhHandling = EnumHandling.Handled;
        }
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] inSlots, ItemSlot outputSlot, IRecipeBase recipe, ref EnumHandling bhHandling)
    {
        // Consume as much materials in the input grid as needed
        if (recipe.Name.Path.Contains("repair"))
        {
            CalculateRepairValue(inSlots, outputSlot, out float repairValue, out int matCostPerMatType);

            foreach (var islot in inSlots)
            {
                if (islot.Empty) continue;

                if (islot.Itemstack.Collectible == collObj) { islot.Itemstack = null; continue; }

                islot.TakeOut(matCostPerMatType);
            }

            bhHandling = EnumHandling.PreventSubsequent;
            return true;
        }

        return false;
    }

    public virtual void CalculateRepairValue(ItemSlot[] inSlots, ItemSlot outputSlot, out float repairValue, out int matCostPerMatType)
    {
        var origMatCount = GetOrigMatCount(inSlots, outputSlot);
        var armorSlot = inSlots.FirstOrDefault(slot => slot.Itemstack?.Collectible.GetCollectibleInterface<IWearable>() != null);
        int curDur = outputSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack);
        int maxDur = collObj.GetMaxDurability(outputSlot.Itemstack);

        // How much 1x mat repairs in %
        float repairValuePerItem = 2f / origMatCount;
        // How much the mat repairs in durability
        float repairDurabilityPerItem = repairValuePerItem * maxDur;
        // Divide missing durability by repair per item = items needed for full repair
        int fullRepairMatCount = (int)Math.Max(1, Math.Round((maxDur - curDur) / repairDurabilityPerItem));
        // Limit repair value to smallest stack size of all repair mats
        var minMatStackSize = GetInputRepairCount(inSlots);
        // Divide the cost amongst all mats
        var matTypeCount = GetRepairMatTypeCount(inSlots);

        var availableRepairMatCount = Math.Min(fullRepairMatCount, minMatStackSize * matTypeCount);
        matCostPerMatType = Math.Min(fullRepairMatCount, minMatStackSize);

        // Repairing costs half as many materials as newly creating it
        repairValue = (float)availableRepairMatCount / origMatCount * 2;
    }

    protected virtual int GetRepairMatTypeCount(ItemSlot[] slots)
    {
        List<ItemStack> stackTypes = new List<ItemStack>();
        foreach (var slot in slots)
        {
            if (slot.Empty) continue;
            bool found = false;
            if (slot.Itemstack.Collectible.GetCollectibleInterface<IWearable>() != null) continue;

            foreach (var stack in stackTypes)
            {
                if (slot.Itemstack.Satisfies(stack))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                stackTypes.Add(slot.Itemstack);
            }
        }

        return stackTypes.Count;
    }


    public virtual int GetInputRepairCount(ItemSlot[] inputSlots)
    {
        API.Datastructures.OrderedDictionary<int, int> matcounts = new();
        foreach (var slot in inputSlots)
        {
            if (slot.Empty || slot.Itemstack.Collectible.GetCollectibleInterface<IWearable>() != null) continue;
            var hash = slot.Itemstack.GetHashCode();
            matcounts.TryGetValue(hash, out int cnt);
            matcounts[hash] = cnt + slot.StackSize;
        }
        return matcounts.Values.Min();
    }

    public virtual int GetOrigMatCount(ItemSlot[] inputSlots, ItemSlot outputSlot)
    {
        var stack = outputSlot.Itemstack;
        var matStack = inputSlots.FirstOrDefault(slot => !slot.Empty && slot.Itemstack.Collectible != collObj).Itemstack;

        var origMatCount = 0;

        foreach (var recipe in api.World.GridRecipes)
        {
            if ((recipe.RecipeOutput.ResolvedItemStack?.Satisfies(stack) ?? false) && !recipe.Name.Path.Contains("repair"))
            {
                foreach (var ingred in recipe.ResolvedIngredients)
                {
                    if (ingred == null) continue;

                    if (ingred.RecipeAttributes?["repairMat"].Exists == true)
                    {
                        var jstack = ingred.RecipeAttributes["repairMat"].AsObject<JsonItemStack>();
                        jstack.Resolve(api.World, string.Format("recipe '{0}' repair mat", recipe.Name));
                        if (jstack.ResolvedItemstack != null)
                        {
                            origMatCount += jstack.ResolvedItemstack.StackSize;
                        }
                    }
                    else
                    {
                        origMatCount += ingred.Quantity;
                    }
                }

                break;
            }
        }

        return origMatCount;
    }

    public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot, ref EnumHandling handling)
    {
        ensureConditionExists(inslot);

        handling = EnumHandling.PassThrough;
        return null;
    }

    public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type, ref EnumHandling handling)
    {
        // Otherwise recipes disappear in the handbook
        if (type != EnumTransitionType.Perish)
        {
            ensureConditionExists(inslot);
        }

        handling = EnumHandling.PassThrough;
        return null;
    }

    public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling)
    {
        if (collObj.Variant["construction"] == "improvised")
        {
            return;
        }

        float amountf = amount;

        EnumCharacterDressType dressType = GetDressType(itemslot);
        if (byEntity is EntityPlayer && (dressType == EnumCharacterDressType.ArmorHead || dressType == EnumCharacterDressType.ArmorBody || dressType == EnumCharacterDressType.ArmorLegs))
        {
            amountf *= byEntity.Stats.GetBlended("armorDurabilityLoss");
        }

        amount = GameMath.RoundRandom(world.Rand, amountf);

        int leftDurability = itemslot.Itemstack.Collectible.GetRemainingDurability(itemslot.Itemstack);

        if (leftDurability > 0 && leftDurability - amount < 0)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.Pos.X, byEntity.Pos.InternalY, byEntity.Pos.Z, (byEntity as EntityPlayer)?.Player);
        }

        itemslot.Itemstack.Collectible.SetDurability(itemslot.Itemstack, Math.Max(0, leftDurability - amount));
        itemslot.MarkDirty();
        bhHandling = EnumHandling.Handled;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        handling = EnumHandling.Handled;
        return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-dress",
                    MouseButton = EnumMouseButton.Right,
                }
            };
    }

    public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority, ref EnumHandling handling)
    {
        if (priority == EnumMergePriority.DirectMerge)
        {
            if (GetMaxWarmth(new DummySlot(sinkStack)) == 0)
            {
                handling = EnumHandling.PassThrough;
                return 0;
            }

            float repstr = sourceStack?.ItemAttributes?["clothingRepairStrength"].AsFloat(0) ?? 0;
            if (repstr > 0)
            {
                if (sinkStack.Attributes.GetFloat("condition") < 1)
                {
                    handling = EnumHandling.PreventDefault;
                    return 1;
                }

                handling = EnumHandling.PreventDefault;
                return 0;
            }
        }

        handling = EnumHandling.PassThrough;
        return 0;
    }

    public override void TryMergeStacks(ItemStackMergeOperation op, ref EnumHandling handling)
    {
        if (op.CurrentPriority == EnumMergePriority.DirectMerge)
        {
            float repstr = op.SourceSlot.Itemstack.ItemAttributes?["clothingRepairStrength"].AsFloat(0) ?? 0;

            if (repstr > 0 && op.SinkSlot.Itemstack.Attributes.GetFloat("condition") < 1)
            {
                ChangeCondition(op.SinkSlot, repstr);
                op.MovedQuantity = 1;
                op.SourceSlot.TakeOut(1);
                handling = EnumHandling.PreventDefault;
                return;
            }
        }

        handling = EnumHandling.PassThrough;
    }

    public virtual bool IsArmorType(ItemSlot slot)
    {
        EnumCharacterDressType dressType = GetDressType(slot);
        return dressType == EnumCharacterDressType.ArmorBody || dressType == EnumCharacterDressType.ArmorHead || dressType == EnumCharacterDressType.ArmorLegs;
    }

    public virtual EnumCharacterDressType GetDressType(ItemSlot slot)
    {
        return DressType;
    }

    public virtual StatModifiers GetStatModifiers(ItemSlot slot)
    {
        return StatModifers;
    }

    public virtual ProtectionModifiers GetProtectionModifiers(ItemSlot slot)
    {
        return ProtectionModifiers;
    }

    public virtual AssetLocation[] GetFootStepSounds(ItemSlot slot)
    {
        return FootStepSounds;
    }

    public virtual float GetMaxWarmth(ItemSlot inslot)
    {
        return inslot.Itemstack.ItemAttributes?["warmth"].AsFloat(0) ?? 0;
    }
}
