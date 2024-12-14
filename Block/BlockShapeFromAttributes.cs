﻿using System;
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
        string filenameForLogging;
        public Dictionary<string, CompositeTexture> textures = new Dictionary<string, CompositeTexture>();
        public TextureAtlasPosition firstTexPos;

        HashSet<AssetLocation> missingTextures = new HashSet<AssetLocation>();

        public ShapeTextureSource(ICoreClientAPI capi, Shape shape, string filenameForLogging)
        {
            this.capi = capi;
            this.shape = shape;
            this.filenameForLogging = filenameForLogging;
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                TextureAtlasPosition texPos;

                if (textures.TryGetValue(textureCode, out var ctex))
                {
                    capi.BlockTextureAtlas.GetOrInsertTexture(ctex, out _, out texPos);
                } else {
                    shape.Textures.TryGetValue(textureCode, out var texturePath);

                    if (texturePath == null)
                    {
                        if (!missingTextures.Contains(texturePath))
                        {
                            capi.Logger.Warning("Shape {0} has an element using texture code {1}, but no such texture exists", filenameForLogging, textureCode);
                            missingTextures.Add(texturePath);
                        }

                        return capi.BlockTextureAtlas.UnknownTexturePosition;
                    }

                    capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texPos);
                }


                if (texPos == null)
                {
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
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
        string TextureFlipCode { get; }
        string TextureFlipGroupCode { get; }
        Dictionary<string, CompositeTexture> Textures { get; }
        byte[] LightHsv { get; }
        string HashKey { get; }
        bool RandomizeYSize { get; }
        string Code { get; }
        Vec3f Rotation { get; }
        Cuboidf[] ColSelBoxes { get; set; }
        Cuboidf[] SelBoxes { get; set; }
        ModelTransform GuiTransform { get; set; }
        ModelTransform FpTtransform { get; set; }
        ModelTransform TpTransform { get; set; }
        ModelTransform GroundTransform { get; set; }
        string RotInterval { get; }
        string FirstTexture { get; set; }
        TextureAtlasPosition TexPos { get; set; }
        Dictionary<long, Cuboidf[]> ColSelBoxesByHashkey { get; }
        Dictionary<long, Cuboidf[]> SelBoxesByHashkey { get; set; }
        AssetLocation ShapePath { get; }
        Shape ShapeResolved { get; set; }

        BlockDropItemStack[] Drops {get;set;}
        string HeldIdleAnim { get; set; }
        string HeldReadyAnim { get; set; }

        /// <summary>
        /// The quantity of resources (abstract units, but 1 corresponds to 1 resin) required to fully repair this clutter block, **PLUS ONE**.  0 means unspecified, 1 means no repair necessary, 6 means reparable with the equivalent of 5 resin.  -1 means cannot be repaired.  If unspecified (0), BlockClutter.Attributes["reparability"] will be checked as a general rule for all types
        /// </summary>
        public int Reparability { get; set; }
        bool CanAttachBlockAt(Vec3f blockRot, BlockFacing blockFace, Cuboidi attachmentArea = null);
    }

    public abstract class BlockShapeFromAttributes : Block, IWrenchOrientable, ITextureFlippable
    {
        protected bool colSelBoxEditMode;
        protected bool transformEditMode;
        protected float rotInterval = GameMath.PIHALF / 4;
        public IDictionary<string, CompositeTexture> blockTextures;
        public abstract string ClassType { get; }
        public abstract IEnumerable<IShapeTypeProps> AllTypes { get; }
        public abstract void LoadTypes();
        public abstract IShapeTypeProps GetTypeProps(string code, ItemStack stack, BEBehaviorShapeFromAttributes be);
        public Dictionary<string, OrderedDictionary<string, CompositeTexture>> OverrideTextureGroups;
        protected Dictionary<string, MeshData> meshDictionary;
        protected string inventoryMeshDictionary;
        protected string blockForLogging;

        public bool AllowRandomizeDims=true;

        public SkillItem[] extraWrenchModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            extraWrenchModes = new SkillItem[]
            {
                new SkillItem() { Code = new AssetLocation("ns"), Name = Lang.Get("Offset 1 Voxel North/South") },
                new SkillItem() { Code = new AssetLocation("ew"), Name = Lang.Get("Offset 1 Voxel East/West") },
                new SkillItem() { Code = new AssetLocation("ud"), Name = Lang.Get("Offset 1 Voxel Up/Down") }
            };

            if (api is ICoreClientAPI capi)
            {
                extraWrenchModes[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/movens.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                extraWrenchModes[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/moveew.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                extraWrenchModes[2].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/moveud.svg"), 48, 48, 5, ColorUtil.WhiteArgb));

                meshDictionary = ObjectCacheUtil.GetOrCreate(api, ClassType + "Meshes", () => new Dictionary<string, MeshData>());  // Best to access ObjectCacheUtil on the main thread; it's fine to hold a reference to this for the whole game, it won't change, it's only this Block who accesses it
                inventoryMeshDictionary = ClassType + "MeshesInventory";
                blockForLogging = ClassType + "block";
                
                capi.Event.RegisterEventBusListener(OnEventBusEvent);
                foreach (var type in AllTypes)
                {
                    if (!Textures.TryGetValue(type.Code + ":" + type.FirstTexture, out CompositeTexture ct)) continue;
                    type.TexPos = capi.BlockTextureAtlas[ct.Baked.BakedName];
                }

                blockTextures = Attributes["textures"].AsObject<IDictionary<string, CompositeTexture>>();
            }
            else
            {
                LoadTypes();   // Client side types are already loaded in OnCollectTextures - which MUST be implemented!
                OverrideTextureGroups = Attributes["overrideTextureGroups"].AsObject<Dictionary<string, OrderedDictionary<string, CompositeTexture>>>();
            }

            AllowRandomizeDims = Attributes?["randomizeDimensions"].AsBool(true) == true;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);

            if (extraWrenchModes != null)
            {
                extraWrenchModes[0].Dispose();
                extraWrenchModes[1].Dispose();
                extraWrenchModes[2].Dispose();
            }

            if (api is ICoreClientAPI capi && inventoryMeshDictionary != null)
            {
                Dictionary<string, MultiTextureMeshRef> clutterMeshRefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(capi, inventoryMeshDictionary);
                if (clutterMeshRefs != null)
                {
                    foreach (MultiTextureMeshRef mesh in clutterMeshRefs.Values) mesh.Dispose();
                    ObjectCacheUtil.Delete(capi, inventoryMeshDictionary);
                }
            }
        }

        public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict)
        {
            OverrideTextureGroups = Attributes["overrideTextureGroups"].AsObject<Dictionary<string, OrderedDictionary<string, CompositeTexture>>>();

            this.api = api;
            LoadTypes();
            foreach (var cprops in AllTypes)
            {
                cprops.ShapeResolved = api.Assets.TryGet(cprops.ShapePath)?.ToObject<Shape>();
                if (cprops.ShapeResolved == null)
                {
                    api.Logger.Error("Block {0}: Could not find {1}, type {2} shape '{3}'.", this.Code, ClassType, cprops.Code, cprops.ShapePath);
                    continue;
                }
                var textures = new FastSmallDictionary<string, CompositeTexture>(1);
                textureDict.CollectAndBakeTexturesFromShape(cprops.ShapeResolved, textures, cprops.ShapePath);
                cprops.FirstTexture = textures.GetFirstKey();
                foreach (var pair in textures)
                {
                    this.Textures.Add(cprops.Code + ":" + pair.Key, pair.Value);
                }
            }

            if (OverrideTextureGroups != null)
            {
                foreach (var group in OverrideTextureGroups)
                {
                    string sourceString = "Block " + Code + ": override texture group " + group.Key;
                    foreach (var val in group.Value)
                    {
                        val.Value.Bake(api.Assets);

                        val.Value.Baked.TextureSubId = textureDict.GetOrAddTextureLocation(new AssetLocationAndSource(val.Value.Baked.BakedName, sourceString, Code));
                    }
                }
            }

            base.OnCollectTextures(api, textureDict);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            // We cannot call the base method, otherwise we'll destroy the block entity
            //base.OnNeighbourBlockChange(world, pos, neibpos);

            EnumHandling handled = EnumHandling.PassThrough;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                behavior.OnNeighbourBlockChange(world, pos, neibpos, ref handled);
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            // Block above was placed => remove snow cover
            if (handled == EnumHandling.PassThrough && (this == snowCovered1 || this == snowCovered2 || this == snowCovered3))
            {
                if (pos.X == neibpos.X && pos.Z == neibpos.Z && pos.Y + 1 == neibpos.Y && world.BlockAccessor.GetBlock(neibpos).Id != 0)
                {
                    world.BlockAccessor.ExchangeBlock(notSnowCovered.Id, pos);
                    world.BlockAccessor.MarkBlockDirty(pos);
                    world.BlockAccessor.MarkBlockEntityDirty(pos);
                }
            }
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            if (extra is string && (string)extra == "melt")
            {
                if (this == snowCovered3)
                {
                    world.BlockAccessor.ExchangeBlock(snowCovered2.Id, pos);
                }
                else if (this == snowCovered2)
                {
                    world.BlockAccessor.ExchangeBlock(snowCovered1.Id, pos);
                }
                else if (this == snowCovered1)
                {
                    world.BlockAccessor.ExchangeBlock(notSnowCovered.Id, pos);
                }

                world.BlockAccessor.MarkBlockDirty(pos);
                world.BlockAccessor.MarkBlockEntityDirty(pos);
            }
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

            string type = slot.Itemstack.Attributes.GetString("type");
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
                if (cprops.GuiTransform == null) cprops.GuiTransform = ModelTransform.BlockDefaultGui();
                GuiTransform = cprops.GuiTransform;

                if (cprops.FpTtransform == null) cprops.FpTtransform = ModelTransform.BlockDefaultFp();
                FpHandTransform = cprops.FpTtransform;

                if (cprops.TpTransform == null) cprops.TpTransform = ModelTransform.BlockDefaultTp();
                TpHandTransform = cprops.TpTransform;

                if (cprops.GroundTransform == null) cprops.GroundTransform = ModelTransform.BlockDefaultGround();
                GroundTransform = cprops.GroundTransform;
            }

            if (eventName == "onapplytransforms")
            {
                cprops.GuiTransform = GuiTransform;
                cprops.FpTtransform = FpHandTransform;
                cprops.TpTransform = TpHandTransform;
                cprops.GroundTransform = GroundTransform;
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
                var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
                var cprops = GetTypeProps(bect?.Type, null, bect);
                if (cprops != null)
                {
                    if (cprops.ColSelBoxes == null) cprops.ColSelBoxes = new Cuboidf[] { Cuboidf.Default() };
                    SelectionBoxes = cprops.ColSelBoxes;
                }
            }

            if (eventName == "onapplyselboxes")
            {
                var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
                var cprops = GetTypeProps(bect?.Type, null, bect);
                if (cprops != null)
                {
                    cprops.ColSelBoxes = SelectionBoxes;
                    SelectionBoxes = new Cuboidf[] { Cuboidf.Default() };
                }
            }
        }

        #endregion

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            if (bect == null) return base.GetCollisionBoxes(blockAccessor, pos);

            var cprops = GetTypeProps(bect.Type, null, bect);
            return getCollisionBoxes(blockAccessor, pos, bect, cprops);
        }

        private Cuboidf[] getCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, BEBehaviorShapeFromAttributes bect, IShapeTypeProps cprops)
        {
            if (cprops?.ColSelBoxes == null) return base.GetCollisionBoxes(blockAccessor, pos);

            if (colSelBoxEditMode)
            {
                return cprops.ColSelBoxes;
            }

            long hashkey =
                  (long)(bect.offsetX * 255 + 255) << 45
                | (long)(bect.offsetY * 255 + 255) << 36
                | (long)(bect.offsetZ * 255 + 255) << 27
                | (long)((bect.rotateY + (bect.rotateY < 0 ? GameMath.TWOPI : 0)) * GameMath.RAD2DEG) << 18
                | (long)((bect.rotateX + (bect.rotateX < 0 ? GameMath.TWOPI : 0)) * GameMath.RAD2DEG) << 9
                | (long)((bect.rotateZ + (bect.rotateZ < 0 ? GameMath.TWOPI : 0)) * GameMath.RAD2DEG)
            ;

            if (cprops.ColSelBoxesByHashkey.TryGetValue(hashkey, out var cuboids))
            {
                return cuboids;
            }

            cprops.ColSelBoxesByHashkey[hashkey] = cuboids = new Cuboidf[cprops.ColSelBoxes.Length];
            for (int i = 0; i < cuboids.Length; i++)
            {
                cuboids[i] = cprops
                    .ColSelBoxes[i]
                    .RotatedCopy(bect.rotateX * GameMath.RAD2DEG, bect.rotateY * GameMath.RAD2DEG, bect.rotateZ * GameMath.RAD2DEG, new Vec3d(0.5, 0.5, 0.5)).ClampTo(Vec3f.Zero, Vec3f.One)
                    .OffsetCopy(bect.offsetX, bect.offsetY, bect.offsetZ)
                ;
            }

            return cuboids;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            if (bect == null) return base.GetCollisionBoxes(blockAccessor, pos);

            var cprops = GetTypeProps(bect.Type, null, bect);
            if (cprops?.SelBoxes == null) return getCollisionBoxes(blockAccessor, pos, bect, cprops);

            if (colSelBoxEditMode)
            {
                return cprops.ColSelBoxes ?? cprops.SelBoxes;
            }

            long hashkey =
                  (long)(bect.offsetX * 255 + 255) << 45
                | (long)(bect.offsetY * 255 + 255) << 36
                | (long)(bect.offsetZ * 255 + 255) << 27
                | (long)((bect.rotateY + (bect.rotateY < 0 ? GameMath.TWOPI : 0)) * GameMath.RAD2DEG) << 18
                | (long)((bect.rotateX + (bect.rotateX < 0 ? GameMath.TWOPI : 0)) * GameMath.RAD2DEG) << 9
                | (long)((bect.rotateZ + (bect.rotateZ < 0 ? GameMath.TWOPI : 0)) * GameMath.RAD2DEG)
            ;

            if (cprops.SelBoxesByHashkey == null) cprops.SelBoxesByHashkey = new Dictionary<long, Cuboidf[]>();
            if (cprops.SelBoxesByHashkey.TryGetValue(hashkey, out var cuboids))
            {
                return cuboids;
            }

            cprops.SelBoxesByHashkey[hashkey] = cuboids = new Cuboidf[cprops.SelBoxes.Length];
            for (int i = 0; i < cuboids.Length; i++)
            {
                cuboids[i] = cprops
                    .SelBoxes[i]
                    .RotatedCopy(bect.rotateX * GameMath.RAD2DEG, bect.rotateY * GameMath.RAD2DEG, bect.rotateZ * GameMath.RAD2DEG, new Vec3d(0.5, 0.5, 0.5)).ClampTo(Vec3f.Zero, Vec3f.One)
                    .OffsetCopy(bect.offsetX, bect.offsetY, bect.offsetZ)
                ;
            }

            return cuboids;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MultiTextureMeshRef> clutterMeshRefs;
            clutterMeshRefs = ObjectCacheUtil.GetOrCreate(capi, inventoryMeshDictionary, () => new Dictionary<string, MultiTextureMeshRef>());
            MultiTextureMeshRef meshref;

            string type = itemstack.Attributes.GetString("type", "");
            var cprops = GetTypeProps(type, itemstack, null);
            if (cprops == null) return;

            float rotX = itemstack.Attributes.GetFloat("rotX");
            float rotY = itemstack.Attributes.GetFloat("rotY");
            float rotZ = itemstack.Attributes.GetFloat("rotZ");
            string otcode = itemstack.Attributes.GetString("overrideTextureCode");

            string hashkey = cprops.HashKey + "-" + rotX + "-" + rotY + "-" + rotZ + "-" + otcode;

            if (!clutterMeshRefs.TryGetValue(hashkey, out meshref))
            {
                MeshData mesh = GetOrCreateMesh(cprops, null, otcode);
                mesh = mesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), rotX, rotY, rotZ);
                meshref = capi.Render.UploadMultiTextureMesh(mesh);
                clutterMeshRefs[hashkey] = meshref;
            }

            renderinfo.ModelRef = meshref;
            
            if (!transformEditMode)
            {
                switch (target)
                {
                    case EnumItemRenderTarget.Ground:
                        if (cprops.GroundTransform != null) renderinfo.Transform = cprops.GroundTransform;
                        break;
                    case EnumItemRenderTarget.Gui:
                        if (cprops.GuiTransform != null) renderinfo.Transform = cprops.GuiTransform;
                        break;
                    /*case EnumItemRenderTarget.HandFp:
                        if (cprops.FpTtransform != null) renderinfo.Transform = cprops.FpTtransform;
                        break;*/
                    case EnumItemRenderTarget.HandTp:
                        if (cprops.TpTransform != null) renderinfo.Transform = cprops.TpTransform;
                        break;
                }
            }
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);

            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            if (bect != null)
            {
                stack.Attributes.SetString("type", bect.Type);
                if (bect.overrideTextureCode != null) stack.Attributes.SetString("overrideTextureCode", bect.overrideTextureCode);
            }

            return stack;
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(blockSel.Position);
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float roundRad = ((int)Math.Round(angleHor / rotInterval)) * rotInterval;

                    bect.rotateX = byItemStack.Attributes.GetFloat("rotX");
                    bect.rotateY = byItemStack.Attributes.GetFloat("rotY", roundRad);
                    bect.rotateZ = byItemStack.Attributes.GetFloat("rotZ");
                    string otcode = byItemStack.Attributes.GetString("overrideTextureCode");
                    if (otcode != null) bect.overrideTextureCode = otcode;

                    bect.OnBlockPlaced(byItemStack); // call again to regen mesh
                }
            }

            return val;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var bes = GetBEBehavior< BEBehaviorShapeFromAttributes>(pos);
            if (bes != null)
            {
                var cprops = GetTypeProps(bes.Type, null, bes);
                if (cprops == null)
                {
                    base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
                    return;
                }

                blockModelData = GetOrCreateMesh(cprops, null, bes.overrideTextureCode).Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), bes.rotateX, bes.rotateY + cprops.Rotation.Y * GameMath.DEG2RAD, bes.rotateZ);
                decalModelData = GetOrCreateMesh(cprops, decalTexSource, bes.overrideTextureCode).Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), bes.rotateX, bes.rotateY + cprops.Rotation.Y * GameMath.DEG2RAD, bes.rotateZ);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public virtual MeshData GetOrCreateMesh(IShapeTypeProps cprops, ITexPositionSource overrideTexturesource = null, string overrideTextureCode = null) {
            var cMeshes = meshDictionary;
            ICoreClientAPI capi = api as ICoreClientAPI;

            if (overrideTexturesource != null || !cMeshes.TryGetValue(cprops.Code + "-" + overrideTextureCode, out var mesh))
            {
                mesh = new MeshData(4, 3);
                var shape = cprops.ShapeResolved;
                ITexPositionSource texSource = overrideTexturesource;

                // Prio 0: Shape textures
                if (texSource == null)
                {
                    var stexSource = new ShapeTextureSource(capi, shape, cprops.ShapePath.ToString());
                    texSource = stexSource;

                    // Prio 1: Block wide custom textures
                    if (blockTextures != null)
                    {
                        foreach (var val in blockTextures)
                        {
                            if (val.Value.Baked == null) val.Value.Bake(capi.Assets);
                            stexSource.textures[val.Key] = val.Value;
                        }
                    }

                    // Prio 2: Variant textures
                    if (cprops.Textures != null)
                    {
                        foreach (var val in cprops.Textures)
                        {
                            var ctex = val.Value.Clone();
                            ctex.Bake(capi.Assets);
                            stexSource.textures[val.Key] = ctex;
                        }
                    }

                    // Prio 3: Override texture
                    if (overrideTextureCode != null && cprops.TextureFlipCode != null)
                    {
                        if (OverrideTextureGroups[cprops.TextureFlipGroupCode].TryGetValue(overrideTextureCode, out var ctex))
                        {
                            ctex.Bake(capi.Assets);
                            stexSource.textures[cprops.TextureFlipCode] = ctex;
                        }
                    }
                }

                if (shape == null) return mesh;
                

                capi.Tesselator.TesselateShape(blockForLogging, shape, out mesh, texSource);
                if (cprops.TexPos == null)
                {
                    api.Logger.Warning("No texture previously loaded for clutter block " + cprops.Code);
                    cprops.TexPos = (texSource as ShapeTextureSource)?.firstTexPos;
                    cprops.TexPos.RndColors = new int[TextureAtlasPosition.RndColorsLength];
                }

                if (overrideTexturesource == null)
                {
                    cMeshes[cprops.Code + "-" + overrideTextureCode] = mesh;
                }
            }

            return mesh;
        }

        byte[] noLight = new byte[3];
        

        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos == null)
            {
                string type = stack.Attributes.GetString("type", "");
                var cprops = GetTypeProps(type, stack, null);
                return cprops?.LightHsv ?? noLight;
            }
            else
            {
                var bect = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorShapeFromAttributes>();
                var cprops = GetTypeProps(bect?.Type, null, bect);
                return cprops?.LightHsv ?? noLight;
            }
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            var cprops = GetTypeProps(bect?.Type, null, bect);
            if (cprops?.TexPos != null)
            {
                return cprops.TexPos.AvgColor;
            }

            return base.GetColor(capi, pos);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            var cprops = GetTypeProps(bect?.Type, null, bect);
            if (cprops != null)
            {
                return cprops.CanAttachBlockAt(new Vec3f(bect.rotateX, bect.rotateY, bect.rotateZ), blockFace, attachmentArea);
            }

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            var cprops = GetTypeProps(bect?.Type, null, bect);
            if (cprops?.TexPos != null)
            {
                return cprops.TexPos.RndColors[rndIndex < 0 ? capi.World.Rand.Next(cprops.TexPos.RndColors.Length) : rndIndex];
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
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            if (bect?.overrideTextureCode != null)
            {
                var name = Lang.GetMatchingIfExists(bect.GetFullCode() + "-" + bect.overrideTextureCode);
                if (name != null) return name;
            }
            return Lang.GetMatching(bect?.GetFullCode() ?? "Unknown");
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            return base.GetPlacedBlockInfo(world, pos, forPlayer) + Lang.GetMatchingIfExists(Code.Domain + ":" + ClassType + "desc-" + bect?.Type?.Replace("/", "-"));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type", "");
            string desc = Lang.GetIfExists(Code.Domain + ":" + ClassType + "desc-" + type.Replace("/", "-"));
            if (desc != null) dsc.AppendLine(desc);
        }

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(blockSel.Position);
            bect.Rotate(byEntity, blockSel, dir);
        }

        public void FlipTexture(BlockPos pos, string newTextureCode)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            bect.overrideTextureCode = newTextureCode;
            bect.loadMesh();
            bect.Blockentity.MarkDirty(true);
        }

        public OrderedDictionary<string, CompositeTexture> GetAvailableTextures(BlockPos pos)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            var cprops = GetTypeProps(bect?.Type, null, bect);
            if (cprops?.TextureFlipGroupCode != null)
            {
                return this.OverrideTextureGroups[cprops.TextureFlipGroupCode];
            }

            return null;

        }

        public virtual string BaseCodeForName()
        {
            return Code.Domain + ":" + ClassType + "-";
        }
    }
}
