using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BloomeryContentsRenderer : IRenderer
    {
        BlockPos pos;
        ICoreClientAPI api;

        MeshRef cubeModelRef;
        int textureId;
        int voxelHeight;
        public int glowLevel;

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public BloomeryContentsRenderer(BlockPos pos, ICoreClientAPI api)
        {
            this.pos = pos;
            this.api = api;
            textureId = api.Render.GetOrLoadTexture(new AssetLocation("block/orecoalmix.png"));
        }


        public void SetFillLevel(int voxelHeight)
        {
            if (this.voxelHeight == voxelHeight && cubeModelRef != null) return;

            this.voxelHeight = voxelHeight;

            if (cubeModelRef != null) {
                api.Render.DeleteMesh(cubeModelRef);
            }

            if (voxelHeight == 0) return;

            MeshData modeldata = CubeMeshUtil.GetCube(8 / 32f, voxelHeight / 24f, new Vec3f(0,0,0));
            modeldata.Flags = new int[6 * 4];

            cubeModelRef = api.Render.UploadMesh(modeldata);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (voxelHeight == 0) return;

            IStandardShaderProgram prog = api.Render.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.ExtraGlow = glowLevel;
            prog.RgbaBlockIn = new Vec4f(1 + glowLevel/128f, 1 + glowLevel / 128f, 1 + glowLevel / 512f, 1);

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.BindTexture2d(textureId);
            rpi.GlPushMatrix();
            rpi.GlLoadMatrix(api.Render.CameraMatrixOrigin);
            rpi.GlTranslate(8 / 16f + pos.X - camPos.X, pos.Y - camPos.Y + voxelHeight / 24f, 8 / 16f + pos.Z - camPos.Z);
            
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.ModelViewMatrix = rpi.CurrentModelviewMatrix;
            rpi.RenderMesh(cubeModelRef);
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
            api.Render.DeleteMesh(cubeModelRef);
        }
    }
}
