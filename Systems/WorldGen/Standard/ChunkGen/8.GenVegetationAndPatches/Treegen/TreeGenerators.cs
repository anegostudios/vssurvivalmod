﻿using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.NoObf;

#nullable disable

namespace Vintagestory.ServerMods
{

    public class WoodWorldProperty : WorldProperty<WorldWoodPropertyVariant>
    {
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class WorldWoodPropertyVariant
    {
        [JsonProperty]
        public AssetLocation Code;

        [JsonProperty]
        public EnumTreeType TreeType;
    }

    class TreeGeneratorsUtil
    {
        ICoreServerAPI sapi;
        public ForestFloorSystem forestFloorSystem;

        public TreeGeneratorsUtil(ICoreServerAPI api)
        {
            sapi = api;
            forestFloorSystem = new ForestFloorSystem(api);
        }

        public void ReloadTreeGenerators()
        {
            int quantity = sapi.Assets.Reload(new AssetLocation("worldgen/treegen"));
            sapi.Server.LogNotification("{0} tree generators reloaded", quantity);

            LoadTreeGenerators();
        }

        public void LoadTreeGenerators()
        {
            var treeGenModelsByTree = sapi.Assets.GetMany<TreeGenConfig>(sapi.Server.Logger, "worldgen/treegen");

            var worldprops = sapi.Assets.Get<WoodWorldProperty>(new AssetLocation("worldproperties/block/wood.json"));
            Dictionary<string, EnumTreeType> treetypes = new Dictionary<string, EnumTreeType>();
            foreach (var val in worldprops.Variants)
            {
                treetypes[val.Code.Path] = val.TreeType;
            }

            bool potatoeMode = sapi.World.Config.GetAsString("potatoeMode", "false").ToBool() == true;

            string names = "";
            foreach (var val in treeGenModelsByTree)
            {
                AssetLocation name = val.Key.Clone();

                if (names.Length > 0)
                {
                    names += ", ";
                }

                names += name;

                name.Path = val.Key.Path.Substring("worldgen/treegen/".Length);
                name.RemoveEnding();

                if (potatoeMode)
                {
                    val.Value.treeBlocks.mossDecorCode = null;
                }
                val.Value.Init(val.Key, sapi.Server.Logger);


                sapi.RegisterTreeGenerator(name, new TreeGen(val.Value, sapi.WorldManager.Seed, forestFloorSystem));
                val.Value.treeBlocks.ResolveBlockNames(sapi, name.Path);

                treetypes.TryGetValue(sapi.World.GetBlock(val.Value.treeBlocks.logBlockId).Variant["wood"], out val.Value.Treetype);
            }


            sapi.Server.LogNotification("Reloaded {0} tree generators", treeGenModelsByTree.Count);

        }

        public ITreeGenerator GetGenerator(AssetLocation generatorCode)
        {
            sapi.World.TreeGenerators.TryGetValue(generatorCode, out ITreeGenerator gen);
            return gen;
        }

        public KeyValuePair<AssetLocation, ITreeGenerator> GetGenerator(int index)
        {
            AssetLocation key = sapi.World.TreeGenerators.GetKeyAtIndex(index);
            if (key != null)
            {
                return new KeyValuePair<AssetLocation, ITreeGenerator>(key, sapi.World.TreeGenerators[key]);
            }
            return new KeyValuePair<AssetLocation, ITreeGenerator>(null, null);
        }

        public void RunGenerator(AssetLocation treeName, IBlockAccessor api, BlockPos pos, TreeGenParams treeGenParams)
        {
            sapi.World.TreeGenerators[treeName].GrowTree(api, pos, treeGenParams, new NormalRandom());
        }
    }
}
