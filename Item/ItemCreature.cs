using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System;
using System.Text;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using System.Collections.Generic;
using System.Collections;

namespace Vintagestory.GameContent
{
    public class ItemCreature : Item
    {
        CompositeShape[] stepParentShapes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            stepParentShapes = Attributes?["stepParentShapes"].AsObject<CompositeShape[]>();
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (stepParentShapes != null && stepParentShapes.Length > 0)
            {
                var dict = ObjectCacheUtil.GetOrCreate(capi, "itemcreaturemeshrefs", () => new Dictionary<AssetLocation, MultiTextureMeshRef>());
                if (dict.TryGetValue(this.Code, out var mmeshref))
                {
                    renderinfo.ModelRef = mmeshref;
                } else
                {
                    dict[this.Code] = renderinfo.ModelRef = CreateOverlaidMeshRef(capi, Shape, stepParentShapes);
                }
                
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        private MultiTextureMeshRef CreateOverlaidMeshRef(ICoreClientAPI capi, CompositeShape cshape, CompositeShape[] stepParentShapes)
        {
            var textures = Textures;

            var shape = capi.Assets.TryGet(cshape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"))?.ToObject<Shape>();
            if (shape == null)
            {
                capi.Logger.Error("Entity {0} defines a shape {1}, but no such file found. Will use default shape.", this.Code, cshape.Base);
                return capi.TesselatorManager.GetDefaultItemMeshRef(this);
            }

            foreach (var stepparentshape in stepParentShapes)
            {
                var overlayshape = capi.Assets.TryGet(stepparentshape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"))?.ToObject<Shape>();
                if (overlayshape == null)
                {
                    capi.Logger.Error("Entity {0} defines a shape overlay {1}, but no such file found. Will ignore.", this.Code, stepparentshape.Base);
                    continue;
                }

                string texturePrefixCode = null;

                if (Attributes?["wearableTexturePrefixCode"].Exists == true)
                {
                    texturePrefixCode = Attributes["wearableTexturePrefixCode"].AsString();
                }

                overlayshape.SubclassForStepParenting(texturePrefixCode);

                shape.StepParentShape(overlayshape, stepparentshape.Base.ToShortString(), cshape.Base.ToShortString(), capi.Logger, (texcode, tloc) =>
                {
                    // Item stack textures take precedence over shape textures
                    if (texturePrefixCode == null && textures.ContainsKey(texcode)) return;

                    var cmpt = textures[texturePrefixCode + texcode] = new CompositeTexture(tloc);
                    cmpt.Bake(capi.Assets);
                    capi.ItemTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
                    cmpt.Baked.TextureSubId = textureSubid;
                });
            }

            TesselationMetaData meta = new TesselationMetaData()
            {
                QuantityElements = cshape.QuantityElements,
                SelectiveElements = cshape.SelectiveElements,
                IgnoreElements = cshape.IgnoreElements,
                TexSource = capi.Tesselator.GetTextureSource(this),
                WithJointIds = false,
                WithDamageEffect = false,
                TypeForLogging = "item",
                Rotation = new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ)
            };

            capi.Tesselator.TesselateShape(meta, shape, out var meshdata);

            return capi.Render.UploadMultiTextureMesh(meshdata);
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            var dict = ObjectCacheUtil.TryGet<Dictionary<AssetLocation, MultiTextureMeshRef>>(api, "itemcreaturemeshrefs");
            if (dict != null)
            {
                foreach (var val in dict.Values)
                {
                    val.Dispose();
                }
            }

            base.OnUnloaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }


            AssetLocation location = new AssetLocation(Code.Domain, CodeEndWithoutParts(1));
            EntityProperties type = byEntity.World.GetEntityType(location);
            if (type == null)
            {
                byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                if (api.World.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", string.Format("No such entity loaded - '{0}'.", location));
                }
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = byEntity.Pos.Yaw + GameMath.PI + GameMath.PIHALF;
                entity.ServerPos.Dimension = blockSel.Position.dimension;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                entity.Attributes.SetString("origin", "playerplaced");

                if (Attributes?.IsTrue("setGuardedEntityAttribute") == true)
                {
                    entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
                    if (byEntity is EntityPlayer eplr)
                    {
                        entity.WatchedAttributes.SetString("guardedPlayerUid", eplr.PlayerUID);
                    }
                }

                byEntity.World.SpawnEntity(entity);
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity byEntity, EnumHand hand)
        {
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(Code.Domain, CodeEndWithoutParts(1)));
            if (type == null) return base.GetHeldTpIdleAnimation(activeHotbarSlot, byEntity, hand);

            float size = Math.Max(type.CollisionBoxSize.X, type.CollisionBoxSize.Y);

            if (size > 1) return "holdunderarm";
            return "holdbothhands";
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (FirstCodePart(1) == "butterfly")
            {
                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
                dsc.Insert(0, "<font color=\"#ccc\"><i>");
                dsc.Append("</i></font>");
                dsc.AppendLine(Lang.Get("itemdesc-creature-butterfly-all"));
                return;
            }

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}