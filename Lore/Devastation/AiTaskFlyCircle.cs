using System;
using System.Diagnostics;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public abstract class AiTaskTargetableAt : AiTaskBaseTargetable
{
    public Vec3d SpawnPos;
    public Vec3d CenterPos;

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
            SpawnPos = entity.ServerPos.XYZ;
            entity.WatchedAttributes.SetDouble("spawnPosX", SpawnPos.X);
            entity.WatchedAttributes.SetDouble("spawnPosY", SpawnPos.Y);
            entity.WatchedAttributes.SetDouble("spawnPosZ", SpawnPos.Z);
        }
    }
}

public class AiTaskFlyCircle : AiTaskTargetableAt
{
    protected bool stayNearSpawn;
    protected float minRadius;
    protected float maxRadius;
    protected float height;
    protected double desiredYPos;
    protected float moveSpeed = 0.04f;

    protected float direction = 1;
    protected float directionChangeCoolDown = 60;

    protected double desiredRadius;


    public AiTaskFlyCircle(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        stayNearSpawn = taskConfig["stayNearSpawn"].AsBool(false);
        minRadius = taskConfig["minRadius"].AsFloat(10f);
        maxRadius = taskConfig["maxRadius"].AsFloat(20f);
        height = taskConfig["height"].AsFloat(5f);
        moveSpeed = taskConfig["moveSpeed"].AsFloat(0.04f);
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

            CenterPos = entity.ServerPos.XYZ.Add(randomX, 0, randomZ);
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

        double dy = desiredYPos - entity.ServerPos.Y;
        double yMot = GameMath.Clamp(dy, -0.33, 0.33);

        double dx = entity.ServerPos.X - CenterPos.X;
        double dz = entity.ServerPos.Z - CenterPos.Z;
        double currentRadius = Math.Sqrt(dx * dx + dz * dz);
        double offs = desiredRadius - currentRadius;

        float targetYaw = (float)Math.Atan2(dx, dz) + GameMath.PIHALF + 0.1f * (float)direction;

        if (offs < -1) targetYaw += GameMath.Clamp((float)-offs / 13f, 0f, GameMath.PIHALF);
        if (offs > 1) targetYaw -= GameMath.Clamp((float)offs / 13f, 0f, GameMath.PIHALF);

        entity.ServerPos.Yaw = targetYaw;

        float bla = (float)GameMath.Clamp(offs / 20.0, -1, 1);
        double cosYaw = Math.Cos(entity.ServerPos.Yaw - bla);
        double sinYaw = Math.Sin(entity.ServerPos.Yaw - bla);
        entity.Controls.WalkVector.Set(sinYaw, yMot, cosYaw);
        entity.Controls.WalkVector.Mul(moveSpeed);
        if (yMot < 0) entity.Controls.WalkVector.Mul(0.5);


        if (entity.Swimming)
        {
            entity.Controls.WalkVector.Y = 2 * moveSpeed;
            entity.Controls.FlyVector.Y = 2 * moveSpeed;
        }

        double speed = entity.ServerPos.Motion.Length();
        if (speed > 0.01)
        {
            entity.ServerPos.Roll = (float)Math.Asin(GameMath.Clamp(-entity.ServerPos.Motion.Y / speed, -1, 1));
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
        int terrainYPos = ba.GetTerrainMapheightAt(entity.SidedPos.AsBlockPos);
        int tries = 10;
        int dim = BlockPos.DimensionBoundary * entity.SidedPos.Dimension;
        while (tries-- > 0)
        {
            Block block = entity.World.BlockAccessor.GetBlockRaw((int)entity.ServerPos.X, terrainYPos + dim,  (int)entity.ServerPos.Z, BlockLayersAccess.Fluid);

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
