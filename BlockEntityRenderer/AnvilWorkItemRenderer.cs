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

        Vec4f outLineColorMul = new Vec4f(1, 1, 1, 1);
        protected Matrixf ModelMat = new Matrixf();

        SurvivalCoreSystem coreMod;

        BlockEntityAnvil beAnvil;
        Vec4f glowRgb = new Vec4f();

        public AnvilWorkItemRenderer(BlockEntityAnvil beAnvil, BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.api = capi;
            this.beAnvil = beAnvil;

            coreMod = capi.ModLoader.GetModSystem<SurvivalCoreSystem>();
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
                if (api.World.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible is ItemHammer)
                {
                    RenderRecipeOutLine();
                }
                return;
            }

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            int temp = (int)ingot.Collectible.GetTemperature(api.World, ingot);

            Vec4f lightrgbs = worldAccess.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            glowRgb.R = glowColor[0];
            glowRgb.G = glowColor[1];
            glowRgb.B = glowColor[2];
            glowRgb.A = extraGlow / 255f;

            rpi.GlDisableCullFace();

            IShaderProgram prog = coreMod.anvilShaderProg;
            prog.Use();
            rpi.BindTexture2d(texId);
            prog.Uniform("rgbaAmbientIn", rpi.AmbientColor);

            prog.Uniform("rgbaFogIn", rpi.FogColor);
            prog.Uniform("fogMinIn", rpi.FogMin);
            prog.Uniform("dontWarpVertices", (int)0);
            prog.Uniform("addRenderFlags", (int)0);
            prog.Uniform("fogDensityIn", rpi.FogDensity);
            prog.Uniform("rgbaTint", ColorUtil.WhiteArgbVec);
            prog.Uniform("rgbaLightIn", lightrgbs);
            prog.Uniform("rgbaGlowIn", glowRgb);
            prog.Uniform("extraGlow", extraGlow);
            
            prog.UniformMatrix("modelMatrix", ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Values
            );
            prog.UniformMatrix("viewMatrix", rpi.CameraMatrixOriginf);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);


            rpi.RenderMesh(workItemMeshRef);

            prog.Stop();
        }



        private void RenderRecipeOutLine()
        {
            if (recipeOutlineMeshRef == null || api.HideGuis) return;
            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            ModelMat.Set(rpi.CameraMatrixOriginf).Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
            outLineColorMul.A = 1 - GameMath.Clamp((float)Math.Sqrt(plrPos.SquareDistanceTo(pos.X, pos.Y, pos.Z)) / 5 - 1f, 0, 1);

            rpi.GLEnableDepthTest();
            rpi.GlToggleBlend(true);

            IShaderProgram prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);

            prog.Use();
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", ModelMat.Values);
            prog.Uniform("colorIn", outLineColorMul);
            rpi.RenderMesh(recipeOutlineMeshRef);
            prog.Stop();
        }



        public void RegenMesh(ItemStack ingot, byte[,,] voxels, bool[,,] recipeToOutlineVoxels)
        {
            workItemMeshRef?.Dispose();
            workItemMeshRef = null;
            
            if (ingot == null) return;

            if (recipeToOutlineVoxels != null)
            {
                RegenOutlineMesh(recipeToOutlineVoxels, voxels);
            }

            this.ingot = ingot;
            MeshData workItemMesh = new MeshData(24, 36, false);
            workItemMesh.CustomBytes = new CustomMeshDataPartByte()
            {
                Conversion = DataConversion.NormalizedFloat,
                Count = workItemMesh.VerticesCount,
                InterleaveSizes = new int[] { 1 },
                Instanced = false,
                InterleaveOffsets = new int[] { 0 },
                InterleaveStride = 1,
                Values = new byte[workItemMesh.VerticesCount]
            };

            //float thickness = 0.33f + 0.66f * beAnvil.AvailableMetalVoxels / 32f;
            TextureAtlasPosition tposMetal;
            TextureAtlasPosition tposSlag;

            if (beAnvil.IsIronBloom)
            {
                tposSlag = api.BlockTextureAtlas.GetPosition(beAnvil.Block, "ironbloom");
                tposMetal = api.BlockTextureAtlas.GetPosition(api.World.GetBlock(new AssetLocation("ingotpile")), "iron");
            } else
            {
                tposMetal = api.BlockTextureAtlas.GetPosition(api.World.GetBlock(new AssetLocation("ingotpile")), ingot.Collectible.LastCodePart());
                tposSlag = tposMetal;
            }
            
            MeshData metalVoxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            CubeMeshUtil.SetXyzFacesAndPacketNormals(metalVoxelMesh);
            metalVoxelMesh.CustomBytes = new CustomMeshDataPartByte()
            {
                Conversion = DataConversion.NormalizedFloat,
                Count = metalVoxelMesh.VerticesCount,
                Values = new byte[metalVoxelMesh.VerticesCount]
            };

            texId = tposMetal.atlasTextureId;

            metalVoxelMesh.XyzFaces = (byte[])CubeMeshUtil.CubeFaceIndices.Clone();
            metalVoxelMesh.XyzFacesCount = 6;
            
            //metalVoxelMesh.ColorMapIds = new int[6];
            //metalVoxelMesh.TintsCount = 6;

            for (int i = 0; i < metalVoxelMesh.Rgba.Length; i++) metalVoxelMesh.Rgba[i] = 255;
            //metalVoxelMesh.Rgba2 = null;


            MeshData slagVoxelMesh = metalVoxelMesh.Clone();

            for (int i = 0; i < metalVoxelMesh.Uv.Length; i++)
            {
                if (i % 2 > 0)
                {
                    metalVoxelMesh.Uv[i] = tposMetal.y1 + metalVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Height;

                    slagVoxelMesh.Uv[i] = tposSlag.y1 + slagVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Height;
                }
                else
                {
                    metalVoxelMesh.Uv[i] = tposMetal.x1 + metalVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Width;

                    slagVoxelMesh.Uv[i] = tposSlag.x1 + slagVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Width;
                }
            }




            MeshData metVoxOffset = metalVoxelMesh.Clone();
            MeshData slagVoxOffset = slagVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        EnumVoxelMaterial mat = (EnumVoxelMaterial)voxels[x, y, z];
                        if (mat == EnumVoxelMaterial.Empty) continue;

                        float px = x / 16f;
                        float py = 10/16f + y / 16f;
                        float pz = z / 16f;

                        MeshData mesh = mat == EnumVoxelMaterial.Metal ? metalVoxelMesh : slagVoxelMesh;
                        MeshData meshVoxOffset = mat == EnumVoxelMaterial.Metal ? metVoxOffset : slagVoxOffset;

                        for (int i = 0; i < mesh.xyz.Length; i += 3)
                        {
                            meshVoxOffset.xyz[i] = px + mesh.xyz[i];
                            meshVoxOffset.xyz[i + 1] = py + mesh.xyz[i + 1];
                            meshVoxOffset.xyz[i + 2] = pz + mesh.xyz[i + 2];
                        }

                        float textureSize = 32f / api.BlockTextureAtlas.Size.Width;

                        float offsetX = px * textureSize;
                        float offsetY = (py * 32f) / api.BlockTextureAtlas.Size.Width;
                        float offsetZ = pz * textureSize;

                        for (int i = 0; i < mesh.Uv.Length; i += 2)
                        {
                            meshVoxOffset.Uv[i] = mesh.Uv[i] + GameMath.Mod(offsetX + offsetY, textureSize);
                            meshVoxOffset.Uv[i + 1] = mesh.Uv[i + 1] + GameMath.Mod(offsetZ + offsetY, textureSize);
                        }

                        for (int i = 0; i < meshVoxOffset.CustomBytes.Values.Length; i++)
                        {
                            byte glowSub = (byte)GameMath.Clamp(10 * (Math.Abs(x - 8) + Math.Abs(z - 8) + Math.Abs(y - 2)), 100, 250);
                            meshVoxOffset.CustomBytes.Values[i] = (mat == EnumVoxelMaterial.Metal) ? (byte)0 : glowSub;
                        }

                        workItemMesh.AddMeshData(meshVoxOffset);
                    }
                }
            }

            //workItemMesh.Rgba2 = null;
            workItemMeshRef = api.Render.UploadMesh(workItemMesh);
        }


        private void RegenOutlineMesh(bool[,,] recipeToOutlineVoxels, byte[,,] voxels)
        {
            recipeOutlineMeshRef?.Dispose();

            MeshData recipeOutlineMesh = new MeshData(24, 36, false, false, true, false);
            recipeOutlineMesh.SetMode(EnumDrawMode.Lines);

            int greenCol = (156 << 24) | (100 << 16) | (200 << 8) | (100);
            int orangeCol = (156 << 24) | (219 << 16) | (92 << 8) | (192);
            MeshData greenVoxelMesh = LineMeshUtil.GetCube(greenCol);
            MeshData orangeVoxelMesh = LineMeshUtil.GetCube(orangeCol);
            for (int i = 0; i < greenVoxelMesh.xyz.Length; i++)
            {
                greenVoxelMesh.xyz[i] = greenVoxelMesh.xyz[i] / 32f + 1 / 32f;
                orangeVoxelMesh.xyz[i] = orangeVoxelMesh.xyz[i] / 32f + 1 / 32f;
            }
            MeshData voxelMeshOffset = greenVoxelMesh.Clone();


            int yMax = recipeToOutlineVoxels.GetLength(1);

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        bool requireMetalHere = y >= yMax ? false : recipeToOutlineVoxels[x, y, z];

                        EnumVoxelMaterial mat = (EnumVoxelMaterial)voxels[x, y, z];

                        if (requireMetalHere && mat == EnumVoxelMaterial.Metal) continue;
                        if (!requireMetalHere && mat == EnumVoxelMaterial.Empty) continue;

                        float px = x / 16f;
                        float py = 10/16f + y / 16f;
                        float pz = z / 16f;

                        for (int i = 0; i < greenVoxelMesh.xyz.Length; i += 3)
                        {
                            voxelMeshOffset.xyz[i] = px + greenVoxelMesh.xyz[i];
                            voxelMeshOffset.xyz[i + 1] = py + greenVoxelMesh.xyz[i + 1];
                            voxelMeshOffset.xyz[i + 2] = pz + greenVoxelMesh.xyz[i + 2];
                        }

                        voxelMeshOffset.Rgba = (requireMetalHere && mat == EnumVoxelMaterial.Empty) ? greenVoxelMesh.Rgba : orangeVoxelMesh.Rgba;

                        recipeOutlineMesh.AddMeshData(voxelMeshOffset);
                    }
                }
            }

            recipeOutlineMeshRef = api.Render.UploadMesh(recipeOutlineMesh);
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
