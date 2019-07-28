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
    Version = GameVersion.NetworkVersion, // So that players with newer versions of the game can still connect
    Description = "The Vintage Story Survival experience. Contains all standard Blocks, Items, Creatures and pretty world generation",
    Authors = new[] { "Tyron" },
    RequiredOnClient = true,
    WorldConfig = @"
    {
	    playstyles: [
		    {
			    code: ""surviveandbuild"",
                langcode: ""surviveandbuild-bands"",
			    requestMods: [""game"", ""survival""],
                listOrder: 5,
			    worldType: ""standard"",
			    worldConfig: {
				    worldClimate: ""realistic"",
				    gameMode: ""survival"",
				    hoursPerDay: 48
			    }
		    },
		    {
			    code: ""surviveandbuild"",
                langcode: ""surviveandbuild-patchy"",
			    requestMods: [""game"", ""survival""],
                listOrder: 6,
			    worldType: ""standard"",
			    worldConfig: {
				    worldClimate: ""patchy"",
				    gameMode: ""survival"",
				    hoursPerDay: 48
			    }
		    }
	    ],
	    worldConfigAttributes: [	
	    ]
    }"
)]

[assembly: ModDependency("game")]
