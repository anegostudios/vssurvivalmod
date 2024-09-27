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

    public class BlockCropProp : Block, ITexPositionSource
    {
        ICoreClientAPI capi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.capi = api as ICoreClientAPI;
        }
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        string nowTesselatingType;

        public TextureAtlasPosition this[string textureCode] {
            get
            {
                capi.BlockTextureAtlas.GetOrInsertTexture(new AssetLocation("block/meta/cropprop/" + nowTesselatingType), out _, out var texPos);
                return texPos;
            }
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            var dict = ObjectCacheUtil.GetOrCreate(capi, "croprop-meshes", ()=>new Dictionary<string, MultiTextureMeshRef>());

            string type = itemstack.Attributes.GetString("type", "unknown");

            if (dict.TryGetValue(type, out var meshref))
            {
                renderinfo.ModelRef = meshref;
            } else
            {
                nowTesselatingType = type;
                capi.Tesselator.TesselateShape("croppropinv", Code, this.Shape, out var meshdata, this);
                dict[type] = renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var beh = GetBEBehavior<BEBehaviorCropProp>(pos);
            string type = beh?.Type;
            if (type == null) return base.GetPlacedBlockName(world, pos);
            return Lang.GetMatching("block-crop-" + type + "-" + beh.Stage);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            dsc.AppendLine(string.Format("Type: {0}", inSlot.Itemstack.Attributes.GetString("type")));

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }


    public class CropPropConfig
    {
        public bool RandomizeRotations;
        public float MonthStart;
        public float MonthEnd;
        public int Stages;
        public CompositeShape Shape;
        public Dictionary<string, CompositeTexture> Textures;
    }

    public class BEBehaviorCropProp : BlockEntityBehavior, ITexPositionSource
    {
        public int Stage=1;
        public string Type;
        MeshData mesh;
        CropPropConfig config;

        ICoreClientAPI capi;

        Shape nowTesselatingShape;
        ITexPositionSource defaultSource;
        bool dead;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode] {
            get
            {
                if (config.Textures != null && config.Textures.ContainsKey(textureCode))
                {
                    capi.BlockTextureAtlas.GetOrInsertTexture(config.Textures[textureCode], out _, out var texPosb);
                    return texPosb;
                }

                capi.BlockTextureAtlas.GetOrInsertTexture(nowTesselatingShape.Textures[textureCode], out _, out var texPos);
                return texPos;
            }
        }

        public BEBehaviorCropProp(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (Api.Side == EnumAppSide.Server)
            {
                this.Blockentity.RegisterGameTickListener(onTick8s, 800, Api.World.Rand.Next(100));
                if (Type != null)
                {
                    loadConfig();
                    onTick8s(0);
                    loadMesh();
                }
            } else
            {
                if (Type != null)
                {
                    loadConfig();
                    loadMesh();
                }
            }            
        }

        private void loadConfig()
        {
            config = this.Block.Attributes["types"][dead ? "dead" : Type].AsObject<CropPropConfig>();

            if (config.Shape != null)
            {
                config.Shape.Base.Path = config.Shape.Base.Path.Replace("{stage}", "" + Stage).Replace("{type}", Type);
            }
            if (config.Textures != null)
            {
                foreach (var val in config.Textures.Values)
                {
                    val.Base.Path = val.Base.Path.Replace("{stage}", "" + Stage).Replace("{type}", Type);
                }
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null)
            {
                this.Type = byItemStack.Attributes.GetString("type");
            }

            loadConfig();
            onTick8s(0);
            loadMesh();
        }

        private void loadMesh()
        {
            if (Api == null || Api.Side != EnumAppSide.Client) return;
            capi = Api as ICoreClientAPI;

            if (config.Shape == null)
            {
                var cropblock = Api.World.GetBlock(new AssetLocation("crop-" + Type + "-" + Stage));
                mesh = capi.TesselatorManager.GetDefaultBlockMesh(cropblock);
                return;
            }

            config.Shape.LoadAlternates(capi.Assets, capi.Logger);

            var cshape = config.Shape;
            if (config.Shape.BakedAlternates != null) {
                cshape = config.Shape.BakedAlternates[GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, config.Shape.BakedAlternates.Length)];
            }

            nowTesselatingShape = capi.Assets.TryGet(cshape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
            capi.Tesselator.TesselateShape("croprop", Block.Code, cshape, out mesh, this);
        }

        private void onTick8s(float dt)
        {
            var mon = Api.World.Calendar.YearRel;
            var len = (config.MonthEnd - config.MonthStart) / 12f;
            int nextStage = GameMath.Clamp((int)((mon - (config.MonthStart-1)/12f)/len * config.Stages), 1, config.Stages);

            var temp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, Api.World.Calendar.TotalDays).Temperature;
            bool nowDead = !dead && temp < -5;
            bool nowAlive = dead && temp > 15;

            if (nowDead) dead = true;
            if (nowAlive) dead = false;

            if (Stage != nextStage || nowDead || nowAlive)
            {
                this.Stage = nextStage;
                loadConfig();
                loadMesh();
                Blockentity.MarkDirty(true);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            int curStage = Stage;
            bool curDead = dead;

            this.Type = tree.GetString("code");
            this.Stage = tree.GetInt("stage");
            this.dead = tree.GetBool("dead");

            if (Stage != curStage || dead != curDead)
            {
                loadConfig();
                loadMesh();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("code", Type);
            tree.SetInt("stage", Stage);
            tree.SetBool("dead", dead);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(mesh);
            return true;
        }
    }
}