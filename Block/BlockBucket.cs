using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    // Concept time
    // - Buckets don't directly hold liquids, they contain itemstacks. In case of liquids they are simply "portions" of that liquid. i.e. a "waterportion" item
    //
    // - The item/block that can be placed into the bucket must have the item/block attribute waterTightContainerProps: { containable: true, itemsPerLitre: 1 }
    //   'itemsPerLitre' defines how many items constitute one litre.

    // - Further item/block more attributes lets you define if a liquid can be obtained from a block source and what should come out when spilled:
    //   - waterTightContainerProps: { containable: true, whenSpilled: { action: "placeblock", stack: { class: "block", code: "water-7" } }  }
    //   or
    //   - waterTightContainerProps: { containable: true, whenSpilled: { action: "dropcontents", stack: { class: "item", code: "honeyportion" } }  }
    // 
    // - BlockBucket has methods for placing/taking liquids from a bucket stack or a placed bucket block
    public class BlockBucket : BlockLiquidContainerBase
    {
        public override float CapacityLitres => 10;


        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue("bucketMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache["bucketMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            ItemStack contentStack = GetContent(capi.World, itemstack);
            if (contentStack == null) return;

            int hashcode = GetBucketHashCode(capi.World, contentStack);

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(hashcode, out meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                meshdata.Rgba2 = null;
                meshrefs[hashcode] = meshRef = capi.Render.UploadMesh(meshdata);

            }

            renderinfo.ModelRef = meshRef;
        }



        public int GetBucketHashCode(IClientWorldAccessor world, ItemStack contentStack)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }



        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("bucketMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("bucketMeshRefs");
            }
        }



        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = capi.Assets.TryGet("shapes/block/wood/bucket/empty.json").ToObject<Shape>();
            MeshData bucketmesh;
            capi.Tesselator.TesselateShape(this, shape, out bucketmesh);

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetStackProps(contentStack);

                ContainerTextureSource contentSource = new ContainerTextureSource(capi, contentStack, props.Texture);
                shape = capi.Assets.TryGet("shapes/block/wood/bucket/contents.json").ToObject<Shape>();
                MeshData contentMesh;
                capi.Tesselator.TesselateShape("bucket", shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                
                contentMesh.Translate(0, GameMath.Min(7 / 16f, contentStack.StackSize / props.ItemsPerLitre * 0.7f / 16f), 0);

                if (props.TintIndex > 0)
                {
                    int col = capi.ApplyColorTintOnRgba(props.TintIndex, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.ApplyColorTintOnRgba(props.TintIndex, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }

                for (int i = 0; i < contentMesh.Flags.Length; i++)
                {
                    contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }

                bucketmesh.AddMeshData(contentMesh);

                // Water flags
                if (forBlockPos != null)
                {
                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount);
                    bucketmesh.CustomInts.Count = bucketmesh.FlagsCount;

                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2);
                    bucketmesh.CustomFloats.Count = bucketmesh.FlagsCount * 2;
                }
            }
            

            return bucketmesh;
        }

        #endregion


    }
}
