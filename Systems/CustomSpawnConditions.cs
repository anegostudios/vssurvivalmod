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

    public class CustomSpawnConditions : ModSystem
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

        private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (properties.Code.Path.StartsWithFast("raccoon"))
            {
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing facing = BlockFacing.HORIZONTALS[i];
                    Vec3i dir = facing.Normali;

                    Block block = blockAccessor.GetBlock((int)spawnPosition.X + dir.X, (int)spawnPosition.Y, (int)spawnPosition.Z + dir.Z);
                    if (block is BlockLog)
                    {
                        return true;
                    }

                    block = blockAccessor.GetBlock((int)spawnPosition.X + dir.X + dir.X, (int)spawnPosition.Y, (int)spawnPosition.Z + dir.Z + dir.Z);
                    if (block is BlockLog)
                    {
                        return true;
                    }

                    block = blockAccessor.GetBlock((int)spawnPosition.X + dir.X + dir.X, (int)spawnPosition.Y + 1, (int)spawnPosition.Z + dir.Z + dir.Z);
                    if (block is BlockLog)
                    {
                        return true;
                    }
                }


                return false;
            }


            if (properties.Code.Path.StartsWithFast("butterfly"))
            {
                ClimateCondition climate = blockAccessor.GetClimateAt(new BlockPos((int)spawnPosition.X, (int)spawnPosition.Y, (int)spawnPosition.Z), EnumGetClimateMode.NowValues);
                if (climate.Temperature < 10) return false;
            }

            if (properties.Code.Path.StartsWithFast("sheep") || properties.Code.Path.StartsWithFast("boar"))
            {
                bool harshWinters = sapi.World.Config.GetString("harshWinters").ToBool(true);
                var month = sapi.World.Calendar.MonthName;
                if (harshWinters && (month == EnumMonth.December || month == EnumMonth.January || month == EnumMonth.February)) return false;
            }

            // Don't spawn bears in the first 4 days
            if (properties.Code.Path.StartsWithFast("bear") && sapi.World.Calendar.ElapsedDays < 4)
            {
                return false;
            }

            // Don't spawn wolves in the first 4 hours
            if (properties.Code.Path.StartsWithFast("wolf") && sapi.World.Calendar.ElapsedHours < 4)
            {
                return false;
            }


            return true;
        }
    }
}
