using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Vintage Story Default Server Mods")]
[assembly: AssemblyDescription("www.vintagestory.at")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Tyron Madlener (Anego Studios)")]
[assembly: AssemblyProduct("Vintage Story")]
[assembly: AssemblyCopyright(GameVersion.CopyRight)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("7d22278b-7ffc-403a-92d0-fd87c7609912")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion(GameVersion.AssemblyVersion)]
[assembly: AssemblyFileVersion(GameVersion.OverallVersion)]
[assembly: InternalsVisibleTo("VSSurvivalModTests")]

[assembly: ModInfo("Survival Mode", "survival",
    Version = GameVersion.ShortGameVersion,
    NetworkVersion = GameVersion.NetworkVersion,
    CoreMod = true,
    IconPath = "game/textures/gui/modicon.png",
    Description = "Survival world blocks, items, crafting mechanics, creatures and pretty world generation",
    Authors = new[] { "Tyron" },
    WorldConfig = """
    {
        "playstyles": [
            {
                "code": "surviveandbuild",
                "playListCode": "survival",
                "langCode": "preset-surviveandbuild",
                "mods": ["game", "survival"],
                "listOrder": 5,
                "worldType": "standard",
                "worldConfig": {
                    "worldClimate": "realistic",
                    "gameMode": "survival",
                    "temporalStability": "true",
                    "temporalStorms": "sometimes",
                    "graceTimer": "0",
                    "microblockChiseling": "stonewood",
                    "polarEquatorDistance": "100000",
                    "lungCapacity": "40000",
                    "harshWinters": "true",
                    "daysPerMonth": "9",
                    "saplingGrowthRate": "1",
                    "propickNodeSearchRadius": "6",
                    "allowUndergroundFarming": "false",
                    "allowFallingBlocks": "true",
                    "allowFireSpread": "true",
                    "temporalGearRespawnUses": "20",
                    "temporalStormSleeping": "0",
                    "clutterObtainable": "ifrepaired"
                }
            },
            {
                "code": "exploration",
                "playListCode": "survival",
                "langCode": "preset-exploration",
                "mods": ["game", "survival"],
                "listOrder": 6,
                "worldType": "standard",
                "worldConfig": {
                    "worldClimate": "realistic",
                    "gameMode": "survival",
                    "microblockChiseling": "all",
                    "deathPunishment": "keep",
                    "graceTimer": "5",
                    "creatureHostility": "passive",
                    "playerHealthPoints": "20",
                    "playerHungerSpeed": "0.5",
                    "playerHealthRegenSpeed": "1",
                    "foodSpoilSpeed": "0.5",
                    "lungCapacity": "120000",
                    "toolDurability": "2",
                    "saplingGrowthRate": "0.5",
                    "playerMoveSpeed": "1.25",
                    "temporalStability": "false",
                    "temporalStorms": "off",
                    "surfaceCopperDeposits": "0.2",
                    "surfaceTinDeposits": "0.03",
                    "globalDepositSpawnRate": "1.6",
                    "propickNodeSearchRadius": "8",
                    "polarEquatorDistance": "50000",
                    "harshWinters": "false",
                    "allowUndergroundFarming": "true",
                    "allowFallingBlocks": "true",
                    "allowFireSpread": "true",
                    "temporalGearRespawnUses": "-1",
                    "temporalStormSleeping": "1",
                    "classExclusiveRecipes": "false",
                    "clutterObtainable": "yes"
                }
            },
            {
                "code": "wildernesssurvival",
                "playListCode": "survival",
                "langCode": "preset-wildernesssurvival",
                "mods": ["game", "survival"],
                "listOrder": 7,
                "worldType": "standard",
                "worldConfig": {
                    "worldClimate": "realistic",
                    "gameMode": "survival",
                    "microblockChiseling": "off",
                    "deathPunishment": "drop",
                    "bodyTemperatureResistance": "10",
                    "blockGravity": "sandgravelsoil",
                    "caveIns": "on",
                    "allowFallingBlocks": "true",
                    "allowFireSpread": "true",
                    "creatureHostility": "aggressive",
                    "playerHealthPoints": "10",
                    "creatureStrength": "1.5",
                    "playerHungerSpeed": "1.25",
                    "playerHealthRegenSpeed": "1",
                    "lungCapacity": "20000",
                    "foodSpoilSpeed": "1.25",
                    "graceTimer": "0",
                    "allowCoordinateHud": "false",
                    "allowMap": "false",
                    "allowLandClaiming": "false",
                    "surfaceCopperDeposits": "0.05",
                    "surfaceTinDeposits": "0",
                    "saplingGrowthRate": "2",
                    "temporalStability": "true",
                    "temporalStorms": "often",
                    "polarEquatorDistance": "100000",
                    "harshWinters": "true",
                    "daysPerMonth": "9",
                    "spawnRadius": "5000",
                    "allowUndergroundFarming": "false",
                    "noLiquidSourceTransport": "true",
                    "temporalGearRespawnUses": "3",
                    "temporalStormSleeping": "0",
                    "clutterObtainable": "ifrepaired",
                    "lightningFires": "true"
                }
            },
            {
                "code": "homosapiens",
                "playListCode": "survival",
                "langCode": "preset-homosapiens",
                "mods": ["game", "survival"],
                "listOrder": 7,
                "worldType": "standard",
                "worldConfig": {
                    "worldClimate": "realistic",
                    "gameMode": "survival",
                    "deathPunishment": "drop",
                    "bodyTemperatureResistance": "5",
                    "blockGravity": "sandgravelsoil",
                    "allowFallingBlocks": "true",
                    "allowFireSpread": "true",
                    "creatureHostility": "aggressive",
                    "playerHealthPoints": "10",
                    "creatureStrength": "1.5",
                    "playerHungerSpeed": "1",
                    "playerHealthRegenSpeed": "1",
                    "lungCapacity": "30000",
                    "foodSpoilSpeed": "1.25",
                    "graceTimer": "0",
                    "allowCoordinateHud": "false",
                    "allowMap": "false",
                    "allowLandClaiming": "false",
                    "surfaceCopperDeposits": "0.05",
                    "surfaceTinDeposits": "0",
                    "saplingGrowthRate": "2",
                    "temporalStorms": "off",
                    "polarEquatorDistance": "200000",
                    "harshWinters": "true",
                    "daysPerMonth": "9",
                    "allowUndergroundFarming": "false",
                    "noLiquidSourceTransport": "true",
                    "spawnRadius": "5000",
                    "temporalGearRespawnUses": "0",
                    "temporalStormSleeping": "0",
                    "temporalRifts": "off",
                    "temporalStability": "false",
                    "loreContent": "false",
                    "clutterObtainable": "no"
                }
            }
        ],

        "worldConfigAttributes": [
            { "category": "spawnndeath", "code": "gameMode", "dataType": "dropdown", "values": ["survival", "creative"], "names": ["Survival", "Creative"], "default": "survival" },
            { "category": "spawnndeath", "code": "playerlives", "dataType": "dropdown", "values": ["1", "2", "3", "4", "5", "10", "20", "-1"], "names": ["1", "2", "3", "4", "5", "10", "20", "infinite"], "default": "-1" },

            { "category": "spawnndeath", "code": "startingClimate", "dataType": "dropdown", "values": ["hot", "warm", "temperate", "cool", "icy"], "names": ["Hot (28-32°C)", "Warm (19-23 °C)", "Temperate (6-14 °C)", "Cool (-5 to 1 °C)", "Icy (-15 to -10°C)"], "default": "temperate", "onlyDuringWorldCreate": true },
            { "category": "spawnndeath", "code": "spawnRadius", "dataType": "dropdown", "values": ["10000", "5000", "2500", "1000", "500", "250", "100", "50", "25", "0"], "names": ["10000 blocks", "5000 blocks", "2500 blocks", "1000 blocks", "500 blocks", "250 blocks", "100 blocks", "50 blocks", "25 blocks","0 blocks"], "default": "50" },
            { "category": "spawnndeath", "code": "graceTimer", "dataType": "dropdown", "values": ["10", "5", "4", "3", "2", "1", "0"], "names": ["10 days before monsters appear", "5 days before monsters appear", "4 days before monsters appear", "3 days before monsters appear", "2 days before monsters appear", "1 day before monsters appear", "No timer. Monsters spawn right away."], "default": "0", "onlyDuringWorldCreate": true },
            { "category": "spawnndeath", "code": "deathPunishment", "dataType": "dropdown", "values": ["drop", "keep"], "names": ["Drop inventory contents", "Keep inventory contents"], "default": "drop" },
            { "category": "spawnndeath", "code": "droppedItemsTimer", "dataType": "dropdown", "values": ["300", "600", "1200", "1800", "3600"], "names": ["5 minutes", "10 minutes", "20 minutes", "30 minutes", "1 hour"], "default": "600" },

            { "category": "survivalchallenges", "code": "seasons", "dataType": "dropdown", "values": ["enabled", "spring", "summer", "fall", "winter"], "names": ["Enabled", "Off, always spring", "Off, always summer", "Off, always fall", "Off, always winter"], "default": "enabled" },
            { "category": "survivalchallenges", "code": "daysPerMonth", "dataType": "dropdown", "values": ["30", "20", "12", "9", "6", "3"], "names": ["30 days (24 real life hours)", "20 days (16 real life hours)", "12 days (9.6 real life hours)", "9 days (7.2 real life hours)", "6 days (4.8 real life hours)", "3 days (2.4 real life hours)"], "default": "9" },
            { "category": "survivalchallenges", "code": "harshWinters", "dataType": "dropdown", "values": ["true", "false"], "names": ["Enabled", "Disabled"], "default": "true" },
            { "category": "survivalchallenges", "code": "blockGravity", "dataType": "dropdown", "values": ["sandgravel", "sandgravelsoil"], "names": ["Sand and gravel", "Sand, gravel and soil with sideways instability"], "default": "sandgravel" },
            { "category": "survivalchallenges", "code": "caveIns", "dataType": "dropdown", "values": ["off", "on"], "names": ["Disabled", "Enabled"], "default": "off" },
            { "category": "survivalchallenges", "code": "allowFallingBlocks", "dataType": "bool", "default": "true" },
            { "category": "survivalchallenges", "code": "allowFireSpread", "dataType": "bool", "default": "true" },
            { "category": "survivalchallenges", "code": "lightningFires", "dataType": "bool", "default": "false" },
            { "category": "survivalchallenges", "code": "allowUndergroundFarming", "dataType": "bool", "default": "false" },
            { "category": "survivalchallenges", "code": "noLiquidSourceTransport", "dataType": "bool", "default": "false" },
            { "category": "survivalchallenges", "code": "playerHealthPoints", "dataType": "dropdown", "values": ["5", "10", "15", "20", "25", "30", "35"], "names": ["5 hp", "10 hp", "15 hp", "20 hp", "25 hp", "30 hp", "35 hp"], "default": "15" },
            { "category": "survivalchallenges", "code": "playerHealthRegenSpeed", "dataType": "dropdown", "values": ["2", "1.5", "1.25", "1", "0.75", "0.5", "0.25"], "names": ["Very fast (200%)", "Fast (150%)", "Slightly faster (125%)", "Normal (100%)", "Slightly slower (75%)", "Slower (50%)", "Much slower (25%)"], "default": "1" },
            { "category": "survivalchallenges", "code": "playerHungerSpeed", "dataType": "dropdown", "values": ["2", "1.5", "1.25", "1", "0.75", "0.5", "0.25"], "names": ["Very fast (200%)", "Fast (150%)", "Slightly faster (125%)", "Normal (100%)", "Slightly slower (75%)", "Slower (50%)", "Much slower (25%)"], "default": "1" },
            { "category": "survivalchallenges", "code": "lungCapacity", "dataType": "dropdown", "values": ["10000", "20000", "30000", "40000", "60000", "120000", "3600000"], "names": ["10 seconds", "20 seconds", "30 seconds", "40 seconds", "60 seconds", "2 minutes", "60 minutes"], "default": "40000" },
            { "category": "survivalchallenges", "code": "bodyTemperatureResistance", "dataType": "dropdown", "values": ["-40", "-30", "-25", "-20", "-15", "-10", "-5", "0", "5", "10", "15", "20"], "names": ["-40", "-30", "-25", "-20", "-15", "-10", "-5", "0", "5", "10", "15", "20"], "default": "0" },
            { "category": "survivalchallenges", "code": "playerMoveSpeed", "dataType": "dropdown", "values": ["2", "1.75", "1.5", "1.25", "1", "0.75"], "names": ["Fast", "Slightly faster", "Normal", "Slightly slower", "Slower", "Much slower"], "default": "1.5" },
            { "category": "survivalchallenges", "code": "creatureHostility", "dataType": "dropdown", "values": ["aggressive", "passive", "off"], "names": ["Aggressive", "Passive", "Never hostile"], "default": "aggressive" },
            { "category": "survivalchallenges", "code": "creatureStrength", "dataType": "dropdown", "values": ["4", "2", "1.5", "1", "0.5", "0.25"], "names": ["Deadly (400%)", "Very Strong (200%)", "Strong (150%)", "Normal (100%)", "Weak (50%)", "Very weak (25%)"], "default": "1" },
            { "category": "survivalchallenges", "code": "creatureSwimSpeed", "dataType": "dropdown", "values": ["0.5", "0.75", "1", "1.25", "1.5", "1.75", "2", "3"], "names": ["50%", "75%", "100%", "125%", "150%", "175%", "200%", "300%"], "default": "2" },
            { "category": "survivalchallenges", "code": "foodSpoilSpeed", "dataType": "dropdown", "values": ["4", "3", "2", "1.5", "1.25", "1", "0.75", "0.5", "0.25"], "names": ["400%", "300%", "200%", "150%", "125%", "100%", "75%", "50%", "25%"], "default": "1" },
            { "category": "survivalchallenges", "code": "saplingGrowthRate", "dataType": "dropdown", "values": ["16", "8", "4", "2", "1.5", "1", "0.75", "0.5", "0.25"], "names": ["Extremely slow (16x)", "Much slower (8x)", "Slower (4x)", "Somewhat slower (2x)", "Slightly slower (1.5x)", "Normal (1x)", "Slightly faster (0.75x)", "Faster (0.5x)", "Much faster (0.25x)"], "default": "1" },
            { "category": "survivalchallenges", "code": "toolDurability", "dataType": "dropdown", "values": ["4", "3", "2", "1.5", "1.25", "1", "0.75", "0.5"], "names": ["400%", "300%", "200%", "150%", "125%", "100%", "75%", "50%"], "default": "1" },
            { "category": "survivalchallenges", "code": "toolMiningSpeed", "dataType": "dropdown", "values": ["3", "2", "1.5", "1.25", "1", "0.75", "0.5", "0.25"], "names": ["300%", "200%", "150%", "125%", "100%", "75%", "50%", "25%"], "default": "1" },
            { "category": "survivalchallenges", "code": "propickNodeSearchRadius", "dataType": "dropdown", "values": ["0", "2", "4", "6", "8"], "names": ["Disabled", "2 blocks", "4 blocks", "6 blocks", "8 blocks"], "default": "0" },
            { "category": "survivalchallenges", "code": "microblockChiseling", "dataType": "dropdown", "values": ["off", "stonewood", "all"], "names": ["Off", "Stone and Wood", "Most cubic blocks"], "default": "stonewood" },
            { "category": "survivalchallenges", "code": "allowCoordinateHud", "dataType": "bool", "default": "true" },
            { "category": "survivalchallenges", "code": "allowMap", "dataType": "bool", "default": "true" },
            { "category": "survivalchallenges", "code": "colorAccurateWorldmap", "dataType": "bool", "default": "false" },
            { "category": "survivalchallenges", "code": "loreContent", "dataType": "bool", "default": "true" },
            { "category": "survivalchallenges", "code": "clutterObtainable", "dataType": "dropdown", "values": ["ifrepaired", "yes", "no"], "names": ["ifrepaired", "yes", "no"], "default": "ifrepaired" },

            { "category": "temporalstability", "code": "temporalStability", "dataType": "bool", "default": "true" },
            { "category": "temporalstability", "code": "temporalStorms", "dataType": "dropdown", "values": ["off", "veryrare", "rare", "sometimes", "often", "veryoften"], "names": ["Off", "Every 30-40 days, increase strength/frequency by 2.5% each time, capped at +25%", "Approx. every 20-30 days, increase strength/frequency by 5% each time, capped at +50%", "Approx. every 10-20 days, increase strength/frequency by +10% each time, capped at 100%", "Approx. every 5-10 days, increase strength/frequency by 15% each time, capped at +150%", "Approx. every 3-6 days, increase strength/frequency by 20% each time, capped at +200%"], "default": "sometimes" },
            { "category": "temporalstability", "code": "tempstormDurationMul", "dataType": "dropdown", "values": ["2", "1.5", "1.25", "1", "0.75", "0.5", "0.25"], "names": ["Much longer (200%)", "Longer (150%)", "Slightly longer (125%)", "Normal (100%)", "Slightly shorter (75%)", "Shorter (50%)", "Much Shorter (25%)"], "default": "1" },
            { "category": "temporalstability", "code": "temporalRifts", "dataType": "dropdown", "values": ["off", "invisible", "visible"], "names": ["Off", "Invisible", "Visible"], "default": "visible" },
            { "category": "temporalstability", "code": "temporalGearRespawnUses", "dataType": "dropdown", "values": ["-1", "20", "10", "5", "4", "3", "2", "1"], "names": ["Infinite", "20 times", "10 times", "5 times", "4 times", "3 times", "2 times", "One time"], "default": "1" },
            { "category": "temporalstability", "code": "temporalStormSleeping", "dataType": "dropdown", "values": ["0", "1"], "names": ["Disallowed", "Allowed"], "default": "1" },

            { "category": "worldgen", "code": "worldClimate", "dataType": "dropdown", "values": ["realistic", "patchy"], "names": ["Realistic", "Patchy"], "default": "realistic", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "landcover", "dataType": "dropdown", "values": ["0", "0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "0.95", "0.975", "1"], "names": ["~0%", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "95%", "97.5%", "100%"], "default": "0.975", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "oceanscale", "dataType": "dropdown", "values": ["0.1", "0.25", "0.5", "0.75", "1", "1.25", "1.5", "1.75", "2", "3", "4", "5"], "names": ["10%", "25%", "50%", "75%", "100%", "125%", "150%", "175%", "200%", "300%", "400%", "500%"], "default": "5", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "upheavelCommonness", "dataType": "dropdown", "values": ["0", "0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1"], "names": ["0%", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%"], "default": "0.3", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "geologicActivity", "dataType": "dropdown", "values": ["0", "0.05", "0.1", "0.2", "0.4"], "names": ["None", "Rare", "Uncommon", "Common", "Very Common"], "default": "0.05", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "landformScale", "dataType": "dropdown", "values": ["0.2", "0.4", "0.6", "0.8", "1.0", "1.2", "1.4", "1.6", "1.8", "2", "3"], "names": ["20%", "40%", "60%", "80%", "100%", "120%", "140%", "160%", "180%", "200%", "300%"], "default": "1.0", "onlyDuringWorldCreate": true },


            { "category": "worldgen", "code": "worldWidth", "dataType": "dropdown", "values": ["8192000", "4096000", "2048000", "1024000", "600000", "512000", "384000", "256000", "102400", "51200", "25600", "10240", "5120", "1024", "512", "384", "256", "128", "64", "32" ], "names": ["8 mil blocks", "4 mil blocks", "2 mil blocks", "1 mil blocks", "600k blocks", "512k blocks", "384k blocks", "256k blocks", "102k blocks", "51k blocks", "25k blocks", "10k blocks", "5120 blocks", "1024 blocks", "512 blocks", "384 blocks", "256 blocks", "128 blocks", "64 blocks", "32 blocks"], "default": "1024000", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "worldLength", "dataType": "dropdown", "values": ["8192000", "4096000", "2048000", "1024000", "600000", "512000", "384000", "256000", "102400", "51200", "25600", "10240", "5120", "1024", "512", "384", "256", "128", "64", "32" ], "names": ["8 mil blocks", "4 mil blocks", "2 mil blocks", "1 mil blocks", "600k blocks", "512k blocks", "384k blocks", "256k blocks", "102k blocks", "51k blocks", "25k blocks", "10k blocks", "5120 blocks", "1024 blocks", "512 blocks", "384 blocks", "256 blocks", "128 blocks", "64 blocks", "32 blocks"], "default": "1024000", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "worldEdge", "dataType": "dropdown", "values": ["blocked", "traversable" ], "names": ["Blocked", "Traversable (Can fall down)"], "default": "traversable" },
            { "category": "worldgen", "code": "polarEquatorDistance", "dataType": "dropdown", "values": ["800000", "400000", "200000", "100000", "50000", "25000", "15000", "10000", "5000"], "names": ["800k blocks", "400k blocks", "200k blocks", "100k blocks", "50k blocks", "25k blocks", "15k blocks", "10k blocks", "5000 blocks"], "default": "50000", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "storyStructuresDistScaling", "dataType": "dropdown", "values": ["0.15", "0.25", "0.5", "0.75", "1", "1.5", "2", "3"], "names": ["15%", "25%", "50%", "75%", "100%", "150%", "200%", "300%"], "default": "1", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "globalTemperature", "dataType": "dropdown", "values": ["4", "2", "1.5", "1", "0.75", "0.5", "0.25"], "names": ["Scorching hot", "Very hot", "Hot", "Normal", "Cold", "Very Cold", "Snowball earth"], "default": "1", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "globalPrecipitation", "dataType": "dropdown", "values": ["4", "2", "1.5", "1", "0.5", "0.25", "0.1"], "names": ["Super humid", "Very humid", "Humid", "Normal", "Semi-Arid", "Arid", "Hyperarid"], "default": "1", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "globalForestation", "dataType": "dropdown", "values": ["1", "0.9", "0.75", "0.5", "0.25", "0", "-0.25", "-0.5", "-0.75", "-0.9", "-1"], "names": ["Forest World (+100%)", "Extremely forested (+90%)", "Very highly forested (+75%)", "Highly forested (+50%)", "Somewhat more forest (+25%)", "Normal", "Somewhat less forest (-25%)", "Significantly less forested (-50%)", "Much less forested (-75%)", "Near Tree-less (-90%)", "Tree-less World (-100%)"], "default": "0", "onlyDuringWorldCreate": true },
            { "category": "worldgen", "code": "globalDepositSpawnRate", "dataType": "dropdown", "values": ["3", "2", "1.8", "1.6", "1.4", "1.2", "1", "0.8", "0.6", "0.4", "0.2"], "names": ["300%", "200%", "180%", "160%", "140%", "120%", "100%", "80%", "60%", "40%", "20%"], "default": "1" },
            { "category": "worldgen", "code": "surfaceCopperDeposits", "dataType": "dropdown", "values": ["1", "0.5", "0.2", "0.12", "0.05", "0.015", "0"], "names": ["Very common", "Common", "Uncommon", "Rare", "Very Rare", "Extremly rare", "Never"], "default": "0.12" },
            { "category": "worldgen", "code": "surfaceTinDeposits", "dataType": "dropdown", "values": ["0.5", "0.25", "0.12", "0.03", "0.014", "0.007", "0"], "names": ["Very common", "Common", "Uncommon", "Rare", "Very Rare", "Extremly rare", "Never"], "default": "0.007" },
            { "category": "worldgen", "code": "snowAccum", "dataType": "dropdown", "values": ["true", "false"], "names": ["Enabled", "Disabled"], "default": "true" },



            { "category": "multiplayer", "code": "allowLandClaiming", "dataType": "bool", "default": "true" },
            { "category": "multiplayer", "code": "classExclusiveRecipes", "dataType": "bool", "default": "true" },
            { "category": "multiplayer", "code": "auctionHouse", "dataType": "bool", "default": "true" }
        ]
    }
    """
)]

[assembly: ModDependency("game")]
