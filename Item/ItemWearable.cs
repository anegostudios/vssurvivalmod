using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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

    public class ItemWearable : Item
    {
        public StatModifiers StatModifers;
        public ProtectionModifiers ProtectionModifiers;
        public AssetLocation[] FootStepSounds;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

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
                    api.World.Logger.Error("Failed loading statModifiers for item/block {0}. Will ignore. Exception: {1}", Code, e);
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
                    api.World.Logger.Error("Failed loading defaultProtLoss for item/block {0}. Will ignore. Exception: {1}", Code, e);
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
                    api.World.Logger.Error("Failed loading protectionModifiers for item/block {0}. Will ignore. Exception: {1}", Code, e);
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

            Dictionary<string, MeshRef> armorMeshrefs = ObjectCacheUtil.TryGet<Dictionary<string, MeshRef>>(api, "armorMeshRefs");
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

            Dictionary<string, MeshRef> armorMeshrefs = ObjectCacheUtil.GetOrCreate(capi, "armorMeshRefs", () => new Dictionary<string, MeshRef>());
            string key = "armorModelRef-" + itemstack.Collectible.Code.ToString();

            if (!armorMeshrefs.TryGetValue(key, out renderinfo.ModelRef))
            {
                renderinfo.ModelRef = armorMeshrefs[key] = genMeshRef(capi, itemstack, renderinfo);
            }
        }


        private MeshRef genMeshRef(ICoreClientAPI capi, ItemStack itemstack, ItemRenderInfo renderinfo)
        {
            MeshRef meshref = renderinfo.ModelRef;
            JsonObject attrObj = itemstack.Collectible.Attributes;
            EntityProperties props = capi.World.GetEntityType(new AssetLocation("player"));
            Shape entityShape = props.Client.LoadedShape;
            AssetLocation shapePathForLogging = props.Client.Shape.Base;

            Shape newShape = new Shape()
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
                return meshref;
            }

            AssetLocation shapePath = compArmorShape.Base.CopyWithPath("shapes/" + compArmorShape.Base.Path + ".json");

            IAsset asset = capi.Assets.TryGet(shapePath);

            if (asset == null)
            {
                capi.World.Logger.Warning("Entity armor shape {0} defined in {1} {2} not found, was supposed to be at {3}. Armor piece will be invisible.", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, shapePath);
                return meshref;
            }

            Shape armorShape;

            try
            {
                armorShape = asset.ToObject<Shape>();
            }
            catch (Exception e)
            {
                capi.World.Logger.Warning("Exception thrown when trying to load entity armor shape {0} defined in {1} {2}. Armor piece will be invisible. Exception: {3}", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, e);
                return meshref;
            }

            newShape.Textures = armorShape.Textures;


            foreach (var val in armorShape.Elements)
            {
                ShapeElement elem;

                if (val.StepParentName != null)
                {
                    elem = newShape.GetElementByName(val.StepParentName, StringComparison.InvariantCultureIgnoreCase);
                    if (elem == null)
                    {
                        capi.World.Logger.Warning("Entity armor shape {0} defined in {1} {2} requires step parent element with name {3}, but no such element was found in shape {3}. Will not be visible.", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, val.StepParentName, shapePathForLogging);
                        continue;
                    }
                }
                else
                {
                    capi.World.Logger.Warning("Entity armor shape element {0} in shape {1} defined in {2} {3} did not define a step parent element. Will not be visible.", val.Name, compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code);
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


            MeshData meshdata;
            ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item);

            capi.Tesselator.TesselateShapeWithJointIds("entity", newShape, out meshdata, texSource, new Vec3f()); //, compositeShape.QuantityElements, compositeShape.SelectiveElements
            meshdata.Rgba2 = null;
            return capi.Render.UploadMesh(meshdata);
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (byEntity.Controls.Sneak) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            EnumCharacterDressType dresstype;
            string strdress = slot.Itemstack.ItemAttributes["clothescategory"].AsString();
            if (!Enum.TryParse(strdress, true, out dresstype)) return;

            IInventory inv = byPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return;

            if (inv[(int)dresstype].TryFlipWith(slot))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            EnumCharacterDressType dresstype;
            string strdress = inSlot.Itemstack.ItemAttributes["clothescategory"].AsString();
            if (!Enum.TryParse(strdress, true, out dresstype))
            {
                dsc.AppendLine(Lang.Get("Cloth Category: Unknown"));
            } else
            {
                dsc.AppendLine(Lang.Get("Cloth Category: {0}", Lang.Get("clothcategory-" + inSlot.Itemstack.ItemAttributes["clothescategory"].AsString())));
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
                    dsc.AppendLine(Lang.Get("Ranged Weapon Charge Time: {1}{0}%", (int)(100 * StatModifers.rangedWeaponsSpeed), StatModifers.rangedWeaponsSpeed > 0 ? "+" : ""));
                }

                if (StatModifers.walkSpeed != 0)
                {
                    dsc.AppendLine(Lang.Get("Walk speed: {1}{0}%", (int)(100 * StatModifers.walkSpeed), StatModifers.walkSpeed > 0 ? "+" : ""));
                }

                if (ProtectionModifiers?.HighDamageTierResistant == true)
                {
                    dsc.AppendLine(Lang.Get("<font color=\"#86aad0\">High damage tier resistant.</font> When damaged by a higher tier attack, the loss of protection is only half as much."));
                }
            }


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
    }
}
