using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IContainedInteractable
    {
        bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
        bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
        void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
    }

    public interface IContainedCustomName
    {
        string GetContainedInfo(ItemSlot inSlot);

        /// <summary>
        /// Return null to use default title
        /// </summary>
        /// <param name="inSlot"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        string GetContainedName(ItemSlot inSlot, int quantity);
    }


    public interface IContainedMeshSource
    {
        MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos);

        /// <summary>
        /// Needs to return a unique identifier for this itemstack
        /// </summary>
        /// <param name="itemstack"></param>
        /// <returns></returns>
        string GetMeshCacheKey(ItemStack itemstack);
    }

    public abstract class BlockEntityDisplay : BlockEntityContainer, ITexPositionSource
    {
        protected CollectibleObject nowTesselatingObj;
        protected Shape nowTesselatingShape;
        protected ICoreClientAPI capi;
        protected MeshData[] meshes;
        

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public virtual string AttributeTransformCode => "onDisplayTransform";

        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                IDictionary<string, CompositeTexture> textures = nowTesselatingObj is Item item ? item.Textures : (nowTesselatingObj as Block).Textures;
                AssetLocation texturePath = null;
                CompositeTexture tex;

                // Prio 1: Get from collectible textures
                if (textures.TryGetValue(textureCode, out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 2: Get from collectible textures, use "all" code
                if (texturePath == null && textures.TryGetValue("all", out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 3: Get from currently tesselating shape
                if (texturePath == null)
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
                }

                // Prio 4: The code is the path
                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                return getOrCreateTexPos(texturePath);
            }
        }


        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

            if (texpos == null)
            {
                IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => texAsset.ToBitmap(capi));
                }
                else
                {
                    capi.World.Logger.Warning("For render in block " + Block.Code + ", item {0} defined texture {1}, no such texture found.", nowTesselatingObj.Code, texturePath);
                }
            }

            return texpos;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                updateMeshes();
            }
        }




        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null) continue;
                mesher.AddMeshData(meshes[i]);
            }

            return false;
        }




        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client && Api != null)
            {
                updateMeshes();
            }
        }

        public virtual void updateMeshes()
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                updateMesh(i);
            }
        }

        protected virtual void updateMesh(int index)
        {
            if (Api == null || Api.Side == EnumAppSide.Server) return;
            if (Inventory[index].Empty)
            {
                meshes[index] = null;
                return;
            }

            MeshData mesh = genMesh(Inventory[index].Itemstack);
            TranslateMesh(mesh, index);
            meshes[index] = mesh;
        }

        protected virtual MeshData genMesh(ItemStack stack)
        {
            MeshData mesh;
            var meshSource = stack.Collectible as IContainedMeshSource;

            if (meshSource != null)
            {
                mesh = meshSource.GenMesh(stack, capi.BlockTextureAtlas, Pos);
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, Block.Shape.rotateY * GameMath.DEG2RAD, 0);
            }
            else
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                if (stack.Class == EnumItemClass.Block)
                {
                    mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
                }
                else
                {
                    nowTesselatingObj = stack.Collectible;
                    nowTesselatingShape = null;
                    if (stack.Item.Shape != null)
                    {
                        nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                    }
                    capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

                    mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
                }
            }

            if (stack.Collectible.Attributes?[AttributeTransformCode].Exists == true)
            {
                ModelTransform transform = stack.Collectible.Attributes?[AttributeTransformCode].AsObject<ModelTransform>();
                transform.EnsureDefaultValues();
                mesh.ModelTransform(transform);

                transform.Rotation.X = 0;
                transform.Rotation.Y = Block.Shape.rotateY;
                transform.Rotation.Z = 0;
                mesh.ModelTransform(transform);
            }

            if (stack.Class == EnumItemClass.Item && (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture))
            {
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), GameMath.PIHALF, 0, 0);
                mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.33f, 0.5f, 0.33f);
                mesh.Translate(0, -7.5f / 16f, 0f);
            }

            return mesh;
        }

        public virtual void TranslateMesh(MeshData mesh, int index)
        {

        }
    }

}
