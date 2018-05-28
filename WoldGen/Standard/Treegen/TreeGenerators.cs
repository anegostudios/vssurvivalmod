using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    class TreeGeneratorsUtil
    {
        ICoreServerAPI sapi;

        public TreeGeneratorsUtil(ICoreServerAPI api)
        {
            sapi = api;
        }

        public void ReloadTreeGenerators()
        {
            int quantity = sapi.Assets.Reload(new AssetLocation("worldgen/tree"));
            sapi.Server.LogNotification("{0} tree generators reloaded", quantity);

            LoadTreeGenerators();
        }

        public void LoadTreeGenerators()
        {
            Dictionary<AssetLocation, TreeGenConfig> TreeGenModelsByTree = sapi.Assets.GetMany<TreeGenConfig>(sapi.Server.Logger, "worldgen/tree");
            
            string names = "";
            foreach (var val in TreeGenModelsByTree)
            {
                AssetLocation name = val.Key.Clone();

                if (names.Length > 0)
                {
                    names += ", ";
                }
                    
                names += name;

                name.Path = val.Key.Path.Substring("worldgen/tree/".Length);
                name.RemoveEnding();

                val.Value.Init(val.Key, sapi.Server.Logger);

                sapi.RegisterTreeGenerator(name, new TreeGen(val.Value, sapi.WorldManager.Seed));
                val.Value.treeBlocks.ResolveBlockNames(sapi);
            }


            sapi.Server.LogNotification("Reloaded tree generators " + names);
        }

        public ITreeGenerator GetGenerator(AssetLocation generatorCode)
        {
            ITreeGenerator gen = null;
            sapi.World.TreeGenerators.TryGetValue(generatorCode, out gen);
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

        public void RunGenerator(AssetLocation treeName, IBlockAccessor api, BlockPos pos, float size = 1)
        {
            sapi.World.TreeGenerators[treeName].GrowTree(api, pos, size);
        }
    }
}
