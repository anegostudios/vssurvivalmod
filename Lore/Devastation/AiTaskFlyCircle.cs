using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public abstract class AiTaskTargetableAt : AiTaskBaseTargetable
{
    public Vec3d SpawnPos = null!;
    public Vec3d CenterPos = null!;

    protected AiTaskTargetableAt(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
    }

    public override void OnEntityLoaded()
    {
        LoadOrCreateSpawnPos();
    }

    public override void OnEntitySpawn()
    {
        LoadOrCreateSpawnPos();
    }

    public void LoadOrCreateSpawnPos()
    {
        if (entity.WatchedAttributes.HasAttribute("spawnPosX"))
        {
            SpawnPos = new Vec3d(
                entity.WatchedAttributes.GetDouble("spawnPosX"),
                entity.WatchedAttributes.GetDouble("spawnPosY"),
                entity.WatchedAttributes.GetDouble("spawnPosZ")
            );
        }
        else
        {
            SpawnPos = entity.Pos.XYZ;
            entity.WatchedAttributes.SetDouble("spawnPosX", SpawnPos.X);
            entity.WatchedAttributes.SetDouble("spawnPosY", SpawnPos.Y);
            entity.WatchedAttributes.SetDouble("spawnPosZ", SpawnPos.Z);
        }
    }
}

public class AiTaskFlyCircle : AiTaskTargetableAt
{
    [JsonProperty]
    protected bool stayNearSpawn = false;
    [JsonProperty]
    protected float minRadius = 10f;
    [JsonProperty]
    protected float maxRadius = 20f;
    [JsonProperty]
    protected float height = 5f;
    protected double desiredYPos;
    [JsonProperty]
    protected float moveSpeed = 0.04f;

    protected float direction = 1;
    protected float directionChangeCoolDown = 60;

    protected double desiredRadius;


    public AiTaskFlyCircle(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        direction = taskConfig["direction"].AsString("left") == "left" ? 1 : -1;
    }

    public override bool ShouldExecute() { return true; }


    public override void StartExecute()
    {
        desiredRadius = minRadius + (float)world.Rand.NextDouble() * (maxRadius - minRadius);

        if (stayNearSpawn)
        {
            CenterPos = SpawnPos;
        }
        else
        {
            float randomYaw = (float)world.Rand.NextDouble() * GameMath.TWOPI;
            double randomX = desiredRadius * Math.Sin(randomYaw);
            double randomZ = desiredRadius * Math.Cos(randomYaw);

            CenterPos = entity.Pos.XYZ.Add(randomX, 0, randomZ);
        }

        base.StartExecute();
    }

    public override bool ContinueExecute(float dt)
    {
        //Check if time is still valid for task.
        if (!IsInValidDayTimeHours(false)) return false;

        if ((int)CenterPos.Y / BlockPos.DimensionBoundary != entity.Pos.Dimension) return false;

        if (entity.OnGround || entity.World.Rand.NextDouble() < 0.03)
        {
            UpdateFlyHeight();
        }

        double dy = desiredYPos - entity.Pos.Y;
        double yMot = GameMath.Clamp(dy, -0.33, 0.33);

        double dx = entity.Pos.X - CenterPos.X;
        double dz = entity.Pos.Z - CenterPos.Z;
        double currentRadius = Math.Sqrt(dx * dx + dz * dz);
        double offs = desiredRadius - currentRadius;

        float targetYaw = (float)Math.Atan2(dx, dz) + GameMath.PIHALF + 0.1f * (float)direction;

        if (offs < -1) targetYaw += GameMath.Clamp((float)-offs / 13f, 0f, GameMath.PIHALF);
        if (offs > 1) targetYaw -= GameMath.Clamp((float)offs / 13f, 0f, GameMath.PIHALF);

        entity.Pos.Yaw = targetYaw;

        float bla = (float)GameMath.Clamp(offs / 20.0, -1, 1);
        double cosYaw = Math.Cos(entity.Pos.Yaw - bla);
        double sinYaw = Math.Sin(entity.Pos.Yaw - bla);
        entity.Controls.WalkVector.Set(sinYaw, yMot, cosYaw);
        entity.Controls.WalkVector.Mul(moveSpeed);
        if (yMot < 0) entity.Controls.WalkVector.Mul(0.5);


        if (entity.Swimming)
        {
            entity.Controls.WalkVector.Y = 2 * moveSpeed;
            entity.Controls.FlyVector.Y = 2 * moveSpeed;
        }

        double speed = entity.Pos.Motion.Length();
        if (speed > 0.01)
        {
            entity.Pos.Roll = (float)Math.Asin(GameMath.Clamp(-entity.Pos.Motion.Y / speed, -1, 1));
        }

        directionChangeCoolDown = Math.Max(0, directionChangeCoolDown - dt);
        if (entity.CollidedHorizontally && directionChangeCoolDown <= 0)
        {
            directionChangeCoolDown = 2;
            direction *= -1;
        }

        return entity.Alive;
    }

    protected void UpdateFlyHeight()
    {
        var ba = entity.World.BlockAccessor;
        int terrainYPos = ba.GetTerrainMapheightAt(entity.Pos.AsBlockPos);
        int tries = 10;
        int dim = BlockPos.DimensionBoundary * entity.Pos.Dimension;
        while (tries-- > 0)
        {
            Block block = entity.World.BlockAccessor.GetBlockRaw((int)entity.Pos.X, terrainYPos + dim,  (int)entity.Pos.Z, BlockLayersAccess.Fluid);

            if (block.IsLiquid())
            {
                terrainYPos++;
            }
            else
            {
                break;
            }
        }

        desiredYPos = terrainYPos + height;
    }
}
