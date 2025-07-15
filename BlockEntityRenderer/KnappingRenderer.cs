﻿using Newtonsoft.Json.Linq;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class KnappingRenderer : IRenderer
    {
        protected ICoreClientAPI api;
        protected BlockPos pos;

        protected MeshRef workItemMeshRef;
        protected MeshRef recipeOutlineMeshRef;

        protected ItemStack workItem;
        protected int texId;
        public string Material;

        protected Matrixf ModelMat = new Matrixf();

        protected Vec4f outLineColorMul = new Vec4f(1, 1, 1, 1);
        protected Vec3f origin = new Vec3f(0, 0, 0);

        public KnappingRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.api = capi;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "knappingsurface");
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterFinalComposition, "knappingsurface");
        }

        public double RenderOrder
        {
            get { return 0.5; }
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

            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            rpi.BindTexture2d(texId);

            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z).Values;

            rpi.RenderMesh(workItemMeshRef);

            prog.ModelMatrix = rpi.CurrentModelviewMatrix;
            prog.Stop();
        }


        

        private void RenderRecipeOutLine()
        {
            if (recipeOutlineMeshRef == null || api.HideGuis) return;

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            outLineColorMul.A = 1 - GameMath.Clamp((float)Math.Sqrt(plrPos.SquareDistanceTo(pos.X, pos.Y, pos.Z)) / 5 - 1f, 0, 1);
            ModelMat.Set(rpi.CameraMatrixOriginf).Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            float linewidth = api.Settings.Float["wireframethickness"];
            rpi.LineWidth = linewidth;
            rpi.GLEnableDepthTest();
            rpi.GlToggleBlend(true);

            IShaderProgram prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);
            prog.Use();
            prog.Uniform("origin", origin);
            prog.Uniform("colorIn", outLineColorMul);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", ModelMat.Values);
            rpi.RenderMesh(recipeOutlineMeshRef);
            prog.Stop();

            if (linewidth != 1.6f) rpi.LineWidth = 1.6f;

            rpi.GLDepthMask(false);   // Helps prevent HUD failing to draw at the start of the next frame, on macOS.  This may be the last GL settings call before the frame is finalised.  The block outline renderer sets this to false prior to rendering its mesh.
        }



        public void RegenMesh(bool[,] Voxels, KnappingRecipe recipeToOutline)
        {
            workItemMeshRef?.Dispose();
            workItemMeshRef = null;

            workItem = new ItemStack(api.World.GetBlock(new AssetLocation("knappingsurface")));
            if (workItem?.Block == null) return;

            if (recipeToOutline != null)
            {
                RegenOutlineMesh(recipeToOutline, Voxels);
            }

            MeshData workItemMesh = new MeshData(24, 36, false);

            float subPixelPaddingx = api.BlockTextureAtlas.SubPixelPaddingX;
            float subPixelPaddingy = api.BlockTextureAtlas.SubPixelPaddingY;

            TextureAtlasPosition tpos = api.BlockTextureAtlas.GetPosition(workItem.Block, Material);

            MeshData singleVoxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            singleVoxelMesh.Rgba = new byte[6 * 4 * 4].Fill((byte)255);
            CubeMeshUtil.SetXyzFacesAndPacketNormals(singleVoxelMesh);

            texId = tpos.atlasTextureId;

            for (int i = 0; i < singleVoxelMesh.Uv.Length; i+=2)
            {
                singleVoxelMesh.Uv[i] = tpos.x1 + singleVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Width - subPixelPaddingx;
                singleVoxelMesh.Uv[i+1] = tpos.y1 + singleVoxelMesh.Uv[i+1] * 2f / api.BlockTextureAtlas.Size.Height - subPixelPaddingy;
            }

            singleVoxelMesh.XyzFaces = (byte[])CubeMeshUtil.CubeFaceIndices.Clone();
            singleVoxelMesh.XyzFacesCount = 6;
            singleVoxelMesh.ClimateColorMapIds = new byte[6];
            singleVoxelMesh.SeasonColorMapIds = new byte[6];
            singleVoxelMesh.ColorMapIdsCount = 6;


            MeshData voxelMeshOffset = singleVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (!Voxels[x, z]) continue;

                    float px = x / 16f;
                    float pz = z / 16f;

                    for (int i = 0; i < singleVoxelMesh.xyz.Length; i += 3)
                    {
                        voxelMeshOffset.xyz[i] = px + singleVoxelMesh.xyz[i];
                        voxelMeshOffset.xyz[i + 1] = singleVoxelMesh.xyz[i + 1];
                        voxelMeshOffset.xyz[i + 2] = pz + singleVoxelMesh.xyz[i + 2];
                    }

                    float offsetX = (px * 32f) / api.BlockTextureAtlas.Size.Width;
                    float offsetZ = (pz * 32f) / api.BlockTextureAtlas.Size.Height;

                    for (int i = 0; i < singleVoxelMesh.Uv.Length; i += 2)
                    {
                        voxelMeshOffset.Uv[i] = singleVoxelMesh.Uv[i] + offsetX;
                        voxelMeshOffset.Uv[i + 1] = singleVoxelMesh.Uv[i + 1] + offsetZ;
                    }

                    workItemMesh.AddMeshData(voxelMeshOffset);
                }
            }

            workItemMeshRef = api.Render.UploadMesh(workItemMesh);
        }



        private void RegenOutlineMesh(KnappingRecipe recipeToOutline, bool[,] Voxels)
        {
            MeshData recipeOutlineMesh = new MeshData(24, 36, false, false, true, false);
            recipeOutlineMesh.SetMode(EnumDrawMode.Lines);

            int greenCol = api.ColorPreset.GetColor("voxelColorGreen");
            int orangeCol = api.ColorPreset.GetColor("voxelColorOrange");

            MeshData greenVoxelMesh = LineMeshUtil.GetCube(greenCol);
            MeshData orangeVoxelMesh = LineMeshUtil.GetCube(orangeCol);
            for (int i = 0; i < greenVoxelMesh.xyz.Length; i++)
            {
                greenVoxelMesh.xyz[i] = greenVoxelMesh.xyz[i] / 32f + 1 / 32f;
                orangeVoxelMesh.xyz[i] = orangeVoxelMesh.xyz[i] / 32f + 1 / 32f;
            }
            MeshData voxelMeshOffset = greenVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    bool shouldFill = recipeToOutline.Voxels[x, 0, z];
                    bool didFill = Voxels[x, z];

                    if (shouldFill == didFill) continue;

                    float px = x / 16f;
                    float py = 0.001f;
                    float pz = z / 16f;

                    for (int i = 0; i < greenVoxelMesh.xyz.Length; i += 3)
                    {
                        voxelMeshOffset.xyz[i] = px + greenVoxelMesh.xyz[i];
                        voxelMeshOffset.xyz[i + 1] = py + greenVoxelMesh.xyz[i + 1];
                        voxelMeshOffset.xyz[i + 2] = pz + greenVoxelMesh.xyz[i + 2];
                    }

                    voxelMeshOffset.Rgba = (shouldFill && !didFill) ? greenVoxelMesh.Rgba : orangeVoxelMesh.Rgba;

                    recipeOutlineMesh.AddMeshData(voxelMeshOffset);
                }
            }

            recipeOutlineMeshRef?.Dispose();
            recipeOutlineMeshRef = null;
            if (recipeOutlineMesh.VerticesCount > 0)
            {
                recipeOutlineMeshRef = api.Render.UploadMesh(recipeOutlineMesh);
            }
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
            recipeOutlineMeshRef?.Dispose();
            workItemMeshRef?.Dispose();
        }
    }
}

