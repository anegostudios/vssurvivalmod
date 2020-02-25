using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{

    public class SystemRaccoonSpawnCondition : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            sapi.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
        }

        private bool Event_OnTrySpawnEntity(ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (!properties.Code.Path.StartsWithFast("raccoon")) return true;

            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[i];
                Vec3i dir = facing.Normali;

                Block block = sapi.World.BlockAccessor.GetBlock((int)spawnPosition.X + dir.X, (int)spawnPosition.Y, (int)spawnPosition.Z + dir.Z);
                if (block is BlockLog)
                {
                    return true;
                }

                block = sapi.World.BlockAccessor.GetBlock((int)spawnPosition.X + dir.X + dir.X, (int)spawnPosition.Y, (int)spawnPosition.Z + dir.Z + dir.Z);
                if (block is BlockLog)
                {
                    return true;
                }

                block = sapi.World.BlockAccessor.GetBlock((int)spawnPosition.X + dir.X + dir.X, (int)spawnPosition.Y + 1, (int)spawnPosition.Z + dir.Z + dir.Z);
                if (block is BlockLog)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
