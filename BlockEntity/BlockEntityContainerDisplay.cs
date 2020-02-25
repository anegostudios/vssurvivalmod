using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public abstract class BlockEntityDisplay : BlockEntityContainer, ITexPositionSource
    {
        
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath=null;
                CompositeTexture tex;
                if (nowTesselatingItem.Textures.TryGetValue(textureCode, out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }
                else
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
                }

                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

                

                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);
                        capi.BlockTextureAtlas.InsertTextureCached(texturePath, bmp, out _, out texpos);
                    }
                    else
                    {
                        capi.World.Logger.Warning("Display cased item {0} defined texture {1}, not no such texture found.", nowTesselatingItem.Code, texturePath);
                    }
                }

                return texpos;
            }
        }

        protected Item nowTesselatingItem;
        protected Shape nowTesselatingShape;
        protected ICoreClientAPI capi;
        protected MeshData[] meshes;




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




        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client && Api != null)
            {
                updateMeshes();
            }
        }

        protected virtual void updateMeshes()
        {
            for (int i = 0; i < Inventory.Count; i++)
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

            ItemStack stack = Inventory[index].Itemstack;

            MeshData mesh = genMesh(stack, index);

            translateMesh(mesh, index);

            meshes[index] = mesh;
        }

        protected virtual MeshData genMesh(ItemStack stack, int index)
        {
            MeshData mesh;
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (stack.Class == EnumItemClass.Block)
            {
                mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
            }
            else
            {
                nowTesselatingItem = stack.Item;
                nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

                if (stack.Item.Shape.VoxelizeTexture)
                {
                    mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), GameMath.PIHALF, 0, 0);
                    mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.33f, 0.5f, 0.33f);
                    mesh.Translate(0, -7.5f / 16f, 0f);
                }

                mesh.RenderPasses.Fill((int)EnumChunkRenderPass.BlendNoCull);

            }

            return mesh;
        }

        protected virtual void translateMesh(MeshData mesh, int index)
        {

        }
    }

}
