using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class DynFoliageProperties
    {
        public string TexturesBasePath;
        public Dictionary<string, CompositeTexture> Textures;
        public CompositeTexture LeafParticlesTexture;
        public CompositeTexture BlossomParticlesTexture;
        public string SeasonColorMap = "seasonalFoliage";
        public string ClimateColorMap = "climatePlantTint";

        public void Rebase(DynFoliageProperties props)
        {
            if (TexturesBasePath == null) TexturesBasePath = props.TexturesBasePath;
            if (Textures == null)
            {
                Textures = new Dictionary<string, CompositeTexture>();
                foreach (var val in props.Textures)
                {
                    Textures[val.Key] = val.Value.Clone();
                }
            }

            LeafParticlesTexture = props.LeafParticlesTexture?.Clone();
            BlossomParticlesTexture = props.BlossomParticlesTexture?.Clone();
        }
    }

    public abstract class BlockEntityFruitTreePart: BlockEntity, ITexPositionSource
    {
        public EnumFoliageState FoliageState = EnumFoliageState.Plain;
        public EnumFruitTreeState FruitTreeState
        {
            get
            {
                if (rootBh == null) return EnumFruitTreeState.Empty;
                if (rootBh.propsByType.TryGetValue(TreeType, out var val))
                {
                    return val.State;
                }

                return EnumFruitTreeState.Empty;
            }
        }

        protected ICoreClientAPI capi;
        protected MeshData sticksMesh;
        protected MeshData leavesMesh;


        public int[] LeafParticlesColor;
        public int[] BlossomParticlesColor;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public double Progress => rootBh.GetCurrentStateProgress(TreeType);

        public string TreeType = "";
        public int Height = 0;
        public Vec3i RootOff = null;

        protected bool listenerOk;
        protected Shape nowTesselatingShape;
        
        public int fruitingSide;
        protected string foliageDictCacheKey;

        protected BlockFruitTreeFoliage blockFoliage;
        public BlockFruitTreeBranch blockBranch;

        public BlockFacing GrowthDir = BlockFacing.UP;
        public EnumTreePartType PartType = EnumTreePartType.Cutting;
        public AssetLocation harvestingSound;


        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                Dictionary<string, CompositeTexture> textures = Block.Textures;
                AssetLocation texturePath = null;
                CompositeTexture tex;

                if (this is BlockEntityFruitTreeBranch && FoliageState == EnumFoliageState.Dead && (textureCode == "bark" || textureCode == "treetrunk"))
                {
                    textureCode = "deadtree";
                }

                // Prio 1: Config
                blockFoliage.foliageProps.TryGetValue(TreeType, out var props);
                if (props != null)
                {
                    string key = textureCode + "-" + FoliageUtil.FoliageStates[(int)FoliageState];
                    if (props.Textures.TryGetValue(key, out var ctex))
                    {
                        capi.BlockTextureAtlas.InsertTextureCached(ctex, out _, out var texPos);
                        return texPos;
                    }

                    key = textureCode;
                    if (props.Textures.TryGetValue(key, out var ctex2))
                    {
                        capi.BlockTextureAtlas.InsertTextureCached(ctex2, out _, out var texPos);
                        return texPos;
                    }
                }

                // Prio 2: Get from collectible textures
                if (textures.TryGetValue(textureCode, out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 3: Get from collectible textures, use "all" code
                if (texturePath == null && textures.TryGetValue("all", out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 4: Get from currently tesselating shape
                if (texturePath == null)
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
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
                    BitmapRef bmp = texAsset.ToBitmap(capi);
                    capi.BlockTextureAtlas.InsertTextureCached(texturePath, bmp, out _, out texpos);
                }
                else
                {
                    capi.World.Logger.Warning("For render in block " + Block.Code + ", item {0} defined texture {1}, not no such texture found.", Block.Code, texturePath);
                    texpos = capi.BlockTextureAtlas.UnknownTexturePosition;
                }
            }

            return texpos;
        }

        public abstract void GenMesh();


        public virtual bool GenFoliageMesh(bool withSticks, out MeshData foliageMesh, out MeshData sticksMesh)
        {
            foliageMesh = null;
            sticksMesh = null;
               
            if (Api?.Side == EnumAppSide.Server || TreeType == null || TreeType == "") return false;

            var foliageProps = blockFoliage.foliageProps[TreeType];

            LeafParticlesColor = getOrCreateTexPos(foliageProps.LeafParticlesTexture.Base).RndColors;
            BlossomParticlesColor = getOrCreateTexPos(foliageProps.BlossomParticlesTexture.Base).RndColors;


            Dictionary<int, MeshData[]> meshesByKey = ObjectCacheUtil.GetOrCreate(Api, foliageDictCacheKey, () => new Dictionary<int, MeshData[]>());

            MeshData[] meshes;
            int meshCacheKey = getHashCodeLeaves();
            if (meshesByKey.TryGetValue(meshCacheKey, out meshes))
            {
                sticksMesh = meshes[0];
                foliageMesh = meshes[1];
                return true;
            }

            meshes = new MeshData[2];

            // Foliage shape
            {
                string shapekey = "foliage-ver";
                if (GrowthDir.IsHorizontal)
                {
                    shapekey = "foliage-hor-" + GrowthDir.Code[0];
                }

                if (!blockBranch.Shapes.TryGetValue(shapekey, out var shapeData)) return false;
                
                nowTesselatingShape = shapeData.Shape;
               
                List<string> selectiveElements = new List<string>();

                bool everGreen = false;
                FruitTreeProperties props = null;
                if (rootBh?.propsByType.TryGetValue(TreeType, out props) == true)
                {
                    everGreen = props.CycleType == EnumTreeCycleType.Evergreen;
                }

                if (withSticks)
                {
                    selectiveElements.Add("sticks/*");
                    capi.Tesselator.TesselateShape("fruittreefoliage", nowTesselatingShape, out meshes[0], this, new Vec3f(shapeData.CShape.rotateX, shapeData.CShape.rotateY, shapeData.CShape.rotateZ), 0, 0, 0, null, selectiveElements.ToArray());
                }

                selectiveElements.Clear();
                if (FoliageState == EnumFoliageState.Flowering)
                {
                    selectiveElements.Add("blossom/*");
                }
                if (FoliageState != EnumFoliageState.Dead && FoliageState != EnumFoliageState.DormantNoLeaves && (FoliageState != EnumFoliageState.Flowering || everGreen))
                {
                    nowTesselatingShape.WalkElements("leaves/*", (elem) => { elem.SeasonColorMap = foliageProps.SeasonColorMap; elem.ClimateColorMap = foliageProps.ClimateColorMap; } );
                    selectiveElements.Add("leaves/*");
                }

                float rndydeg = GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 3) * 22.5f - 22.5f;
                capi.Tesselator.TesselateShape("fruittreefoliage", nowTesselatingShape, out meshes[1], this, new Vec3f(shapeData.CShape.rotateX, shapeData.CShape.rotateY + rndydeg, shapeData.CShape.rotateZ), 0, 0, 0, null, selectiveElements.ToArray());

                sticksMesh = meshes[0];
                foliageMesh = meshes[1];
            }

            // Fruit shape 
            if (FoliageState == EnumFoliageState.Fruiting || FoliageState == EnumFoliageState.Ripe) { 
                string shapekey = "fruit-" + TreeType;

                if (!blockBranch.Shapes.TryGetValue(shapekey, out var shapeData)) return false;

                nowTesselatingShape = shapeData.Shape;

                List<string> selectiveElements = new List<string>();
                for (int i = 0; i < 4; i++)
                {
                    char f = BlockFacing.HORIZONTALS[i].Code[0];
                    if ((fruitingSide & (1 << i)) > 0) selectiveElements.Add("fruits-" + f + "/*");
                }

                float rndydeg = GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 3) * 22.5f - 22.5f;
                capi.Tesselator.TesselateShape("fruittreefoliage", nowTesselatingShape, out var fruitMesh, this, new Vec3f(shapeData.CShape.rotateX, shapeData.CShape.rotateY, shapeData.CShape.rotateZ), 0, 0, 0, null, selectiveElements.ToArray());

                foliageMesh.AddMeshData(fruitMesh);
            }

            meshesByKey[meshCacheKey] = meshes;
            

            return true;
        }

        protected virtual int getHashCodeLeaves()
        {
            return (GrowthDir.Code[0] + "-" + TreeType + "-" + FoliageState + "-" + fruitingSide + "-" + (GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 3) * 22.5f - 22.5f)).GetHashCode();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            foliageDictCacheKey = "fruitTreeFoliageMeshes" + Block.Code.ToShortString();

            capi = api as ICoreClientAPI;

            if (!getRootBhSetupListener())
            {
                RegisterDelayedCallback((dt) => getRootBhSetupListener(), 1000);
            }

            if (api.Side == EnumAppSide.Client)
            {
                string code = Block.Attributes["harvestingSound"].AsString("sounds/block/plant");
                if (code != null)
                {
                    harvestingSound = AssetLocation.Create(code, Block.Code.Domain);
                }

                GenMesh();
            }
        }


        FruitTreeRootBH rootBh;
        protected bool getRootBhSetupListener()
        {
            if (RootOff == null || RootOff.IsZero)
            {
                rootBh = GetBehavior<FruitTreeRootBH>();
            } else
            {
                var be = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(RootOff)) as BlockEntityFruitTreeBranch;
                rootBh = be?.GetBehavior<FruitTreeRootBH>();
            }

            if (TreeType == null)
            {
                Api.World.Logger.Error("Coding error. Fruit tree without fruit tree type @" + Pos);
                return false;
            }

            if (rootBh != null && rootBh.propsByType.TryGetValue(TreeType, out var val))
            {
                switch (val.State)
                {
                    case EnumFruitTreeState.EnterDormancy: FoliageState = EnumFoliageState.Plain; break;                    
                    case EnumFruitTreeState.Dormant: FoliageState = EnumFoliageState.DormantNoLeaves; break;
                    case EnumFruitTreeState.Flowering: FoliageState = EnumFoliageState.Flowering; break;
                    case EnumFruitTreeState.Fruiting: FoliageState = EnumFoliageState.Fruiting; break;
                    case EnumFruitTreeState.Ripe: FoliageState = EnumFoliageState.Ripe; break;
                    case EnumFruitTreeState.Empty: FoliageState = EnumFoliageState.Plain; break;
                    case EnumFruitTreeState.Young: FoliageState = EnumFoliageState.Plain; break;
                    
                    case EnumFruitTreeState.Dead: FoliageState = EnumFoliageState.Dead; break;
                }

                if (Api.Side == EnumAppSide.Server)
                {
                    rootBh.propsByType[TreeType].OnFruitingStateChange += RootBh_OnFruitingStateChange;
                }
                listenerOk = true;
                return true;
            }

            return false;
        }


        protected void RootBh_OnFruitingStateChange(EnumFruitTreeState nowState)
        {
            switch (nowState)
            {
                case EnumFruitTreeState.EnterDormancy: FoliageState = EnumFoliageState.Plain; break;
                case EnumFruitTreeState.Dormant: FoliageState = EnumFoliageState.DormantNoLeaves; break;
                case EnumFruitTreeState.Flowering: FoliageState = EnumFoliageState.Flowering; break;
                case EnumFruitTreeState.Fruiting: FoliageState = EnumFoliageState.Fruiting; break;
                case EnumFruitTreeState.Ripe: FoliageState = EnumFoliageState.Ripe; break;
                case EnumFruitTreeState.Empty: FoliageState = EnumFoliageState.Plain; break;
                case EnumFruitTreeState.Young: FoliageState = EnumFoliageState.Plain; break;
                case EnumFruitTreeState.Dead: FoliageState = EnumFoliageState.Dead; break;
            }

            calcFruitingSide();
            MarkDirty();
        }

        protected void calcFruitingSide()
        {
            fruitingSide = 0;
            for (int i = 0; i < 4; i++)
            {
                var face = BlockFacing.HORIZONTALS[i];
                if (Api.World.BlockAccessor.GetBlock(Pos.X + face.Normali.X, Pos.Y, Pos.Z + face.Normali.Z).Id == 0)
                {
                    fruitingSide |= 1 << i;
                }
            }
        }


        public void OnGrown()
        {
            if (!listenerOk) getRootBhSetupListener();
            GenMesh();

            calcFruitingSide(); // remove me
        }



        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (FoliageState == EnumFoliageState.Ripe)
            {
                Api.World.PlaySoundAt(harvestingSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                return true;
            }

            return false;
        }

        public bool OnBlockInteractStep(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
            if (Api.World.Rand.NextDouble() < 0.1)
            {
                Api.World.PlaySoundAt(harvestingSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }

            return FoliageState == EnumFoliageState.Ripe && secondsUsed < 1.3;
        }

        public void OnBlockInteractStop(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (secondsUsed > 1.1 && FoliageState == EnumFoliageState.Ripe)
            {
                FoliageState = EnumFoliageState.Plain;
                MarkDirty(true);
                
                var loc = AssetLocation.Create(Block.Attributes["branchBlock"].AsString(), Block.Code.Domain);
                var block = Api.World.GetBlock(loc) as BlockFruitTreeBranch;

                var drops = block.TypeProps[TreeType].FruitStacks;

                foreach (var drop in drops)
                {
                    ItemStack stack = drop.GetNextItemStack(1);
                    if (stack == null) continue;

                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        Api.World.SpawnItemEntity(stack, byPlayer.Entity.Pos.XYZ.Add(0, 0.5, 0));
                    }

                    if (drop.LastDrop) break;
                }

            }
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            if (FoliageState == EnumFoliageState.Ripe)
            {
                var loc = AssetLocation.Create(Block.Attributes["branchBlock"].AsString(), Block.Code.Domain);
                var block = Api.World.GetBlock(loc) as BlockFruitTreeBranch;
                var drops = block.TypeProps[TreeType].FruitStacks;
                foreach (var drop in drops)
                {
                    ItemStack stack = drop.GetNextItemStack(1);
                    if (stack == null) continue;

                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    if (drop.LastDrop) break;
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (FoliageState == EnumFoliageState.Dead && PartType != EnumTreePartType.Cutting)
            {
                dsc.AppendLine("<font color=\"#ff8080\">"+Lang.Get("Dead tree.")+"</font>");
            }

            if (rootBh != null && TreeType != null && PartType != EnumTreePartType.Cutting)
            {
                var props = rootBh?.propsByType[TreeType];
                if (props.State == EnumFruitTreeState.Ripe)
                {
                    double days = props.lastStateChangeTotalDays + props.RipeDays - rootBh.LastRootTickTotalDays;
                    dsc.AppendLine(Lang.Get("Fresh fruit for about {0:0.#} days.", days));
                }
                if (props.State == EnumFruitTreeState.Fruiting)
                {
                    double days = props.lastStateChangeTotalDays + props.FruitingDays - rootBh.LastRootTickTotalDays;
                    dsc.AppendLine(Lang.Get("Ripe in about {0:0.#} days, weather permitting.", days));
                }
                if (props.State == EnumFruitTreeState.Flowering)
                {
                    double days = props.lastStateChangeTotalDays + props.FloweringDays - rootBh.LastRootTickTotalDays;
                    dsc.AppendLine(Lang.Get("Flowering for about {0:0.#} days, weather permitting.", days));
                }
            }

            base.GetBlockInfo(forPlayer, dsc);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            EnumFoliageState prevState = FoliageState;

            PartType = (EnumTreePartType)tree.GetInt("partType");
            FoliageState = (EnumFoliageState)tree.GetInt("foliageState");
            GrowthDir = BlockFacing.ALLFACES[tree.GetInt("growthDir")];
            TreeType = tree.GetString("treeType");
            Height = tree.GetInt("height");
            fruitingSide = tree.GetInt("fruitingSide", fruitingSide);

            if (tree.HasAttribute("rootOffX"))
            {
                RootOff = new Vec3i(tree.GetInt("rootOffX"), tree.GetInt("rootOffY"), tree.GetInt("rootOffZ"));
            }

            if (Api != null && Api.Side == EnumAppSide.Client && prevState != FoliageState)
            {
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("partType", (int)PartType);
            tree.SetInt("foliageState", (int)FoliageState);
            tree.SetInt("growthDir", GrowthDir.Index);
            tree.SetString("treeType", TreeType);
            tree.SetInt("height", Height);
            tree.SetInt("fruitingSide", fruitingSide);

            if (RootOff != null)
            {
                tree.SetInt("rootOffX", RootOff.X);
                tree.SetInt("rootOffY", RootOff.Y);
                tree.SetInt("rootOffZ", RootOff.Z);
            }
        }

    }

}
