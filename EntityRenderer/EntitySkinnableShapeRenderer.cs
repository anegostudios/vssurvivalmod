using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    class EntitySkinnableShapeRenderer : EntityShapeRenderer, ITexPositionSource
    {
        TextureAtlasPosition skinTexPos;
        int skinTextureSubId;
        IInventory gearInv;
        EntityAgent eagent;

        public int AtlasSize { get { return capi.EntityTextureAtlas.Size; } }

        public TextureAtlasPosition this[string textureCode]
        {
            get { return skinTexPos; }
        }


        public EntitySkinnableShapeRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
        {
            eagent = entity as EntityAgent;
            api.Event.OnReloadTextures += ReloadSkin;
        }

        public override void BeforeRender(float dt)
        {
            if (gearInv == null && eagent?.GearInventory != null)
            {
                eagent.GearInventory.SlotModified += (slotid) => ReloadSkin();
                gearInv = eagent.GearInventory;
                ReloadSkin();
            }

            base.BeforeRender(dt);
        }

        public override void PrepareForGuiRender(float dt, double posX, double posY, double posZ, float yawDelta, float size, out MeshRef meshRef, out float[] modelviewMatrix)
        {
            if (gearInv == null && eagent?.GearInventory != null)
            {
                eagent.GearInventory.SlotModified += (slotid) => ReloadSkin();
                gearInv = eagent.GearInventory;
                ReloadSkin();
            }

            base.PrepareForGuiRender(dt, posX, posY, posZ, yawDelta, size, out meshRef, out modelviewMatrix);
        }

        protected override ITexPositionSource GetTextureSource()
        {
            TextureAtlasPosition origTexPos = capi.EntityTextureAtlas.Positions[entity.Type.Client.FirstTexture.Baked.TextureSubId];
            int width = (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize);
            int height = (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize);

            capi.EntityTextureAtlas.AllocateTextureSpace(width, height, out skinTextureSubId, out skinTexPos);

            return this;
        }


        void ReloadSkin()
        {
            TextureAtlasPosition origTexPos = capi.EntityTextureAtlas.Positions[entity.Type.Client.FirstTexture.Baked.TextureSubId];

            LoadedTexture skinnedTex = new LoadedTexture(capi, origTexPos.atlasTextureId, AtlasSize, AtlasSize);

            
            LoadedTexture entityAtlas = new LoadedTexture(null) {
                TextureId = origTexPos.atlasTextureId,
                Width = capi.EntityTextureAtlas.Size,
                Height = capi.EntityTextureAtlas.Size
            };

            capi.Render.GlToggleBlend(false);
            capi.EntityTextureAtlas.RenderTextureIntoAtlas(
                entityAtlas,
                (int)(origTexPos.x1 * AtlasSize),
                (int)(origTexPos.y1 * AtlasSize),
                (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize),
                (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize),
                skinTexPos.x1 * capi.EntityTextureAtlas.Size,
                skinTexPos.y1 * capi.EntityTextureAtlas.Size,
                -1
            );
            capi.Render.GlToggleBlend(true, EnumBlendMode.Overlay);

            int[] renderOrder = new int[]
            {
                (int)EnumCharacterDressType.LowerBody,
                (int)EnumCharacterDressType.Foot,
                (int)EnumCharacterDressType.UpperBody,
                (int)EnumCharacterDressType.UpperBodyOver,
                (int)EnumCharacterDressType.Waist,
                (int)EnumCharacterDressType.Shoulder,
                (int)EnumCharacterDressType.Emblem,
                (int)EnumCharacterDressType.Neck,
                (int)EnumCharacterDressType.Head,
                (int)EnumCharacterDressType.Ring,
                (int)EnumCharacterDressType.Arm,
                (int)EnumCharacterDressType.Hand,
            };

            if (gearInv == null && eagent?.GearInventory != null)
            {
                eagent.GearInventory.SlotModified += (slotid) => ReloadSkin();
                gearInv = eagent.GearInventory;
            }

            for (int i = 0; i < renderOrder.Length; i++)
            {
                int slotid = renderOrder[i];

                ItemStack stack = gearInv.GetSlot(slotid)?.Itemstack;
                if (stack == null) continue;

                int itemTextureSubId = stack.Item.FirstTexture.Baked.TextureSubId;

                TextureAtlasPosition itemTexPos = capi.ItemTextureAtlas.Positions[itemTextureSubId];
                
                LoadedTexture itemAtlas = new LoadedTexture(null) {
                    TextureId = itemTexPos.atlasTextureId,
                    Width = capi.ItemTextureAtlas.Size,
                    Height = capi.ItemTextureAtlas.Size
                };
                

                capi.EntityTextureAtlas.RenderTextureIntoAtlas(
                    itemAtlas,
                    itemTexPos.x1 * capi.ItemTextureAtlas.Size,
                    itemTexPos.y1 * capi.ItemTextureAtlas.Size,
                    (itemTexPos.x2 - itemTexPos.x1) * capi.ItemTextureAtlas.Size,
                    (itemTexPos.y2 - itemTexPos.y1) * capi.ItemTextureAtlas.Size,
                    //entityTexPos.atlasNumber,
                    skinTexPos.x1 * capi.EntityTextureAtlas.Size,
                    skinTexPos.y1 * capi.EntityTextureAtlas.Size
                );
            }

            capi.Render.GlToggleBlend(true);
            capi.Render.BindTexture2d(skinTexPos.atlasTextureId);
            capi.Render.GlGenerateTex2DMipmaps();
        }


        public override void Dispose()
        {
            base.Dispose();

            capi.Event.OnReloadTextures -= ReloadSkin;
        }
    }
}
