using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class SurvivalConfig
    {
        public AssetLocation CurrencyItemIcon = new AssetLocation("gear-rusty");
        public JsonItemStack[] StartStacks = new JsonItemStack[] {
            new JsonItemStack() { Type = EnumItemClass.Item, Code = new AssetLocation("bread-spelt"), StackSize = 8 },
            new JsonItemStack() { Type = EnumItemClass.Block, Code = new AssetLocation("torch-up"), StackSize = 1 }
        };

        public float[] SunLightLevels = new float[] { 0.015f, 0.176f, 0.206f, 0.236f, 0.266f, 0.296f, 0.326f, 0.356f, 0.386f, 0.416f, 0.446f, 0.476f, 0.506f, 0.536f, 0.566f, 0.596f, 0.626f, 0.656f, 0.686f, 0.716f, 0.746f, 0.776f, 0.806f, 0.836f, 0.866f, 0.896f, 0.926f, 0.956f, 0.986f, 1f, 1f, 1f};

        //public float[] BlockLightLevels = new float[] { 0.0175f, 0.149f, 0.184f, 0.219f, 0.254f, 0.289f, 0.324f, 0.359f, 0.394f, 0.429f, 0.464f, 0.499f, 0.534f, 0.569f, 0.604f, 0.639f, 0.674f, 0.709f, 0.744f, 0.779f, 0.814f, 0.849f, 0.884f, 0.919f, 0.954f, 0.989f, 1f, 1f, 1f, 1f, 1f, 1f };

        public float[] BlockLightLevels = new float[] { 0.0175f, 0.06f, 0.12f, 0.18f, 0.254f, 0.289f, 0.324f, 0.359f, 0.394f, 0.429f, 0.464f, 0.499f, 0.534f, 0.569f, 0.604f, 0.639f, 0.674f, 0.709f, 0.744f, 0.779f, 0.814f, 0.849f, 0.884f, 0.919f, 0.954f, 0.989f, 1f, 1f, 1f, 1f, 1f, 1f };

        public float PerishSpeedModifier = 1f;
        public float CreatureDamageModifier = 1;
        public float ToolDurabilityModifier = 1;
        public float ToolMiningSpeedModifier = 1;
        public float HungerSpeedModifier = 1;
        public float BaseMoveSpeed = 1.5f;

        public int SunBrightness = 22;


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
    public class CoreSystem : ModSystem
	{
        ICoreAPI api;
        ICoreClientAPI capi;
        SurvivalConfig config = new SurvivalConfig();

        public IShaderProgram anvilShaderProg;

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
            api.Assets.AddPathOrigin("game", Path.Combine(GamePaths.AssetsPath, "survival"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.BlockTexturesLoaded += () => { loadConfig(); applyConfig(); };

            api.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        private bool LoadShader()
        {
            anvilShaderProg = capi.Shader.NewShaderProgram();

            anvilShaderProg.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            anvilShaderProg.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            capi.Shader.RegisterFileShaderProgram("anvilworkitem", anvilShaderProg);

            return anvilShaderProg.Compile();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {

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
            api.Event.ServerRunPhase(EnumServerRunPhase.LoadGame, () => {
                applyConfig();
                config.ResolveStartItems(api.World);
            });

            api.Event.ServerRunPhase(EnumServerRunPhase.LoadGamePre, loadConfig);
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

            if (api.Side == EnumAppSide.Server)
            {
                GlobalConstants.CreatureDamageModifier = config.CreatureDamageModifier;

                foreach (var obj in api.World.Collectibles)
                {
                    if (obj.Tool != null)
                    {
                        obj.Durability = (int)(obj.Durability * config.ToolDurabilityModifier);
                    }
                }
            }
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            
            RegisterDefaultBlocks();
            RegisterDefaultBlockBehaviors();
            RegisterDefaultBlockEntityBehaviors();
            RegisterDefaultCropBehaviors();
            RegisterDefaultItems();
            RegisterDefaultEntities();
            RegisterDefaultEntityBehaviors();
            RegisterDefaultBlockEntities();

            api.RegisterMountable("bed", BlockBed.GetMountable);
        }
        

        private void RegisterDefaultBlocks()
        {
            api.RegisterBlockClass("BlockFirepit", typeof(BlockFirepit));
            api.RegisterBlockClass("BlockTorch", typeof(BlockTorch));
            api.RegisterBlockClass("BlockStairs", typeof(BlockStairs));
            api.RegisterBlockClass("BlockFence", typeof(BlockFence));
            api.RegisterBlockClass("BlockDoor", typeof(BlockDoor));
            api.RegisterBlockClass("BlockFenceGate", typeof(BlockFenceGate));
            api.RegisterBlockClass("BlockLayered", typeof(BlockLayered));
            api.RegisterBlockClass("BlockVines", typeof(BlockVines));
            api.RegisterBlockClass("BlockPlant", typeof(BlockPlant));
            api.RegisterBlockClass("BlockRails", typeof(BlockRails));
            api.RegisterBlockClass("BlockCactus", typeof(BlockCactus));
            api.RegisterBlockClass("BlockSlab", typeof(BlockSlab));
            api.RegisterBlockClass("BlockFlowerPot", typeof(BlockFlowerPot));
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
            api.RegisterBlockClass("BlockLootVessel", typeof(BlockLootVessel));
            api.RegisterBlockClass("BlockBomb", typeof(BlockBomb));
            api.RegisterBlockClass("BlockToolMold", typeof(BlockToolMold));
            api.RegisterBlockClass("BlockLayeredSlowDig", typeof(BlockLayeredSlowDig));
            api.RegisterBlockClass("BlockClayForm", typeof(BlockClayForm));
            api.RegisterBlockClass("BlockKnappingSurface", typeof(BlockKnappingSurface));
            api.RegisterBlockClass("BlockBamboo", typeof(BlockBamboo));
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
            api.RegisterBlockClass("BlockTorchHolder", typeof(BlockTorchHolder));
            api.RegisterBlockClass("BlockGenericTypedContainer", typeof(BlockGenericTypedContainer));
            api.RegisterBlockClass("BlockTeleporter", typeof(BlockTeleporter));
            api.RegisterBlockClass("BlockQuern", typeof(BlockQuern));
            api.RegisterBlockClass("BlockWithGrassOverlay", typeof(BlockWithGrassOverlay));
            api.RegisterBlockClass("BlockTinted", typeof(BlockTinted));
            api.RegisterBlockClass("BlockPlaceOnDrop", typeof(BlockPlaceOnDrop));
            api.RegisterBlockClass("BlockLooseGears", typeof(BlockLooseGears));
            api.RegisterBlockClass("BlockSpawner", typeof(BlockSpawner));
            api.RegisterBlockClass("BlockMeal", typeof(BlockMeal));
            api.RegisterBlockClass("BlockBowl", typeof(BlockBowl));
            api.RegisterBlockClass("BlockWateringCan", typeof(BlockWateringCan));
            api.RegisterBlockClass("BlockTrough", typeof(BlockTrough));
            api.RegisterBlockClass("BlockLeaves", typeof(BlockLeaves));
            api.RegisterBlockClass("BlockTroughDoubleBlock", typeof(BlockTroughDoubleBlock));
            api.RegisterBlockClass("BlockFarmland", typeof(BlockFarmland));

            api.RegisterBlockClass("BlockAxle", typeof(BlockAxle));
            api.RegisterBlockClass("BlockAngledGears", typeof(BlockAngledGears));
            api.RegisterBlockClass("BlockWindmillRotor", typeof(BlockWindmillRotor));
            api.RegisterBlockClass("BlockToggle", typeof(BlockToggle));

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
            api.RegisterBlockClass("BlockHopper", typeof(BlockHopper));
            api.RegisterBlockClass("BlockBarrel", typeof(BlockBarrel));
            api.RegisterBlockClass("BlockWaterflowing", typeof(BlockWaterflowing));
            api.RegisterBlockClass("BlockShelf", typeof(BlockShelf));
            api.RegisterBlockClass("BlockCrock", typeof(BlockCrock));
            api.RegisterBlockClass("BlockThermalDiff", typeof(BlockThermalDifference));
            api.RegisterBlockClass("BlockSignPost", typeof(BlockSignPost));

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
            api.RegisterBlockClass("BlockDisplayCase", typeof(BlockDisplayCase));
            api.RegisterBlockClass("BlockTapestry", typeof(BlockTapestry));
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

            
        }

        private void RegisterDefaultBlockEntityBehaviors()
        {
            api.RegisterBlockEntityBehaviorClass("Animatable", typeof(BEBehaviorAnimatable));
            api.RegisterBlockEntityBehaviorClass("MPAxle", typeof(BEBehaviorMPAxle));
            api.RegisterBlockEntityBehaviorClass("MPToggle", typeof(BEBehaviorMPToggle));

            api.RegisterBlockEntityBehaviorClass("MPAngledGears", typeof(BEBehaviorMPAngledGears));
            api.RegisterBlockEntityBehaviorClass("MPWindmillRotor", typeof(BEBehaviorWindmillRotor));
            api.RegisterBlockEntityBehaviorClass("MPConsumer", typeof(BEBehaviorMPConsumer));

            api.RegisterBlockEntityBehaviorClass("MPBrake", typeof(BEBehaviorMPBrake));
            api.RegisterBlockEntityBehaviorClass("MPTransmission", typeof(BEBehaviorMPTransmission));

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
            api.RegisterBlockEntityClass("Farmland", typeof(BlockEntityFarmland));
            api.RegisterBlockEntityClass("FirewoodPile", typeof(BlockEntityFirewoodPile));
            api.RegisterBlockEntityClass("ToolRack", typeof(BlockEntityToolrack));
            api.RegisterBlockEntityClass("IngotMold", typeof(BlockEntityIngotMold));

            api.RegisterBlockEntityClass("CookedContainer", typeof(BlockEntityCookedContainer));
            api.RegisterBlockEntityClass("Meal", typeof(BlockEntityMeal));
            api.RegisterBlockEntityClass("SmeltedContainer", typeof(BlockEntitySmeltedContainer));

            api.RegisterBlockEntityClass("Anvil", typeof(BlockEntityAnvil));
            api.RegisterBlockEntityClass("Forge", typeof(BlockEntityForge));
            api.RegisterBlockEntityClass("Bomb", typeof(BlockEntityBomb));
            api.RegisterBlockEntityClass("ToolMold", typeof(BlockEntityToolMold));

            api.RegisterBlockEntityClass("Fire", typeof(BlockEntityFire));
            api.RegisterBlockEntityClass("CharcoalPit", typeof(BlockEntityCharcoalPit));
            api.RegisterBlockEntityClass("PumpkinVine", typeof(BlockEntityPumpkinVine));
            api.RegisterBlockEntityClass("ClayForm", typeof(BlockEntityClayForm));
            api.RegisterBlockEntityClass("KnappingSurface", typeof(BlockEntityKnappingSurface));
            api.RegisterBlockEntityClass("Bloomery", typeof(BlockEntityBloomery));
            api.RegisterBlockEntityClass("Bed", typeof(BlockEntityBed));
            api.RegisterBlockEntityClass("Firepit", typeof(BlockEntityFirepit));
            api.RegisterBlockEntityClass("Stove", typeof(BlockEntityStove));
            api.RegisterBlockEntityClass("Beehive", typeof(BlockEntityBeehive));
            api.RegisterBlockEntityClass("Lantern", typeof(BELantern));
            api.RegisterBlockEntityClass("Chisel", typeof(BlockEntityChisel));
            api.RegisterBlockEntityClass("Teleporter", typeof(BlockEntityTeleporter));
            api.RegisterBlockEntityClass("Quern", typeof(BlockEntityQuern));
            api.RegisterBlockEntityClass("Spawner", typeof(BlockEntitySpawner));
            api.RegisterBlockEntityClass("WateringCan", typeof(BlockEntityWateringCan));
            api.RegisterBlockEntityClass("Bucket", typeof(BlockEntityBucket));
            api.RegisterBlockEntityClass("Trough", typeof(BlockEntityTrough));

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

            api.RegisterBlockEntityClass("DisplayCase", typeof(BlockEntityDisplayCase));
            api.RegisterBlockEntityClass("Tapestry", typeof(BlockEntityTapestry));
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
        }


        private void RegisterDefaultEntityBehaviors()
        {
            api.RegisterEntityBehaviorClass("temporalStabilityAffected", typeof(EntityBehaviorTemporalStabilityAffected));
        }






    }
}
