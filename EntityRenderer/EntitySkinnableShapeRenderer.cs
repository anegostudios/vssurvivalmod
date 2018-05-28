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

        public int AtlasSize { get { return api.EntityTextureAtlas.Size; } }

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
            TextureAtlasPosition origTexPos = api.EntityTextureAtlas.Positions[entity.Type.Client.FirstTexture.Baked.TextureSubId];
            int width = (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize);
            int height = (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize);

            api.EntityTextureAtlas.AllocateTextureSpace(width, height, out skinTextureSubId, out skinTexPos);

            return this;
        }


        void ReloadSkin()
        {
            TextureAtlasPosition origTexPos = api.EntityTextureAtlas.Positions[entity.Type.Client.FirstTexture.Baked.TextureSubId];

            LoadedTexture skinnedTex = new LoadedTexture(origTexPos.atlasTextureId, AtlasSize, AtlasSize);

            
            LoadedTexture entityAtlas = new LoadedTexture(null) {
                textureId = origTexPos.atlasTextureId,
                width = api.EntityTextureAtlas.Size,
                height = api.EntityTextureAtlas.Size
            };

            api.Render.GlToggleBlend(false);
            api.EntityTextureAtlas.RenderTextureIntoAtlas(
                entityAtlas,
                (int)(origTexPos.x1 * AtlasSize),
                (int)(origTexPos.y1 * AtlasSize),
                (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize),
                (int)((origTexPos.x2 - origTexPos.x1) * AtlasSize),
                skinTexPos.x1 * api.EntityTextureAtlas.Size,
                skinTexPos.y1 * api.EntityTextureAtlas.Size,
                -1
            );
            api.Render.GlToggleBlend(true, EnumBlendMode.Overlay);

            int[] renderOrder = new int[]
            {
                (int)EnumCharacterDressType.LowerBody,
                (int)EnumCharacterDressType.Foot,
                (int)EnumCharacterDressType.UpperBody,
                (int)EnumCharacterDressType.Waist,
                (int)EnumCharacterDressType.Shoulder,
                (int)EnumCharacterDressType.Emblem,
                (int)EnumCharacterDressType.Necklace,
                (int)EnumCharacterDressType.Head,
                (int)EnumCharacterDressType.Ring,
                (int)EnumCharacterDressType.Hand,
            };

            //int q = gearInv.QuantitySlots;
            //for (int slotid = 0; slotid < gearInv.QuantitySlots; slotid++)
            for (int i = 0; i < renderOrder.Length; i++)
            {
                int slotid = renderOrder[i];

                ItemStack stack = gearInv.GetSlot(slotid).Itemstack;
                if (stack == null) continue;

                int itemTextureSubId = stack.Item.FirstTexture.Baked.TextureSubId;

                TextureAtlasPosition itemTexPos = api.ItemTextureAtlas.Positions[itemTextureSubId];
                
                LoadedTexture itemAtlas = new LoadedTexture(null) {
                    textureId = itemTexPos.atlasTextureId,
                    width = api.ItemTextureAtlas.Size,
                    height = api.ItemTextureAtlas.Size
                };
                

                api.EntityTextureAtlas.RenderTextureIntoAtlas(
                    itemAtlas,
                    itemTexPos.x1 * api.ItemTextureAtlas.Size,
                    itemTexPos.y1 * api.ItemTextureAtlas.Size,
                    (itemTexPos.x2 - itemTexPos.x1) * api.ItemTextureAtlas.Size,
                    (itemTexPos.y2 - itemTexPos.y1) * api.ItemTextureAtlas.Size,
                    //entityTexPos.atlasNumber,
                    skinTexPos.x1 * api.EntityTextureAtlas.Size,
                    skinTexPos.y1 * api.EntityTextureAtlas.Size
                );
            }

            api.Render.GlToggleBlend(true);
            api.Render.BindTexture2d(skinTexPos.atlasTextureId);
            api.Render.GlGenerateTex2DMipmaps();
        }


        public override void Dispose()
        {
            base.Dispose();

            api.Event.OnReloadTextures -= ReloadSkin;
        }
    }
}
