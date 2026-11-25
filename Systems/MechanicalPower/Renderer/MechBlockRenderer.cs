using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class MechBlockRenderer
    {
        // 16 floats matrix, 4 floats light rgbs
        public const int InstanceDataSize = 16 + 4;
        // The same followed by a 2 float atlas UV offset, for meshes shared by several materials.
        public const int MaterialInstanceDataSize = InstanceDataSize + 2;
        public const int MaxInstances = 10100;
        public const int InstanceBufferSize = InstanceDataSize * MaxInstances;

        protected ICoreClientAPI capi;

        protected MeshData updateMesh = new MeshData();

        protected int quantityBlocks = 0;

        protected float[] tmpMat = Mat4f.Create();
        protected double[] quat = Quaterniond.Create();
        protected float[] rotMat = Mat4f.Create();
        protected MechanicalPowerMod mechanicalPowerMod;

        protected Dictionary<BlockPos, IMechanicalPowerRenderable> renderedDevices = new Dictionary<BlockPos, IMechanicalPowerRenderable>();

        protected Vec3f tmp = new Vec3f();

        public MechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;
        }

        // The instanced shader samples one atlas page per draw call, so each page the mesh uses becomes its own part.
        protected MultiTextureMeshRef uploadInstancedMesh(MeshData mesh, CustomMeshDataPartFloat floats)
        {
            MeshData[] parts = mesh.SplitByTextureId();
            MeshRef[] meshRefs = new MeshRef[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].CustomFloats = floats;
                meshRefs[i] = capi.Render.UploadMesh(parts[i]);
            }

            return new MultiTextureMeshRef(meshRefs, mesh.TextureIds);
        }

        protected static CustomMeshDataPartFloat createMaterialInstanceFloats()
        {
            CustomMeshDataPartFloat floats = new(MaterialInstanceDataSize * MaxInstances)
            {
                Instanced = true,
                InterleaveOffsets = [0, 16, 32, 48, 64, 80],
                InterleaveSizes = [4, 4, 4, 4, 4, 2],
                InterleaveStride = MaterialInstanceDataSize * sizeof(float),
                StaticDraw = false
            };
            floats.SetAllocationSize(MaterialInstanceDataSize * MaxInstances);
            return floats;
        }
        
        protected List<(string MaterialTextureCode, MeshData Mesh)> tesselateMaterialPieces(Block block, Shape shape, Vec3f rotation, int? quantityElements = null, string[] selectiveElements = null, MechanicalMaterialProperties materialProperties = null, ITexPositionSource textureSource = null)
        {
            materialProperties ??= (block as BlockMPBase)?.MaterialProperties;
            textureSource ??= capi.Tesselator.GetTextureSource(block);
            if (materialProperties == null)
            {
                capi.Tesselator.TesselateShape(block.Code + " shape", shape, out MeshData mesh, textureSource, rotation, quantityElements: quantityElements, selectiveElements: selectiveElements);
                return [(null, mesh)];
            }

            capi.Tesselator.TesselateShape(block.Code + " fixed texture", materialProperties.GetMaterialRenderShape(shape, null), out MeshData fixedMesh, textureSource, rotation, quantityElements: quantityElements, selectiveElements: selectiveElements);
            List<(string MaterialTextureCode, MeshData Mesh)> pieces = [(null, fixedMesh)];
            foreach (string textureCode in materialProperties.MaterialTextureCodes)
            {
                Shape materialShape = materialProperties.GetMaterialRenderShape(shape, textureCode);
                ITexPositionSource materialSource = materialProperties.GetMaterialRenderTextureSource(capi, textureSource, textureCode);
                capi.Tesselator.TesselateShape(block.Code + " material texture " + textureCode, materialShape, out MeshData materialMesh, materialSource, rotation, quantityElements: quantityElements, selectiveElements: selectiveElements);
                pieces.Add((textureCode, materialMesh));
            }
            return pieces;
        }

        // The pieces of one typed mesh, drawn with a shared instance count
        protected class MaterialMeshPool
        {
            public readonly List<(MultiTextureMeshRef MeshRef, CustomMeshDataPartFloat Floats, string MaterialTextureCode)> Pieces = [];
            public int Count;
        }

        protected MaterialMeshPool createMaterialMeshPool(Block block, Shape shape, Vec3f rotation, string[] selectiveElements = null, MechanicalMaterialProperties materialProperties = null)
        {
            return uploadMaterialMeshPool(tesselateMaterialPieces(block, shape, rotation, selectiveElements: selectiveElements, materialProperties: materialProperties));
        }

        protected MaterialMeshPool uploadMaterialMeshPool(List<(string MaterialTextureCode, MeshData Mesh)> meshes)
        {
            MaterialMeshPool pool = new();
            foreach ((string materialTextureCode, MeshData mesh) in meshes)
            {
                if (mesh.VerticesCount == 0) continue;

                CustomMeshDataPartFloat floats = createMaterialInstanceFloats();
                pool.Pieces.Add((uploadInstancedMesh(mesh, floats), floats, materialTextureCode));
            }
            return pool;
        }

        protected void renderMaterialMeshPool(IShaderProgram prog, MaterialMeshPool pool)
        {
            if (pool.Count <= 0) return;

            foreach (var piece in pool.Pieces) renderInstancedMesh(prog, piece.MeshRef, piece.Floats, pool.Count);
        }

        protected static void disposeMaterialMeshPool(MaterialMeshPool pool)
        {
            foreach (var piece in pool.Pieces) piece.MeshRef.Dispose();
        }

        protected static void setMaterialInstance(float[] values, int index, float[] lightAndTransform, Vec2f uvOffset)
        {
            int start = index * MaterialInstanceDataSize;
            Array.Copy(lightAndTransform, 0, values, start, InstanceDataSize);
            values[start + InstanceDataSize] = uvOffset.X;
            values[start + InstanceDataSize + 1] = uvOffset.Y;
        }

        protected void renderInstancedMesh(IShaderProgram prog, MultiTextureMeshRef meshRef, CustomMeshDataPartFloat floats, int count)
        {
            int instanceDataSize = floats.InterleaveStride / sizeof(float);
            floats.Count = count * instanceDataSize;
            updateMesh.CustomFloats = floats;
            prog.Uniform("useUvOffset", instanceDataSize == MaterialInstanceDataSize ? 1 : 0);
            for (int i = 0; i < meshRef.meshrefs.Length; i++)
            {
                prog.BindTexture2D("tex", meshRef.textureids[i], 0);
                capi.Render.UpdateMesh(meshRef.meshrefs[i], updateMesh);
                capi.Render.RenderMeshInstanced(meshRef.meshrefs[i], count);
            }
        }
        
        public void AddDevice(IMechanicalPowerRenderable device)
        {
            renderedDevices[device.Position] = device;
            quantityBlocks = renderedDevices.Count;
        }

        public bool RemoveDevice(IMechanicalPowerRenderable device)
        {
            bool ok = renderedDevices.Remove(device.Position);
            quantityBlocks = renderedDevices.Count;
            return ok;
        }

        protected virtual void UpdateCustomFloatBuffer()
        {
            Vec3d pos = capi.World.Player.Entity.CameraPos;

            int i = 0;
            foreach (var dev in renderedDevices.Values)
            {
                //double precision int-double subtraction is needed here (even though the desired result is a float).  
                // It's needed to have enough significant figures in the result, as the integer size could be large e.g. 50000 but the difference should be small (can easily be less than 5)
                tmp.Set((float)(dev.Position.X - pos.X), (float)(dev.Position.InternalY - pos.Y), (float)(dev.Position.Z - pos.Z));  

                UpdateLightAndTransformMatrix(i, tmp, dev.AngleRad % GameMath.TWOPI, dev);
                i++;
            }
        }

        protected abstract void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev);

        protected virtual void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotY, float rotZ)
        {
            Mat4f.Identity(tmpMat);

            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + 0.5f, distToCamera.Y + 0.5f, distToCamera.Z + 0.5f);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotY != 0f) Quaterniond.RotateY(quat, quat, rotY);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            Mat4f.MulQuat(tmpMat, quat);

            Mat4f.Translate(tmpMat, tmpMat, -0.5f, -0.5f, -0.5f);

            int j = index * InstanceDataSize;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
            {
                values[++j] = tmpMat[i];
            }
        }


        public virtual void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();
        }

        public virtual void Dispose()
        {

        }
    }
}

