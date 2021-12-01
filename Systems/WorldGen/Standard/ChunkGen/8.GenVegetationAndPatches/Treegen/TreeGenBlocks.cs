using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TreeGenBlocks
    {
        [JsonProperty]
        public AssetLocation logBlockCode = null;

        [JsonProperty]
        public AssetLocation otherLogBlockCode = null;
        [JsonProperty]
        public double otherLogChance = 0.01;

        [JsonProperty]
        public AssetLocation leavesBlockCode = null;
        [JsonProperty]
        public AssetLocation leavesBranchyBlockCode = null;
        [JsonProperty]
        public AssetLocation vinesBlockCode = null;
        [JsonProperty]
        public AssetLocation vinesEndBlockCode = null;
        [JsonProperty]
        public string trunkSegmentBase = null;
        [JsonProperty]
        public string[] trunkSegmentVariants = null;
        [JsonProperty]
        public int leavesLevels = 0;

        public Block vinesBlock;
        public Block vinesEndBlock;
        public int logBlockId;
        public int otherLogBlockId;
        public int leavesBlockId;
        public int leavesBranchyBlockId;
        public int leavesBranchyDeadBlockId;
        public int[] trunkSegmentBlockIds;

        private float leafLevelFactor = 5f;
        private int[] leavesByLevel = new int[2];

        public HashSet<int> blockIds = new HashSet<int>();

        public void ResolveBlockNames(ICoreServerAPI api, string treeName)
        {
            int logBlockId = api.WorldManager.GetBlockId(logBlockCode);
            if (logBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree " + treeName + ": No block found with the blockcode " + logBlockCode);
                logBlockId = 0;
            }
            this.logBlockId = logBlockId;


            if (otherLogBlockCode != null)
            {
                int otherLogBlockId = api.WorldManager.GetBlockId(otherLogBlockCode);
                if (otherLogBlockId == -1)
                {
                    api.Server.LogWarning("Tree gen tree " + treeName + ": No block found with the blockcode " + otherLogBlockCode);
                    otherLogBlockId = 0;
                }
                this.otherLogBlockId = otherLogBlockId;
            }


            int leavesBlockId = api.WorldManager.GetBlockId(leavesBlockCode);
            if (leavesBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree " + treeName + ": No block found with the blockcode " + leavesBlockCode);
                leavesBlockId = 0;
            }
            this.leavesBlockId = leavesBlockId;


            int leavesBranchyBlockId = api.WorldManager.GetBlockId(leavesBranchyBlockCode);
            if (leavesBranchyBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree " + treeName + ": No block found with the blockcode " + leavesBranchyBlockCode);
                leavesBranchyBlockId = 0;
            }
            this.leavesBranchyBlockId = leavesBranchyBlockId;

            if (vinesBlockCode != null)
            {
                int vinesBlockId = api.WorldManager.GetBlockId(vinesBlockCode);
                if (vinesBlockId == -1)
                {
                    api.Server.LogWarning("Tree gen tree " + treeName + ": No block found with the blockcode " + vinesBlockCode);
                    vinesBlockId = 0;
                } else
                {
                    this.vinesBlock = api.World.Blocks[vinesBlockId];
                }
            }

            if (vinesEndBlockCode != null)
            {
                int vinesEndBlockId = api.WorldManager.GetBlockId(vinesEndBlockCode);
                if (vinesEndBlockId == -1)
                {
                    api.Server.LogWarning("Tree gen tree " + treeName + ": No block found with the blockcode " + vinesEndBlockCode);
                    vinesEndBlockId = 0;
                } else
                {
                    this.vinesEndBlock = api.World.Blocks[vinesEndBlockId];
                }
            }

            if (trunkSegmentVariants != null && trunkSegmentVariants.Length > 0 && trunkSegmentBase != null)
            {
                trunkSegmentBlockIds = new int[trunkSegmentVariants.Length];
                for (int i = 0; i < trunkSegmentVariants.Length; i++)
                {
                    string blockCode = trunkSegmentBase + trunkSegmentVariants[i] + "-ud";
                    trunkSegmentBlockIds[i] = api.WorldManager.GetBlockId(new AssetLocation(blockCode));
                    blockIds.Add(trunkSegmentBlockIds[i]);
                }
            }

            if (leavesLevels == 0)
            {
                leavesByLevel[0] = leavesBlockId;
                leavesByLevel[1] = leavesBranchyBlockId;
                blockIds.Add(leavesBlockId);
                blockIds.Add(leavesBranchyBlockId);
            }
            else
            {
                leavesByLevel = new int[leavesLevels];
                Block baseBlock = api.World.Blocks[leavesBlockId];
                for (int i = 0; i < leavesLevels; i++)
                {
                    leavesByLevel[i] = api.WorldManager.GetBlockId(baseBlock.CodeWithParts((i + 1).ToString()));
                    blockIds.Add(leavesByLevel[i]);
                }
                leafLevelFactor = (leavesLevels - 0.5f) / 0.3f;
            }

            blockIds.Add(logBlockId);
            if (otherLogBlockId != 0) blockIds.Add(otherLogBlockId);
        }

        public int GetLeaves(float width)
        {
            return leavesByLevel[Math.Min(leavesByLevel.Length - 1, (int)(width * leafLevelFactor + 0.5f))];
        }
    }
}
