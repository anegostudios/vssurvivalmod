using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

#nullable disable

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
        public float[] BlockLightLevels = new float[] { 0.016f, 0.146f, 0.247f, 0.33f, 0.401f, 0.463f, 0.519f, 0.569f, 0.615f, 0.656f, 0.695f, 0.73f, 0.762f, 0.792f, 0.82f, 0.845f, 0.868f, 0.89f, 0.91f, 0.927f, 0.944f, 0.958f, 0.972f, 0.983f, 0.993f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
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
                ResolvedStartStacks = Array.Empty<ItemStack>();
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
        ICoreServerAPI sapi;
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
            api.Assets.AddModOrigin(GlobalConstants.DefaultDomain, Path.Combine(GamePaths.AssetsPath, "survival"), api.Side == EnumAppSide.Client ? "textures" : null);
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

            if (api is ICoreServerAPI sapi)
            {
                // Set up day/night light levels
                sapi.WorldManager.SetBlockLightLevels(config.BlockLightLevels);
                sapi.WorldManager.SetSunLightLevels(config.SunLightLevels);
                sapi.WorldManager.SetSunBrightness(config.SunBrightness);
                sapi.Event.SaveGameLoaded += applySeasonConfig;
            }
            else
            {
                ((ICoreClientAPI)api).Network.GetChannel("survivalCoreConfig").SetMessageHandler<SurvivalConfig>(onConfigFromServer);
            }

            if (api.ModLoader.IsModSystemEnabled("Vintagestory.ServerMods.WorldEdit.WorldEdit"))
            {
                TreeToolRegisterUtil.Register(api.ModLoader.GetModSystem("Vintagestory.ServerMods.WorldEdit.WorldEdit"));
                ChiselToolRegisterUtil.Register(api.ModLoader.GetModSystem("Vintagestory.ServerMods.WorldEdit.WorldEdit"));
            }
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            metalsByCode = new Dictionary<string, MetalPropertyVariant>();
            var metalAssets = api.Assets.GetMany<MetalProperty>(api.Logger, "worldproperties/block/metal.json");
            foreach (var metals in metalAssets.Values)
            {
                for (int i = 0; i < metals.Variants.Length; i++)
                {
                    // Metals currently don't have a domain
                    var metal = metals.Variants[i];
                    metalsByCode[metal.Code.Path] = metal;
                }
            }

            if (api is ICoreClientAPI capi)
            {
                IAsset colorPresets = api.Assets.TryGet("config/colorpresets.json");
                capi.ColorPreset.Initialize(colorPresets);
            }
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            // loadConfig (which calls applyConfig) are called after the end of all mods' AssetsLoaded() stages. Cannot be called sooner (e.g. in this ModSystem's AssetsLoaded() method) because the globmodifiers.json patch will not have been applied yet, in that case.  This ModSystem loads before JsonPatchLoader.
            // Also it seems best to apply the toolDurability multiplier only after mods have done anything they need to to the tools
            if (api.Side == EnumAppSide.Server)
            {
                loadConfig();
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            api.RegisterEntityRendererClass("EchoChamber", typeof(EchoChamberRenderer));

            api.Event.LevelFinalize += () =>
            {
                api.World.Calendar.OnGetSolarSphericalCoords = GetSolarSphericalCoords;
                api.World.Calendar.OnGetHemisphere = GetHemisphere;
                applySeasonConfig();
            };

            api.Event.ReloadShader += LoadShader;
            LoadShader();
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Event.PlayerCreate += Event_PlayerCreate;
            api.Event.PlayerNowPlaying += Event_PlayerPlaying;
            api.Event.PlayerJoin += Event_PlayerJoin;

            api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => {
                config.ResolveStartItems(api.World);
                api.World.Calendar.OnGetSolarSphericalCoords = GetSolarSphericalCoords;
                api.World.Calendar.OnGetHemisphere = GetHemisphere;
            });

            AiTaskRegistry.Register<AiTaskBellAlarm>("bellalarm");
            AiTaskRegistry.Register<AiTaskThrowAtEntity>("throwatentity");
            AiTaskRegistry.Register<AiTaskStayInRange>("stayinrange");
            AiTaskRegistry.Register<AiTaskTurretMode>("turretmode");
            AiTaskRegistry.Register<AiTaskFollowLeadHolder>("followleadholder");
            AiTaskRegistry.Register<AiTaskFollowLeadHolderR>("followleadholder-r");

            EntityBehaviorPassivePhysicsMultiBox.InitServer(api);   // Needed to guarantee registration to the OnPhysicsThreadStart event before that event is fired, even if no entities with this behavior (i.e. boats) are yet loaded in the early game - it might be hours before we see one of these entities
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


        public float EarthAxialTilt = 23.44f * GameMath.DEG2RAD;

        // This method was contributed by Eliam  (Avdudia#0696) on Discord <3
        public SolarSphericalCoords GetSolarSphericalCoords(double posX, double posZ, float yearRel, float dayRel)
        {
            // Tyron: For your understanding, this would be the simple most spherical coord calculator - this is how the sun rises and sets if you were standing at the equator and without earth axial tilt
            // return new (GameMath.TWOPI * GameMath.Mod(api.World.Calendar.HourOfDay / api.World.Calendar.HoursPerDay, 1f), 0);

            double latitude = api.World.Calendar.OnGetLatitude(posZ) * Math.PI / 2.0;

            float hourAngle = GameMath.TWOPI * (dayRel - 0.5f);

            // The Sun's declination at any given moment
            // The number 10 is the approximate number of days after the December solstice to January 1
            double declination = -EarthAxialTilt * Math.Cos(GameMath.TWOPI * (yearRel + 10 / 365f));

            double sinLatitude = Math.Sin(latitude);    // For performance, we calculate and store some of the values used multiple times in the formulae
            double cosLatitude = Math.Cos(latitude);
            double sinDeclination = Math.Sin(declination);
            double cosZenithAngle = GameMath.Clamp(sinLatitude * sinDeclination + cosLatitude * GameMath.Cos(declination) * GameMath.Cos(hourAngle), -1.0, 1.0);
            double sinZenithAngle = Math.Sqrt(1.0 - cosZenithAngle * cosZenithAngle);    // This is a fast formula to obtain sin from cos; in theory sign would need adjusting outside the range 0-pi, but Math.Acos(cosZenithAngle) always returns a value in the range 0-pi

            // Added 1.e-10 to prevent division by 0
            double b = ((sinLatitude * cosZenithAngle) - sinDeclination) / (cosLatitude * sinZenithAngle + 0.0000001f);
            float azimuthAngle = (float)Math.Acos(GameMath.Clamp(b, -1.0, 1.0));

            // The sign function gives the correct azimuth angle sign depending on the hour without using IF statement (branchless)     radfast note: ahem, look at the source code for Math.Sign()
            azimuthAngle = (GameMath.PI + Math.Sign(hourAngle) * azimuthAngle) % GameMath.TWOPI;

            return new SolarSphericalCoords(GameMath.TWOPI - (float)Math.Acos(cosZenithAngle), GameMath.TWOPI - azimuthAngle);
        }


        HashSet<string> createdPlayers = new HashSet<string>();
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
                api.World.Logger.Error("Failed loading survivalconfig.json. Will initialize new one");
                api.World.Logger.Error(e);
                config = new SurvivalConfig();
            }

            applyConfig();

            // [1.17.0-pre.4] currently called on server-side only, should it be on both sides?
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
            if (api.World.Calendar == null) return; // Server side this gets called too early during AssetFinalize (it gets called again later)
            ITreeAttribute worldConfig = api.World.Config;
            string seasons = worldConfig.GetString("seasons");
            if (seasons == "spring")
            {
                api.World.Calendar.SetSeasonOverride(0.33f);
            }
            if (seasons == "summer")
            {
                api.World.Calendar.SetSeasonOverride(0.6f);
            }
            if (seasons == "fall")
            {
                api.World.Calendar.SetSeasonOverride(0.77f);
            }
            if (seasons == "winter")
            {
                api.World.Calendar.SetSeasonOverride(0.05f);
            }
        }

        private void RegisterDefaultBlocks()
        {
            api.RegisterBlockClass("BlockDoor", typeof(BlockDoor));
            api.RegisterBlockClass("BlockTrapdoor", typeof(BlockTrapdoor));

            api.RegisterBlockClass("BlockFirepit", typeof(BlockFirepit));
            api.RegisterBlockClass("BlockCharcoalPit", typeof(BlockCharcoalPit));
            api.RegisterBlockClass("BlockTorch", typeof(BlockTorch));
            api.RegisterBlockClass("BlockStairs", typeof(BlockStairs));
            api.RegisterBlockClass("BlockFence", typeof(BlockFence));
            api.RegisterBlockClass("BlockWattleFence", typeof(BlockWattleFence));
            api.RegisterBlockClass("BlockDaubWattle", typeof(BlockDaubWattle));
            api.RegisterBlockClass("BlockFenceStackAware", typeof(BlockFenceStackAware));
            api.RegisterBlockClass("BlockFenceGate", typeof(BlockFenceGate));
            api.RegisterBlockClass("BlockFenceGateRoughHewn", typeof(BlockFenceGateRoughHewn));
            api.RegisterBlockClass("BlockLayered", typeof(BlockLayered));
            api.RegisterBlockClass("BlockVines", typeof(BlockVines));
            api.RegisterBlockClass("BlockPlant", typeof(BlockPlant));
            api.RegisterBlockClass("BlockTallGrass", typeof(BlockTallGrass));
            api.RegisterBlockClass("BlockRails", typeof(BlockRails));
            api.RegisterBlockClass("BlockCactus", typeof(BlockCactus));
            api.RegisterBlockClass("BlockSlab", typeof(BlockSlab));
            api.RegisterBlockClass("BlockPlantContainer", typeof(BlockPlantContainer));
            api.RegisterBlockClass("BlockSapling", typeof(BlockSapling));
            api.RegisterBlockClass("BlockSign", typeof(BlockSign));
            api.RegisterBlockClass("BlockSimpleCoating", typeof(BlockSimpleCoating));
            api.RegisterBlockClass("BlockFullCoating", typeof(BlockFullCoating));
            api.RegisterBlockClass("BlockBed", typeof(BlockBed));
            api.RegisterBlockClass("BlockBerryBush", typeof(BlockBerryBush));
            api.RegisterBlockClass("BlockWaterLily", typeof(BlockWaterLily));
            api.RegisterBlockClass("BlockWaterLilyGiant", typeof(BlockWaterLilyGiant));
            api.RegisterBlockClass("BlockLooseStones", typeof(BlockLooseStones));
            api.RegisterBlockClass("BlockIngotPile", typeof(BlockIngotPile));
            api.RegisterBlockClass("BlockPeatPile", typeof(BlockPeatPile));

            api.RegisterBlockClass("BlockLightningRod", typeof(BlockLightningRod));

            api.RegisterBlockClass("BlockBucket", typeof(BlockBucket));
            api.RegisterBlockClass("BlockCrop", typeof(BlockCrop));
            api.RegisterBlockClass("BlockFruiting", typeof(BlockFruiting));
            api.RegisterBlockClass("BlockWaterPlant", typeof(BlockWaterPlant));
            api.RegisterBlockClass("BlockSeaweed", typeof(BlockSeaweed));
            api.RegisterBlockClass("BlockCrowfoot", typeof(BlockCrowfoot));
            api.RegisterBlockClass("BlockCoral", typeof(BlockCoral));
            api.RegisterBlockClass("BlockDevastationGrowth", typeof(BlockDevastationGrowth));
            api.RegisterBlockClass("BlockJonasLensTower", typeof(BlockJonasLensTower));

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
            api.RegisterBlockClass("BlockBituCoal", typeof(BlockBituCoal));
            api.RegisterBlockClass("BlockLava", typeof(BlockLava));
            api.RegisterBlockClass("BlockMushroom", typeof(BlockMushroom));
            api.RegisterBlockClass("BlockSoil", typeof(BlockSoil));
            api.RegisterBlockClass("BlockForestFloor", typeof(BlockForestFloor));
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
            api.RegisterBlockClass("BlockRainAmbient", typeof(BlockRainAmbient));
            api.RegisterBlockClass("BlockPlaceOnDrop", typeof(BlockPlaceOnDrop));
            api.RegisterBlockClass("BlockLooseGears", typeof(BlockLooseGears));
            api.RegisterBlockClass("BlockSpawner", typeof(BlockSpawner));
            api.RegisterBlockClass("BlockMeal", typeof(BlockMeal));
            api.RegisterBlockClass("BlockLiquidContainerTopOpened", typeof(BlockLiquidContainerTopOpened));
            api.RegisterBlockClass("BlockWateringCan", typeof(BlockWateringCan));
            api.RegisterBlockClass("BlockTrough", typeof(BlockTrough));
            api.RegisterBlockClass("BlockTroughDoubleBlock", typeof(BlockTroughDoubleBlock));
            api.RegisterBlockClass("BlockLeaves", typeof(BlockLeaves));
            api.RegisterBlockClass("BlockLeavesNarrow", typeof(BlockLeavesNarrow));
            api.RegisterBlockClass("BlockBough", typeof(BlockBough));
            api.RegisterBlockClass("BlockFarmland", typeof(BlockFarmland));
            api.RegisterBlockClass("BlockSticksLayer", typeof(BlockSticksLayer));
            api.RegisterBlockClass("BlockVariheight", typeof(BlockVariheight));

            api.RegisterBlockClass("BlockAxle", typeof(BlockAxle));
            api.RegisterBlockClass("BlockAngledGears", typeof(BlockAngledGears));
            api.RegisterBlockClass("BlockWindmillRotor", typeof(BlockWindmillRotor));
            api.RegisterBlockClass("BlockToggle", typeof(BlockToggle));
            api.RegisterBlockClass("BlockPulverizer", typeof(BlockPulverizer));

            api.RegisterBlockClass("BlockSoilDeposit", typeof(BlockSoilDeposit));
            api.RegisterBlockClass("BlockMetalPartPile", typeof(BlockMetalPartPile));

            api.RegisterBlockClass("BlockStaticTranslocator", typeof(BlockStaticTranslocator));
            api.RegisterBlockClass("BlockTobiasTeleporter", typeof(BlockTobiasTeleporter));

            api.RegisterBlockClass("BlockCrystal", typeof(BlockCrystal));

            api.RegisterBlockClass("BlockWaterfall", typeof(BlockWaterfall));
            api.RegisterBlockClass("BlockLupine", typeof(BlockLupine));

            api.RegisterBlockClass("BlockResonator", typeof(BlockResonator));
            api.RegisterBlockClass("BlockMeteorite", typeof(BlockMeteorite));

            api.RegisterBlockClass("BlockChandelier", typeof(BlockChandelier));

            api.RegisterBlockClass("BlockRequireSolidGround", typeof(BlockRequireSolidGround));
            api.RegisterBlockClass("BlockLocustNest", typeof(BlockLocustNest));
            api.RegisterBlockClass("BlockDamageOnTouch", typeof(BlockDamageOnTouch));
            api.RegisterBlockClass("BlockPlantDamageOnTouch", typeof(BlockPlantDamageOnTouch));
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
            api.RegisterBlockClass("BlockCandle", typeof(BlockCandle));
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
            api.RegisterBlockClass("BlockBeeHiveKilnDoor", typeof(BlockBeeHiveKilnDoor));
            api.RegisterBlockClass("BlockStoneCoffinLid", typeof(BlockStoneCoffinLid));

            api.RegisterBlockClass("BlockGroundStorage", typeof(BlockGroundStorage));
            api.RegisterBlockClass("BlockPitkiln", typeof(BlockPitkiln));
            api.RegisterBlockClass("BlockPie", typeof(BlockPie));
            api.RegisterBlockClass("BlockHangingLichen", typeof(BlockHangingLichen));
            api.RegisterBlockClass("BlockDeadCrop", typeof(BlockDeadCrop));
            api.RegisterBlockClass("BlockFruitPress", typeof(BlockFruitPress));
            api.RegisterBlockClass("BlockFruitPressTop", typeof(BlockFruitPressTop));
            api.RegisterBlockClass("BlockBoiler", typeof(BlockBoiler));
            api.RegisterBlockClass("BlockCondenser", typeof(BlockCondenser));
            api.RegisterBlockClass("BlockCrate", typeof(BlockCrate));
            api.RegisterBlockClass("BlockDynamicTreeFoliage", typeof(BlockFruitTreeFoliage));
            api.RegisterBlockClass("BlockDynamicTreeBranch", typeof(BlockFruitTreeBranch));
            api.RegisterBlockClass("BlockGenericTypedContainerTrunk", typeof(BlockGenericTypedContainerTrunk));

            api.RegisterBlockClass("BlockClutter", typeof(BlockClutter));
            api.RegisterBlockClass("BlockFigurehead", typeof(BlockFigurehead));
            api.RegisterBlockClass("BlockShapeMaterialFromAttributes", typeof(BlockShapeMaterialFromAttributes));
            api.RegisterBlockClass("BlockMaterialFromAttributes", typeof(BlockMaterialFromAttributes));


            api.RegisterBlockClass("ToggleCollisionBox", typeof(BlockToggleCollisionBox));

            api.RegisterBlockClass("BlockClutterBookshelf", typeof(BlockClutterBookshelf));

            api.RegisterBlockClass("BlockBookshelf", typeof(BlockBookshelf));
            api.RegisterBlockClass("BlockClutterBookshelfWithLore", typeof(BlockClutterBookshelfWithLore));
            api.RegisterBlockClass("BlockLooseRock", typeof(BlockLooseRock));
            api.RegisterBlockClass("BlockTermiteMound", typeof(BlockTermiteMound));
            api.RegisterBlockClass("BlockRiftWard", typeof(BlockRiftWard));

            api.RegisterBlockClass("BlockSmoothTextureTransition", typeof(BlockSmoothTextureTransition));
            api.RegisterBlockClass("BlockSupportBeam", typeof(BlockSupportBeam));
            api.RegisterBlockClass("BlockOmokTable", typeof(BlockOmokTable));

            api.RegisterBlockClass("BlockGeneric", typeof(BlockGeneric));

            api.RegisterBlockClass("BlockCommand", typeof(BlockCommand));
            api.RegisterBlockClass("BlockTicker", typeof(BlockTicker));
            api.RegisterBlockClass("BlockWorldgenHook", typeof(BlockWorldgenHook));

            api.RegisterBlockClass("BlockBaseReturnTeleporter", typeof(BlockBaseReturnTeleporter));
            api.RegisterBlockClass("BlockCorpseReturnTeleporter", typeof(BlockCorpseReturnTeleporter));

            api.RegisterBlockClass("BlockRandomizer", typeof(BlockRandomizer));
            api.RegisterBlockClass("BlockScrollRack", typeof(BlockScrollRack));
            api.RegisterBlockClass("BlockBasketTrap", typeof(BlockAnimalTrap));
            api.RegisterBlockClass("BlockAntlerMount", typeof(BlockAntlerMount));

            api.RegisterBlockClass("BlockRockTyped", typeof(BlockRockTyped));

            api.RegisterBlockClass("BlockGasifier", typeof(BlockGasifier));
            api.RegisterBlockClass("BlockSlantedRoofingHalf", typeof(BlockSlantedRoofingHalf));
            api.RegisterBlockClass("BlockTileConnector", typeof(BlockTileConnector));
            api.RegisterBlockClass("BlockMetaRemainSelectable", typeof(BlockMetaRemainSelectable));
            api.RegisterBlockClass("BlockCropProp", typeof(BlockCropProp));
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
            api.RegisterBlockBehaviorClass("Lockable", typeof(BlockBehaviorLockable));
            api.RegisterBlockBehaviorClass("DropNotSnowCovered", typeof(BlockBehaviorDropNotSnowCovered));
            api.RegisterBlockBehaviorClass("CanAttach", typeof(BlockBehaviorCanAttach));
            api.RegisterBlockBehaviorClass("MilkingContainer", typeof(BlockBehaviorMilkingContainer));
            api.RegisterBlockBehaviorClass("HeatSource", typeof(BlockBehaviorHeatSource));
            api.RegisterBlockBehaviorClass("BreakSnowFirst", typeof(BlockBehaviorBreakSnowFirst));
            api.RegisterBlockBehaviorClass("RopeTieable", typeof(BlockBehaviorRopeTieable));
            api.RegisterBlockBehaviorClass("MyceliumHost", typeof(BlockBehaviorMyceliumHost));
            api.RegisterBlockBehaviorClass("WrenchOrientable", typeof(BlockBehaviorWrenchOrientable));
            api.RegisterBlockBehaviorClass("ElevatorControl", typeof(BlockBehaviorElevatorControl));
            api.RegisterBlockBehaviorClass("RainDrip", typeof(BlockBehaviorRainDrip));
            api.RegisterBlockBehaviorClass("Steaming", typeof(BlockBehaviorSteaming));
            api.RegisterBlockBehaviorClass("BlockEntityInteract", typeof(BlockBehaviorBlockEntityInteract));
            api.RegisterBlockBehaviorClass("Door", typeof(BlockBehaviorDoor));
            api.RegisterBlockBehaviorClass("TrapDoor", typeof(BlockBehaviorTrapDoor));
            api.RegisterBlockBehaviorClass("Reparable", typeof(BlockBehaviorReparable));

            api.RegisterBlockBehaviorClass("JonasBoilerDoor", typeof(BlockBehaviorJonasBoilerDoor));
            api.RegisterBlockBehaviorClass("JonasHydraulicPump", typeof(BlockBehaviorJonasHydraulicPump));
            api.RegisterBlockBehaviorClass("JonasGasifier", typeof(BlockBehaviorJonasGasifier));
            api.RegisterBlockBehaviorClass("UnstableRock", typeof(BlockBehaviorUnstableRock));
            api.RegisterBlockBehaviorClass("CreatureContainer", typeof(BlockBehaviorCreatureContainer));
            api.RegisterBlockBehaviorClass("Chimney", typeof(BlockBehaviorChimney));

            api.RegisterBlockBehaviorClass("GiveItemPerPlayer", typeof(BlockBehaviorGiveItemPerPlayer));
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

            api.RegisterBlockEntityBehaviorClass("AttractsLightning", typeof(BEBehaviorAttractsLightning));
            api.RegisterBlockEntityBehaviorClass("Burning", typeof(BEBehaviorBurning));
            api.RegisterBlockEntityBehaviorClass("FirepitAmbient", typeof(BEBehaviorFirepitAmbient));
            api.RegisterBlockEntityBehaviorClass("Fruiting", typeof(BEBehaviorFruiting));
            api.RegisterBlockEntityBehaviorClass("SupportBeam", typeof(BEBehaviorSupportBeam));
            api.RegisterBlockEntityBehaviorClass("Door", typeof(BEBehaviorDoor));
            api.RegisterBlockEntityBehaviorClass("DoorBarLock", typeof(BEBehaviorDoorBarLock));
            api.RegisterBlockEntityBehaviorClass("TrapDoor", typeof(BEBehaviorTrapDoor));
            api.RegisterBlockEntityBehaviorClass("ShapeFromAttributes", typeof(BEBehaviorShapeFromAttributes));
            api.RegisterBlockEntityBehaviorClass("ClutterBookshelf", typeof(BEBehaviorClutterBookshelf));
            api.RegisterBlockEntityBehaviorClass("MicroblockSnowCover", typeof(BEBehaviorMicroblockSnowCover));
            api.RegisterBlockEntityBehaviorClass("ControlPointAnimatable", typeof(BEBehaviorControlPointAnimatable));

            api.RegisterBlockEntityBehaviorClass("JonasBoilerDoor", typeof(BEBehaviorJonasBoilerDoor));
            api.RegisterBlockEntityBehaviorClass("JonasHydraulicPump", typeof(BEBehaviorJonasHydraulicPump));
            api.RegisterBlockEntityBehaviorClass("JonasGasifier", typeof(BEBehaviorJonasGasifier));
            api.RegisterBlockEntityBehaviorClass("ControlPointLampNode", typeof(BEBehaviorControlPointLampNode));
            api.RegisterBlockEntityBehaviorClass("ElevatorControl", typeof(BEBehaviorElevatorControl));
            api.RegisterBlockEntityBehaviorClass("ToggleCollisionBox", typeof(BEBehaviorToggleCollisionBox));


            api.RegisterBlockEntityBehaviorClass("ClutterBookshelfWithLore", typeof(BEBehaviorClutterBookshelfWithLore));
            api.RegisterBlockEntityBehaviorClass("RockRubbleFromAttributes", typeof(BEBehaviorRockRubbleFromAttributes));
            api.RegisterBlockEntityBehaviorClass("TemperatureSensitive", typeof(BEBehaviorTemperatureSensitive));
            api.RegisterBlockEntityBehaviorClass("CropProp", typeof(BEBehaviorCropProp));

            api.RegisterBlockEntityBehaviorClass("GiveItemPerPlayer", typeof(BEBehaviorGiveItemPerPlayer));
            api.RegisterBlockEntityBehaviorClass("MaterialFromAttributes", typeof(BEBehaviorMaterialFromAttributes));
            api.RegisterBlockEntityBehaviorClass("ShapeMaterialFromAttributes", typeof(BEBehaviorShapeMaterialFromAttributes));
        }

        private void RegisterDefaultCollectibleBehaviors()
        {
            api.RegisterCollectibleBehaviorClass("GroundStorable", typeof(CollectibleBehaviorGroundStorable));
            api.RegisterCollectibleBehaviorClass("ArtPigment", typeof(CollectibleBehaviorArtPigment));
            api.RegisterCollectibleBehaviorClass("BoatableGenericTypedContainer", typeof(CollectibleBehaviorBoatableGenericTypedContainer));
            api.RegisterCollectibleBehaviorClass("BoatableCrate", typeof(CollectibleBehaviorBoatableCrate));
            api.RegisterCollectibleBehaviorClass("EntityDeconstructTool", typeof(EntityDeconstructTool));
            api.RegisterCollectibleBehaviorClass("HealingItem", typeof(BehaviorHealingItem));
            api.RegisterCollectibleBehaviorClass("Squeezable", typeof(CollectibleBehaviorSqueezable));
        }


        private void RegisterDefaultBlockEntities()
        {
            api.RegisterBlockEntityClass("Sapling", typeof(BlockEntitySapling));
            api.RegisterBlockEntityClass("GenericContainer", typeof(BlockEntityGenericContainer));
            api.RegisterBlockEntityClass("GenericTypedContainer", typeof(BlockEntityGenericTypedContainer));
            api.RegisterBlockEntityClass("Sign", typeof(BlockEntitySign));

            api.RegisterBlockEntityClass("LightningRod", typeof(BlockEntityLightningRod));

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
            api.RegisterBlockEntityClass("TobiasTeleporter", typeof(BlockEntityTobiasTeleporter));

            api.RegisterBlockEntityClass("Resonator", typeof(BlockEntityResonator));

            api.RegisterBlockEntityClass("LocustNest", typeof(BlockEntityLocustNest));
            api.RegisterBlockEntityClass("LabeledChest", typeof(BlockEntityLabeledChest));
            api.RegisterBlockEntityClass("Barrel", typeof(BlockEntityBarrel));
			api.RegisterBlockEntityClass("ItemFlow", typeof(BlockEntityItemFlow));
            api.RegisterBlockEntityClass("Crock", typeof(BlockEntityCrock));
            api.RegisterBlockEntityClass("Shelf", typeof(BlockEntityShelf));
            api.RegisterBlockEntityClass("SignPost", typeof(BlockEntitySignPost));
            api.RegisterBlockEntityClass("Torch", typeof(BlockEntityTorch));
            api.RegisterBlockEntityClass("Mycelium", typeof(BlockEntityMycelium));

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
            api.RegisterBlockEntityClass("BeeHiveKiln", typeof(BlockEntityBeeHiveKiln));

            api.RegisterBlockEntityClass("GroundStorage", typeof(BlockEntityGroundStorage));
            api.RegisterBlockEntityClass("PitKiln", typeof(BlockEntityPitKiln));
            api.RegisterBlockEntityClass("Pie", typeof(BlockEntityPie));
            api.RegisterBlockEntityClass("DeadCrop", typeof(BlockEntityDeadCrop));
            api.RegisterBlockEntityClass("FruitPress", typeof(BlockEntityFruitPress));

            api.RegisterBlockEntityClass("Boiler", typeof(BlockEntityBoiler));
            api.RegisterBlockEntityClass("Condenser", typeof(BlockEntityCondenser));
            api.RegisterBlockEntityClass("Crate", typeof(BlockEntityCrate));

            api.RegisterBlockEntityClass("DynamicTreeBranch", typeof(BlockEntityFruitTreeBranch));
            api.RegisterBlockEntityClass("DynamicTreeFoliage", typeof(BlockEntityFruitTreeFoliage));

            api.RegisterBlockEntityClass("TorchHolder", typeof(BlockEntityTorchHolder));

            api.RegisterBlockEntityClass("Bookshelf", typeof(BlockEntityBookshelf));
            api.RegisterBlockEntityClass("RiftWard", typeof(BlockEntityRiftWard));

            api.RegisterBlockEntityClass("OmokTable", typeof(BlockEntityOmokTable));

            api.RegisterBlockEntityClass("GuiConfigurableCommands", typeof(BlockEntityGuiConfigurableCommands));
            api.RegisterBlockEntityClass("Commands", typeof(BlockEntityCommands));
            api.RegisterBlockEntityClass("Conditional", typeof(BlockEntityConditional));
            api.RegisterBlockEntityClass("Ticker", typeof(BlockEntityTicker));
            api.RegisterBlockEntityClass("WorldgenHook", typeof(BlockEntityWorldgenHook));
            api.RegisterBlockEntityClass("MusicTrigger", typeof(BlockEntityMusicTrigger));

            api.RegisterBlockEntityClass("CorpseReturnTeleporter", typeof(BlockEntityCorpseReturnTeleporter));
            api.RegisterBlockEntityClass("BaseReturnTeleporter", typeof(BlockEntityBaseReturnTeleporter));

            api.RegisterBlockEntityClass("BlockRandomizer", typeof(BlockEntityBlockRandomizer));

            api.RegisterBlockEntityClass("ScrollRack", typeof(BlockEntityScrollRack));
            api.RegisterBlockEntityClass("AntlerMount", typeof(BlockEntityAntlerMount));

            api.RegisterBlockEntityClass("BasketTrap", typeof(BlockEntityAnimalTrap));
            api.RegisterBlockEntityClass("AnimalBasket", typeof(BlockEntityAnimalBasket));
            api.RegisterBlockEntityClass("TileConnector", typeof(BETileConnector));
            api.RegisterBlockEntityClass("JonasLensTower", typeof(BEJonasLensTower));
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
            api.RegisterItemClass("ItemWrench", typeof(ItemWrench));
            api.RegisterItemClass("ItemWorkItem", typeof(ItemWorkItem));
            api.RegisterItemClass("ItemBow", typeof(ItemBow));
            api.RegisterItemClass("ItemArrow", typeof(ItemArrow));
            api.RegisterItemClass("ItemShears", typeof(ItemShears));
            api.RegisterItemClass("ItemCattailRoot", typeof(ItemCattailRoot));
            api.RegisterItemClass("ItemClay", typeof(ItemClay));
            api.RegisterItemClass("ItemRandomLore", typeof(ItemRandomLore));
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
            api.RegisterItemClass("ItemSnowball", typeof(ItemSnowball));
            api.RegisterItemClass("ItemCandle", typeof(ItemCandle));
            api.RegisterItemClass("ItemWearable", typeof(ItemWearable));
            api.RegisterItemClass("ItemWearableAttachment", typeof(ItemWearableAttachment));
            api.RegisterItemClass("ItemStackRandomizer", typeof(ItemStackRandomizer));
            api.RegisterItemClass("ItemChisel", typeof(ItemChisel));
            api.RegisterItemClass("ItemLiquidPortion", typeof(ItemLiquidPortion));

            api.RegisterItemClass("ItemKnife", typeof(ItemKnife));
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
            api.RegisterItemClass("ItemMeasuringRope", typeof(ItemMeasuringRope));
            api.RegisterItemClass("ItemTreeSeed", typeof(ItemTreeSeed));
            api.RegisterItemClass("ItemDough", typeof(ItemDough));
            api.RegisterItemClass("ItemSling", typeof(ItemSling));
            api.RegisterItemClass("ItemShield", typeof(ItemShield));

            api.RegisterItemClass("ItemPressedMash", typeof(ItemPressedMash));
            api.RegisterItemClass("ItemCreatureInventory", typeof(ItemCreatureInventory));

            api.RegisterItemClass("ItemLocatorMap", typeof(ItemLocatorMap));
            api.RegisterItemClass("ItemBook", typeof(ItemBook));
            api.RegisterItemClass("ItemRollable", typeof(ItemRollable));
            api.RegisterItemClass("ItemDeadButterfly", typeof(ItemDeadButterfly));
            api.RegisterItemClass("ItemBugnet", typeof(ItemBugnet));
            api.RegisterItemClass("ItemGlider", typeof(ItemGlider));
            api.RegisterItemClass("ItemOar", typeof(ItemOar));
            api.RegisterItemClass("ItemTextureFlipper", typeof(ItemTextureFlipper));

            api.RegisterItemClass("ItemNightvisiondevice", typeof(ItemNightvisiondevice));
            api.RegisterItemClass("ItemMechHelper", typeof(ItemMechHelper));
            api.RegisterItemClass("ItemNpcGuideStick", typeof(ItemNpcGuideStick));
            api.RegisterItemClass("ItemTongs", typeof(ItemTongs));
            api.RegisterItemClass("ItemFlute", typeof(ItemFlute));
            api.RegisterItemClass("ItemMedallion", typeof(ItemMedallion));

            api.RegisterItemClass("ItemSkillTimeswitch", typeof(ItemSkillTimeswitch));
            api.RegisterItemClass("ItemAnchor", typeof(ItemAnchor));
            api.RegisterItemClass("ItemEgg", typeof(ItemEgg));
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
            api.RegisterEntity("EntityThrownSnowball", typeof(EntityThrownSnowball));
            api.RegisterEntity("EntityTrader", typeof(EntityTrader));
            api.RegisterEntity("EntityDressedHumanoid", typeof(EntityDressedHumanoid));
            api.RegisterEntity("EntityVillager", typeof(EntityVillager));

            api.RegisterEntity("EntityStrawDummy", typeof(EntityStrawDummy));
            api.RegisterEntity("EntityGlowingAgent", typeof(EntityGlowingAgent));
            api.RegisterEntity("EntityLocust", typeof(EntityLocust));
            api.RegisterEntity("EntityArmorStand", typeof(EntityArmorStand));
            api.RegisterEntity("EntityBell", typeof(EntityBell));
            api.RegisterEntity("EntityFish", typeof(EntityFish));
            api.RegisterEntity("EntityDrifter", typeof(EntityDrifter));
            api.RegisterEntity("EntityEidolon", typeof(EntityEidolon));
            api.RegisterEntity("EntityEchoChamber", typeof(EntityEchoChamber));
            api.RegisterEntity("EntityMechHelper", typeof(EntityMechHelper));
            api.RegisterEntity("EntityLibraryResonator", typeof(EntityLibraryResonator));
            api.RegisterEntity("EntityShiver", typeof(EntityShiver));
            api.RegisterEntity("EntityErel", typeof(EntityErel));
        }


        private void RegisterDefaultEntityBehaviors()
        {
            api.RegisterEntityBehaviorClass("temporalStabilityAffected", typeof(EntityBehaviorTemporalStabilityAffected));
            api.RegisterEntityBehaviorClass("milkable", typeof(EntityBehaviorMilkable));
            api.RegisterEntityBehaviorClass("pettable", typeof(EntityBehaviorPettable));
            api.RegisterEntityBehaviorClass("bodytemperature", typeof(EntityBehaviorBodyTemperature));
            api.RegisterEntityBehaviorClass("extraskinnable", typeof(EntityBehaviorExtraSkinnable));
            api.RegisterEntityBehaviorClass("ropetieable", typeof(EntityBehaviorRopeTieable));
            api.RegisterEntityBehaviorClass("commandable", typeof(EntityBehaviorCommandable));
            api.RegisterEntityBehaviorClass("conversable", typeof(EntityBehaviorConversable));
            api.RegisterEntityBehaviorClass("boss", typeof(EntityBehaviorBoss));
            api.RegisterEntityBehaviorClass("bossErel", typeof(EntityBehaviorErelBoss));
            api.RegisterEntityBehaviorClass("antlergrowth", typeof(EntityBehaviorAntlerGrowth));
            api.RegisterEntityBehaviorClass("idleanimations", typeof(EntityBehaviorIdleAnimations));
            api.RegisterEntityBehaviorClass("rideable", typeof(EntityBehaviorRideable));
            api.RegisterEntityBehaviorClass("attachable", typeof(EntityBehaviorAttachable));
            api.RegisterEntityBehaviorClass("rideableaccessories", typeof(EntityBehaviorRideableAccessories));

            api.RegisterEntityBehaviorClass("seraphinventory", typeof(EntityBehaviorSeraphInventory));
            api.RegisterEntityBehaviorClass("armorstandinventory", typeof(EntityBehaviorArmorStandInventory));
            api.RegisterEntityBehaviorClass("mortallywoundable", typeof(EntityBehaviorMortallyWoundable));
            api.RegisterEntityBehaviorClass("playerrevivable", typeof(EntityBehaviorPlayerRevivable));
            api.RegisterEntityBehaviorClass("selectionboxes", typeof(EntityBehaviorSelectionBoxes));
            api.RegisterEntityBehaviorClass("hidewatersurface", typeof(EntityBehaviorHideWaterSurface));
            api.RegisterEntityBehaviorClass("creaturecarrier", typeof(EntityBehaviorCreatureCarrier));
            api.RegisterEntityBehaviorClass("seatable", typeof(EntityBehaviorSeatable));

            api.RegisterEntityBehaviorClass("passivephysicsmultibox", typeof(EntityBehaviorPassivePhysicsMultiBox));
            api.RegisterEntityBehaviorClass("activitydriven", typeof(EntityBehaviorActivityDriven));
            api.RegisterEntityBehaviorClass("villagerinventory", typeof(EntityBehaviorVillagerInv));
            api.RegisterEntityBehaviorClass("ownable", typeof(EntityBehaviorOwnable));

            api.RegisterEntityBehaviorClass("ripharvestable", typeof(EntityBehaviorRipHarvestable));
            api.RegisterEntityBehaviorClass("writingsurface", typeof(EntityBehaviorWritingSurface));
        }
    }
}
