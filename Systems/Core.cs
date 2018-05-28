using Newtonsoft.Json;
using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.ServerMods
{
    public class SurvivalConfig
    {
        public AssetLocation CurrencyItemIcon = new AssetLocation("gear-rusty");
    }
    
    /// <summary>
    /// This class contains core settings for the Vintagestory server
    /// </summary>
    public class Core : ModSystem
	{
        ICoreServerAPI sapi;
        ICoreAPI api;
        //SurvivalConfig config;

        public override double ExecuteOrder()
        {
            return 0;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            
            return true;
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            api.RegisterEntityRendererClass("Item", typeof(EntityItemRenderer));
            
            api.RegisterEntityRendererClass("BlockFalling", typeof(EntityBlockFallingRenderer));
            api.RegisterEntityRendererClass("Shape", typeof(EntityShapeRenderer));
            api.RegisterEntityRendererClass("SkinnableShape", typeof(EntitySkinnableShapeRenderer));
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            if (api.ModLoader.IsModSystemEnabled("Vintagestory.ServerMods.WorldEdit.WorldEdit"))
            {
                RegisterUtil.RegisterTool(api.ModLoader.GetModSystem("Vintagestory.ServerMods.WorldEdit.WorldEdit"));
            }

            

            
            this.sapi = api;

            float[] sunlightLevels = new float[]
       {0.06f, 0.176f, 0.206f, 0.236f, 0.266f, 0.296f, 0.326f, 0.356f, 0.386f, 0.416f, 0.446f, 0.476f, 0.506f, 0.536f, 0.566f, 0.596f, 0.626f, 0.656f, 0.686f, 0.716f, 0.746f, 0.776f, 0.806f, 0.836f, 0.866f, 0.896f, 0.926f, 0.956f, 0.986f, 1f, 1f, 1f}
       ;

            float[] blocklightlevels = new float[] { 0.08f, 0.149f, 0.184f, 0.219f, 0.254f, 0.289f, 0.324f, 0.359f, 0.394f, 0.429f, 0.464f, 0.499f, 0.534f, 0.569f, 0.604f, 0.639f, 0.674f, 0.709f, 0.744f, 0.779f, 0.814f, 0.849f, 0.884f, 0.919f, 0.954f, 0.989f, 1f, 1f, 1f, 1f, 1f, 1f };

            // Set up day/night cycle
            api.WorldManager.SetBlockLightLevels(blocklightlevels);
            api.WorldManager.SetSunLightLevels(sunlightLevels);
            api.WorldManager.SetSunBrightness(22);

            //api.WorldManager.SetCurrencyIcon(config.CurrencyItemIcon);
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            /*try
            {
                using (TextReader textReader = new StreamReader(Path.Combine(api.DataBasePath, "survivalconfig.json")))
                {
                    config = JsonConvert.DeserializeObject<SurvivalConfig>(textReader.ReadToEnd());
                    textReader.Close();
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading survivalconfig.json, error {0}. Will initialize new one", e);
                config = new SurvivalConfig();
            }*/

            RegisterDefaultBlocks();
            RegisterDefaultBlockBehaviors();
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
            //api.RegisterBlockClass("BlockUnstable", typeof(BlockUnstable));
            api.RegisterBlockClass("BlockLooseStones", typeof(BlockLooseStones));
            api.RegisterBlockClass("BlockIngotPile", typeof(BlockIngotPile));
            api.RegisterBlockClass("BlockBucket", typeof(BlockBucket));
            api.RegisterBlockClass("BlockCrop", typeof(BlockCrop));
            api.RegisterBlockClass("BlockWaterPlant", typeof(BlockWaterPlant));
            api.RegisterBlockClass("BlockSeaweed", typeof(BlockSeaweed));
            api.RegisterBlockClass("BlockIngotPile", typeof(BlockIngotPile));
            api.RegisterBlockClass("BlockFirewoodPile", typeof(BlockFirewoodPile));
            api.RegisterBlockClass("BlockToolRack", typeof(BlockToolRack));
            api.RegisterBlockClass("BlockSmeltingContainer", typeof(BlockSmeltingContainer));
            api.RegisterBlockClass("BlockCookingContainer", typeof(BlockCookingContainer));
            api.RegisterBlockClass("BlockLiquidMetalContainer", typeof(BlockLiquidMetalContainer));
            api.RegisterBlockClass("BlockIngotMold", typeof(BlockIngotMold));
            api.RegisterBlockClass("BlockPlatePile", typeof(BlockPlatePile));
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
            api.RegisterBlockClass("BlockEmptyTorchHolder", typeof(BlockEmptyTorchHolder));
            api.RegisterBlockClass("BlockGenericTypedContainer", typeof(BlockGenericTypedContainer));
            api.RegisterBlockClass("BlockTeleporter", typeof(BlockTeleporter));
            api.RegisterBlockClass("BlockQuern", typeof(BlockQuern));
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
        }



        private void RegisterDefaultBlockEntities()
        {
            api.RegisterBlockEntityClass("GenericContainer", typeof(BlockEntityGenericContainer));
            api.RegisterBlockEntityClass("GenericTypedContainer", typeof(BlockEntityGenericTypedContainer));
            api.RegisterBlockEntityClass("Crucible", typeof(BlockEntityLiquidMetalContainer));
            api.RegisterBlockEntityClass("Sign", typeof(BlockEntitySign));
            api.RegisterBlockEntityClass("ParticleEmitter", typeof(BlockEntityParticleEmitter));
            api.RegisterBlockEntityClass("BerryBush", typeof(BlockEntityBerryBush));
            api.RegisterBlockEntityClass("IngotPile", typeof(BlockEntityIngotPile));
            api.RegisterBlockEntityClass("PlatePile", typeof(BlockEntityPlatePile));
            api.RegisterBlockEntityClass("Farmland", typeof(BlockEntityFarmland));
            api.RegisterBlockEntityClass("FirewoodPile", typeof(BlockEntityFirewoodPile));
            api.RegisterBlockEntityClass("ToolRack", typeof(BlockEntityToolrack));
            api.RegisterBlockEntityClass("IngotMold", typeof(BlockEntityIngotMold));
            api.RegisterBlockEntityClass("MetalLiquidContainer", typeof(BlockEntityLiquidMetalContainer));
            api.RegisterBlockEntityClass("Anvil", typeof(BlockEntityAnvil));
            api.RegisterBlockEntityClass("Forge", typeof(BlockEntityForge));
            api.RegisterBlockEntityClass("Bomb", typeof(BlockEntityBomb));
            api.RegisterBlockEntityClass("ToolMold", typeof(BlockEntityToolMold));

            api.RegisterBlockEntityClass("Fire", typeof(BlockEntityFire));
            api.RegisterBlockEntityClass("CharcoalPit", typeof(BlockEntityCharcoalPit));
            api.RegisterBlockEntityClass("Transient", typeof(BlockEntityTransient));
            api.RegisterBlockEntityClass("PumpkinVine", typeof(BlockEntityPumpkinVine));
            api.RegisterBlockEntityClass("ClayForm", typeof(BlockEntityClayForm));
            api.RegisterBlockEntityClass("KnappingSurface", typeof(BlockEntityKnappingSurface));
            api.RegisterBlockEntityClass("Bloomery", typeof(BlockEntityBloomery));
            api.RegisterBlockEntityClass("Bed", typeof(BlockEntityBed));
            api.RegisterBlockEntityClass("Firepit", typeof(BlockEntityFirepit));
            api.RegisterBlockEntityClass("Stove", typeof(BlockEntityStove));
            api.RegisterBlockEntityClass("Beehive", typeof(BlockEntityBeehive));
            api.RegisterBlockEntityClass("Lantern", typeof(BELantern));

            api.RegisterBlockEntityClass("AngledGears", typeof(BlockEntityAngledGears));
            api.RegisterBlockEntityClass("Chisel", typeof(BlockEntityChisel));
            api.RegisterBlockEntityClass("Teleporter", typeof(BlockEntityTeleporter));
            api.RegisterBlockEntityClass("Quern", typeof(BlockEntityQuern));
        }


        private void RegisterDefaultCropBehaviors()
        {
            api.RegisterCropBehavior("Pumpkin", typeof(PumpkinCropBehavior));
        }


        private void RegisterDefaultItems()
        {
            
            api.RegisterItemClass("ItemOre", typeof(ItemOre));
            api.RegisterItemClass("ItemStone", typeof(ItemStone));
            api.RegisterItemClass("ItemDryGrass", typeof(ItemDryGrass));
            api.RegisterItemClass("ItemFirewood", typeof(ItemFirewood));
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
            api.RegisterItemClass("ItemLore", typeof(ItemLore));
            api.RegisterItemClass("ItemFlint", typeof(ItemFlint));
            api.RegisterItemClass("ItemSpear", typeof(ItemSpear));
            api.RegisterItemClass("ItemAxe", typeof(ItemAxe));
            api.RegisterItemClass("ItemProspectingPick", typeof(ItemProspectingPick));
            api.RegisterItemClass("ItemStrawDummy", typeof(ItemStrawDummy));
            api.RegisterItemClass("ItemCreature", typeof(ItemCreature));
            api.RegisterItemClass("ItemLootRandomizer", typeof(ItemLootRandomizer));
            api.RegisterItemClass("ItemScythe", typeof(ItemScythe));
            api.RegisterItemClass("ItemTemporalGear", typeof(ItemTemporalGear));
            api.RegisterItemClass("ItemHoneyComb", typeof(ItemHoneyComb));
            api.RegisterItemClass("ItemOpenedBeenade", typeof(ItemOpenedBeenade));
            api.RegisterItemClass("ItemClosedBeenade", typeof(ItemClosedBeenade));
            api.RegisterItemClass("ItemCandle", typeof(ItemCandle));
            api.RegisterItemClass("ItemDress", typeof(ItemDress));
            api.RegisterItemClass("ItemStackRandomizer", typeof(ItemStackRandomizer));
            api.RegisterItemClass("ItemChisel", typeof(ItemChisel));
        }




        private void RegisterDefaultEntities()
        {    
            api.RegisterEntity("EntityNpc", typeof(EntityPlayerNpc));
            api.RegisterEntity("EntityBlockfalling", typeof(EntityBlockFalling));
            api.RegisterEntity("EntityProjectile", typeof(EntityProjectile));
            api.RegisterEntity("EntityThrownStone", typeof(EntityThrownStone));
            api.RegisterEntity("EntityBeeMob", typeof(EntityBeeMob));
            api.RegisterEntity("EntityThrownBeenade", typeof(EntityThrownBeenade));
        }


        private void RegisterDefaultEntityBehaviors()
        {
            api.RegisterEntityBehaviorClass("collectitems", typeof(EntityBehaviorCollectEntities));
            api.RegisterEntityBehaviorClass("health", typeof(EntityBehaviorHealth));
            api.RegisterEntityBehaviorClass("hunger", typeof(EntityBehaviorHunger));
            api.RegisterEntityBehaviorClass("breathe", typeof(EntityBehaviorBreathe));
            
            api.RegisterEntityBehaviorClass("playerphysics", typeof(EntityBehaviorPlayerPhysics));
            api.RegisterEntityBehaviorClass("controlledphysics", typeof(EntityBehaviorControlledPhysics));
            
            api.RegisterEntityBehaviorClass("taskai", typeof(EntityBehaviorTaskAI));
            api.RegisterEntityBehaviorClass("interpolateposition", typeof(EntityBehaviorInterpolatePosition));
            api.RegisterEntityBehaviorClass("despawn", typeof(EntityBehaviorDespawn));

            api.RegisterEntityBehaviorClass("grow", typeof(EntityBehaviorGrow));
            api.RegisterEntityBehaviorClass("multiply", typeof(EntityBehaviorMultiply));
            api.RegisterEntityBehaviorClass("aimingaccuracy", typeof(EntityBehaviorAimingAccuracy));
            api.RegisterEntityBehaviorClass("emotionstates", typeof(EntityBehaviorEmotionStates));
            api.RegisterEntityBehaviorClass("repulseagents", typeof(EntityBehaviorRepulseAgents));
            api.RegisterEntityBehaviorClass("tiredness", typeof(EntityBehaviorTiredness));
            api.RegisterEntityBehaviorClass("nametag", typeof(EntityBehaviorNameTag));
        }






    }
}
