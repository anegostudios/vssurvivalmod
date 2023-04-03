using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityBaseReturnTeleporter : BlockEntity
    {

    }

    public class BlockBaseReturnTeleporter : Block
    {

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api is ICoreServerAPI sapi)
            {
                var plr = byPlayer as IServerPlayer;
                var pos = plr.GetSpawnPosition(false);
                plr.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
            }

            return true;
        }
    }
}
