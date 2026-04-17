using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
            /*if (properties.Code.Path.StartsWithFast("raccoon"))
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
                if (climate == null || climate.Temperature < 10) return false;
            }*/

            bool harshWinters = sapi.World.Config.GetString("harshWinters").ToBool(true);
            var hemi = sapi.World.Calendar.OnGetHemisphere(spawnPosition.X, spawnPosition.Z);
            var month = sapi.World.Calendar.MonthName;
            if (hemi == EnumHemisphere.South) month += 6;
            if (harshWinters && (month == EnumMonth.December || month == EnumMonth.January || month == EnumMonth.February))
            {
                float spawnRate = properties.Attributes?["harshWinterSpawnRate"].AsFloat() ?? 1;
                return sapi.World.Rand.NextDouble() < spawnRate;
            }            

            float newWorldSpawnDelayHours = properties.Attributes?["newWorldSpawnDelayHours"].AsFloat() ?? 0;
            if (sapi.World.Calendar.ElapsedHours < newWorldSpawnDelayHours)
            {
                return false;
            }

            return true;
        }
    }
}
