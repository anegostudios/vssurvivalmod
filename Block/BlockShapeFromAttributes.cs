using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class ShapeTextureSource : ITexPositionSource
    {
        ICoreClientAPI capi;
        Shape shape;
        Dictionary<string, TextureAtlasPosition> texturePositions = new Dictionary<string, TextureAtlasPosition>();
        public TextureAtlasPosition firstTexPos;

        public ShapeTextureSource(ICoreClientAPI capi, Shape shape)
        {
            this.capi = capi;
            this.shape = shape;
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (!texturePositions.TryGetValue(textureCode, out var texPos))
                {
                    capi.BlockTextureAtlas.GetOrInsertTexture(shape.Textures[textureCode], out _, out texPos);

                    if (texPos == null)
                    {
                        return capi.BlockTextureAtlas.UnknownTexturePosition;
                    }
                }

                if (this.firstTexPos == null)
                {
                    this.firstTexPos = texPos;
                }

                return texPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }

    public interface IShapeTypeProps
    {
        string HashKey { get; }

        string Code { get; }
        Vec3f Rotation { get; }
        Cuboidf[] ColSelBoxes { get; set; }
        ModelTransform GuiTf { get; set; }
        ModelTransform FpTf { get; set; }
        ModelTransform TpTf { get; set; }
        ModelTransform GroundTf { get; set; }

        string RotInterval { get; }

        string firstTexture { get; set; }
        TextureAtlasPosition texPos { get; set; }
        Dictionary<int, Cuboidf[]> ColSelBoxesByDeg { get; }

        AssetLocation ShapePath { get; }
        Shape ShapeResolved { get; set; }
    }

    public abstract class BlockShapeFromAttributes : Block
    {

        bool colSelBoxEditMode;
        bool transformEditMode;

        public abstract string ClassType { get; }
        public abstract IEnumerable<IShapeTypeProps> AllTypes { get; }

        public abstract void LoadTypes();

        public abstract IShapeTypeProps GetTypeProps(string code, ItemStack stack, BlockEntityShapeFromAttributes be);

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi)
            {
                capi.Event.RegisterEventBusListener(OnEventBusEvent);
                foreach (var type in AllTypes)
                {
                    if (!Textures.TryGetValue(type.Code + ":" + type.firstTexture, out CompositeTexture ct)) continue;
                    type.texPos = capi.BlockTextureAtlas[ct.Baked.BakedName];
                }
            }
            else LoadTypes();   // Client side types are already loaded in OnCollectTextures - which MUST be implemented!
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            if (api is ICoreClientAPI capi)
            {
                Dictionary<string, MeshRef> clutterMeshRefs = ObjectCacheUtil.TryGet<Dictionary<string, MeshRef>>(capi, ClassType + "MeshesInventory");
                if (clutterMeshRefs != null)
                {
                    foreach (MeshRef mesh in clutterMeshRefs.Values) mesh.Dispose();
                    ObjectCacheUtil.Delete(capi, ClassType + "MeshesInventory");
                }
            }
        }

        public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict)
        {
            this.api = api;
            LoadTypes();
            foreach (var type in AllTypes)
            {
                type.ShapeResolved = api.Assets.TryGet(type.ShapePath)?.ToObject<Shape>();
                if (type.ShapeResolved == null)
                {
                    api.Logger.Error("Could not find clutter/bookshelf shape " + type.ShapePath);
                    continue;
                }
                var textures = new FakeDictionary<string, CompositeTexture>(1);
                textureDict.CollectAndBakeTexturesFromShape(type.ShapeResolved, textures, type.ShapePath);
                type.firstTexture = textures.GetFirstKey();
                foreach (var pair in textures)
                {
                    this.Textures.Add(type.Code + ":" + pair.Key, pair.Value);
                }
            }
            base.OnCollectTextures(api, textureDict);
        }


        #region Model Transform and Col/Selbox editor helpers

        private void OnEventBusEvent(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (eventName == "oncloseeditselboxes" || eventName == "oneditselboxes" || eventName == "onapplyselboxes")
            {
                onSelBoxEditorEvent(eventName, data);
            }

            if (eventName == "oncloseedittransforms" || eventName == "onedittransforms" || eventName == "onapplytransforms" || eventName == "genjsontransform")
            {
                onTfEditorEvent(eventName, data);
            }
        }

        private void onTfEditorEvent(string eventName, IAttribute data)
        {
            var capi = api as ICoreClientAPI;
            ItemSlot slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return;

            string type = slot.Itemstack.Attributes.GetString(ClassType + "Type", "");
            var cprops = GetTypeProps(type, slot.Itemstack, null);
            if (cprops == null) return;

            // User changed tabs
            if (transformEditMode && eventName == "onedittransforms") return;

            if (eventName == "genjsontransform")
            {
                return;

            }

            transformEditMode = eventName == "onedittransforms";

            if (transformEditMode)
            {
                if (cprops.GuiTf == null) cprops.GuiTf = ModelTransform.BlockDefaultGui();
                GuiTransform = cprops.GuiTf;

                if (cprops.FpTf == null) cprops.FpTf = ModelTransform.BlockDefaultFp();
                FpHandTransform = cprops.FpTf;

                if (cprops.TpTf == null) cprops.TpTf = ModelTransform.BlockDefaultTp();
                TpHandTransform = cprops.TpTf;

                if (cprops.GroundTf == null) cprops.GroundTf = ModelTransform.BlockDefaultGround();
                GroundTransform = cprops.GroundTf;
            }

            if (eventName == "onapplytransforms")
            {
                cprops.GuiTf = GuiTransform;
                cprops.FpTf = FpHandTransform;
                cprops.TpTf = TpHandTransform;
                cprops.GroundTf = GroundTransform;
            }

            if (eventName == "oncloseedittransforms")
            {
                GuiTransform = ModelTransform.BlockDefaultGui();
                FpHandTransform = ModelTransform.BlockDefaultFp();
                TpHandTransform = ModelTransform.BlockDefaultTp();
                GroundTransform = ModelTransform.BlockDefaultGround();
            }

        }


        private void onSelBoxEditorEvent(string eventName, IAttribute data)
        {
            var tree = data as TreeAttribute;

            if (tree?.GetInt("nowblockid") != Id) return;

            colSelBoxEditMode = eventName == "oneditselboxes";

            var pos = tree.GetBlockPos("pos");

            if (colSelBoxEditMode)
            {
                BlockEntityShapeFromAttributes bect = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
                var cprops = GetTypeProps(bect?.Type, null, bect);
                if (cprops != null)
                {
                    if (cprops.ColSelBoxes == null) cprops.ColSelBoxes = new Cuboidf[] { Cuboidf.Default() };
                    SelectionBoxes = cprops.ColSelBoxes;
                }
            }

            if (eventName == "onapplyselboxes")
            {
                BlockEntityShapeFromAttributes bect = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
                var cprops = GetTypeProps(bect?.Type, null, bect);
                if (cprops != null)
                {
                    cprops.ColSelBoxes = SelectionBoxes;
                    SelectionBoxes = new Cuboidf[] { Cuboidf.Default() };
                }
            }
        }

        #endregion

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityShapeFromAttributes bect = blockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
            if (bect == null) return base.GetCollisionBoxes(blockAccessor, pos);

            var cprops = GetTypeProps(bect.Type, null, bect);
            if (cprops?.ColSelBoxes == null) return base.GetCollisionBoxes(blockAccessor, pos);

            if (colSelBoxEditMode)
            {
                return cprops.ColSelBoxes;
            }

            int rot = (int)(bect.MeshAngleRad * GameMath.RAD2DEG);
            if (cprops.ColSelBoxesByDeg.TryGetValue(rot, out var cuboids))
            {
                return cuboids;
            }

            cprops.ColSelBoxesByDeg[rot] = cuboids = new Cuboidf[cprops.ColSelBoxes.Length];
            for (int i = 0; i < cuboids.Length; i++)
            {
                cuboids[i] = cprops.ColSelBoxes[i].RotatedCopy(0, bect.MeshAngleRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)).ClampTo(Vec3f.Zero, Vec3f.One);
                //if (((int)bect.MeshAngleRad * GameMath.RAD2DEG) % 45 == 0) cuboids[i].ShrinkBy(0.2f);
            }

            return cuboids;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetCollisionBoxes(blockAccessor, pos);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MeshRef> clutterMeshRefs;
            clutterMeshRefs = ObjectCacheUtil.GetOrCreate(capi, ClassType + "MeshesInventory", () => new Dictionary<string, MeshRef>());
            renderinfo.NormalShaded = false;
            MeshRef meshref;

            string type = itemstack.Attributes.GetString("type", "");
            var cprops = GetTypeProps(type, itemstack, null);
            if (cprops == null) return;

            if (!clutterMeshRefs.TryGetValue(cprops.HashKey, out meshref))
            {
                MeshData mesh = GenMesh(cprops);
                meshref = capi.Render.UploadMesh(mesh);
                clutterMeshRefs[cprops.HashKey] = meshref;
            }

            renderinfo.ModelRef = meshref;
            
            if (!transformEditMode)
            {
                switch (target)
                {
                    case EnumItemRenderTarget.Ground:
                        if (cprops.GroundTf != null) renderinfo.Transform = cprops.GroundTf;
                        break;
                    case EnumItemRenderTarget.Gui:
                        if (cprops.GuiTf != null) renderinfo.Transform = cprops.GuiTf;
                        break;
                    case EnumItemRenderTarget.HandFp:
                        if (cprops.FpTf != null) renderinfo.Transform = cprops.FpTf;
                        break;
                    case EnumItemRenderTarget.HandTp:
                        if (cprops.TpTf != null) renderinfo.Transform = cprops.TpTf;
                        break;
                }
            }
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BlockEntityShapeFromAttributes bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityShapeFromAttributes;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngleRad = roundRad;
                    bect.OnBlockPlaced(byItemStack); // call again to regen mesh
                }
            }

            return val;
        }


        public virtual MeshData GenMesh(IShapeTypeProps cprops) {
            var cMeshes = ObjectCacheUtil.GetOrCreate(api, ClassType+"Meshes", () => new Dictionary<string, MeshData>());
            ICoreClientAPI capi = api as ICoreClientAPI;

            if (!cMeshes.TryGetValue(cprops.Code, out var mesh))
            {
                mesh = new MeshData(4, 3);
                var shape = cprops.ShapeResolved;
                var texSource = new ShapeTextureSource(capi, shape);

                if (shape == null) return mesh;

                capi.Tesselator.TesselateShape(ClassType+"block", shape, out mesh, texSource);
                if (cprops.texPos == null)
                {
                    api.Logger.Warning("No texture previously loaded for clutter block " + cprops.Code);
                    cprops.texPos = texSource.firstTexPos;
                    cprops.texPos.RndColors = new int[TextureAtlasPosition.RndColorsLength];
                }

                cMeshes[cprops.Code] = mesh;
            }

            return mesh;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);
            BlockEntityShapeFromAttributes bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;

            stack.Attributes.SetString("type", bec?.Type);

            return stack;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[0];
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            BlockEntityShapeFromAttributes bect = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
            var cprops = GetTypeProps(bect?.Type, null, bect);
            if (cprops?.texPos != null)
            {
                return cprops.texPos.AvgColor;
            }

            return base.GetColor(capi, pos);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntityShapeFromAttributes bect = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;

            var cprops = GetTypeProps(bect?.Type, null, bect);
            if (cprops?.texPos != null)
            {
                return cprops.texPos.RndColors[rndIndex < 0 ? capi.World.Rand.Next(cprops.texPos.RndColors.Length) : rndIndex];
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", "");
            return Lang.GetMatching(Code.Domain + ":" + ClassType + "-" + type.Replace("/", "-"));
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityShapeFromAttributes bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
            return Lang.GetMatching(Code.Domain + ":" + ClassType + "-" + bec?.Type?.Replace("/", "-"));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityShapeFromAttributes bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
            return Lang.GetMatchingIfExists(Code.Domain + ":" + ClassType + "desc-" + bec?.Type?.Replace("/", "-"));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine(Lang.Get(Code.Domain + ":block-" + ClassType));
        }
    }
}
