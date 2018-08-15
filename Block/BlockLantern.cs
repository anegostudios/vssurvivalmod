using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLantern : Block, ITexPositionSource
    {
        public int AtlasSize { get; set; }

        string curMat, curLining;
        ITexPositionSource tmpTextureSource;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "material") return tmpTextureSource[curMat];
                if (textureCode == "material-deco") return tmpTextureSource["deco-" + curMat];
                if (textureCode == "lining") return tmpTextureSource[curLining == "plain" ? curMat : curLining];
                return tmpTextureSource[textureCode];
            }
        }


        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos != null)
            {
                BELantern be = blockAccessor.GetBlockEntity(pos) as BELantern;
                if (be != null)
                {
                    return be.GetLightHsv();
                }
            }
            if (stack != null)
            {
                string lining = stack.Attributes.GetString("lining");

                byte[] lightHsv = new byte[] { this.LightHsv[0], this.LightHsv[1], (byte)(this.LightHsv[2] + (lining != "plain" ? 2 : 0)) };
                return lightHsv;
            }

            return base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            
            Dictionary<string, MeshRef> meshrefs = new Dictionary<string, MeshRef>();

            object obj;
            if (capi.ObjectCache.TryGetValue("blockLanternGuiMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<string, MeshRef>;
            } else
            {
                Dictionary<string, MeshData> lanternMeshes = GenGuiMeshes(capi);

                foreach (var val in lanternMeshes)
                {
                    meshrefs[val.Key] = capi.Render.UploadMesh(val.Value);
                }

                capi.ObjectCache["blockLanternGuiMeshRefs"] = meshrefs;
            }

            string material = itemstack.Attributes.GetString("material");
            string lining = itemstack.Attributes.GetString("lining");

            meshrefs.TryGetValue(material + "-" + lining, out renderinfo.ModelRef);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("blockLanternGuiMeshRefs", out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("blockLanternGuiMeshRefs");
            }
        }



        public Dictionary<string, MeshData> GenGuiMeshes(ICoreClientAPI capi)
        {
            string[] materials = new string[] { "copper", "brass", "bismuth", "blackbronze", "tinbronze", "bismuthbronze", "iron", "molybdochalkos", "silver", "gold" };
            string[] linings = new string[] { "plain", "silver", "gold" };
            AssetLocation shapeloc = Shape.Base.Clone().WithPathPrefix("shapes/").WithPathAppendix(".json");
            Shape shape = capi.Assets.TryGet(shapeloc).ToObject<Shape>();

            Dictionary<string, MeshData> meshes = new Dictionary<string, MeshData>();

            foreach (string mat in materials)
            {
                foreach (string lining in linings)
                {
                    if (mat == lining) continue;
                    meshes[mat + "-" + lining] = GenMesh(capi, mat, lining, shape);
                }
            }

            return meshes;
        }


        public MeshData GenMesh(ICoreClientAPI capi, string material, string lining, Shape shape = null)
        {
            tmpTextureSource = capi.Tesselator.GetTexSource(this);

            if (shape == null)
            {
                shape = capi.Assets.TryGet("shapes/" + this.Shape.Base.Path + ".json").ToObject<Shape>();
            }

            this.AtlasSize = capi.BlockTextureAtlas.Size;
            curMat = material;
            curLining = lining;
            MeshData mesh;
            capi.Tesselator.TesselateShape("blocklantern", shape, out mesh, this, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            return mesh;
        }


        public override void DoPlaceBlock(IWorldAccessor world, BlockPos pos, BlockFacing onBlockFace, ItemStack byItemStack)
        {
            base.DoPlaceBlock(world, pos, onBlockFace, byItemStack);

            //string sdf = this.Code.Path;

            BELantern be = world.BlockAccessor.GetBlockEntity(pos) as BELantern;
            if (be != null)
            {
                string material = byItemStack.Attributes.GetString("material");
                string lining = byItemStack.Attributes.GetString("lining");
                be.DidPlace(material, lining);
            }
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BELantern be = world.BlockAccessor.GetBlockEntity(pos) as BELantern;
            if (be != null)
            {
                stack.Attributes.SetString("material", be.material);
                stack.Attributes.SetString("lining", be.lining);
            } else
            {
                stack.Attributes.SetString("material", "copper");
                stack.Attributes.SetString("lining", "plain");
            }

            return stack;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                    }
                }

                if (Sounds?.Break != null)
                {
                    world.PlaySoundAt(Sounds.Break, pos.X, pos.Y, pos.Z, byPlayer);
                }
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken();
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            string material = stack.Attributes.GetString("material");
            string lining = stack.Attributes.GetString("lining");

            dsc.AppendLine(Lang.Get("Material: {0}", Lang.Get("material-" + material)));
            dsc.AppendLine(Lang.Get("Lining: {0}", lining == "plain" ? "-" : Lang.Get("material-" + lining)));
        }


        public override int TextureSubIdForRandomBlockPixel(IWorldAccessor world, BlockPos pos, BlockFacing facing, ref int tintIndex)
        {
            BELantern be = world.BlockAccessor.GetBlockEntity(pos) as BELantern;
            if (be != null)
            {
                CompositeTexture tex = null;
                if (Textures.TryGetValue(be.material, out tex)) return tex.Baked.TextureSubId;
            }

            return base.TextureSubIdForRandomBlockPixel(world, pos, facing, ref tintIndex);
        }
    }
}
