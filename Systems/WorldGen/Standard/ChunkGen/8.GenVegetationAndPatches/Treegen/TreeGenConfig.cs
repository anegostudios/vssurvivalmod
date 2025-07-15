﻿using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods.NoObf
{
    public class TreeGenConfig
    {
        [JsonProperty]
        public int yOffset;
        [JsonProperty]
        public float sizeMultiplier;
        [JsonProperty]
        public NatFloat sizeVar = NatFloat.Zero;
        [JsonProperty]
        public float heightMultiplier;
        [JsonProperty]
        public TreeGenTrunk[] trunks;
        [JsonProperty]
        public TreeGenBranch[] branches;
        [JsonProperty]
        public TreeGenBlocks treeBlocks = null;

        public EnumTreeType Treetype;

        internal void Init(AssetLocation location, ILogger logger)
        {
            if (trunks == null) trunks = System.Array.Empty<TreeGenTrunk>();
            if (branches == null) branches = System.Array.Empty<TreeGenBranch>();

            for (int i = 1; i < trunks.Length; i++)
            {
                if (trunks[i].inherit != null)
                {
                    Inheritance inherit = trunks[i].inherit;
                    if (inherit.from >= i || inherit.from < 0)
                    {
                        logger.Warning("Inheritance value out of bounds in trunk element " + i + " in " + location + ". Skipping.");
                        continue;
                    }

                    trunks[i].InheritFrom(trunks[inherit.from], inherit.skip);
                }
            }

            for (int i = 1; i < branches.Length; i++)
            {
                if (branches[i].inherit != null)
                {
                    Inheritance inherit = branches[i].inherit;
                    if (inherit.from >= i || inherit.from < 0)
                    {
                        logger.Warning("Inheritance value out of bounds in branch element " + i + " in " + location + ". Skipping.");
                        continue;
                    }

                    branches[i].InheritFrom(branches[inherit.from], inherit.skip);
                }
            }
        }

    }
}
