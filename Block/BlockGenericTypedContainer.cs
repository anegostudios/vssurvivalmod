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
        public Size2i AtlasSize { get { return tmpTextureSource.AtlasSize; } }

        string curType;
        string defaultType;
        string variantByGroup;
        string variantByGroupInventory;
        ITexPositionSource tmpTextureSource;
            
        public string Subtype
        {
            get
            {
                return variantByGroup == null ? "" : Variant[variantByGroup];
            }
        }

        public string SubtypeInventory
        {
            get
            {
                return variantByGroupInventory == null ? "" : Variant[variantByGroupInventory];
            }
        }

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
            variantByGroup = Attributes["variantByGroup"].AsString(null);
            variantByGroupInventory = Attributes["variantByGroupInventory"].AsString(null);
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


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val) {
                BlockEntityGenericTypedContainer bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGenericTypedContainer;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.GetOpposite()) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);


                    string type = bect.type;
                    string rotatatableInterval = Attributes?["rotatatableInterval"][type]?.AsString("22.5deg") ?? "22.5deg";

                    if (rotatatableInterval == "22.5degnot45deg")
                    {
                        float rounded90degRad = ((int)Math.Round(angleHor / GameMath.PIHALF)) * GameMath.PIHALF;
                        float deg45rad = GameMath.PIHALF / 4;


                        if (Math.Abs(angleHor - rounded90degRad) >= deg45rad)
                        {
                            bect.MeshAngle = rounded90degRad + 22.5f * GameMath.DEG2RAD * Math.Sign(angleHor - rounded90degRad);
                        }
                        else
                        {
                            bect.MeshAngle = rounded90degRad;
                        }
                    }
                    if (rotatatableInterval == "22.5deg")
                    {
                        float deg22dot5rad = GameMath.PIHALF / 4;
                        float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                        bect.MeshAngle = roundRad;
                    }
                }
            }

            return val;
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<string, MeshRef> meshrefs = new Dictionary<string, MeshRef>();

            string key = "genericTypedContainerMeshRefs" + FirstCodePart() + SubtypeInventory;

            meshrefs = ObjectCacheUtil.GetOrCreate(capi, key, () =>
            {
                Dictionary<string, MeshData> meshes = GenGuiMeshes(capi);

                foreach (var val in meshes)
                {
                    val.Value.Rgba2 = null;
                    meshrefs[val.Key] = capi.Render.UploadMesh(val.Value);
                }

                return meshrefs;
            });
            

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

            string key = "genericTypedContainerMeshRefs" + FirstCodePart() + SubtypeInventory;
            Dictionary<string, MeshRef> meshrefs = ObjectCacheUtil.TryGet<Dictionary<string, MeshRef>>(api, key);
            
            if (meshrefs != null) {
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(key);
            }
        }


        MeshData GenGuiMesh(ICoreClientAPI capi, string type)
        {

            string shapename = this.Attributes["shape"][type].AsString();
            return GenMesh(capi, type, shapename);
        }

        public Dictionary<string, MeshData> GenGuiMeshes(ICoreClientAPI capi)
        {
            string[] types = this.Attributes["types"].AsArray<string>();
            

            Dictionary<string, MeshData> meshes = new Dictionary<string, MeshData>();

            foreach (string type in types)
            {
                string shapename = this.Attributes["shape"][type].AsString();
                meshes[type] = GenMesh(capi, type, shapename, null, ShapeInventory == null ? null : new Vec3f(ShapeInventory.rotateX, ShapeInventory.rotateY, ShapeInventory.rotateZ));
            }

            return meshes;
        }


        public MeshData GenMesh(ICoreClientAPI capi, string type, string shapename, ITesselatorAPI tesselator = null, Vec3f rotation = null)
        {
            if (shapename == null) return new MeshData();
            if (tesselator == null) tesselator = capi.Tesselator;

            tmpTextureSource = tesselator.GetTexSource(this);

            AssetLocation shapeloc = AssetLocation.Create(shapename, Code.Domain).WithPathPrefix("shapes/");
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
            tesselator.TesselateShape("typedcontainer", shape, out mesh, this, rotation == null ? new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ) : rotation);
            return mesh;
        }
        
        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                string shapename = this.Attributes["shape"][be.type].AsString();
                if (shapename == null)
                {
                    base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
                    return;
                }

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


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(world.GetBlock(CodeWithVariant("side", "east")));

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
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;


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



        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            string type = handbookStack.Attributes?.GetString("type");

            if (type == null)
            {
                api.World.Logger.Warning("BlockGenericTypedContainer.GetDropsForHandbook(): type not set for block " + handbookStack.Collectible?.Code);
                return new BlockDropItemStack[0];
            }

            if (Attributes?["drop"]?[type]?.AsBool() == false)
            {
                return new BlockDropItemStack[0]; 
            } else
            {
                return base.GetDropsForHandbook(handbookStack, forPlayer);
            }
        }

        

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { new ItemStack(world.GetBlock(CodeWithVariant("side", "east"))) };
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

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type");

            if (type != null)
            {
                int? qslots = inSlot.Itemstack.ItemAttributes?["quantitySlots"]?[type]?.AsInt(0);
                dsc.AppendLine("\n" + Lang.Get("Quantity Slots: {0}", qslots));
            }
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
                return capi.BlockTextureAtlas.GetRandomColor(tex?.Baked == null ? 0 : tex.Baked.TextureSubId);
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
