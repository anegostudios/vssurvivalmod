using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public interface IHeatSource
    {
        float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos);
    }


    public class EntityBehaviorBodyTemperature : EntityBehavior
    {
        ITreeAttribute tempTree;
        ICoreAPI api;
        EntityAgent eagent;

        float accum;
        float slowaccum;
        float veryslowaccum;
        BlockPos plrpos = new BlockPos();

        bool inEnclosedRoom;
        
        float tempChange;
        float clothingBonus;

        float damagingFreezeHours;
        int sprinterCounter;

        double lastWearableHoursTotalUpdate;

        float bodyTemperatureResistance;

        ICachingBlockAccessor blockAccess;


        public float CurBodyTemperature
        {
            get { return tempTree.GetFloat("bodytemp"); }
            set { tempTree.SetFloat("bodytemp", value); entity.WatchedAttributes.MarkPathDirty("bodyTemp"); }
        }


        protected float nearHeatSourceStrength
        {
            get { return tempTree.GetFloat("nearHeatSourceStrength"); }
            set { tempTree.SetFloat("nearHeatSourceStrength", value); }
        }

        public float Wetness
        {
            get { return entity.WatchedAttributes.GetFloat("wetness"); }
            set { entity.WatchedAttributes.SetFloat("wetness", value); }
        }

        public double LastWetnessUpdateTotalHours
        {
            get { return entity.WatchedAttributes.GetDouble("lastWetnessUpdateTotalHours"); }
            set { entity.WatchedAttributes.SetDouble("lastWetnessUpdateTotalHours", value); }
        }

        public double BodyTempUpdateTotalHours
        {
            get { return tempTree.GetDouble("bodyTempUpdateTotalHours"); }
            set { tempTree.SetDouble("bodyTempUpdateTotalHours", value); entity.WatchedAttributes.MarkPathDirty("bodyTemp"); }
        }


        public float NormalBodyTemperature;

        public EntityBehaviorBodyTemperature(Entity entity) : base(entity)
        {
            eagent = entity as EntityAgent;
        }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            api = entity.World.Api;
            blockAccess = api.World.GetCachingBlockAccessor(false, false);

            tempTree = entity.WatchedAttributes.GetTreeAttribute("bodyTemp");

            NormalBodyTemperature = typeAttributes["defaultBodyTemperature"].AsFloat(37);

            if (tempTree == null)
            {
                entity.WatchedAttributes.SetAttribute("bodyTemp", tempTree = new TreeAttribute());

                CurBodyTemperature = NormalBodyTemperature + 4;

                // Run this every time a entity spawns so it doesnt freeze while unloaded / offline
                BodyTempUpdateTotalHours = api.World.Calendar.TotalHours;
                LastWetnessUpdateTotalHours = api.World.Calendar.TotalHours;

                return;
            }

            // Run this every time a entity spawns so it doesnt freeze while unloaded / offline
            BodyTempUpdateTotalHours = api.World.Calendar.TotalHours;
            LastWetnessUpdateTotalHours = api.World.Calendar.TotalHours;

            bodyTemperatureResistance = entity.World.Config.GetString("bodyTemperatureResistance").ToFloat(0);
        }

        public override void OnGameTick(float deltaTime)
        {
            accum += deltaTime;
            slowaccum += deltaTime;
            veryslowaccum += deltaTime;

            plrpos.Set((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);

            if (veryslowaccum > 10 && damagingFreezeHours > 3)
            {
                if (api.World.Config.GetString("harshWinters").ToBool(true))
                {
                    entity.ReceiveDamage(new DamageSource() { DamageTier = 0, Source = EnumDamageSource.Weather, Type = EnumDamageType.Frost }, 0.2f);
                }

                veryslowaccum = 0;

                if (eagent.Controls.Sprint)
                {
                    sprinterCounter = GameMath.Clamp(sprinterCounter + 1, 0, 10);
                } else
                {
                    sprinterCounter = GameMath.Clamp(sprinterCounter - 1, 0, 10);
                }
            }

            if (slowaccum > 3)
            {
                // No need to call this on the client, because we sync nearHeatSourceStrength
                if (api.World.Side == EnumAppSide.Server)
                {
                    Room room = api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(plrpos);
                    // Check whether it is a proper room, or something like a room i.e. with a roof, for exaample a natural cave
                    inEnclosedRoom = room.ExitCount == 0 || room.SkylightCount < room.NonSkylightCount;
                    nearHeatSourceStrength = 0;

                    double px = entity.Pos.X;
                    double py = entity.Pos.Y + 0.9;
                    double pz = entity.Pos.Z;

                    // Fire heat proximity effect (measured by straight-line shortest distance to fire, i.e. what the player sees visually)
                    // within 1 block from the edge of the fire block: full heat
                    // within 2 blocks from the edge of the fire block: ~66% heat
                    // within 3 blocks from the edge of the fire block: ~33% heat
                    // max range (3 blocks diagonally in both X and Z directions): ~12% heat

                    // similar consequences in a room, but slower falloff, still 33% heat at 6 blocks range

                    // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtaW4oMSw5Lyg4K3heMi41KSkiLCJjb2xvciI6IiMwMDAwMDAifSx7InR5cGUiOjAsImVxIjoibWluKDEsOS8oOCt4XjEuNzUpKSIsImNvbG9yIjoiIzAwMDAwMCJ9LHsidHlwZSI6MTAwMCwid2luZG93IjpbIjAiLCI5IiwiMCIsIjEuMiJdfV0-

                    double proximityPower = inEnclosedRoom ? 0.875 : 1.25;
                    BlockPos min, max;
                    if (inEnclosedRoom && room.Location.SizeX >= 1 && room.Location.SizeY >= 1 && room.Location.SizeZ >= 1)
                    {
                        min = new BlockPos(room.Location.MinX, room.Location.MinY, room.Location.MinZ);
                        max = new BlockPos(room.Location.MaxX, room.Location.MaxY, room.Location.MaxZ);
                    }
                    else
                    {
                        min = plrpos.AddCopy(-3, -3, -3);
                        max = plrpos.AddCopy(3, 3, 3);
                    }

                    blockAccess.Begin();
                    blockAccess.WalkBlocks(min, max, (block, pos) =>
                    {
                        BlockBehavior src;
                        if ((src = block.GetBehavior(typeof(IHeatSource), true)) != null)
                        {
                            float factor = Math.Min(1f, 9 / (8 + (float)Math.Pow(pos.DistanceSqToNearerEdge(px, py, pz), proximityPower)));
                            nearHeatSourceStrength += (src as IHeatSource).GetHeatStrength(api.World, pos, plrpos) * factor;
                        }
                    });
                }

                updateWearableConditions();
                entity.WatchedAttributes.MarkPathDirty("bodyTemp");
                slowaccum = 0;
            }

            if (accum > 1)
            {
                IPlayer plr = (entity as EntityPlayer)?.Player;

                if (entity.World.Side == EnumAppSide.Server && (plr as IServerPlayer)?.ConnectionState != EnumClientState.Playing) return;

                if ((plr?.WorldData.CurrentGameMode == EnumGameMode.Creative || plr?.WorldData.CurrentGameMode == EnumGameMode.Spectator))
                {
                    CurBodyTemperature = NormalBodyTemperature;
                    entity.WatchedAttributes.SetFloat("freezingEffectStrength", 0);
                    return;
                }

                if (plr.Entity.Controls.TriesToMove || plr.Entity.Controls.Jump || plr.Entity.Controls.LeftMouseDown || plr.Entity.Controls.RightMouseDown)
                {
                    lastMoveMs = entity.World.ElapsedMilliseconds;
                }

                ClimateCondition conds = api.World.BlockAccessor.GetClimateAt(plrpos, EnumGetClimateMode.NowValues);
                if (conds == null) return;
                Vec3d windspeed = api.World.BlockAccessor.GetWindSpeedAt(plrpos);

                bool rainExposed = api.World.BlockAccessor.GetRainMapHeightAt(plrpos) <= plrpos.Y;

                Wetness = GameMath.Clamp(
                    Wetness
                    + conds.Rainfall * (rainExposed ? 0.06f : 0) * (conds.Temperature < -1 ? 0.2f : 1) /* Get wet 5 times slower with snow */
                    + (entity.Swimming ? 1 : 0)
                    - (float)Math.Max(0, (api.World.Calendar.TotalHours - LastWetnessUpdateTotalHours) * GameMath.Clamp(nearHeatSourceStrength, 1, 2))
                , 0, 1);

                LastWetnessUpdateTotalHours = api.World.Calendar.TotalHours;
                accum = 0;

                float sprintBonus = sprinterCounter / 2f;
                float wetnessDebuff = (float)Math.Max(0, Wetness - 0.1) * 10f;

                // Can bear anything above 10 degrees without clothing, while standing still
                float hereTemperature = conds.Temperature + clothingBonus + sprintBonus - wetnessDebuff;

                float tempDiff = hereTemperature - GameMath.Clamp(hereTemperature, bodyTemperatureResistance, 30);
                // Above 10 degrees, slowly warms up
                if (tempDiff == 0) tempDiff = Math.Max((hereTemperature - bodyTemperatureResistance), 0);

                float ambientTempChange = GameMath.Clamp(tempDiff / 6f, -6, 6);

                tempChange = nearHeatSourceStrength + (inEnclosedRoom ? 1 : -(float)Math.Max((windspeed.Length() - 0.15) * 2, 0) + ambientTempChange);

                bool sleeping = entity.GetBehavior<EntityBehaviorTiredness>()?.IsSleeping == true;
                if (sleeping)
                {
                    if (inEnclosedRoom)
                    {
                        tempChange = GameMath.Clamp(NormalBodyTemperature - CurBodyTemperature, -0.15f, 0.15f);
                    } else if (!rainExposed)
                    {
                        tempChange += GameMath.Clamp(NormalBodyTemperature - CurBodyTemperature, 1f, 1f);
                    }
                }

                if (entity.IsOnFire) tempChange = Math.Max(25, tempChange);


                float tempUpdateHoursPassed = (float)(api.World.Calendar.TotalHours - BodyTempUpdateTotalHours);
                if (tempUpdateHoursPassed > 0.01)
                {
                    if (tempChange < -0.5 || tempChange > 0)
                    {
                        if (tempChange > 0.5) tempChange *= 2; // Warming up with a firepit is twice as fast, because nobody wants to wait forever
                        CurBodyTemperature = GameMath.Clamp(CurBodyTemperature + tempChange * tempUpdateHoursPassed, 31, 45);
                    }

                    BodyTempUpdateTotalHours = api.World.Calendar.TotalHours;

                    entity.WatchedAttributes.SetFloat("freezingEffectStrength", GameMath.Clamp((NormalBodyTemperature - CurBodyTemperature) / 4f - 0.5f, 0, 1));

                    if (NormalBodyTemperature - CurBodyTemperature > 4)
                    {
                        damagingFreezeHours += tempUpdateHoursPassed;
                    } else
                    {
                        damagingFreezeHours = 0;
                    }
                    
                }
            }
        }

        long lastMoveMs=0;


        /// <summary>
        /// Affect the players body temperature by eating meals with a larger temperature difference
        /// </summary>
        /// <param name="stack"></param>
        public void didConsume(ItemStack stack, float intensity = 1f)
        {
            float temp = stack.Collectible.GetTemperature(api.World, stack);
            if (Math.Abs(temp - CurBodyTemperature) > 10)
            {
                //float intensity = Math.Min(1, (temp - CurBodyTemperature) / 30f);
                //bh.CurBodyTemperature += 2 * servingsToEat * intensity;
            }
        }

        private void updateWearableConditions()
        {
            double hoursPassed = api.World.Calendar.TotalHours - lastWearableHoursTotalUpdate;

            if (hoursPassed < -1) // When someone turns back time via command
            {
                lastWearableHoursTotalUpdate = api.World.Calendar.TotalHours;
                return;
            }
            if (hoursPassed < 0.5f) return;


            EntityAgent eagent = entity as EntityAgent;

            clothingBonus = 0f;

            float conditionloss = 0f;

            bool isStandingStill = (entity.World.ElapsedMilliseconds - lastMoveMs) > 3000;

            // 1296 hours is half a default year
            if (!isStandingStill) conditionloss = -(float)hoursPassed / 1296f;

            IInventory gearWorn = eagent?.GearInventory;
            if (gearWorn != null)  //can be null when creating a new world and entering for the first time
            {
                foreach (var slot in gearWorn)
                {
                    ItemWearable wearableItem = slot.Itemstack?.Collectible as ItemWearable;

                    if (wearableItem == null || wearableItem.IsArmor) continue;

                    clothingBonus += wearableItem.GetWarmth(slot);

                    wearableItem.ChangeCondition(slot, conditionloss);
                }
            }

            lastWearableHoursTotalUpdate = api.World.Calendar.TotalHours;
        }


        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            base.OnEntityReceiveDamage(damageSource, damage);
        }

        public override void OnEntityRevive()
        {
            BodyTempUpdateTotalHours = api.World.Calendar.TotalHours;
            LastWetnessUpdateTotalHours = api.World.Calendar.TotalHours;
            Wetness = 0;
            CurBodyTemperature = NormalBodyTemperature + 4;
        }

        public override string PropertyName()
        {
            return "bodytemperature";
        }

    }
 
}
