using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class CachedModel
    {
        public MultiTextureMeshRef MeshRef;
        public float Age;
    }


    public class MicroBlockModelCache : ModSystem
    {
        Dictionary<long, CachedModel> cachedModels = new Dictionary<long, CachedModel>();
        long nextMeshId = 1;

        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.RegisterGameTickListener(OnSlowTick, 1000);
        }

        private void OnSlowTick(float dt)
        {
            List<long> toDelete = new List<long>();

            foreach (var val in cachedModels)
            {
                val.Value.Age++;

                if (val.Value.Age > 180)
                {
                    toDelete.Add(val.Key);
                }
            }

            foreach (long key in toDelete)
            {
                cachedModels[key].MeshRef.Dispose();
                cachedModels.Remove(key);
            }
        }

        public MultiTextureMeshRef GetOrCreateMeshRef(ItemStack forStack)
        {
            long meshid = forStack.Attributes.GetLong("meshId", 0);
            
            if (!cachedModels.ContainsKey(meshid))
            {
                MultiTextureMeshRef meshref = CreateModel(forStack);
                forStack.Attributes.SetLong("meshId", nextMeshId);
                cachedModels[nextMeshId++] = new CachedModel() { MeshRef = meshref, Age = 0 };
                return meshref;
            } else
            {
                cachedModels[meshid].Age = 0;
                return cachedModels[meshid].MeshRef;
            }
        }


        private MultiTextureMeshRef CreateModel(ItemStack forStack)
        {
            return capi.Render.UploadMultiTextureMesh(GenMesh(forStack));
        }

        public MeshData GenMesh(ItemStack forStack)
        {
            ITreeAttribute tree = forStack.Attributes;
            if (tree == null) tree = new TreeAttribute();
            int[] materials = BlockEntityMicroBlock.MaterialIdsFromAttributes(tree, capi.World);
            uint[] cuboids = (tree["cuboids"] as IntArrayAttribute)?.AsUint;

            // When loaded from json
            if (cuboids == null)
            {
                cuboids = (tree["cuboids"] as LongArrayAttribute)?.AsUint;
            }

            List<uint> voxelCuboids = cuboids == null ? new List<uint>() : new List<uint>(cuboids);

            var firstblock = capi.World.Blocks[materials[0]];
            bool collBoxCuboid = firstblock.Attributes?.IsTrue("chiselShapeFromCollisionBox") == true;
            uint[] originalCuboids = null;
            if (collBoxCuboid)
            {
                Cuboidf[] collboxes = firstblock.CollisionBoxes;
                originalCuboids = new uint[collboxes.Length];

                for (int i = 0; i < collboxes.Length; i++)
                {
                    Cuboidf box = collboxes[i];
                    var uintbox = BlockEntityMicroBlock.ToUint((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1), (int)(16 * box.X2), (int)(16 * box.Y2), (int)(16 * box.Z2), 0);
                    originalCuboids[i] = uintbox;
                }
            }

            MeshData mesh = BlockEntityMicroBlock.CreateMesh(capi, voxelCuboids, materials, null, null, originalCuboids, 0);
            mesh.Rgba.Fill((byte)255);
            return mesh;
        }


        private void Event_LeaveWorld()
        {
            foreach (var val in cachedModels)
            {
                val.Value.MeshRef.Dispose();
            }
        }
    }
}
