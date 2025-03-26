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

namespace Vintagestory.GameContent
{
    public class StatModifiers
    {
        public float rangedWeaponsSpeed = 0f;
        public float rangedWeaponsAcc = 0f;
        public float walkSpeed = 0f;
        public float healingeffectivness = 0f;
        public float hungerrate = 0f;
        public bool canEat = true;
    }

    public class ProtectionModifiers
    {
        public float RelativeProtection;
        public float[] PerTierRelativeProtectionLoss;
        public float FlatDamageReduction;
        public float[] PerTierFlatDamageReductionLoss;
        public int ProtectionTier;
        public bool HighDamageTierResistant;
    }

    public class ItemWearable : ItemWearableAttachment
    {
        public StatModifiers StatModifers;
        public ProtectionModifiers ProtectionModifiers;
        public AssetLocation[] FootStepSounds;

        public EnumCharacterDressType DressType { get; private set; }

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            return "wearableModelRef-" + itemstack.Collectible.Code.ToString();
        }

        public bool IsArmor
        {
            get
            {
                return DressType == EnumCharacterDressType.ArmorBody || DressType == EnumCharacterDressType.ArmorHead || DressType == EnumCharacterDressType.ArmorLegs;
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            string strdress = Attributes["clothescategory"].AsString();
            EnumCharacterDressType dt;
            Enum.TryParse(strdress, true, out dt);
            DressType = dt;


            JsonObject jsonObj = Attributes?["footStepSound"];
            if (jsonObj?.Exists == true)
            {
                string soundloc = jsonObj.AsString(null);
                if (soundloc != null)
                {
                    AssetLocation loc = AssetLocation.Create(soundloc, Code.Domain).WithPathPrefixOnce("sounds/");

                    if (soundloc.EndsWith('*'))
                    {
                        loc.Path = loc.Path.TrimEnd('*');
                        FootStepSounds = api.Assets.GetLocations(loc.Path, loc.Domain).ToArray();
                    } else
                    {
                        FootStepSounds = new AssetLocation[] { loc };
                    }                    
                }
            }

            jsonObj = Attributes?["statModifiers"];
            if (jsonObj?.Exists == true)
            {
                try
                {
                    StatModifers = jsonObj.AsObject<StatModifiers>();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading statModifiers for item/block {0}. Will ignore.", Code);
                    api.World.Logger.Error(e);
                    StatModifers = null;
                }
            }

            ProtectionModifiers defMods = null;
            jsonObj = Attributes?["defaultProtLoss"];
            if (jsonObj?.Exists == true)
            {
                try
                {
                    defMods = jsonObj.AsObject<ProtectionModifiers>();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading defaultProtLoss for item/block {0}. Will ignore.", Code);
                    api.World.Logger.Error(e);
                }
            }

            jsonObj = Attributes?["protectionModifiers"];
            if (jsonObj?.Exists == true)
            {
                try
                {
                    ProtectionModifiers = jsonObj.AsObject<ProtectionModifiers>();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading protectionModifiers for item/block {0}. Will ignore.", Code);
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

        public override void OnHandbookRecipeRender(ICoreClientAPI capi, GridRecipe recipe, ItemSlot dummyslot, double x, double y, double z, double size)
        {
            bool isRepairRecipe = recipe.Name.Path.Contains("repair");
            int prevDura = 0;
            if (isRepairRecipe)
            {
                prevDura = dummyslot.Itemstack.Collectible.GetRemainingDurability(dummyslot.Itemstack);
                dummyslot.Itemstack.Attributes.SetInt("durability", 0);
            }
            
            base.OnHandbookRecipeRender(capi, recipe, dummyslot, x, y, z, size);

            if (isRepairRecipe)
            {
                dummyslot.Itemstack.Attributes.SetInt("durability", prevDura);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IInventory inv = byPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return;
            if (DressType == EnumCharacterDressType.Unknown) return;

            if (inv[(int)DressType].TryFlipWith(slot))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            // Remove the descText from the base result so far, so that we can append it at the very end instead; this keeps all tabular data neatly together
            // (a better solution would be for GetHeldItemInfo to call a GetHeldItemTabularInfo() method to generate all the tabular data, which can be overridden, prior to adding the descText, but hey, backward compatibility)

            string descTextToAppend = "";
            string descText = base.GetItemDescText();
            if (descText.Length > 1)   // Only do this if the descText has material content
            {
                int descIndex = dsc.ToString().IndexOfOrdinal(descText);
                if (descIndex >= 0)
                {
                    if (descIndex > 0) descIndex--;   // remove the newline as well
                    else descTextToAppend = "\n";
                    descTextToAppend += dsc.ToString(descIndex, dsc.Length - descIndex);
                    dsc.Remove(descIndex, dsc.Length - descIndex);   // Remove the descText and everything subsequent, we will append it (descTextToAppend) at the end of this method instead
                }
            }

            if ((api as ICoreClientAPI).Settings.Bool["extendedDebugInfo"])
            {
                if (DressType == EnumCharacterDressType.Unknown)
                {
                    dsc.AppendLine(Lang.Get("Cloth Category: Unknown"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Cloth Category: {0}", Lang.Get("clothcategory-" + inSlot.Itemstack.ItemAttributes["clothescategory"].AsString())));
                }
            }


            if (ProtectionModifiers != null)
            {
                if (ProtectionModifiers.FlatDamageReduction != 0)
                {
                    dsc.AppendLine(Lang.Get("Flat damage reduction: {0} hp", ProtectionModifiers.FlatDamageReduction));
                }

                if (ProtectionModifiers.RelativeProtection != 0)
                {
                    dsc.AppendLine(Lang.Get("Percent protection: {0}%", (int)(100 * ProtectionModifiers.RelativeProtection)));
                }

                dsc.AppendLine(Lang.Get("Protection tier: {0}", (int)(ProtectionModifiers.ProtectionTier)));
            }

            if (StatModifers != null)
            {
                if (ProtectionModifiers != null) dsc.AppendLine();

                if (StatModifers.healingeffectivness != 0)
                {
                    dsc.AppendLine(Lang.Get("Healing effectivness: {0}%", (int)(100*StatModifers.healingeffectivness)));
                }

                if (StatModifers.hungerrate != 0)
                {
                    dsc.AppendLine(Lang.Get("Hunger rate: {1}{0}%", (int)(100 * StatModifers.hungerrate), StatModifers.hungerrate  > 0 ? "+" : ""));
                }

                if (StatModifers.rangedWeaponsAcc != 0)
                {
                    dsc.AppendLine(Lang.Get("Ranged Weapon Accuracy: {1}{0}%", (int)(100 * StatModifers.rangedWeaponsAcc), StatModifers.rangedWeaponsAcc > 0 ? "+" : ""));
                }

                if (StatModifers.rangedWeaponsSpeed != 0)
                {
                    dsc.AppendLine(Lang.Get("Ranged Weapon Charge Time: {1}{0}%", -(int)(100 * StatModifers.rangedWeaponsSpeed), -StatModifers.rangedWeaponsSpeed > 0 ? "+" : ""));
                }

                if (StatModifers.walkSpeed != 0)
                {
                    dsc.AppendLine(Lang.Get("Walk speed: {1}{0}%", (int)(100 * StatModifers.walkSpeed), StatModifers.walkSpeed > 0 ? "+" : ""));
                }
            }


            if (ProtectionModifiers?.HighDamageTierResistant == true)
            {
                dsc.AppendLine("<font color=\"#86aad0\">" + Lang.Get("High damage tier resistant") + "</font> " + Lang.Get("When damaged by a higher tier attack, the loss of protection is only half as much."));
            }

            // Condition: Useless (0-10%)
            // Condition: Heavily Tattered (10-20%)
            // Condition: Slightly Tattered (20-30%)
            // Condition: Heavily Worn (30-40%)
            // Condition: Worn (40-50%)
            // Condition: Good (50-100%)

            // Condition: 0-40%
            // Warmth: +1.5°C


            if (inSlot.Itemstack.ItemAttributes?["warmth"].Exists == true && inSlot.Itemstack.ItemAttributes?["warmth"].AsFloat() != 0)
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

                float maxWarmth = inSlot.Itemstack.ItemAttributes?["warmth"].AsFloat(0) ?? 0;
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("clothing-maxwarmth", maxWarmth));
            }

            dsc.Append(descTextToAppend);
        }

        public float GetWarmth(ItemSlot inslot)
        {
            ensureConditionExists(inslot);
            float maxWarmth = inslot.Itemstack.ItemAttributes?["warmth"].AsFloat(0) ?? 0;
            float condition = inslot.Itemstack.Attributes.GetFloat("condition", 1);
            return Math.Min(maxWarmth, condition * 2 * maxWarmth); 
        }

        public void ChangeCondition(ItemSlot slot, float changeVal)
        {
            if (changeVal == 0) return;

            ensureConditionExists(slot);
            slot.Itemstack.Attributes.SetFloat("condition", GameMath.Clamp(slot.Itemstack.Attributes.GetFloat("condition", 1) + changeVal, 0, 1));
            slot.MarkDirty();
        }

        public override bool RequiresTransitionableTicking(IWorldAccessor world, ItemStack itemstack)
        {
            return !itemstack.Attributes.HasAttribute("condition");
        }

        private void ensureConditionExists(ItemSlot slot, bool markdirty=true)
        {
            // Prevent derp in the handbook
            if (slot is DummySlot) return;

            if (!slot.Itemstack.Attributes.HasAttribute("condition") && api.Side == EnumAppSide.Server)
            {
                if (slot.Itemstack.ItemAttributes?["warmth"].Exists == true && slot.Itemstack.ItemAttributes?["warmth"].AsFloat() != 0)
                {
                    if (slot is ItemSlotTrade)
                    {
                        slot.Itemstack.Attributes.SetFloat("condition", (float)api.World.Rand.NextDouble() * 0.25f + 0.75f);
                    } else
                    {
                        slot.Itemstack.Attributes.SetFloat("condition", (float)api.World.Rand.NextDouble() * 0.4f);
                    }
                    
                    if (markdirty) slot.MarkDirty();
                }
            }
        }


        public override void OnCreatedByCrafting(ItemSlot[] inSlots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            base.OnCreatedByCrafting(inSlots, outputSlot, byRecipe);

            // Prevent derp in the handbook
            if (outputSlot is DummySlot) return;

            ensureConditionExists(outputSlot);
            outputSlot.Itemstack.Attributes.SetFloat("condition", 1);

            if (byRecipe.Name.Path.Contains("repair"))
            {
                CalculateRepairValue(inSlots, outputSlot, out float repairValue, out int matCostPerMatType);

                int curDur = outputSlot.Itemstack.Collectible.GetRemainingDurability(outputSlot.Itemstack);
                int maxDur = GetMaxDurability(outputSlot.Itemstack);

                outputSlot.Itemstack.Attributes.SetInt("durability", Math.Min(maxDur, (int)(curDur + maxDur * repairValue)));
            }
        }

        public override bool ConsumeCraftingIngredients(ItemSlot[] inSlots, ItemSlot outputSlot, GridRecipe recipe)
        {
            // Consume as much materials in the input grid as needed
            if (recipe.Name.Path.Contains("repair"))
            {
                CalculateRepairValue(inSlots, outputSlot, out float repairValue, out int matCostPerMatType);

                foreach (var islot in inSlots)
                {
                    if (islot.Empty) continue;

                    if (islot.Itemstack.Collectible == this) { islot.Itemstack = null; continue; }

                    islot.TakeOut(matCostPerMatType);
                }

                return true;
            }

            return false;
        }


        public void CalculateRepairValue(ItemSlot[] inSlots, ItemSlot outputSlot, out float repairValue, out int matCostPerMatType)
        {
            var origMatCount = GetOrigMatCount(inSlots, outputSlot);
            var armorSlot = inSlots.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemWearable);
            int curDur = outputSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack);
            int maxDur = GetMaxDurability(outputSlot.Itemstack);

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


        private int GetRepairMatTypeCount(ItemSlot[] slots)
        {
            List<ItemStack> stackTypes = new List<ItemStack>();
            foreach (var slot in slots)
            {
                if (slot.Empty) continue;
                bool found = false;
                if (slot.Itemstack.Collectible is ItemWearable) continue;

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

        public int GetInputRepairCount(ItemSlot[] inputSlots)
        {
            OrderedDictionary<int, int> matcounts = new OrderedDictionary<int, int>();
            foreach (var slot in inputSlots)
            {
                if (slot.Empty || slot.Itemstack.Collectible is ItemWearable) continue;
                var hash = slot.Itemstack.GetHashCode();
                int cnt = 0;
                matcounts.TryGetValue(hash, out cnt);
                matcounts[hash] = cnt + slot.StackSize;
            }
            return matcounts.Values.Min();
        }

        public int GetOrigMatCount(ItemSlot[] inputSlots, ItemSlot outputSlot)
        {
            var stack = outputSlot.Itemstack;
            var matStack = inputSlots.FirstOrDefault(slot => !slot.Empty && slot.Itemstack.Collectible != this).Itemstack;

            var origMatCount = 0;

            foreach (var recipe in api.World.GridRecipes)
            {
                if ((recipe.Output.ResolvedItemstack?.Satisfies(stack) ?? false) && !recipe.Name.Path.Contains("repair"))
                {
                    foreach (var ingred in recipe.resolvedIngredients)
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
                        } else
                        {
                            origMatCount += ingred.Quantity;
                        }
                    }

                    break;
                }
            }

            return origMatCount;
        }

        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ensureConditionExists(inslot);

            return base.UpdateAndGetTransitionStates(world, inslot);
        }

        public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            // Otherwise recipes disappear in the handbook
            if (type != EnumTransitionType.Perish)
            {
                ensureConditionExists(inslot);
            }

            return base.UpdateAndGetTransitionState(world, inslot, type);
        }

        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1)
        {
            if (Variant["construction"] == "improvised")
            {
                base.DamageItem(world, byEntity, itemslot, amount);
                return;
            }

            float amountf = amount;

            if (byEntity is EntityPlayer && (DressType == EnumCharacterDressType.ArmorHead || DressType == EnumCharacterDressType.ArmorBody || DressType == EnumCharacterDressType.ArmorLegs))
            {
                amountf *= byEntity.Stats.GetBlended("armorDurabilityLoss");
            }

            amount = GameMath.RoundRandom(world.Rand, amountf);

            int leftDurability = itemslot.Itemstack.Attributes.GetInt("durability", GetMaxDurability(itemslot.Itemstack));

            if (leftDurability > 0 && leftDurability - amount < 0)
            {
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.SidedPos.X, byEntity.SidedPos.InternalY, byEntity.SidedPos.Z, (byEntity as EntityPlayer)?.Player);
            }

            itemslot.Itemstack.Attributes.SetInt("durability", Math.Max(0, leftDurability - amount));
            itemslot.MarkDirty();
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-dress",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }


        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge)
            {
                if (sinkStack.ItemAttributes?["warmth"].Exists != true || sinkStack.ItemAttributes?["warmth"].AsFloat() == 0) return base.GetMergableQuantity(sinkStack, sourceStack, priority);

                float repstr = sourceStack?.ItemAttributes?["clothingRepairStrength"].AsFloat(0) ?? 0;
                if (repstr > 0)
                {
                    if (sinkStack.Attributes.GetFloat("condition") < 1) return 1;
                    return 0;
                }
            }
            

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                float repstr = op.SourceSlot.Itemstack.ItemAttributes?["clothingRepairStrength"].AsFloat(0) ?? 0;

                if (repstr > 0 && op.SinkSlot.Itemstack.Attributes.GetFloat("condition") < 1)
                {
                    ChangeCondition(op.SinkSlot, repstr);
                    op.MovedQuantity = 1;
                    op.SourceSlot.TakeOut(1);
                    return;
                }
            }

            base.TryMergeStacks(op);
        }
    }
}
