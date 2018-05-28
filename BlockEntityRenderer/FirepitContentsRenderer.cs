using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class FirepitContentsRenderer : IRenderer
    {
        MeshRef meshref;
        ICoreClientAPI api;
        BlockPos pos;
        ItemStack contents;
        int textureId;
        

        public double RenderOrder
        {
            get { return 0; }
        }

        public int RenderRange
        {
            get { return 48; }
        }

        public FirepitContentsRenderer(ICoreClientAPI api, BlockPos pos)
        {
            this.api = api;
            this.pos = pos;
        }

        public void SetContents(ItemStack stack)
        {
            if (meshref != null)
            {
                api.Render.DeleteMesh(meshref);
                meshref = null;
            }

            if (stack == null || stack.Class == EnumItemClass.Block)
            {
                this.contents = null;
                return;
            }

            MeshData ingredientMesh;
            if (stack.Class == EnumItemClass.Item)
            {
                api.Tesselator.TesselateItem(stack.Item, out ingredientMesh);
                textureId = api.ItemTextureAtlas.GetPosition(stack.Item).atlasTextureId;
            }
            else
            {
                api.Tesselator.TesselateBlock(stack.Block, out ingredientMesh);
                textureId = api.BlockTextureAtlas.GetPosition(stack.Block, "ember").atlasTextureId;
            }

            meshref = api.Render.UploadMesh(ingredientMesh);
            this.contents = stack;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null) return;
            
            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            api.Render.BindTexture2d(textureId);
            api.Render.GlMatrixModeModelView();

            api.Render.GlPushMatrix();
            api.Render.GlLoadMatrix(api.Render.CameraMatrixOrigin);

            rpi.GlTranslate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            rpi.GlTranslate(0.25f + 0.125f, 0.6f, 0.5f + 0.125f);
            rpi.GlRotate(90, 0, 1, 0);
            rpi.GlScale(0.25f, 0.25f, 0.25f);
            prog.ModelViewMatrix = rpi.CurrentModelviewMatrix;

            rpi.RenderMesh(meshref);

            rpi.GlPopMatrix();

            prog.Stop();
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            api.Render.DeleteMesh(meshref);
        }


    }
}
