using System;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.WorldEdit;

namespace Vintagestory.ServerMods
{
    public static class TreeToolRegisterUtil
    {
        public static void Register(ModSystem mod)
        {
            ((WorldEdit.WorldEdit)mod).RegisterTool("TreeGen", typeof(TreeGenTool));
        }
    }

    internal class TreeGenTool : ToolBase
    {
        private readonly LCGRandom _rand;
        private TreeGeneratorsUtil _treeGenerators;

        public float MinTreeSize
        {
            get { return workspace.FloatValues["std.treeToolMinTreeSize"]; }
            set { workspace.FloatValues["std.treeToolMinTreeSize"] = value; }
        }

        public float MaxTreeSize
        {
            get { return workspace.FloatValues["std.treeToolMaxTreeSize"]; }
            set { workspace.FloatValues["std.treeToolMaxTreeSize"] = value; }
        }

        public string TreeVariant
        {
            get { return workspace.StringValues["std.treeToolTreeVariant"]; }
            set { workspace.StringValues["std.treeToolTreeVariant"] = value; }
        }

        public int WithForestFloor
        {
            get { return workspace.IntValues["std.treeToolWithForestFloor"]; }
            set { workspace.IntValues["std.treeToolWithForestFloor"] = value; }
        }

        public float VinesGrowthChance
        {
            get { return workspace.FloatValues["std.treeToolVinesGrowthChance"]; }
            set { workspace.FloatValues["std.treeToolVinesGrowthChance"] = value; }
        }

        public TreeGenTool()
        {
        }

        public TreeGenTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            _rand = new LCGRandom();
            if (!workspace.FloatValues.ContainsKey("std.treeToolMinTreeSize")) MinTreeSize = 0.7f;
            if (!workspace.FloatValues.ContainsKey("std.treeToolMaxTreeSize")) MaxTreeSize = 1.3f;
            if (!workspace.StringValues.ContainsKey("std.treeToolTreeVariant")) TreeVariant = null;

            if (!workspace.FloatValues.ContainsKey("std.treeToolVinesGrowthChance")) VinesGrowthChance = 0;
            if (!workspace.IntValues.ContainsKey("std.treeToolWithForestFloor")) WithForestFloor = 0;
        }

        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }


        public override bool OnWorldEditCommand(WorldEdit.WorldEdit worldEdit, TextCommandCallingArgs callerArgs)
        {
            var player = (IServerPlayer)callerArgs.Caller.Player;
            var args = callerArgs.RawArgs;
            if (_treeGenerators == null)
            {
                _treeGenerators = new TreeGeneratorsUtil(worldEdit.sapi);
            }

            string cmd = args.PopWord();
            switch (cmd)
            {
                case "tsizemin":
                    {
                        float size = 0.7f;
                        if (args.Length > 0) float.TryParse(args[0], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out size);
                        MinTreeSize = size;

                        WorldEdit.WorldEdit.Good(player, "Tree Min Size=" + size + " set.");

                        return true;
                    }

                case "tsizemax":
                    {
                        float size = 0.7f;
                        if (args.Length > 0) float.TryParse(args[0], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out size);
                        MaxTreeSize = size;

                        WorldEdit.WorldEdit.Good(player, "Tree Max Size=" + size + " set.");

                        return true;
                    }

                case "tsize":
                    {
                        float min = 0.7f;
                        if (args.Length > 0) float.TryParse(args[0], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out min);
                        MinTreeSize = min;

                        float max = 1.3f;
                        if (args.Length > 1) float.TryParse(args[1], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out max);
                        MaxTreeSize = max;

                        WorldEdit.WorldEdit.Good(player, "Tree Min Size=" + min + ", max size =" + MaxTreeSize + " set.");

                        return true;
                    }

                case "trnd":
                    return true;

                case "tforestfloor":
                    var on = args.PopBool(false);
                    this.WithForestFloor = on==true ? 1 : 0;
                    WorldEdit.WorldEdit.Good(player, "Forest floor generation now {0}.", on == true ? "on": "off");
                    return true;

                case "tvines":
                    float chance = (float)args.PopFloat(0);
                    this.VinesGrowthChance = chance;
                    WorldEdit.WorldEdit.Good(player, "Vines growth chance now at {0}.", chance);
                    return true;


                case "tv":
                    int index;

                    string variant = args.PopWord();

                    bool numeric = int.TryParse(variant, NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out index);

                    _treeGenerators.ReloadTreeGenerators();

                    if (numeric)
                    {
                        var val = _treeGenerators.GetGenerator(index);
                        if (val.Key == null)
                        {
                            WorldEdit.WorldEdit.Bad(player, "No such tree variant found.");
                            return true;
                        }

                        TreeVariant = val.Key.ToShortString();
                        WorldEdit.WorldEdit.Good(player, "Tree variant " + val.Key + " set.");

                    } else
                    {
                        if (variant != null && _treeGenerators.GetGenerator(new AssetLocation(variant)) != null)
                        {
                            TreeVariant = variant;
                            WorldEdit.WorldEdit.Good(player, "Tree variant " + variant + " set.");
                        } else
                        {
                            WorldEdit.WorldEdit.Bad(player, "No such tree variant found.");
                        }
                    }

                    return true;
            }

            return false;
        }

        public override void OnBuild(WorldEdit.WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (_treeGenerators == null)
            {
                _treeGenerators = new TreeGeneratorsUtil(worldEdit.sapi);
            }
            _rand.SetWorldSeed(worldEdit.sapi.World.Seed);

            if (TreeVariant == null)
            {
                var player = (IServerPlayer)worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID);
                WorldEdit.WorldEdit.Bad(player, "Please select a tree variant first.");
                return;
            }

            worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
            blockSel.Position.Add(blockSel.Face.Opposite); // - prevented trees from growing o.O   - seems to work again and with this disabled trees float in the air 0.O

            ba.ReadFromStagedByDefault = true;

            _treeGenerators.ReloadTreeGenerators();
            var gen = _treeGenerators.GetGenerator(new AssetLocation(TreeVariant));

            var treeParams = new TreeGenParams()
            {
                skipForestFloor = WithForestFloor == 0,
                size = MinTreeSize + (float)_rand.NextDouble() * (MaxTreeSize - MinTreeSize),
                vinesGrowthChance = VinesGrowthChance
            };

            gen.GrowTree(ba, blockSel.Position, treeParams, _rand);

            ba.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, ba.GetStagedBlockId(blockSel.Position));
            ba.Commit();
        }
    }
}
