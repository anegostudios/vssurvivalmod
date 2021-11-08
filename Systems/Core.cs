using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class SurvivalConfig
    {
        public JsonItemStack[] StartStacks = new JsonItemStack[] {
            new JsonItemStack() { Type = EnumItemClass.Item, Code = new AssetLocation("bread-spelt-perfect"), StackSize = 8 },
            new JsonItemStack() { Type = EnumItemClass.Block, Code = new AssetLocation("torch-up"), StackSize = 1 }
        };

        [ProtoMember(1)]
        public float[] SunLightLevels = new float[] { 0.015f, 0.176f, 0.206f, 0.236f, 0.266f, 0.296f, 0.326f, 0.356f, 0.386f, 0.416f, 0.446f, 0.476f, 0.506f, 0.536f, 0.566f, 0.596f, 0.626f, 0.656f, 0.686f, 0.716f, 0.746f, 0.776f, 0.806f, 0.836f, 0.866f, 0.896f, 0.926f, 0.956f, 0.986f, 1f, 1f, 1f};

        [ProtoMember(2)]
        public float[] BlockLightLevels = new float[] { 0.0175f, 0.06f, 0.12f, 0.18f, 0.254f, 0.289f, 0.324f, 0.359f, 0.394f, 0.429f, 0.464f, 0.499f, 0.534f, 0.569f, 0.604f, 0.639f, 0.674f, 0.709f, 0.744f, 0.779f, 0.814f, 0.849f, 0.884f, 0.919f, 0.954f, 0.989f, 1f, 1f, 1f, 1f, 1f, 1f };
        [ProtoMember(3)]
        public float PerishSpeedModifier = 1f;
        [ProtoMember(4)]
        public float CreatureDamageModifier = 1;
        [ProtoMember(5)]
        public float ToolDurabilityModifier = 1;
        [ProtoMember(6)]
        public float ToolMiningSpeedModifier = 1;
        [ProtoMember(7)]
        public float HungerSpeedModifier = 1;
        [ProtoMember(8)]
        public float BaseMoveSpeed = 1.5f;
        [ProtoMember(9)]
        public int SunBrightness = 22;
        [ProtoMember(10)]
        public int PolarEquatorDistance = 50000;


        public ItemStack[] ResolvedStartStacks;

        public void ResolveStartItems(IWorldAccessor world)
        {
            if (StartStacks == null)
            {
                ResolvedStartStacks = new ItemStack[0];
                return;
            }


            List<ItemStack> resolvedStacks = new List<ItemStack>();

            for (int i = 0; i < StartStacks.Length; i++)
            {
                if (StartStacks[i].Resolve(world, "start item stack"))
                {
                    resolvedStacks.Add(StartStacks[i].ResolvedItemstack);
                }
            }

            this.ResolvedStartStacks = resolvedStacks.ToArray();
        }
    }

    
    
    /// <summary>
    /// This class contains core settings for the Vintagestory server
    /// </summary>
    public class SurvivalCoreSystem : ModSystem
	{
        ICoreAPI api;
        ICoreClientAPI capi;
        SurvivalConfig config = new SurvivalConfig();

        public IShaderProgram anvilShaderProg;

        public Dictionary<string, MetalPropertyVariant> metalsByCode;

        public override double ExecuteOrder()
        {
            return 0.001;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void StartPre(ICoreAPI api)
        {
            // When loaded, load survival assets
            api.Assets.AddModOrigin(GlobalConstants.DefaultDomain, Path.Combine(GamePaths.AssetsPath, "survival"));
        }

        public override void Start(ICoreAPI api)
        {
            GameVersion.EnsureEqualVersionOrKillExecutable(api, System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion, GameVersion.OverallVersion, "VSSurvivalMod");

            this.api = api;
            api.Network.RegisterChannel("survivalCoreConfig").RegisterMessageType<SurvivalConfig>();

            RegisterDefaultBlocks();
            RegisterDefaultBlockBehaviors();
            RegisterDefaultBlockEntityBehaviors();

            RegisterDefaultCollectibleBehaviors();

            RegisterDefaultCropBehaviors();
            RegisterDefaultItems();
            RegisterDefaultEntities();
            RegisterDefaultEntityBehaviors();
            RegisterDefaultBlockEntities();

            api.RegisterMountable("bed", BlockBed.GetMountable);


            metalsByCode = new Dictionary<string, MetalPropertyVariant>();

            MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();
            for (int i = 0; i < metals.Variants.Length; i++)
            {
                // Metals currently don't have a domain
                metalsByCode[metals.Variants[i].Code.Path] = metals.Variants[i];
            }
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Network.GetChannel("survivalCoreConfig").SetMessageHandler<SurvivalConfig>(onConfigFromServer);

            api.Event.LevelFinalize += () =>
            {
                api.World.Calendar.OnGetSolarAltitude = GetSolarAltitude;
                api.World.Calendar.OnGetHemisphere = GetHemisphere;
                applySeasonConfig();
            };

            api.Event.ReloadShader += LoadShader;
            LoadShader();            
        }


        ICoreServerAPI sapi;


        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            if (api.ModLoader.IsModSystemEnabled("Vintagestory.ServerMods.WorldEdit.WorldEdit"))
            {
                RegisterUtil.RegisterTool(api.ModLoader.GetModSystem("Vintagestory.ServerMods.WorldEdit.WorldEdit"));
            }

            // Set up day/night cycle
            api.WorldManager.SetBlockLightLevels(config.BlockLightLevels);
            api.WorldManager.SetSunLightLevels(config.SunLightLevels);
            api.WorldManager.SetSunBrightness(config.SunBrightness);

            api.Event.PlayerCreate += Event_PlayerCreate;
            api.Event.PlayerNowPlaying += Event_PlayerPlaying;
            api.Event.PlayerJoin += Event_PlayerJoin;
            
            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadConfig);

            api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => {
                applyConfig();
                config.ResolveStartItems(api.World);
                api.World.Calendar.OnGetSolarAltitude = GetSolarAltitude;
                api.World.Calendar.OnGetHemisphere = GetHemisphere;
            });

            AiTaskRegistry.Register<AiTaskBellAlarm>("bellalarm");
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            sapi.Network.GetChannel("survivalCoreConfig").SendPacket(config, byPlayer);
        }

        private void onConfigFromServer(SurvivalConfig networkMessage)
        {
            this.config = networkMessage;
            applyConfig();
        }

        private EnumHemisphere GetHemisphere(double posX, double posZ)
        {
            return api.World.Calendar.OnGetLatitude(posZ) > 0 ? EnumHemisphere.North : EnumHemisphere.South;
        }

        private bool LoadShader()
        {
            anvilShaderProg = capi.Shader.NewShaderProgram();

            anvilShaderProg.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            anvilShaderProg.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            capi.Shader.RegisterFileShaderProgram("anvilworkitem", anvilShaderProg);

            return anvilShaderProg.Compile();
        }


        HashSet<string> createdPlayers = new HashSet<string>();
        public float EarthAxialTilt = 23.44f * GameMath.DEG2RAD;



        /// <summary>
        /// Returns the solar altitude from -1 to 1 for given latitude
        /// </summary>
        /// <param name="latitude">Must be between -1 and 1. -1 is the south pole, 0 is the equater, and 1 is the north pole</param>
        /// <returns></returns>
        public float GetSolarAltitude(double posX, double posZ, float yearRel, float dayRel)
        {
            float latitude = (float)api.World.Calendar.OnGetLatitude(posZ);

            // https://en.wikipedia.org/wiki/Solar_zenith_angle
            // theta = sin(phi) * sin(delta) + cos(phi) * cos(delta) * cos(h)
            // theta: the solar zenith angle

            // phi: the local latitude
            // h: the hour angle, in the local solar time.
            // delta: is the current declination of the Sun

            //  the solar hour angle is an expression of time, expressed in angular measurement, usually degrees, from solar noon
            float h = GameMath.TWOPI * (dayRel - 0.5f);

            float dayOfYear = api.World.Calendar.DayOfYear;
            float daysPerYear = api.World.Calendar.DaysPerYear;

            // The Sun's declination at any given moment is calculated by: 
            // delta = arcsin(sin(-23.44°) * sin(EL))
            // EL is the ecliptic longitude (essentially, the Earth's position in its orbit). Since the Earth's orbital eccentricity is small, its orbit can be approximated as a circle which causes up to 1° of error. 
            // delta = -23.44° * cos(360° / 365 * (yearRel + 10))
            // The number 10, in (N+10), is the approximate number of days after the December solstice to January 1
            float delta = -EarthAxialTilt * GameMath.Cos(GameMath.TWOPI * (dayOfYear + 10) / daysPerYear);

            // sin(1.35) * sin(-0.37) + cos(1.35) * cos(-0.37) * cos((x/24 - 0.5) * 3.14159 * 2)

            // sample 1
            // latitude = 0.5 (equator)
            // day of year = 0.5 (summer)
            // sin(0.5) * sin(0.5 * -0.4) + cos(0.5) * cos(0.5 * -0.4) * cos((x / 24 - 0.5) * 3.14159 * 2)
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJzaW4oMC41KSpzaW4oMC41Ki0wLjQpK2NvcygwLjUpKmNvcygwLjUqLTAuNCkqY29zKCh4LzI0LTAuNSkqMy4xNDE1OSoyKSIsImNvbG9yIjoiIzAwMDAwMCJ9LHsidHlwZSI6MTAwMCwid2luZG93IjpbIjAiLCIyNCIsIi0xIiwiMSJdfV0-

            // sample 2
            // latitude = 0.5 (~austria, europe)
            // day of year = 1 (winter)
            // sin(3.14159/2 * 0.5) * sin(1 * -0.4) + cos(3.14159/2 * 0.5) * cos(1 * -0.4) * cos((x / 24 - 0.5) * 3.14159 * 2)
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJzaW4oMy4xNDE1OS80KSpzaW4oMSotMC40KStjb3MoMy4xNDE1OS80KSpjb3MoMSotMC40KSpjb3MoKHgvMjQtMC41KSozLjE0MTU5KjIpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjI0IiwiLTEiLCIxIl19XQ--

            // sample 3
            // latitude = -1 (south pole)
            // day of year = 1 (winter)
            // sin(3.14159/2 * -1) * sin(1 * -0.4) + cos(3.14159/2 * -1) * cos(1 * -0.4) * cos((x / 24 - 0.5) * 3.14159 * 2)
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJzaW4oMy4xNDE1OS8yKi0xKSpzaW4oMSotMC40KStjb3MoMy4xNDE1OS8yKi0xKSpjb3MoMSotMC40KSpjb3MoKHgvMjQtMC41KSozLjE0MTU5KjIpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjI0IiwiLTEiLCIxIl19XQ--


            // So this method is supposed to return the solar zenith or 90 - SolartAltitude, but apparently its inverted, dunno why
            return GameMath.Sin(latitude * GameMath.PIHALF) * GameMath.Sin(delta) + GameMath.Cos(latitude * GameMath.PIHALF) * GameMath.Cos(delta) * GameMath.Cos(h);
        }





        private void Event_PlayerPlaying(IServerPlayer byPlayer)
        {
            if (createdPlayers.Contains(byPlayer.PlayerUID))
            {
                createdPlayers.Remove(byPlayer.PlayerUID);
                if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative || byPlayer.WorldData.CurrentGameMode == EnumGameMode.Spectator) return;

                for (int i = 0; i < config.ResolvedStartStacks.Length; i++)
                {
                    byPlayer.Entity.TryGiveItemStack(config.ResolvedStartStacks[i].Clone());
                }
            }

        }

        private void Event_PlayerCreate(IServerPlayer byPlayer)
        {
            createdPlayers.Add(byPlayer.PlayerUID);
        }


        private void loadConfig()
        {
            try
            {
                IAsset asset = api.Assets.TryGet("config/general.json");
                if (asset != null)
                {
                    config = asset.ToObject<SurvivalConfig>();
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading survivalconfig.json, error {0}. Will initialize new one", e);
                config = new SurvivalConfig();
            }


            // Called on both sides
        }


        private void applyConfig()
        {
            GlobalConstants.PerishSpeedModifier = config.PerishSpeedModifier;
            GlobalConstants.ToolMiningSpeedModifier = config.ToolMiningSpeedModifier;
            GlobalConstants.HungerSpeedModifier = config.HungerSpeedModifier;
            GlobalConstants.BaseMoveSpeed = config.BaseMoveSpeed;
            GlobalConstants.CreatureDamageModifier = config.CreatureDamageModifier;

            if (api.Side == EnumAppSide.Server) // Don't apply on the client because we already sent him the increased durabilities for each tool
            {
                foreach (var obj in api.World.Collectibles)
                {
                    if (obj.Tool != null)
                    {
                        obj.Durability = (int)(obj.Durability * config.ToolDurabilityModifier);
                    }
                }
            }

            applySeasonConfig();
        }

        void applySeasonConfig()
        {
            ITreeAttribute worldConfig = api.World.Config;
            string seasons = worldConfig.GetString("seasons");
            if (seasons == "spring")
            {
                api.World.Calendar.SetSeasonOverride(0.33f);
            }
        }


        

        private void RegisterDefaultBlocks()
        {
            api.RegisterBlockClass("BlockFirepit", typeof(BlockFirepit));
            api.RegisterBlockClass("BlockCharcoalPit", typeof(BlockCharcoalPit));
            api.RegisterBlockClass("BlockTorch", typeof(BlockTorch));
            api.RegisterBlockClass("BlockStairs", typeof(BlockStairs));
            api.RegisterBlockClass("BlockFence", typeof(BlockFence));
            api.RegisterBlockClass("BlockFenceStackAware", typeof(BlockFenceStackAware));
            api.RegisterBlockClass("BlockDoor", typeof(BlockDoor));
            api.RegisterBlockClass("BlockTrapdoor", typeof(BlockTrapdoor));
            api.RegisterBlockClass("BlockFenceGate", typeof(BlockFenceGate));
            api.RegisterBlockClass("BlockFenceGateRoughHewn", typeof(BlockFenceGateRoughHewn));
            api.RegisterBlockClass("BlockLayered", typeof(BlockLayered));
            api.RegisterBlockClass("BlockVines", typeof(BlockVines));
            api.RegisterBlockClass("BlockPlant", typeof(BlockPlant));
            api.RegisterBlockClass("BlockRails", typeof(BlockRails));
            api.RegisterBlockClass("BlockCactus", typeof(BlockCactus));
            api.RegisterBlockClass("BlockSlab", typeof(BlockSlab));
            api.RegisterBlockClass("BlockPlantContainer", typeof(BlockPlantContainer));
            api.RegisterBlockClass("BlockSign", typeof(BlockSign));
            api.RegisterBlockClass("BlockSimpleCoating", typeof(BlockSimpleCoating));
            api.RegisterBlockClass("BlockFullCoating", typeof(BlockFullCoating));
            api.RegisterBlockClass("BlockBed", typeof(BlockBed));
            api.RegisterBlockClass("BlockBerryBush", typeof(BlockBerryBush));
            api.RegisterBlockClass("BlockWaterLily", typeof(BlockWaterLily));
            api.RegisterBlockClass("BlockLooseStones", typeof(BlockLooseStones));
            api.RegisterBlockClass("BlockIngotPile", typeof(BlockIngotPile));
            api.RegisterBlockClass("BlockPeatPile", typeof(BlockPeatPile));

            api.RegisterBlockClass("BlockBucket", typeof(BlockBucket));
            api.RegisterBlockClass("BlockCrop", typeof(BlockCrop));
            api.RegisterBlockClass("BlockCropCustomWave", typeof(BlockCropCustomWave));
            api.RegisterBlockClass("BlockFruiting", typeof(BlockFruiting));
            api.RegisterBlockClass("BlockWaterPlant", typeof(BlockWaterPlant));
            api.RegisterBlockClass("BlockSeaweed", typeof(BlockSeaweed));
            
            api.RegisterBlockClass("BlockFirewoodPile", typeof(BlockFirewoodPile));
            api.RegisterBlockClass("BlockToolRack", typeof(BlockToolRack));
            api.RegisterBlockClass("BlockSmeltingContainer", typeof(BlockSmeltingContainer));
            api.RegisterBlockClass("BlockSmeltedContainer", typeof(BlockSmeltedContainer));

            api.RegisterBlockClass("BlockCookingContainer", typeof(BlockCookingContainer));
            api.RegisterBlockClass("BlockCookedContainer", typeof(BlockCookedContainer));

            api.RegisterBlockClass("BlockIngotMold", typeof(BlockIngotMold));
            api.RegisterBlockClass("BlockPlatePile", typeof(BlockPlatePile));
            api.RegisterBlockClass("BlockPlankPile", typeof(BlockPlankPile));

            api.RegisterBlockClass("BlockAnvil", typeof(BlockAnvil));
            api.RegisterBlockClass("BlockForge", typeof(BlockForge));
            api.RegisterBlockClass("BlockClayOven", typeof(BlockClayOven));
            api.RegisterBlockClass("BlockLootVessel", typeof(BlockLootVessel));
            api.RegisterBlockClass("BlockBomb", typeof(BlockBomb));
            api.RegisterBlockClass("BlockToolMold", typeof(BlockToolMold));
            api.RegisterBlockClass("BlockLayeredSlowDig", typeof(BlockLayeredSlowDig));
            api.RegisterBlockClass("BlockClayForm", typeof(BlockClayForm));
            api.RegisterBlockClass("BlockKnappingSurface", typeof(BlockKnappingSurface));
            api.RegisterBlockClass("BlockBamboo", typeof(BlockBamboo));
            api.RegisterBlockClass("BlockWithLeavesMotion", typeof(BlockWithLeavesMotion));
            api.RegisterBlockClass("BlockReeds", typeof(BlockReeds));
            api.RegisterBlockClass("BlockBloomery", typeof(BlockBloomery));
            api.RegisterBlockClass("BlockOre", typeof(BlockOre));
            api.RegisterBlockClass("BlockLava", typeof(BlockLava));
            api.RegisterBlockClass("BlockMushroom", typeof(BlockMushroom));
            api.RegisterBlockClass("BlockSoil", typeof(BlockSoil));
            api.RegisterBlockClass("BlockSkep", typeof(BlockSkep));
            api.RegisterBlockClass("BlockBeehive", typeof(BlockBeehive));
            api.RegisterBlockClass("BlockLantern", typeof(BlockLantern));
            api.RegisterBlockClass("BlockChisel", typeof(BlockChisel));
            api.RegisterBlockClass("BlockMicroBlock", typeof(BlockMicroBlock));
            api.RegisterBlockClass("BlockTorchHolder", typeof(BlockTorchHolder));
            api.RegisterBlockClass("BlockGenericTypedContainer", typeof(BlockGenericTypedContainer));
            api.RegisterBlockClass("BlockTeleporter", typeof(BlockTeleporter));
            api.RegisterBlockClass("BlockQuern", typeof(BlockQuern));
            api.RegisterBlockClass("BlockWithGrassOverlay", typeof(BlockWithGrassOverlay));
            api.RegisterBlockClass("BlockTinted", typeof(BlockTinted));
            api.RegisterBlockClass("BlockGlassPane", typeof(BlockGlassPane));
            api.RegisterBlockClass("BlockPlaceOnDrop", typeof(BlockPlaceOnDrop));
            api.RegisterBlockClass("BlockLooseGears", typeof(BlockLooseGears));
            api.RegisterBlockClass("BlockSpawner", typeof(BlockSpawner));
            api.RegisterBlockClass("BlockMeal", typeof(BlockMeal));
            api.RegisterBlockClass("BlockBowl", typeof(BlockBowl));
            api.RegisterBlockClass("BlockWateringCan", typeof(BlockWateringCan));
            api.RegisterBlockClass("BlockTrough", typeof(BlockTrough));
            api.RegisterBlockClass("BlockTroughDoubleBlock", typeof(BlockTroughDoubleBlock));
            api.RegisterBlockClass("BlockLeaves", typeof(BlockLeaves));
            api.RegisterBlockClass("BlockLeavesNarrow", typeof(BlockLeavesNarrow));
            api.RegisterBlockClass("BlockBough", typeof(BlockBough));
            api.RegisterBlockClass("BlockFarmland", typeof(BlockFarmland));
            api.RegisterBlockClass("BlockSticksLayer", typeof(BlockSticksLayer));

            api.RegisterBlockClass("BlockAxle", typeof(BlockAxle));
            api.RegisterBlockClass("BlockAngledGears", typeof(BlockAngledGears));
            api.RegisterBlockClass("BlockWindmillRotor", typeof(BlockWindmillRotor));
            api.RegisterBlockClass("BlockToggle", typeof(BlockToggle));
            api.RegisterBlockClass("BlockPulverizer", typeof(BlockPulverizer));

            api.RegisterBlockClass("BlockSoilDeposit", typeof(BlockSoilDeposit));
            api.RegisterBlockClass("BlockMetalPartPile", typeof(BlockMetalPartPile));

            api.RegisterBlockClass("BlockStaticTranslocator", typeof(BlockStaticTranslocator));

            api.RegisterBlockClass("BlockCrystal", typeof(BlockCrystal));

            api.RegisterBlockClass("BlockWaterfall", typeof(BlockWaterfall));
            api.RegisterBlockClass("BlockLupine", typeof(BlockLupine));

            api.RegisterBlockClass("BlockEchoChamber", typeof(BlockEchoChamber));
            api.RegisterBlockClass("BlockMeteorite", typeof(BlockMeteorite));

            api.RegisterBlockClass("BlockChandelier", typeof(BlockChandelier));

            api.RegisterBlockClass("BlockRequireSolidGround", typeof(BlockRequireSolidGround));
            api.RegisterBlockClass("BlockLocustNest", typeof(BlockLocustNest));
            api.RegisterBlockClass("BlockMetalSpikes", typeof(BlockMetalSpikes));
            api.RegisterBlockClass("BlockLabeledChest", typeof(BlockLabeledChest));
            api.RegisterBlockClass("BlockStalagSection", typeof(BlockStalagSection));
            api.RegisterBlockClass("BlockLooseOres", typeof(BlockLooseOres));
            api.RegisterBlockClass("BlockPan", typeof(BlockPan));

            api.RegisterBlockClass("BlockLog", typeof(BlockLog));
            api.RegisterBlockClass("BlockLogSection", typeof(BlockLogSection));
            api.RegisterBlockClass("BlockHopper", typeof(BlockHopper));
            api.RegisterBlockClass("BlockBarrel", typeof(BlockBarrel));
            api.RegisterBlockClass("BlockWaterflowing", typeof(BlockWaterflowing));
            api.RegisterBlockClass("BlockShelf", typeof(BlockShelf));
            api.RegisterBlockClass("BlockCrock", typeof(BlockCrock));
            api.RegisterBlockClass("BlockThermalDiff", typeof(BlockThermalDifference));
            api.RegisterBlockClass("BlockSignPost", typeof(BlockSignPost));
            api.RegisterBlockClass("BlockHenbox", typeof(BlockHenbox));

            api.RegisterBlockClass("BlockPeatbrick", typeof(BlockPeatbrick));
            api.RegisterBlockClass("BlockWater", typeof(BlockWater));
            api.RegisterBlockClass("BlockSeashell", typeof(BlockSeashell));
            api.RegisterBlockClass("BlockCanvas", typeof(BlockCanvas));
            api.RegisterBlockClass("BlockGlowworms", typeof(BlockGlowworms));

            api.RegisterBlockClass("BlockHelveHammer", typeof(BlockHelveHammer));
            api.RegisterBlockClass("BlockSnow", typeof(BlockSnow));

            api.RegisterBlockClass("BlockClutch", typeof(BlockClutch));
            api.RegisterBlockClass("BlockTransmission", typeof(BlockTransmission));
            api.RegisterBlockClass("BlockBrake", typeof(BlockBrake));
            api.RegisterBlockClass("BlockCreativeRotor", typeof(BlockCreativeRotor));
            api.RegisterBlockClass("BlockLargeGear3m", typeof(BlockLargeGear3m));
            api.RegisterBlockClass("BlockMPMultiblockGear", typeof(BlockMPMultiblockGear));
            api.RegisterBlockClass("BlockMPMultiblockPulverizer", typeof(BlockMPMultiblockPulverizer));

            api.RegisterBlockClass("BlockDisplayCase", typeof(BlockDisplayCase));
            api.RegisterBlockClass("BlockTapestry", typeof(BlockTapestry));

            api.RegisterBlockClass("BlockBunchOCandles", typeof(BlockBunchOCandles));
            api.RegisterBlockClass("BlockChute", typeof(BlockChute));

            api.RegisterBlockClass("BlockArchimedesScrew", typeof(BlockArchimedesScrew));
            api.RegisterBlockClass("BlockFernTree", typeof(BlockFernTree));
            api.RegisterBlockClass("BlockFern", typeof(BlockFern));
            api.RegisterBlockClass("BlockSlabSnowRemove", typeof(BlockSlabSnowRemove));

            api.RegisterBlockClass("BlockLakeIce", typeof(BlockLakeIce));
            api.RegisterBlockClass("BlockGlacierIce", typeof(BlockGlacierIce));
            
            api.RegisterBlockClass("BlockSnowLayer", typeof(BlockSnowLayer));
            api.RegisterBlockClass("BlockAnvilPart", typeof(BlockAnvilPart));
            api.RegisterBlockClass("BlockCheeseCurdsBundle", typeof(BlockCheeseCurdsBundle));
            api.RegisterBlockClass("BlockCheese", typeof(BlockCheese));
            api.RegisterBlockClass("BlockLinen", typeof(BlockLinen));
            api.RegisterBlockClass("BlockMoldRack", typeof(BlockMoldRack));
            api.RegisterBlockClass("BlockStoneCoffinSection", typeof(BlockStoneCoffinSection));
            api.RegisterBlockClass("BlockCoalPile", typeof(BlockCoalPile));
            api.RegisterBlockClass("BlockCharcoalPile", typeof(BlockCharcoalPile));
            api.RegisterBlockClass("BlockRefractoryBrick", typeof(BlockRefractoryBrick));
            api.RegisterBlockClass("BlockCokeOvenDoor", typeof(BlockCokeOvenDoor));
            api.RegisterBlockClass("BlockStoneCoffinLid", typeof(BlockStoneCoffinLid));

            api.RegisterBlockClass("BlockGroundStorage", typeof(BlockGroundStorage));
            api.RegisterBlockClass("BlockPitkiln", typeof(BlockPitkiln));
            api.RegisterBlockClass("BlockPie", typeof(BlockPie));
            api.RegisterBlockClass("BlockHangingLichen", typeof(BlockHangingLichen));
            api.RegisterBlockClass("BlockDeadCrop", typeof(BlockDeadCrop));
            api.RegisterBlockClass("BlockFruitPress", typeof(BlockFruitPress));
            api.RegisterBlockClass("BlockFruitPressTop", typeof(BlockFruitPressTop));
        }

        
        private void RegisterDefaultBlockBehaviors()
        {
            api.RegisterBlockBehaviorClass("HorizontalAttachable", typeof(BlockBehaviorHorizontalAttachable));
            api.RegisterBlockBehaviorClass("HorizontalOrientable", typeof(BlockBehaviorHorizontalOrientable));
            api.RegisterBlockBehaviorClass("NWOrientable", typeof(BlockBehaviorNWOrientable));
            api.RegisterBlockBehaviorClass("Pillar", typeof(BlockBehaviorPillar));
            api.RegisterBlockBehaviorClass("Slab", typeof(BlockBehaviorSlab));
            api.RegisterBlockBehaviorClass("HorizontalUpDownOrientable", typeof(BlockBehaviorHorUDOrientable));
            api.RegisterBlockBehaviorClass("FiniteSpreadingLiquid", typeof(BlockBehaviorFiniteSpreadingLiquid));
            api.RegisterBlockBehaviorClass("OmniAttachable", typeof(BlockBehaviorOmniAttachable));
            api.RegisterBlockBehaviorClass("Unplaceable", typeof(BlockBehaviorUnplaceable));
            api.RegisterBlockBehaviorClass("Unstable", typeof(BlockBehaviorUnstable));
            api.RegisterBlockBehaviorClass("Harvestable", typeof(BlockBehaviorHarvestable));
            api.RegisterBlockBehaviorClass("NoParticles", typeof(BlockBehaviorNoParticles));
            api.RegisterBlockBehaviorClass("Container", typeof(BlockBehaviorContainer));
            api.RegisterBlockBehaviorClass("Ignitable", typeof(BlockBehaviorIgniteable));
            api.RegisterBlockBehaviorClass("UnstableFalling", typeof(BlockBehaviorUnstableFalling));
            api.RegisterBlockBehaviorClass("BreakIfFloating", typeof(BlockBehaviorBreakIfFloating));
            api.RegisterBlockBehaviorClass("CanIgnite", typeof(BlockBehaviorCanIgnite));
            api.RegisterBlockBehaviorClass("ExchangeOnInteract", typeof(BlockBehaviorExchangeOnInteract));
            api.RegisterBlockBehaviorClass("Ladder", typeof(BlockBehaviorLadder));
            api.RegisterBlockBehaviorClass("OmniRotatable", typeof(BlockBehaviorOmniRotatable));
            api.RegisterBlockBehaviorClass("PushEventOnBlockBroken", typeof(BlockBehaviorPushEventOnBlockBroken));
            api.RegisterBlockBehaviorClass("RightClickPickup", typeof(BlockBehaviorRightClickPickup));
            api.RegisterBlockBehaviorClass("SneakPlacing", typeof(BlockBehaviorSneakPlacing));
            api.RegisterBlockBehaviorClass("CollectFrom", typeof(BehaviorCollectFrom));
            api.RegisterBlockBehaviorClass("Lockable", typeof(BlockBehaviorLockable));
            api.RegisterBlockBehaviorClass("DropNotSnowCovered", typeof(BlockBehaviorDropNotSnowCovered));
            api.RegisterBlockBehaviorClass("CanAttach", typeof(BlockBehaviorCanAttach));
            api.RegisterBlockBehaviorClass("MilkingContainer", typeof(BlockBehaviorMilkingContainer));
            api.RegisterBlockBehaviorClass("HeatSource", typeof(BlockBehaviorHeatSource));
            api.RegisterBlockBehaviorClass("BreakSnowFirst", typeof(BlockBehaviorBreakSnowFirst));
            api.RegisterBlockBehaviorClass("RopeTieable", typeof(BlockBehaviorRopeTieable));
        }

        private void RegisterDefaultBlockEntityBehaviors()
        {
            api.RegisterBlockEntityBehaviorClass("Animatable", typeof(BEBehaviorAnimatable));

            api.RegisterBlockEntityBehaviorClass("MPAxle", typeof(BEBehaviorMPAxle));
            api.RegisterBlockEntityBehaviorClass("MPToggle", typeof(BEBehaviorMPToggle));
            api.RegisterBlockEntityBehaviorClass("MPAngledGears", typeof(BEBehaviorMPAngledGears));
            api.RegisterBlockEntityBehaviorClass("MPConsumer", typeof(BEBehaviorMPConsumer));
            api.RegisterBlockEntityBehaviorClass("MPBrake", typeof(BEBehaviorMPBrake));
            api.RegisterBlockEntityBehaviorClass("MPTransmission", typeof(BEBehaviorMPTransmission));
            api.RegisterBlockEntityBehaviorClass("MPWindmillRotor", typeof(BEBehaviorWindmillRotor));
            api.RegisterBlockEntityBehaviorClass("MPCreativeRotor", typeof(BEBehaviorMPCreativeRotor));
            api.RegisterBlockEntityBehaviorClass("MPLargeGear3m", typeof(BEBehaviorMPLargeGear3m));
            api.RegisterBlockEntityBehaviorClass("MPArchimedesScrew", typeof(BEBehaviorMPArchimedesScrew));
            api.RegisterBlockEntityBehaviorClass("MPPulverizer", typeof(BEBehaviorMPPulverizer));

            api.RegisterBlockEntityBehaviorClass("Burning", typeof(BEBehaviorBurning));
            api.RegisterBlockEntityBehaviorClass("FirepitAmbient", typeof(BEBehaviorFirepitAmbient));
            api.RegisterBlockEntityBehaviorClass("Fruiting", typeof(BEBehaviorFruiting));
        }

        private void RegisterDefaultCollectibleBehaviors()
        {
            api.RegisterCollectibleBehaviorClass("GroundStorable", typeof(CollectibleBehaviorGroundStorable));
            api.RegisterCollectibleBehaviorClass("ArtPigment", typeof(CollectibleBehaviorArtPigment));
        }



        private void RegisterDefaultBlockEntities()
        {
            api.RegisterBlockEntityClass("GenericContainer", typeof(BlockEntityGenericContainer));
            api.RegisterBlockEntityClass("GenericTypedContainer", typeof(BlockEntityGenericTypedContainer));
            api.RegisterBlockEntityClass("Sign", typeof(BlockEntitySign));

            api.RegisterBlockEntityClass("BerryBush", typeof(BlockEntityBerryBush));
            
            api.RegisterBlockEntityClass("IngotPile", typeof(BlockEntityIngotPile));
            api.RegisterBlockEntityClass("PeatPile", typeof(BlockEntityPeatPile));
            api.RegisterBlockEntityClass("PlatePile", typeof(BlockEntityPlatePile));
            api.RegisterBlockEntityClass("PlankPile", typeof(BlockEntityPlankPile));
            api.RegisterBlockEntityClass("FirewoodPile", typeof(BlockEntityFirewoodPile));
            api.RegisterBlockEntityClass("CoalPile", typeof(BlockEntityCoalPile));

            api.RegisterBlockEntityClass("Farmland", typeof(BlockEntityFarmland));
            api.RegisterBlockEntityClass("ToolRack", typeof(BlockEntityToolrack));
            api.RegisterBlockEntityClass("IngotMold", typeof(BlockEntityIngotMold));

            api.RegisterBlockEntityClass("CookedContainer", typeof(BlockEntityCookedContainer));
            api.RegisterBlockEntityClass("Meal", typeof(BlockEntityMeal));
            api.RegisterBlockEntityClass("SmeltedContainer", typeof(BlockEntitySmeltedContainer));

            api.RegisterBlockEntityClass("Anvil", typeof(BlockEntityAnvil));
            api.RegisterBlockEntityClass("Forge", typeof(BlockEntityForge));
            api.RegisterBlockEntityClass("Bomb", typeof(BlockEntityBomb));
            api.RegisterBlockEntityClass("ToolMold", typeof(BlockEntityToolMold));

            
            api.RegisterBlockEntityClass("CharcoalPit", typeof(BlockEntityCharcoalPit));
            api.RegisterBlockEntityClass("PumpkinVine", typeof(BlockEntityPumpkinVine));
            api.RegisterBlockEntityClass("ClayForm", typeof(BlockEntityClayForm));
            api.RegisterBlockEntityClass("KnappingSurface", typeof(BlockEntityKnappingSurface));
            api.RegisterBlockEntityClass("Bloomery", typeof(BlockEntityBloomery));
            api.RegisterBlockEntityClass("Bed", typeof(BlockEntityBed));
            api.RegisterBlockEntityClass("Firepit", typeof(BlockEntityFirepit));
            api.RegisterBlockEntityClass("Stove", typeof(BlockEntityStove));
            api.RegisterBlockEntityClass("Oven", typeof(BlockEntityOven));
            api.RegisterBlockEntityClass("Beehive", typeof(BlockEntityBeehive));
            api.RegisterBlockEntityClass("Lantern", typeof(BELantern));
            api.RegisterBlockEntityClass("Chisel", typeof(BlockEntityChisel));
            api.RegisterBlockEntityClass("MicroBlock", typeof(BlockEntityMicroBlock));
            api.RegisterBlockEntityClass("Teleporter", typeof(BlockEntityTeleporter));
            api.RegisterBlockEntityClass("Quern", typeof(BlockEntityQuern));
            api.RegisterBlockEntityClass("Spawner", typeof(BlockEntitySpawner));
            api.RegisterBlockEntityClass("WateringCan", typeof(BlockEntityWateringCan));
            api.RegisterBlockEntityClass("Bucket", typeof(BlockEntityBucket));
            api.RegisterBlockEntityClass("Trough", typeof(BlockEntityTrough));
            api.RegisterBlockEntityClass("Layer", typeof(BlockEntityLayer));
            api.RegisterBlockEntityClass("HenBox", typeof(BlockEntityHenBox));

            api.RegisterBlockEntityClass("StaticTranslocator", typeof(BlockEntityStaticTranslocator));
            api.RegisterBlockEntityClass("EchoChamber", typeof(BlockEntityEchoChamber));

            api.RegisterBlockEntityClass("LocustNest", typeof(BlockEntityLocustNest));
            api.RegisterBlockEntityClass("LabeledChest", typeof(BlockEntityLabeledChest));
            api.RegisterBlockEntityClass("Barrel", typeof(BlockEntityBarrel));
			api.RegisterBlockEntityClass("ItemFlow", typeof(BlockEntityItemFlow));
            api.RegisterBlockEntityClass("Crock", typeof(BlockEntityCrock));
            api.RegisterBlockEntityClass("Shelf", typeof(BlockEntityShelf));
            api.RegisterBlockEntityClass("SignPost", typeof(BlockEntitySignPost));
            api.RegisterBlockEntityClass("Torch", typeof(BlockEntityTorch));
            api.RegisterBlockEntityClass("Canvas", typeof(BlockEntityCanvas));

            api.RegisterBlockEntityClass("HelveHammer", typeof(BEHelveHammer));
            api.RegisterBlockEntityClass("Clutch", typeof(BEClutch));
            api.RegisterBlockEntityClass("Brake", typeof(BEBrake));
            api.RegisterBlockEntityClass("Pulverizer", typeof(BEPulverizer));
            api.RegisterBlockEntityClass("LargeGear3m", typeof(BELargeGear3m));
            api.RegisterBlockEntityClass("MPMultiblock", typeof(BEMPMultiblock));

            api.RegisterBlockEntityClass("DisplayCase", typeof(BlockEntityDisplayCase));
            api.RegisterBlockEntityClass("Tapestry", typeof(BlockEntityTapestry));
            api.RegisterBlockEntityClass("PlantContainer", typeof(BlockEntityPlantContainer));

            api.RegisterBlockEntityClass("ArchimedesScrew", typeof(BlockEntityArchimedesScrew));

            api.RegisterBlockEntityClass("AnvilPart", typeof(BlockEntityAnvilPart));
            api.RegisterBlockEntityClass("Cheese", typeof(BECheese));
            api.RegisterBlockEntityClass("CheeseCurdsBundle", typeof(BECheeseCurdsBundle));
            api.RegisterBlockEntityClass("MoldRack", typeof(BlockEntityMoldRack));
            api.RegisterBlockEntityClass("StoneCoffin", typeof(BlockEntityStoneCoffin));

            api.RegisterBlockEntityClass("GroundStorage", typeof(BlockEntityGroundStorage));
            api.RegisterBlockEntityClass("PitKiln", typeof(BlockEntityPitKiln));
            api.RegisterBlockEntityClass("Pie", typeof(BlockEntityPie));
            api.RegisterBlockEntityClass("DeadCrop", typeof(BlockEntityDeadCrop));
            api.RegisterBlockEntityClass("FruitPress", typeof(BlockEntityFruitPress));
        }


        private void RegisterDefaultCropBehaviors()
        {
            api.RegisterCropBehavior("Pumpkin", typeof(PumpkinCropBehavior));
        }


        private void RegisterDefaultItems()
        {
            api.RegisterItemClass("ItemNugget", typeof(ItemNugget));
            api.RegisterItemClass("ItemOre", typeof(ItemOre));
            api.RegisterItemClass("ItemStone", typeof(ItemStone));
            api.RegisterItemClass("ItemDryGrass", typeof(ItemDryGrass));
            api.RegisterItemClass("ItemFirewood", typeof(ItemFirewood));
            api.RegisterItemClass("ItemPlank", typeof(ItemPlank));
            api.RegisterItemClass("ItemHoe", typeof(ItemHoe));
            api.RegisterItemClass("ItemPlantableSeed", typeof(ItemPlantableSeed));
            api.RegisterItemClass("ItemIngot", typeof(ItemIngot));
            api.RegisterItemClass("ItemMetalPlate", typeof(ItemMetalPlate));
            api.RegisterItemClass("ItemHammer", typeof(ItemHammer));
            api.RegisterItemClass("ItemWorkItem", typeof(ItemWorkItem));
            api.RegisterItemClass("ItemBow", typeof(ItemBow));
            api.RegisterItemClass("ItemArrow", typeof(ItemArrow));
            api.RegisterItemClass("ItemShears", typeof(ItemShears));
            api.RegisterItemClass("ItemCattailRoot", typeof(ItemCattailRoot));
            api.RegisterItemClass("ItemClay", typeof(ItemClay));
            api.RegisterItemClass("ItemLore", typeof(ItemRandomLore));
            api.RegisterItemClass("ItemFlint", typeof(ItemFlint));
            api.RegisterItemClass("ItemSpear", typeof(ItemSpear));
            api.RegisterItemClass("ItemAxe", typeof(ItemAxe));
            api.RegisterItemClass("ItemProspectingPick", typeof(ItemProspectingPick));
            api.RegisterItemClass("ItemStrawDummy", typeof(ItemStrawDummy));
            api.RegisterItemClass("ItemArmorStand", typeof(ItemArmorStand));
            
            api.RegisterItemClass("ItemCreature", typeof(ItemCreature));
            api.RegisterItemClass("ItemLootRandomizer", typeof(ItemLootRandomizer));
            api.RegisterItemClass("ItemScythe", typeof(ItemScythe));
            api.RegisterItemClass("ItemTemporalGear", typeof(ItemTemporalGear));
            api.RegisterItemClass("ItemHoneyComb", typeof(ItemHoneyComb));
            api.RegisterItemClass("ItemOpenedBeenade", typeof(ItemOpenedBeenade));
            api.RegisterItemClass("ItemClosedBeenade", typeof(ItemClosedBeenade));
            api.RegisterItemClass("ItemCandle", typeof(ItemCandle));
            api.RegisterItemClass("ItemWearable", typeof(ItemWearable));
            api.RegisterItemClass("ItemStackRandomizer", typeof(ItemStackRandomizer));
            api.RegisterItemClass("ItemChisel", typeof(ItemChisel));
            api.RegisterItemClass("ItemLiquidPortion", typeof(ItemLiquidPortion));

            api.RegisterItemClass("ItemKnife", typeof(ItemKnife));
            api.RegisterItemClass("ItemWoodenClub", typeof(ItemWoodenClub));
            api.RegisterItemClass("ItemSword", typeof(ItemSword));
            api.RegisterItemClass("ItemPoultice", typeof(ItemPoultice));
            api.RegisterItemClass("ItemRustyGear", typeof(ItemRustyGear));
            api.RegisterItemClass("ItemJournalEntry", typeof(ItemJournalEntry));
            api.RegisterItemClass("ItemCleaver", typeof(ItemCleaver));

            api.RegisterItemClass("ItemGem", typeof(ItemGem));
            api.RegisterItemClass("ItemPadlock", typeof(ItemPadlock));
            api.RegisterItemClass("ItemFirestarter", typeof(ItemFirestarter));
            api.RegisterItemClass("ItemIronBloom", typeof(ItemIronBloom));
            api.RegisterItemClass("ItemScrapWeaponKit", typeof(ItemScrapWeaponKit));
            api.RegisterItemClass("ItemCheese", typeof(ItemCheese));
            api.RegisterItemClass("ItemCoal", typeof(ItemCoal));
            api.RegisterItemClass("ItemRope", typeof(ItemRope));
            api.RegisterItemClass("ItemTreeSeed", typeof(ItemTreeSeed));
            api.RegisterItemClass("ItemDough", typeof(ItemDough));
        }



        private void RegisterDefaultEntities()
        {    
            api.RegisterEntity("EntityPlayerBot", typeof(EntityPlayerBot));
            api.RegisterEntity("EntityAnimalBot", typeof(EntityAnimalBot));
            api.RegisterEntity("EntityProjectile", typeof(EntityProjectile));
            api.RegisterEntity("EntityThrownStone", typeof(EntityThrownStone));
            api.RegisterEntity("EntityBeeMob", typeof(EntityBeeMob));
            api.RegisterEntity("EntityButterfly", typeof(EntityButterfly));
            api.RegisterEntity("EntityThrownBeenade", typeof(EntityThrownBeenade));
            api.RegisterEntity("EntityTrader", typeof(EntityTrader));
            api.RegisterEntity("EntityStrawDummy", typeof(EntityStrawDummy));
            api.RegisterEntity("EntityGlowingAgent", typeof(EntityGlowingAgent));
            api.RegisterEntity("EntityLocust", typeof(EntityLocust));
            api.RegisterEntity("EntityArmorStand", typeof(EntityArmorStand));
            api.RegisterEntity("EntityBell", typeof(EntityBell));
            api.RegisterEntity("EntityFish", typeof(EntityFish));
        }


        private void RegisterDefaultEntityBehaviors()
        {
            api.RegisterEntityBehaviorClass("temporalStabilityAffected", typeof(EntityBehaviorTemporalStabilityAffected));
            api.RegisterEntityBehaviorClass("milkable", typeof(EntityBehaviorMilkable));
            api.RegisterEntityBehaviorClass("bodytemperature", typeof(EntityBehaviorBodyTemperature));
            api.RegisterEntityBehaviorClass("extraskinnable", typeof(EntityBehaviorExtraSkinnable));
            api.RegisterEntityBehaviorClass("ropetieable", typeof(EntityBehaviorRopeTieable));
        }


    }

}
