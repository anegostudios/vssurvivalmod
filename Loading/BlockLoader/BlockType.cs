using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Vintagestory.API;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockType : CollectibleType
    {
        public static Cuboidf DefaultCollisionBox = new Cuboidf(0, 0, 0, 1, 1, 1);
        public static RotatableCube DefaultCollisionBoxR = new RotatableCube(0, 0, 0, 1, 1, 1);

        public BlockType()
        {
            Class = "Block";
            Shape = new CompositeShape() { Base = new AssetLocation("game", "block/basic/cube") };
            GuiTransform = ModelTransform.BlockDefaultGui();
            FpHandTransform = ModelTransform.BlockDefault();
            TpHandTransform = ModelTransform.BlockDefaultTp();
            GroundTransform = ModelTransform.BlockDefault();
            MaxStackSize = 64;
        }

        [JsonProperty]
        public string EntityClass;
        [JsonProperty]
        public BlockBehaviorType[] Behaviors = new BlockBehaviorType[0];
        [JsonProperty]
        public EnumDrawType DrawType = EnumDrawType.JSON;
        [JsonProperty]
        public EnumRandomizeAxes RandomizeAxes = EnumRandomizeAxes.XYZ;
        [JsonProperty]
        public bool RandomDrawOffset;
        [JsonProperty]
        public EnumChunkRenderPass RenderPass = EnumChunkRenderPass.Opaque;
        [JsonProperty]
        public EnumFaceCullMode FaceCullMode = EnumFaceCullMode.Default;
        [JsonProperty]
        public CompositeShape ShapeInventory = null;
        [JsonProperty]
        public bool Ambientocclusion = true;
        [JsonProperty]
        public BlockSounds Sounds;
        [JsonProperty]
        public Dictionary<string, CompositeTexture> TexturesInventory = new Dictionary<string, CompositeTexture>();
        
        [JsonProperty]
        public Dictionary<string, bool> SideOpaque;
        [JsonProperty]
        public Dictionary<string, bool> SideAo;
        [JsonProperty]
        public Dictionary<string, bool> SideSolid;

        [JsonProperty]
        public int TintIndex;
        [JsonProperty]
        public int Replaceable;
        [JsonProperty]
        public int Fertility;

        [JsonProperty]
        public VertexFlags VertexFlags;

        [JsonProperty]
        public byte[] LightHsv = new byte[] { 0, 0, 0 };
        [JsonProperty]
        public ushort LightAbsorption = 99;
        [JsonProperty]
        public AdvancedParticleProperties[] ParticleProperties = null;
        [JsonProperty]
        public float Resistance = 6f; // How long it takes to break this block in seconds
        [JsonProperty]
        public EnumBlockMaterial BlockMaterial = EnumBlockMaterial.Stone; // Helps with finding out mining speed for each tool type
        [JsonProperty]
        public EnumMatterState MatterState = EnumMatterState.Solid;
        [JsonProperty]
        public int RequiredMiningTier;

        [JsonProperty("CollisionBox")]
        private RotatableCube CollisionBoxR = DefaultCollisionBoxR.Clone();
        [JsonProperty("SelectionBox")]
        private RotatableCube SelectionBoxR = DefaultCollisionBoxR.Clone();
        [JsonProperty("CollisionSelectionBox")]
        private RotatableCube CollisionSelectionBoxR = null;

        [JsonProperty("CollisionBoxes")]
        private RotatableCube[] CollisionBoxesR = null;
        [JsonProperty("SelectionBoxes")]
        private RotatableCube[] SelectionBoxesR = null;
        [JsonProperty("CollisionSelectionBoxes")]
        private RotatableCube[] CollisionSelectionBoxesR = null;

        public Cuboidf[] CollisionBoxes = null;
        public Cuboidf[] SelectionBoxes = null;

        [JsonProperty]
        public bool Climbable = false;
        [JsonProperty]
        public bool RainPermeable = false;
        [JsonProperty]
        public bool? SnowCoverage = null;
        
        [JsonProperty]
        public int LiquidLevel;
        [JsonProperty]
        public float WalkspeedMultiplier = 1f;
        [JsonProperty]
        public float DragMultiplier = 1f;
        [JsonProperty]
        public BlockDropItemStack[] Drops;
        
        [JsonProperty]
        public BlockCropPropertiesType CropProps = null;

        [JsonProperty]
        public string[] AllowSpawnCreatureGroups = new string[] { "*" };


        Cuboidf[] ToCuboidf(params RotatableCube[] cubes)
        {
            Cuboidf[] outcubes = new Cuboidf[cubes.Length];
            for (int i = 0; i < cubes.Length; i++)
            {
                outcubes[i] = cubes[i].RotatedCopy();
            }
            return outcubes;
        }

        Cuboidf[] ToCuboidf(InerhitableRotatableCube cube, Cuboidf parentCube)
        {
            if (parentCube == null) parentCube = DefaultCollisionBox;
            return new Cuboidf[] { cube.InheritedCopy(parentCube) };
        }


        Cuboidf[] ToCuboidf(InerhitableRotatableCube[] cubes, Cuboidf[] parentCubes)
        {
            Cuboidf[] outcubes = new Cuboidf[cubes.Length];
            for (int i = 0; i < cubes.Length; i++)
            {
                Cuboidf parentCube = null;
                if (i < parentCubes.Length) parentCube = parentCubes[i];
                else parentCube = DefaultCollisionBox;

                outcubes[i] = cubes[i].InheritedCopy(parentCube);
            }

            return outcubes;
        }

        override internal void OnDeserialized()
        {
            base.OnDeserialized();

            // Only one collision/selectionbox 
            if (CollisionBoxR != null) CollisionBoxes = ToCuboidf(CollisionBoxR);
            if (SelectionBoxR != null) SelectionBoxes = ToCuboidf(SelectionBoxR);

            // Multiple collision/selectionboxes
            if (CollisionBoxesR != null) CollisionBoxes = ToCuboidf(CollisionBoxesR);
            if (SelectionBoxesR != null) SelectionBoxes = ToCuboidf(SelectionBoxesR);

            // Merged collision+selectioboxes
            if (CollisionSelectionBoxR != null)
            {
                CollisionBoxes = ToCuboidf(CollisionSelectionBoxR);
                SelectionBoxes = ToCuboidf(CollisionSelectionBoxR);    
            }

            if (CollisionSelectionBoxesR != null)
            {
                CollisionBoxes = ToCuboidf(CollisionSelectionBoxesR);
                SelectionBoxes = ToCuboidf(CollisionSelectionBoxesR);
            }


            ResolveStringBoolDictFaces(SideSolid);
            ResolveStringBoolDictFaces(SideOpaque);
            ResolveStringBoolDictFaces(SideAo);

            TintIndex = GameMath.Clamp(TintIndex, 0, 2);

            if (LightHsv == null) LightHsv = new byte[3];

            // Boundary check light values, if they go beyond allowed values the lighting system will crash
            LightHsv[0] = (byte)GameMath.Clamp(LightHsv[0], 0, ColorUtil.HueQuantities - 1);
            LightHsv[1] = (byte)GameMath.Clamp(LightHsv[1], 0, ColorUtil.SatQuantities - 1);
            LightHsv[2] = (byte)GameMath.Clamp(LightHsv[2], 0, ColorUtil.BrightQuantities - 1);
        }
        
        
        public void InitBlock(IClassRegistryAPI instancer, ILogger logger, Block block, Dictionary<string, string> searchReplace)
        {
            BlockBehaviorType[] behaviorTypes = Behaviors;

            if (behaviorTypes != null)
            {
                List<BlockBehavior> behaviors = new List<BlockBehavior>();

                for (int i = 0; i < behaviorTypes.Length; i++)
                {
                    BlockBehaviorType behaviorType = behaviorTypes[i];

                    if (instancer.GetBlockBehaviorClass(behaviorType.name) == null)
                    {
                        logger.Warning(Lang.Get("Block behavior {0} for block {1} not found", behaviorType.name, block.Code));
                        continue;
                    }

                    BlockBehavior behavior = instancer.CreateBlockBehavior(block, behaviorType.name);
                    if (behaviorType.properties == null) behaviorType.properties = new JsonObject(new JObject());

                    behavior.Initialize(behaviorType.properties);
                    behavior.properties = behaviorType.properties;

                    behaviors.Add(behavior);
                }

                block.BlockBehaviors = behaviors.ToArray();
            }

            if (CropProps != null)
            {
                block.CropProps = new BlockCropProperties();
                block.CropProps.GrowthStages = CropProps.GrowthStages;
                block.CropProps.HarvestGrowthStageLoss = CropProps.HarvestGrowthStageLoss;
                block.CropProps.MultipleHarvests = CropProps.MultipleHarvests;
                block.CropProps.NutrientConsumption = CropProps.NutrientConsumption;
                block.CropProps.RequiredNutrient = CropProps.RequiredNutrient;
                block.CropProps.TotalGrowthDays = CropProps.TotalGrowthDays;

                if (CropProps.Behaviors != null)
                {
                    block.CropProps.Behaviors = new CropBehavior[CropProps.Behaviors.Length];
                    for(int i = 0; i < CropProps.Behaviors.Length; i++)
                    {
                        CropBehaviorType behaviorType = CropProps.Behaviors[i];
                        CropBehavior behavior = instancer.CreateCropBehavior(block, behaviorType.name);
                        if(behaviorType.properties != null)
                        {
                            behavior.Initialize(behaviorType.properties);
                        }
                        block.CropProps.Behaviors[i] = behavior;
                    }
                }
            }

            if (block.Drops == null)
            {
                block.Drops = new BlockDropItemStack[] { new BlockDropItemStack() {
                    Code = block.Code,
                    Type = EnumItemClass.Block,
                    Quantity = NatFloat.One
                } };
            }

            block.CreativeInventoryTabs = GetCreativeTabs(block.Code, CreativeInventory, searchReplace);

            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                if (SideAo != null && SideAo.ContainsKey(facing.Code))
                {
                    block.SideAo[facing.Index] = SideAo[facing.Code];
                }

                if (SideSolid != null && SideSolid.ContainsKey(facing.Code))
                {
                    block.SideSolid[facing.Index] = SideSolid[facing.Code];
                }

                if (SideOpaque != null && SideOpaque.ContainsKey(facing.Code))
                {
                    block.SideOpaque[facing.Index] = SideOpaque[facing.Code];
                }
            }
        }

        public static string[] GetCreativeTabs(AssetLocation code, Dictionary<string, string[]> CreativeInventory, Dictionary<string, string> searchReplace)
        {
            List<string> tabs = new List<string>();

            foreach (var val in CreativeInventory)
            {
                for (int i = 0; i < val.Value.Length; i++)
                {
                    string blockCode = Block.FillPlaceHolder(val.Value[i], searchReplace);

                    if (WildCardMatch(blockCode, code.Path))
                    {
                        string tabCode = val.Key;
                        tabs.Add(tabCode);
                    }
                }
            }

            return tabs.ToArray();
        }

        public static bool WildCardMatch(string wildCard, string text)
        {
            if (wildCard == text) return true;
            string pattern = Regex.Escape(wildCard).Replace(@"\*", @"(.*)");
            return Regex.IsMatch(text, @"^" + pattern + @"$");
        }

        public static bool WildCardMatches(string blockCode, List<string> wildCards, out string matchingWildcard)
        {
            foreach (string wildcard in wildCards)
            {
                if (WildCardMatch(wildcard, blockCode))
                {
                    matchingWildcard = wildcard;
                    return true;
                }
            }
            matchingWildcard = null;
            return false;
        }

        public static bool WildCardMatch(AssetLocation wildCard, AssetLocation blockCode)
        {
            if (wildCard == blockCode) return true;

            string pattern = Regex.Escape(wildCard.Path).Replace(@"\*", @"(.*)");

            return Regex.IsMatch(blockCode.Path, @"^" + pattern + @"$");
        }

        public static bool WildCardMatches(AssetLocation blockCode, List<AssetLocation> wildCards, out AssetLocation matchingWildcard)
        {
            foreach (AssetLocation wildcard in wildCards)
            {
                if (WildCardMatch(wildcard, blockCode))
                {
                    matchingWildcard = wildcard;
                    return true;
                }
            }

            matchingWildcard = null;

            return false;
        }


        void ResolveStringBoolDictFaces(Dictionary<string, bool> stringBoolDict)
        {
            if (stringBoolDict != null)
            {
                if (stringBoolDict.ContainsKey("horizontals"))
                {
                    foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                    {
                        if (!stringBoolDict.ContainsKey(facing.Code)) stringBoolDict[facing.Code] = stringBoolDict["horizontals"];
                    }
                }

                if (stringBoolDict.ContainsKey("verticals"))
                {
                    foreach (BlockFacing facing in BlockFacing.VERTICALS)
                    {
                        if (!stringBoolDict.ContainsKey(facing.Code)) stringBoolDict[facing.Code] = stringBoolDict["verticals"];
                    }
                }

                if (stringBoolDict.ContainsKey("all"))
                {
                    foreach (BlockFacing facing in BlockFacing.ALLFACES)
                    {
                        if (!stringBoolDict.ContainsKey(facing.Code)) stringBoolDict[facing.Code] = stringBoolDict["all"];
                    }
                }
            }

        }

    }
}
