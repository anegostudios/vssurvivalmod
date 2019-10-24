using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemDress : Item
    {

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (itemstack.Collectible.Attributes?["armorShape"].Exists != true) return;

            MeshRef meshref = renderinfo.ModelRef;

            renderinfo.ModelRef = ObjectCacheUtil.GetOrCreate(capi, "armorModelRef-" + itemstack.Collectible.Code.ToString(), () =>
            {
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

                CompositeShape compArmorShape = itemstack.Collectible.Attributes["armorShape"].AsObject<CompositeShape>(null, itemstack.Collectible.Code.Domain);
                AssetLocation shapePath = shapePath = compArmorShape.Base.CopyWithPath("shapes/" + compArmorShape.Base.Path + ".json");

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


                MeshData meshdata = new MeshData();
                ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item);

                capi.Tesselator.TesselateShapeWithJointIds("entity", newShape, out meshdata, texSource, new Vec3f()); //, compositeShape.QuantityElements, compositeShape.SelectiveElements
                return capi.Render.UploadMesh(meshdata);
            });
        }



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
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
