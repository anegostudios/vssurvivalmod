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
    public class AnvilWorkItemRenderer : IRenderer
    {
        private ICoreClientAPI api;
        private BlockPos pos;

        MeshRef workItemMeshRef;
        MeshRef recipeOutlineMeshRef;

        ItemStack ingot;
        int texId;

        public AnvilWorkItemRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.api = capi;
        }

        public double RenderOrder
        {
            get { return 0; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (workItemMeshRef == null) return;
            if (stage == EnumRenderStage.AfterFinalComposition)
            {
                RenderRecipeOutLine();
                return;
            }

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;

            rpi.GlDisableCullFace();

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;

            rpi.GlMatrixModeModelView();

            int temp = (int)ingot.Collectible.GetTemperature(api.World, ingot);

            Vec4f lightrgbs = worldAccess.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] glowColor = ColorUtil.getIncandescenceColorAsColor4f(temp);
            lightrgbs[0] += 2 * glowColor[0];
            lightrgbs[1] += 2 * glowColor[1];
            lightrgbs[2] += 2 * glowColor[2];

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.ExtraGlow = GameMath.Clamp((temp - 700) / 4, 0, 255);


            rpi.BindTexture2d(texId);
            rpi.GlPushMatrix();
            rpi.GlLoadMatrix(api.Render.CameraMatrixOrigin);
            rpi.GlTranslate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.ModelViewMatrix = rpi.CurrentModelviewMatrix;
            rpi.RenderMesh(workItemMeshRef);
            rpi.GlPopMatrix();


            prog.Stop();
        }



        private void RenderRecipeOutLine()
        {
            if (recipeOutlineMeshRef == null || api.HideGuis) return;
            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            IShaderProgram prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);
            prog.Use();
            rpi.GlMatrixModeModelView();
            
            rpi.GLEnableDepthTest();
            rpi.GlToggleBlend(true);
            

            rpi.GlPushMatrix();
            rpi.GlLoadMatrix(rpi.CameraMatrixOrigin);

            rpi.GlTranslate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", rpi.CurrentModelviewMatrix);

            rpi.RenderMesh(recipeOutlineMeshRef);

            rpi.GlPopMatrix();

            prog.Stop();
        }



        public void RegenMesh(ItemStack ingot, bool[,,] Voxels, SmithingRecipe recipeToOutline)
        {
            if (workItemMeshRef != null)
            {
                api.Render.DeleteMesh(workItemMeshRef);
                workItemMeshRef = null;
            }

            if (ingot == null) return;

            if (recipeToOutline != null)
            {
                RegenOutlineMesh(recipeToOutline);
            }

            this.ingot = ingot;
            MeshData workItemMesh = new MeshData(24, 36, false);

            TextureAtlasPosition tpos = api.BlockTextureAtlas.GetPosition(api.World.GetBlock(new AssetLocation("ingotpile")), ingot.Collectible.LastCodePart());
            MeshData voxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            texId = tpos.atlasTextureId;

            for (int i = 0; i < voxelMesh.Uv.Length; i++)
            {
                voxelMesh.Uv[i] = (i % 2 > 0 ? tpos.y1 : tpos.x1) + voxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size;
            }

            voxelMesh.XyzFaces = (int[])CubeMeshUtil.CubeFaceIndices.Clone();
            voxelMesh.XyzFacesCount = 6;
            voxelMesh.Tints = new int[6];
            voxelMesh.Flags = new int[24];
            voxelMesh.TintsCount = 6;
            for (int i = 0; i < voxelMesh.Rgba.Length; i++) voxelMesh.Rgba[i] = 255;
            voxelMesh.rgba2 = voxelMesh.Rgba;


            MeshData voxelMeshOffset = voxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 10; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z]) continue;

                        float px = x / 16f;
                        float py = y / 16f;
                        float pz = z / 16f;

                        for (int i = 0; i < voxelMesh.xyz.Length; i += 3)
                        {
                            voxelMeshOffset.xyz[i] = px + voxelMesh.xyz[i];
                            voxelMeshOffset.xyz[i + 1] = py + voxelMesh.xyz[i + 1];
                            voxelMeshOffset.xyz[i + 2] = pz + voxelMesh.xyz[i + 2];
                        }

                        float offsetX = (px * 32f) / api.BlockTextureAtlas.Size;
                        float offsetZ = (pz * 32f) / api.BlockTextureAtlas.Size;

                        for (int i = 0; i < voxelMesh.Uv.Length; i += 2)
                        {
                            voxelMeshOffset.Uv[i] = voxelMesh.Uv[i] + offsetX;
                            voxelMeshOffset.Uv[i + 1] = voxelMesh.Uv[i + 1] + offsetZ;
                        }

                        workItemMesh.AddMeshData(voxelMeshOffset);
                    }
                }
            }

            workItemMeshRef = api.Render.UploadMesh(workItemMesh);
        }


        private void RegenOutlineMesh(SmithingRecipe recipeToOutline)
        {
            MeshData recipeOutlineMesh = new MeshData(24, 36, false, false, true, false, false);
            recipeOutlineMesh.setMode(EnumDrawMode.Lines);

            MeshData voxelMesh = LineMeshUtil.GetCube((180 << 24) | (100 << 16) | (200 << 8) | (200));
            for (int i = 0; i < voxelMesh.xyz.Length; i++)
            {
                voxelMesh.xyz[i] = voxelMesh.xyz[i] / 32f + 1 / 32f;
            }
            MeshData voxelMeshOffset = voxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                int y = 10;
                for (int z = 0; z < 16; z++)
                {
                    if (!recipeToOutline.Voxels[x, z]) continue;

                    float px = x / 16f;
                    float py = y / 16f;
                    float pz = z / 16f;

                    for (int i = 0; i < voxelMesh.xyz.Length; i += 3)
                    {
                        voxelMeshOffset.xyz[i] = px + voxelMesh.xyz[i];
                        voxelMeshOffset.xyz[i + 1] = py + voxelMesh.xyz[i + 1];
                        voxelMeshOffset.xyz[i + 2] = pz + voxelMesh.xyz[i + 2];
                    }

                    recipeOutlineMesh.AddMeshData(voxelMeshOffset);
                }
            }

            recipeOutlineMeshRef = api.Render.UploadMesh(recipeOutlineMesh);
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            if (recipeOutlineMeshRef != null) api.Render.DeleteMesh(recipeOutlineMeshRef);
            if (workItemMeshRef != null) api.Render.DeleteMesh(workItemMeshRef);
        }
    }
}
