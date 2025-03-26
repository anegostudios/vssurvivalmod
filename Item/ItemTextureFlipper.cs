using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public interface ITextureFlippable
    {
        void FlipTexture(BlockPos pos, string newTextureCode);

        OrderedDictionary<string, CompositeTexture> GetAvailableTextures(BlockPos pos);
    }

    public class ItemTextureFlipper : Item
    {
        SkillItem[] skillitems;
        BlockPos pos;
        ICoreClientAPI capi;

        Dictionary<AssetLocation, MultiTextureMeshRef> skillTextures = new Dictionary<AssetLocation, MultiTextureMeshRef>();

        private void renderSkillItem(AssetLocation code, float dt, double atPosX, double atPosY)
        {
            var block = api.World.BlockAccessor.GetBlock(pos) as ITextureFlippable;
            if (block == null) return;
            var textures = block.GetAvailableTextures(pos);
            if (textures == null) return;

            MultiTextureMeshRef meshref;
            if (!skillTextures.TryGetValue(code, out meshref))
            {
                var pos = textures[code.Path].Baked.TextureSubId;
                var texPos = capi.BlockTextureAtlas.Positions[pos];
                var mesh = QuadMeshUtil.GetCustomQuadModelData(texPos.x1, texPos.y1, texPos.x2, texPos.y2, 0, 0, 1, 1, 255, 255, 255, 255);
                mesh.TextureIds = new int[] { texPos.atlasTextureId };
                mesh.TextureIndices = new byte[] { 0 };
                meshref = capi.Render.UploadMultiTextureMesh(mesh);
                skillTextures[code] = meshref;
            }

            float scale = RuntimeEnv.GUIScale;
            capi.Render.Render2DTexture(meshref, (float)atPosX - 24 * scale, (float)atPosY - 24 * scale, scale * 64, scale * 64);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return base.GetToolMode(slot, byPlayer, blockSelection);
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return null;

            var pos = blockSel.Position;

            if (pos != this.pos)
            {
                var block = api.World.BlockAccessor.GetBlock(pos) as ITextureFlippable;
                if (block == null) return null;
                var textures = block.GetAvailableTextures(pos);
                if (textures == null) return null;
                skillitems = new SkillItem[textures.Count];
                int i = 0;
                foreach (var val in textures)
                {
                    skillitems[i++] = new SkillItem()
                    {
                        Code = new AssetLocation(val.Key),
                        Name = val.Key,
                        Data = val.Key,
                        RenderHandler = renderSkillItem
                    };
                }

                this.pos = pos;
            }

            return skillitems;
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);

            base.SetToolMode(slot, byPlayer, blockSelection, toolMode);
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;


            handling = EnumHandHandling.PreventDefault;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;
            if (blockSel == null) return;


            int toolMode = slot.Itemstack.Attributes.GetInt("toolMode");
            var pos = blockSel.Position;
            var block = api.World.BlockAccessor.GetBlock(pos) as ITextureFlippable;
            if (block != null)
            {
                var textures = block.GetAvailableTextures(pos);
                if (textures == null) return;
                block.FlipTexture(pos, textures.GetKeyAtIndex(toolMode));
            }


            handling = EnumHandHandling.PreventDefault;
        }


        
    }
}
