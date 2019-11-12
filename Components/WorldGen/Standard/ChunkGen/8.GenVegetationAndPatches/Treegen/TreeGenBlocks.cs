using Newtonsoft.Json;
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

        public Block vinesBlock;
        public Block vinesEndBlock;
        public int logBlockId;
        public int otherLogBlockId;
        public int leavesBlockId;
        public int leavesBranchyBlockId;
        public int leavesBranchyDeadBlockId;

        public HashSet<int> blockIds = new HashSet<int>();


        public void ResolveBlockNames(ICoreServerAPI api)
        {
            int logBlockId = api.WorldManager.GetBlockId(logBlockCode);
            if (logBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + logBlockCode);
                logBlockId = 0;
            }
            this.logBlockId = logBlockId;


            if (otherLogBlockCode != null)
            {
                int otherLogBlockId = api.WorldManager.GetBlockId(otherLogBlockCode);
                if (otherLogBlockId == -1)
                {
                    api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + otherLogBlockCode);
                    otherLogBlockId = 0;
                }
                this.otherLogBlockId = otherLogBlockId;
            }


            int leavesBlockId = api.WorldManager.GetBlockId(leavesBlockCode);
            if (leavesBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + leavesBlockCode);
                leavesBlockId = 0;
            }
            this.leavesBlockId = leavesBlockId;


            int leavesBranchyBlockId = api.WorldManager.GetBlockId(leavesBranchyBlockCode);
            if (leavesBranchyBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + leavesBranchyBlockCode);
                leavesBranchyBlockId = 0;
            }
            this.leavesBranchyBlockId = leavesBranchyBlockId;

            int vinesBlockId = api.WorldManager.GetBlockId(vinesBlockCode);
            if (vinesBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + vinesBlockCode);
                vinesBlockId = 0;
            } else
            {
                this.vinesBlock = api.World.Blocks[vinesBlockId];
            }
            


            int vinesEndBlockId = api.WorldManager.GetBlockId(vinesEndBlockCode);
            if (vinesEndBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + vinesEndBlockCode);
                vinesEndBlockId = 0;
            } else
            {
                this.vinesEndBlock = api.World.Blocks[vinesEndBlockId];
            }

            blockIds.Add(leavesBlockId);
            blockIds.Add(leavesBranchyBlockId);
            blockIds.Add(logBlockId);
            blockIds.Add(otherLogBlockId);
        }
    }
}
