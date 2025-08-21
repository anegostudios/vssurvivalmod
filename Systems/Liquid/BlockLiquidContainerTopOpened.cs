using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// For liquid containers that are open on the top and thus need render their contents
    /// </summary>
    public class BlockLiquidContainerTopOpened : BlockLiquidContainerBase, IContainedMeshSource, IContainedCustomName
    {
        LiquidTopOpenContainerProps Props;
        protected virtual string meshRefsCacheKey => Code.ToShortString() + "meshRefs";
        protected virtual AssetLocation emptyShapeLoc => Props.EmptyShapeLoc;
        protected virtual AssetLocation contentShapeLoc => Props.OpaqueContentShapeLoc;
        protected virtual AssetLocation liquidContentShapeLoc => Props.LiquidContentShapeLoc;

        public override float TransferSizeLitres => Props.TransferSizeLitres;

        public override float CapacityLitres => Props.CapacityLitres;

        public override bool CanDrinkFrom => true;
        public override bool IsTopOpened => true;
        public override bool AllowHeldLiquidTransfer => true;

        /// <summary>
        /// Max fill height
        /// </summary>
        protected virtual float liquidMaxYTranslate => Props.LiquidMaxYTranslate;
        protected virtual float liquidYTranslatePerLitre => liquidMaxYTranslate / CapacityLitres;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Props = new LiquidTopOpenContainerProps();
            if (Attributes?["liquidContainerProps"].Exists == true)
            {
                Props = Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
            }
        }

        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;

            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out object obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs = new Dictionary<int, MultiTextureMeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;


            int hashcode = GetStackCacheHashCode(contentStack);

            if (!meshrefs.TryGetValue(hashcode, out MultiTextureMeshRef meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }


            renderinfo.ModelRef = meshRef;
        }


        protected int GetStackCacheHashCode(ItemStack contentStack)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }



        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out object obj))
            {
                Dictionary<int, MultiTextureMeshRef> meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        MeshData origcontainermesh;
        Shape contentShape;
        Shape liquidContentShape;

        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            if (origcontainermesh == null)
            {
                Shape shape = API.Common.Shape.TryGet(capi, emptyShapeLoc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                if (shape == null)
                {
                    capi.World.Logger.Error("Empty shape {0} not found. Liquid container {1} will be invisible.", emptyShapeLoc, Code);
                    return new MeshData();
                }
                capi.Tesselator.TesselateShape(this, shape, out origcontainermesh, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }

            MeshData containerMesh = origcontainermesh.Clone();

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);
                if (props == null)
                {
                    capi.World.Logger.Error("Contents ('{0}') has no liquid properties, contents of liquid container {1} will be invisible.", contentStack.GetName(), Code);
                    return containerMesh;
                }

                ContainerTextureSource contentSource = new ContainerTextureSource(capi, contentStack, props.Texture);

                var shape = props.IsOpaque ? contentShape : liquidContentShape;
                var loc = props.IsOpaque ? contentShapeLoc : liquidContentShapeLoc;
                if (shape == null)
                {
                    shape = API.Common.Shape.TryGet(capi, loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));

                    if (props.IsOpaque) this.contentShape = shape;
                    else this.liquidContentShape = shape;
                }

                if (shape == null)
                {
                    capi.World.Logger.Error("Content shape {0} not found. Contents of liquid container {1} will be invisible.", loc, Code);
                    return containerMesh;
                }

                capi.Tesselator.TesselateShape(GetType().Name, shape, out MeshData contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), props.GlowLevel);

                contentMesh.Translate(0, GameMath.Min(liquidMaxYTranslate, contentStack.StackSize / props.ItemsPerLitre * liquidYTranslatePerLitre), 0);

                if (props.ClimateColorMap != null)
                {
                    int col;
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }
                    else
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }

                for (int i = 0; i < contentMesh.FlagsCount; i++)
                {
                    contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }

                containerMesh.AddMeshData(contentMesh);

                // Water flags
                if (forBlockPos != null)
                {
                    containerMesh.CustomInts = new CustomMeshDataPartInt(containerMesh.FlagsCount);
                    containerMesh.CustomInts.Count = containerMesh.FlagsCount;
                    containerMesh.CustomInts.Values.Fill(VertexFlags.LiquidWeakFoamBitMask); // light foam only

                    containerMesh.CustomFloats = new CustomMeshDataPartFloat(containerMesh.FlagsCount * 2);
                    containerMesh.CustomFloats.Count = containerMesh.FlagsCount * 2;
                }
            }


            return containerMesh;
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            ItemStack contentStack = GetContent(itemstack);
            return GenMesh(api as ICoreClientAPI, contentStack, forBlockPos);
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            var contentStack = GetContent(itemstack);
            string s = itemstack.Collectible.Code.ToShortString() + "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
            return s;
        }

        public string GetContainedInfo(ItemSlot inSlot)
        {
            float litres = GetCurrentLitres(inSlot.Itemstack);
            ItemStack contentStack = GetContent(inSlot.Itemstack);

            if (litres <= 0) return Lang.GetWithFallback("contained-empty-container", "{0} (Empty)", inSlot.Itemstack.GetName());

            string incontainername = Lang.Get(contentStack.Collectible.Code.Domain + ":incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);

            return Lang.Get("contained-liquidcontainer-compact", inSlot.Itemstack.GetName(), litres, incontainername, PerishableInfoCompactContainer(api, inSlot));
        }

        public string GetContainedName(ItemSlot inSlot, int quantity)
        {
            return inSlot.Itemstack.GetName();
        }

        #endregion
    }


}
