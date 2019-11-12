using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vintagestory.API;
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
    Description = "The Vintage Story Survival experience. Contains all standard Blocks, Items, Creatures and pretty world generation",
    Authors = new[] { "Tyron" },
    RequiredOnClient = true,
    WorldConfig = @"
    {
	    playstyles: [
		    {
			    code: ""surviveandbuild"",
                langcode: ""preset-surviveandbuild"",
			    requestMods: [""game"", ""survival""],
                listOrder: 5,
			    worldType: ""standard"",
			    worldConfig: {
				    worldClimate: ""realistic"",
				    gameMode: ""survival"",
                    saplingGrowthDays: ""8""
			    }
		    },
		    {
			    code: ""exploration"",
                langcode: ""preset-exploration"",
			    requestMods: [""game"", ""survival""],
                listOrder: 6,
			    worldType: ""standard"",
			    worldConfig: {
				    worldClimate: ""realistic"",
				    gameMode: ""survival"",
                    microblockChiseling: ""true"",
                    deathPunishment: ""keep"",
                    creatureHostility: ""passive"",
                    playerHealthPoints: ""20"",
                    playerHungerSpeed: ""0.5"",
                    foodSpoilSpeed: ""0.5"",
                    toolDurability: ""2"",
                    saplingGrowthDays: ""5""
                }
			},
            {
                code: ""wildernesssurvival"",
                langcode: ""preset-wildernesssurvival"",
			    requestMods: [""game"", ""survival""],
                listOrder: 7,
			    worldType: ""standard"",
			    worldConfig: {
				    worldClimate: ""realistic"",
				    gameMode: ""survival"",
                    microblockChiseling: ""false"",
                    deathPunishment: ""drop"",
                    blockGravity: ""sandgravelsoil"",
                    creatureHostility: ""aggressive"",
                    playerHealthPoints: ""10"",
                    creatureStrength: ""1.5"",
                    playerHungerSpeed: ""1.25"",
                    foodSpoilSpeed: ""1.25"",
                    graceTimer: ""0"",
                    allowCoordinateHud: ""false"",
                    allowMap: ""false"",
                    surfaceCopperDeposits: ""0.015"",
                    surfaceTinDeposits: ""0"",
                    saplingGrowthDays: ""24""
			    }
		    }
	    ],
	    worldConfigAttributes: [
            { code: ""gameMode"", dataType: ""dropdown"", values: [""survival"", ""creative""], names: [""Survival"", ""Creative""], default: ""survival"" },
            { code: ""worldClimate"", dataType: ""dropdown"", values: [""realistic"", ""patchy""], names: [""Realistic"", ""Patchy""], default: ""realistic"" },

            { code: ""globalTemperature"", dataType: ""dropdown"", values: [""4"", ""2"", ""1.5"", ""1"", ""0.5"", ""0.25"", ""0.15""], names: [""Scorching hot"", ""Very hot"", ""Hot"", ""Normal"", ""Cold"", ""Very Cold"", ""Snowball earth""], default: ""1"" },
            { code: ""globalPrecipitation"", dataType: ""dropdown"", values: [""4"", ""2"", ""1.5"", ""1"", ""0.5"", ""0.25"", ""0.1""], names: [""Super humid"", ""Very humid"", ""Humid"", ""Normal"", ""Semi-Arid"", ""Arid"", ""Hyperarid""], default: ""1"" },
            
            { code: ""microblockChiseling"", dataType: ""bool"", default: ""false"" },
            { code: ""deathPunishment"", dataType: ""dropdown"", values: [""drop"", ""keep""], names: [""Drop inventory contents"", ""Keep inventory contents""], default: ""drop"" },
            { code: ""graceTimer"", dataType: ""dropdown"", values: [""10"", ""5"", ""4"", ""3"", ""2"", ""1"", ""0""], names: [""10 days before monsters appear"", ""5 days before monsters appear"", ""4 days before monsters appear"", ""3 days before monsters appear"", ""2 days before monsters appear"", ""1 day before monsters appear"", ""No timer. Monsters spawn right away.""], default: ""5"" },
            { code: ""creatureHostility"", dataType: ""dropdown"", values: [""aggressive"", ""passive"", ""off""], names: [""Aggressive"", ""Passive"", ""Never hostile""], default: ""aggressive"" },
            { code: ""creatureStrength"", dataType: ""dropdown"", values: [""4"", ""2"", ""1.5"", ""1"", ""0.5"", ""0.25""], names: [""Deadly (400%)"", ""Very Strong (200%)"", ""Strong (150%)"", ""Normal (100%)"", ""Weak (50%)"", ""Very weak (25%)""], default: ""1"" },
            { code: ""playerHealthPoints"", dataType: ""dropdown"", values: [""5"", ""10"", ""15"", ""20"", ""25"", ""30"", ""35""], names: [""5 hp"", ""10 hp"", ""15 hp"", ""20 hp"", ""25 hp"", ""30 hp"", ""35 hp""], default: ""15"" },
            { code: ""playerHungerSpeed"", dataType: ""dropdown"", values: [""2"", ""1.5"", ""1.25"", ""1"", ""0.75"", ""0.5"", ""0.25""], names: [""Very fast"", ""Fast"", ""Slightly faster"", ""Normal"", ""Slightly slower"", ""Slower"", ""Much slower""], default: ""1"" },

            { code: ""blockGravity"", dataType: ""dropdown"", values: [""sandgravel"", ""sandgravelsoil""], names: [""Sand and gravel"", ""Sand, gravel and soil""], default: ""sandgravel"" },

            { code: ""foodSpoilSpeed"", dataType: ""dropdown"", values: [""4"", ""3"", ""2"", ""1.5"", ""1.25"", ""1"", ""0.75"", ""0.5"", ""0.25""], names: [""400%"", ""300%"", ""200%"", ""150%"", ""125%"", ""100%"", ""75%"", ""50%"", ""25%""], default: ""1"" },
            { code: ""saplingGrowthDays"", dataType: ""dropdown"", values: [""1.5"", ""3"", ""5"", ""6.5"", ""8"", ""10"", ""12"", ""24"", ""48"", ""96""], names: [""1.5 days"", ""3 days"", ""5 days"", ""6.5 days"", ""8 days"", ""10 days"", ""12 days"", ""24 days"", ""48 days"", ""96 days""], default: ""8"" },
            { code: ""toolDurability"", dataType: ""dropdown"", values: [""4"", ""3"", ""2"", ""1.5"", ""1.25"", ""1"", ""0.75"", ""0.5""], names: [""400%"", ""300%"", ""200%"", ""150%"", ""125%"", ""100%"", ""75%"", ""50%""], default: ""1"" },
            { code: ""toolMiningSpeed"", dataType: ""dropdown"", values: [""3"", ""2"", ""1.5"", ""1.25"", ""1"", ""0.75"", ""0.5"", ""0.25""], names: [""300%"", ""200%"", ""150%"", ""125%"", ""100%"", ""75%"", ""50%"", ""25%""], default: ""1"" },

            { code: ""allowCoordinateHud"", dataType: ""bool"", default: ""true"" },
            { code: ""allowMap"", dataType: ""bool"", default: ""true"" },

            { code: ""surfaceCopperDeposits"", dataType: ""dropdown"", values: [""1"", ""0.5"", ""0.2"", ""0.1"", ""0.05"", ""0.015"", ""0""], names: [""Very common"", ""Common"", ""Uncommon"", ""Rare"", ""Very Rare"", ""Extremly rare"", ""Never""], default: ""0.2"" },
            { code: ""surfaceTinDeposits"", dataType: ""dropdown"", values: [""0.5"", ""0.25"", ""0.12"", ""0.03"", ""0.014"", ""0.007"", ""0""], names: [""Very common"", ""Common"", ""Uncommon"", ""Rare"", ""Very Rare"", ""Extremly rare"", ""Never""], default: ""0.007"" },

	    ]
    }"
)]

[assembly: ModDependency("game")]
