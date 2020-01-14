using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TreeGenTree
    {
        public ushort vinesBlockId;

        public ushort logBlockId;
        public ushort leavesBlockId;
        public ushort leavesBranchyBlockId;

        [JsonProperty]
        public EnumTreeGenMode mode = EnumTreeGenMode.NORMAL;
        [JsonProperty]
        AssetLocation logBlockCode = null;
        [JsonProperty]
        AssetLocation leavesBlockCode = null;
        [JsonProperty]
        AssetLocation leavesBranchyBlockCode = null;

        public void ResolveBlockNames(ICoreServerAPI api)
        {
            int logBlockId = api.WorldManager.GetBlockId(logBlockCode);
            if (logBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + logBlockCode);
                logBlockId = 0;
            }
            this.logBlockId = (ushort)logBlockId;



            int leavesBlockId = api.WorldManager.GetBlockId(leavesBlockCode);
            if (leavesBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + leavesBlockCode);
                leavesBlockId = 0;
            }
            this.leavesBlockId = (ushort)leavesBlockId;


            int leavesBranchyBlockId = api.WorldManager.GetBlockId(leavesBranchyBlockCode);
            if (leavesBranchyBlockId == -1)
            {
                api.Server.LogWarning("Tree gen tree: No block found with the blockcode " + leavesBranchyBlockCode);
                leavesBranchyBlockId = 0;
            }
            this.leavesBranchyBlockId = (ushort)leavesBranchyBlockId;
        }
    }
}
