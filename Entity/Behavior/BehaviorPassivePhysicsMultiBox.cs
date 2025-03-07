using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace Vintagestory.API.Common;

public class EntityBehaviorPassivePhysicsMultiBox : EntityBehaviorPassivePhysics, IRenderer
{
    protected Cuboidf[] OrigCollisionBoxes;
    protected Cuboidf[] CollisionBoxes;
    WireframeCube entityWf;

    [ThreadStatic]
    protected internal static MultiCollisionTester mcollisionTester;

    public EntityBehaviorPassivePhysicsMultiBox(Entity entity) : base(entity)
    {
        mcollisionTester ??= new MultiCollisionTester();   // Required on clientside
    }

    public static void InitServer(ICoreServerAPI sapi)
    {
        mcollisionTester = new MultiCollisionTester();
        sapi.Event.PhysicsThreadStart += () => mcollisionTester = new MultiCollisionTester();
    }


    public double RenderOrder => 0.5;
    public int RenderRange => 99;


    public void Dispose()
    {
        entityWf?.Dispose();
        capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "PassivePhysicsMultiBoxWf");
            entityWf = WireframeCube.CreateCenterOriginCube(capi, ColorUtil.WhiteArgb);
        }

        base.Initialize(properties, attributes);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        base.AfterInitialized(onFirstSpawn);
        AdjustCollisionBoxesToYaw(1, false, entity.SidedPos.Yaw);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        base.OnEntityDespawn(despawn);
        Dispose();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (capi.Render.WireframeDebugRender.Entity)
        {
            // Physics are done only server side, unless mounted. In order to see the collisionbox wireframe at the right position on the client
            // so we need to run AdjustCollisionBoxesToYaw if not mounted
            if (capi.World.Player.Entity.MountedOn != entity)
            {
                AdjustCollisionBoxesToYaw(deltaTime * 60, false, entity.SidedPos.Yaw);
            }

            foreach (var collbox in CollisionBoxes)
            {
                float colScaleX = collbox.XSize / 2;
                float colScaleY = collbox.YSize / 2;
                float colScaleZ = collbox.ZSize / 2;

                var x = entity.Pos.X + collbox.X1 + colScaleX;
                var y = entity.Pos.Y + collbox.Y1 + colScaleY;
                var z = entity.Pos.Z + collbox.Z1 + colScaleZ;

                entityWf.Render(capi, x, y, z, colScaleX, colScaleY, colScaleZ, 1, new Vec4f(1, 0, 1, 1));
            }
        }
    }

    public override void SetProperties(JsonObject attributes)
    {
        base.SetProperties(attributes);

        CollisionBoxes = attributes["collisionBoxes"].AsArray<Cuboidf>();
        OrigCollisionBoxes = attributes["collisionBoxes"].AsArray<Cuboidf>();
    }

    protected override void applyCollision(EntityPos pos, float dtFactor)
    {
        AdjustCollisionBoxesToYaw(dtFactor, true, entity.SidedPos.Yaw);

        mcollisionTester.ApplyTerrainCollision(CollisionBoxes, CollisionBoxes.Length, entity, pos, dtFactor, ref newPos, 0, CollisionYExtra);
    }

    Matrixf mat = new Matrixf();
    Vec3d tmpPos = new Vec3d();
    float pushVelocityMul = 1;

    public bool AdjustCollisionBoxesToYaw(float dtFac, bool push, float newYaw)
    {
        adjustBoxesToYaw(newYaw);

        if (push)
        {
            tmpPos.Set(entity.SidedPos.X, entity.SidedPos.Y, entity.SidedPos.Z);
            Cuboidd ccollbox = mcollisionTester.GetCollidingCollisionBox(entity.World.BlockAccessor, CollisionBoxes, CollisionBoxes.Length, tmpPos, false);
            if (ccollbox != null)
            {
                bool pushed = PushoutOfCollisionbox(dtFac / 60f, ccollbox);
                if (pushed) return true;
                return false;
            }
        }

        return true;
    }

    private void adjustBoxesToYaw(float newYaw)
    {
        for (int i = 0; i < OrigCollisionBoxes.Length; i++)
        {
            var ocollbox = OrigCollisionBoxes[i];

            // This is the offset from center we need to rotate around
            float x = ocollbox.MidX;
            float y = ocollbox.MidY;
            float z = ocollbox.MidZ;

            mat.Identity();
            mat.RotateY(newYaw + GameMath.PI);
            var newMid = mat.TransformVector(new Vec4d(x, y, z, 1));

            var collbox = CollisionBoxes[i];
            double motionX = newMid.X - collbox.MidX;
            double motionZ = newMid.Z - collbox.MidZ;

            if (Math.Abs(motionX) > 0.01 || Math.Abs(motionZ) > 0.01)
            {
                float wh = ocollbox.Width / 2;
                float hh = ocollbox.Height / 2;
                float lh = ocollbox.Length / 2;

                collbox.Set((float)newMid.X - wh, (float)newMid.Y - hh, (float)newMid.Z - lh, (float)newMid.X + wh, (float)newMid.Y + hh, (float)newMid.Z + lh);
            }
        }
    }


    private bool PushoutOfCollisionbox(float dt, Cuboidd collBox)
    {
        double posX = entity.SidedPos.X;
        double posY = entity.SidedPos.Y;
        double posZ = entity.SidedPos.Z;

        var ba = entity.World.BlockAccessor;

        Vec3i pushDir = null;
        double shortestDist = 99;
        for (int i = 0; i < Cardinal.ALL.Length; i++)
        {
            // Already found a good solution, no need to search further
            if (shortestDist <= 0.25f) break;

            var cardinal = Cardinal.ALL[i];

            for (int dist = 1; dist <= 4; dist++)
            {
                var r = dist / 4f;

                Cuboidd ccollbox = mcollisionTester.GetCollidingCollisionBox(ba, CollisionBoxes, CollisionBoxes.Length, tmpPos.Set(posX + cardinal.Normali.X * r, posY, posZ + cardinal.Normali.Z * r), false);
                if (ccollbox == null)
                {
                    if (r < shortestDist)
                    {
                        // Make going diagonal a bit more costly
                        shortestDist = r + (cardinal.IsDiagnoal ? 0.1f : 0);
                        pushDir = cardinal.Normali;
                        break;
                    }
                }
            }
        }

        if (pushDir == null)
        {
            return false;
        }

        dt = Math.Min(dt, 0.1f);

        // Add some tiny amounts of random horizontal motion to give it a chance to wiggle out of an edge case
        float rndx = ((float)entity.World.Rand.NextDouble() - 0.5f) / 600f;
        float rndz = ((float)entity.World.Rand.NextDouble() - 0.5f) / 600f;

        entity.SidedPos.X += pushDir.X * dt * 1.5f;
        entity.SidedPos.Z += pushDir.Z * dt * 1.5f;

        entity.SidedPos.Motion.X = pushVelocityMul * pushDir.X * dt + rndx;
        entity.SidedPos.Motion.Z = pushVelocityMul * pushDir.Z * dt + rndz;

        return true;
    }

}
