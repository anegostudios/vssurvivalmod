using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Handles rendering of falling blocks
    /// </summary>
    public class EntityBlockFallingRenderer : EntityRenderer
    {
        private EntityBlockFalling blockFallingEntity;
        private MeshRef meshRef;
        private Block block;
        private ITesselatorAPI tesselator;
        private int atlasTextureId;
        private IRenderAPI rapi;
        private IEntityPlayer player;

        public EntityBlockFallingRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
        {
            this.blockFallingEntity = (EntityBlockFalling)entity;
            this.block = blockFallingEntity.Block;

            this.tesselator = api.Tesselator;
            this.rapi = api.Render;
            this.meshRef = api.Render.UploadMesh(api.TesselatorManager.GetDefaultBlockMesh(block));

            int textureSubId = block.FirstTextureInventory.Baked.TextureSubId;
            this.atlasTextureId = api.BlockTextureAtlas.Positions[textureSubId].atlasTextureId;

            this.player = api.World.Player.Entity;
        }

        public override void DoRender3DOpaque(float dt, bool isShadowPass)
        {
            // TODO: ADD
            if (isShadowPass) return;

            if (!blockFallingEntity.InitialBlockRemoved) return;

            float x = (float)entity.Pos.X + entity.CollisionBox.X1;
            float y = (float)entity.Pos.Y + entity.CollisionBox.Y1;
            float z = (float)entity.Pos.Z + entity.CollisionBox.Z1;

            RenderFallingBlockEntity(x, y, z);
        }

        private void RenderFallingBlockEntity(float x, float y, float z)
        {
            rapi.GlDisableCullFace();
            rapi.GlMatrixModeModelView();
            rapi.GlToggleBlend(true, EnumBlendMode.Standard);

            IStandardShaderProgram prog = rapi.PreparedStandardShader((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
            prog.Tex2D = atlasTextureId;
            
            rapi.GlPushMatrix();
            rapi.GlLoadMatrix(rapi.CameraMatrixOrigin);
            rapi.GlTranslate(x - player.CameraPos.X, y - player.CameraPos.Y, z - player.CameraPos.Z);

            prog.ProjectionMatrix = rapi.CurrentProjectionMatrix;
            prog.ModelViewMatrix = rapi.CurrentModelviewMatrix;

            rapi.RenderMesh(meshRef);

            prog.Stop();

            rapi.GlPopMatrix();
        }

        public override void Dispose()
        {
            rapi.DeleteMesh(meshRef);
        }
    }
}
