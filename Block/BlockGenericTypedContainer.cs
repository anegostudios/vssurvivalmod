using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockGenericTypedContainer : Block, ITexPositionSource
    {
        public int AtlasSize { get { return tmpTextureSource.AtlasSize; } }

        string curType;
        string defaultType;
        ITexPositionSource tmpTextureSource;
             

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                return tmpTextureSource[curType + "-" + textureCode];
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            defaultType = Attributes["defaultType"].AsString("normal-generic");
        }


        public string GetType(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer be = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                return be.type;
            }

            return defaultType;
        }


        public override List<ItemStack> GetHandBookStacks(ICoreClientAPI capi)
        {
            return base.GetHandBookStacks(capi);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<string, MeshRef> meshrefs = new Dictionary<string, MeshRef>();

            object obj;
            if (capi.ObjectCache.TryGetValue("genericTypedContainerMeshRefs" + FirstCodePart(), out obj))
            {
                meshrefs = obj as Dictionary<string, MeshRef>;
            }
            else
            {
                Dictionary<string, MeshData> meshes = GenGuiMeshes(capi);

                foreach (var val in meshes)
                {
                    meshrefs[val.Key] = capi.Render.UploadMesh(val.Value);
                }

                capi.ObjectCache["genericTypedContainerMeshRefs" + FirstCodePart()] = meshrefs;
            }

            string type = itemstack.Attributes.GetString("type", defaultType);

            if (!meshrefs.TryGetValue(type, out renderinfo.ModelRef))
            {
                MeshData mesh = GenGuiMesh(capi, type);
                meshrefs[type] = renderinfo.ModelRef = capi.Render.UploadMesh(mesh);
            }
            
        }




        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("genericTypedContainerMeshRefs" + FirstCodePart(), out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("genericTypedContainerMeshRefs" + FirstCodePart());
            }
        }


        MeshData GenGuiMesh(ICoreClientAPI capi, string type)
        {
            string shapename = this.Attributes["shape"][type].AsString();
            return GenMesh(capi, type, shapename);
        }

        public Dictionary<string, MeshData> GenGuiMeshes(ICoreClientAPI capi)
        {
            string[] types = this.Attributes["types"].AsStringArray();
            

            Dictionary<string, MeshData> meshes = new Dictionary<string, MeshData>();

            foreach (string type in types)
            {
                string shapename = this.Attributes["shape"][type].AsString();
                meshes[type] = GenMesh(capi, type, shapename);
            }

            return meshes;
        }


        public MeshData GenMesh(ICoreClientAPI capi, string type, string shapename, ITesselatorAPI tesselator = null)
        {
            if (shapename == null) return new MeshData();
            if (tesselator == null) tesselator = capi.Tesselator;

            tmpTextureSource = tesselator.GetTexSource(this);

            AssetLocation shapeloc = new AssetLocation(shapename).WithPathPrefix("shapes/");
            Shape shape = capi.Assets.TryGet(shapeloc + ".json")?.ToObject<Shape>();
            if (shape == null)
            {
                shape = capi.Assets.TryGet(shapeloc + "1.json")?.ToObject<Shape>();
            }
            if (shape == null)
            {
                return new MeshData();
            }
            
            curType = type;
            MeshData mesh;
            tesselator.TesselateShape("typedcontainer", shape, out mesh, this, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            return mesh;
        }
        
        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                string shapename = this.Attributes["shape"][be.type].AsString();
                blockModelData = GenMesh(capi, be.type, shapename);

                AssetLocation shapeloc = new AssetLocation(shapename).WithPathPrefix("shapes/");
                Shape shape = capi.Assets.TryGet(shapeloc + ".json")?.ToObject<Shape>();
                if (shape == null)
                {
                    shape = capi.Assets.TryGet(shapeloc + "1.json").ToObject<Shape>();
                }

                MeshData md;
                capi.Tesselator.TesselateShape("typedcontainer-decal", shape, out md, decalTexSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                decalModelData = md;

                string facing = LastCodePart();
                if (facing == "north") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 1 * GameMath.PIHALF, 0); }
                if (facing == "east") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 0 * GameMath.PIHALF, 0); }
                if (facing == "south") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 3 * GameMath.PIHALF, 0); }
                if (facing == "west") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 2 * GameMath.PIHALF, 0); }


                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void DoPlaceBlock(IWorldAccessor world, BlockPos pos, BlockFacing onBlockFace, ItemStack byItemStack)
        {
            base.DoPlaceBlock(world, pos, onBlockFace, byItemStack);
            
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                stack.Attributes.SetString("type", be.type);
            }
            else
            {
                stack.Attributes.SetString("type", defaultType);
            }

            return stack;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                if (this.Attributes["drop"]?[GetType(world.BlockAccessor, pos)]?.AsBool() == true && drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                    }
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
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

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type");
            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + "block-" + type + "-" + Code?.Path);
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            string type = stack.Attributes.GetString("type");
            int? qslots = stack.ItemAttributes?["quantitySlots"]?[type]?.AsInt(0);
            
            dsc.AppendLine("\n" + Lang.Get("Quantity Slots: {0}", qslots));
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntityGenericTypedContainer be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                CompositeTexture tex = null;
                if (!Textures.TryGetValue(be.type + "-lid", out tex)) {
                    Textures.TryGetValue(be.type + "-top", out tex);
                }
                return capi.BlockTextureAtlas.GetRandomPixel(tex?.Baked == null ? 0 : tex.Baked.TextureSubId);
            }

            return base.GetRandomColor(capi, pos, facing);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-chest-open",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
