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

    public class ItemWearable : Item, IContainedMeshSource, ITexPositionSource
    {
        public StatModifiers StatModifers;
        public ProtectionModifiers ProtectionModifiers;
        public AssetLocation[] FootStepSounds;

        public EnumCharacterDressType DressType { get; private set; }

        public bool IsArmor
        {
            get
            {
                return DressType == EnumCharacterDressType.ArmorBody || DressType == EnumCharacterDressType.ArmorHead || DressType == EnumCharacterDressType.ArmorLegs;
            }
        }


        #region For ground storable mesh
        ITextureAtlasAPI curAtlas;
        Shape nowTesselatingShape;

        public Size2i AtlasSize => curAtlas.Size;


        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = null;
                CompositeTexture tex;

                // Prio 1: Get from collectible textures
                if (Textures.TryGetValue(textureCode, out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 2: Get from collectible textures, use "all" code
                if (texturePath == null && Textures.TryGetValue("all", out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 3: Get from currently tesselating shape
                if (texturePath == null)
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
                }

                // Prio 4: The code is the path
                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                return getOrCreateTexPos(texturePath);
            }
        }


        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            var capi = api as ICoreClientAPI;

            curAtlas.GetOrInsertTexture(texturePath, out _, out var texpos, () =>
            {
                IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    return texAsset.ToBitmap(capi);
                }

                capi.World.Logger.Warning("Item {0} defined texture {1}, not no such texture found.", Code, texturePath);
                return null;
            }, 0.1f);

            return texpos;
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            var capi = api as ICoreClientAPI;
            if (targetAtlas == capi.ItemTextureAtlas)
            {
                ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item);
                return genMesh(capi, itemstack, texSource);
            }


            curAtlas = targetAtlas;
            MeshData mesh = genMesh(api as ICoreClientAPI, itemstack, this);
            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.OpaqueNoCull);
            return mesh;
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            return "armorModelRef-" + itemstack.Collectible.Code.ToString();
        }
        #endregion


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            string strdress = Attributes["clothescategory"].AsString();
            EnumCharacterDressType dt = EnumCharacterDressType.Unknown;
            Enum.TryParse(strdress, true, out dt);
            DressType = dt;


            JsonObject jsonObj = Attributes?["footStepSound"];
            if (jsonObj?.Exists == true)
            {
                string soundloc = jsonObj.AsString(null);
                if (soundloc != null)
                {
                    AssetLocation loc = AssetLocation.Create(soundloc, Code.Domain).WithPathPrefixOnce("sounds/");

                    if (soundloc.EndsWith("*"))
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



        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            JsonObject attrObj = itemstack.Collectible.Attributes;
            if (attrObj?["wearableAttachment"].Exists != true) return;

            Dictionary<string, MultiTextureMeshRef> armorMeshrefs = ObjectCacheUtil.GetOrCreate(capi, "armorMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());
            string key = "armorModelRef-" + itemstack.Collectible.Code.ToString();

            if (!armorMeshrefs.TryGetValue(key, out renderinfo.ModelRef))
            {
                ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item);
                var mesh = genMesh(capi, itemstack, texSource);
                renderinfo.ModelRef = armorMeshrefs[key] = mesh == null ? renderinfo.ModelRef : capi.Render.UploadMultiTextureMesh(mesh);

                
            }

            if (Attributes["visibleDamageEffect"].AsBool())
            {
                renderinfo.DamageEffect = Math.Max(0, 1 - (float)GetRemainingDurability(itemstack) / GetMaxDurability(itemstack) * 1.1f);
            }
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


        private MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            JsonObject attrObj = itemstack.Collectible.Attributes;
            EntityProperties props = capi.World.GetEntityType(new AssetLocation("player"));
            Shape entityShape = props.Client.LoadedShape;
            AssetLocation shapePathForLogging = props.Client.Shape.Base;
            Shape newShape;

            bool iswearableAttachment = attrObj.IsTrue("wearableAttachment");
            if (!iswearableAttachment)
            {
                // No need to step parent anything if its just a texture on the seraph
                newShape = entityShape;
            }
            else
            {
                newShape = new Shape()
                {
                    Elements = entityShape.CloneElements(),
                    Animations = entityShape.Animations,
                    AnimationsByCrc32 = entityShape.AnimationsByCrc32,
                    AttachmentPointsByCode = entityShape.AttachmentPointsByCode,
                    JointsById = entityShape.JointsById,
                    TextureWidth = entityShape.TextureWidth,
                    TextureHeight = entityShape.TextureHeight,
                    Textures = null,
                };

                CompositeShape compArmorShape = !attrObj["attachShape"].Exists ? (itemstack.Class == EnumItemClass.Item ? itemstack.Item.Shape : itemstack.Block.Shape) : attrObj["attachShape"].AsObject<CompositeShape>(null, itemstack.Collectible.Code.Domain);

                if (compArmorShape == null)
                {
                    capi.World.Logger.Warning("Entity armor {0} {1} does not define a shape through either the shape property or the attachShape Attribute. Armor pieces will be invisible.", itemstack.Class, itemstack.Collectible.Code);
                    return null;
                }

                AssetLocation shapePath = compArmorShape.Base.CopyWithPath("shapes/" + compArmorShape.Base.Path + ".json");

                Shape armorShape = API.Common.Shape.TryGet(capi, shapePath);
                if (armorShape == null)
                {
                    capi.World.Logger.Warning("Entity wearable shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Armor piece will be invisible.", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, shapePath);
                    return null;
                }

                newShape.Textures = armorShape.Textures;

                if (armorShape.Textures.Count > 0 && armorShape.TextureSizes.Count == 0)
                {
                    foreach (var val in armorShape.Textures)
                    {
                        armorShape.TextureSizes.Add(val.Key, new int[] { armorShape.TextureWidth, armorShape.TextureHeight });
                    }
                }

                foreach (var val in armorShape.TextureSizes)
                {
                    newShape.TextureSizes[val.Key] = val.Value;
                }

                foreach (var val in armorShape.Elements)
                {
                    ShapeElement elem;

                    if (val.StepParentName != null)
                    {
                        elem = newShape.GetElementByName(val.StepParentName, StringComparison.InvariantCultureIgnoreCase);
                        if (elem == null)
                        {
                            capi.World.Logger.Warning("Entity wearable shape {0} defined in {1} {2} requires step parent element with name {3}, but no such element was found in shape {3}. Will not be visible.", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, val.StepParentName, shapePathForLogging);
                            continue;
                        }
                    }
                    else
                    {
                        capi.World.Logger.Warning("Entity wearable shape element {0} in shape {1} defined in {2} {3} did not define a step parent element. Will not be visible.", val.Name, compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code);
                        continue;
                    }

                    if (elem.Children == null)
                    {
                        elem.Children = new ShapeElement[] { val };
                    }
                    else
                    {
                        elem.Children = elem.Children.Append(val);
                    }
                }
            }



            MeshData meshdata;

            nowTesselatingShape = newShape;

            capi.Tesselator.TesselateShapeWithJointIds("entity", newShape, out meshdata, texSource, new Vec3f());

            nowTesselatingShape = null;

            return meshdata;
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

        private void ensureConditionExists(ItemSlot slot)
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
                    
                    slot.MarkDirty();
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
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.SidedPos.X, byEntity.SidedPos.Y, byEntity.SidedPos.Z, (byEntity as EntityPlayer)?.Player);
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
