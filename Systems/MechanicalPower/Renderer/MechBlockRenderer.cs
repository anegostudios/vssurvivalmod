using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// Represents a single GPU mesh upload tied to a specific texture atlas.
    /// </summary>
    public struct MeshGroup
    {
        public MeshRef Ref;
        public int AtlasTextureId;
    }

    public abstract class MechBlockRenderer
    {
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

        /// <summary>
        /// Splits mesh by texture atlas ID and uploads each part as a MeshGroup.
        /// All resulting groups share the same CustomMeshDataPartFloat (instanced transform buffer),
        /// so a single UpdateLightAndTransformMatrix call covers all atlas parts of the same logical mesh.
        /// </summary>
        protected List<MeshGroup> UploadMeshGrouped(MeshData mesh, CustomMeshDataPartFloat floats)
        {
            var groups = new List<MeshGroup>();

            MeshData[] splitMeshes = mesh.SplitByTextureId();
            for (int i = 0; i < splitMeshes.Length; i++)
            {
                MeshData split = splitMeshes[i];
                if (split == null || split.VerticesCount == 0) continue;

                split.CustomFloats = floats;

                int atlasId = (split.TextureIds != null && split.TextureIds.Length > 0)
                    ? split.TextureIds[0]
                    : capi.BlockTextureAtlas.Positions[0].atlasTextureId;

                groups.Add(new MeshGroup
                {
                    Ref = capi.Render.UploadMesh(split),
                    AtlasTextureId = atlasId
                });
            }

            return groups;
        }

        /// <summary>
        /// Updates the instanced float buffer and issues one instanced draw call per atlas group.
        /// Call this instead of manual UpdateMesh + RenderMeshInstanced in OnRenderFrame.
        /// </summary>
        protected void RenderGroups(IShaderProgram prog, List<MeshGroup> groups, CustomMeshDataPartFloat floats, int quantity)
        {
            floats.Count = quantity * 20;
            updateMesh.CustomFloats = floats;

            for (int i = 0; i < groups.Count; i++)
            {
                MeshGroup group = groups[i];
                prog.BindTexture2D("tex", group.AtlasTextureId, 0);
                capi.Render.UpdateMesh(group.Ref, updateMesh);
                capi.Render.RenderMeshInstanced(group.Ref, quantity);
            }
        }

        /// <summary>
        /// Disposes all MeshRefs in a group list and clears it.
        /// </summary>
        protected static void DisposeGroups(List<MeshGroup> groups)
        {
            if (groups == null) return;
            for (int i = 0; i < groups.Count; i++)
                groups[i].Ref?.Dispose();
            groups.Clear();
        }

        protected virtual void UpdateCustomFloatBuffer()
        {
            Vec3d pos = capi.World.Player.Entity.CameraPos;

            int i = 0;
            foreach (var dev in renderedDevices.Values)
            {
                // Double-precision subtraction is critical here: block positions can be very large
                // (e.g. 50000), but the camera-relative offset must stay accurate to sub-block scale.
                tmp.Set(
                    (float)(dev.Position.X - pos.X),
                    (float)(dev.Position.InternalY - pos.Y),
                    (float)(dev.Position.Z - pos.Z)
                );

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

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
                values[++j] = tmpMat[i];
        }

        public virtual void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();
        }

        public virtual void Dispose() { }
    }
}
