using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json.Linq;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemCreatureInventory : Item, ITexPositionSource
    {
        ICoreClientAPI capi;
        public Size2i AtlasSize => capi.ItemTextureAtlas.Size;

        EntityProperties nowTesselatingEntityType;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texPath;
                nowTesselatingEntityType.Client.Textures.TryGetValue(textureCode, out var cTex);
                if (cTex == null)
                {
                    nowTesselatingEntityType.Client.LoadedShape.Textures.TryGetValue(textureCode, out texPath);
                } else
                {
                    texPath = cTex.Base;
                }

                if (texPath != null)
                {
                    capi.ItemTextureAtlas.GetOrInsertTexture(texPath, out _, out var texPos);
                    return texPos;
                }

                return null;
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;

            List<JsonItemStack> stacks = new List<JsonItemStack>();

            foreach (var entitytype in api.World.EntityTypes)
            {
                if (entitytype.Attributes?["inCreativeInventory"].AsBool(true) == false) continue;

                var jstack = new JsonItemStack()
                {
                    Code = this.Code,
                    Type = EnumItemClass.Item,
                    Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + entitytype.Code + "\" }"))
                };

                jstack.Resolve(api.World, "creatureinventory");
                stacks.Add(jstack);
            }

            CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[]{ "general", "items", "creatures" } }
            };
        }

        static Dictionary<EnumItemRenderTarget, string> map = new Dictionary<EnumItemRenderTarget, string>() {
            { EnumItemRenderTarget.Ground, "groundTransform" },
            { EnumItemRenderTarget.HandTp, "tpHandTransform" },
            //{ EnumItemRenderTarget.HandFp, "fpHandTransform" },
            { EnumItemRenderTarget.Gui, "guiTransform" },
            { EnumItemRenderTarget.HandTpOff, "tpOffHandTransform" },
        };

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            var meshrefs = ObjectCacheUtil.GetOrCreate(capi, "itemcreatureinventorymeshes", () => new Dictionary<string, MultiTextureMeshRef>());
            string code = itemstack.Attributes.GetString("type");

            if (!meshrefs.ContainsKey(code))
            {
                AssetLocation location = new AssetLocation(code);
                EntityProperties type = nowTesselatingEntityType = api.World.GetEntityType(location);
                Shape shape = type.Client.LoadedShape;

                if (shape != null)
                {
                    capi.Tesselator.TesselateShape("itemcreatureinventory", shape, out var meshdata, this);

                    ModelTransform tf = type.Attributes?[map[target]]?.AsObject<ModelTransform>();
                    if (tf != null)
                    {
                        meshdata.ModelTransform(tf);
                    }

                    renderinfo.ModelRef = meshrefs[code] = capi.Render.UploadMultiTextureMesh(meshdata);
                }
            } else
            {
                renderinfo.ModelRef = meshrefs[code];
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
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

            AssetLocation location = new AssetLocation(slot.Itemstack.Attributes.GetString("type"));
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
                entity.Pos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.Pos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.Pos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.Pos.Yaw = (float)byEntity.World.Rand.NextDouble() * 2 * GameMath.PI;

                entity.PositionBeforeFalling.Set(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);

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
    }
}
