using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

#nullable disable

namespace Vintagestory.API.Common;


public class ModSystemThreadStaticCollisionTester : ModSystem
{
    public static MultiCollisionTester CollisionTester
    {
        get
        {
            var threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (disposableCollisionTesters.TryGetValue(threadID, out var mCollisionTester)) return mCollisionTester;
            return disposableCollisionTesters[threadID] = new MultiCollisionTester();
        }
    }
    // Looks a bit heavy, but we write to this only once per game per thread; then later we can ensure that we dispose of all the blockAccessors even if the thread itself is *not* disposed (ThreadPool threads for example are not disposed)
    private static System.Collections.Concurrent.ConcurrentDictionary<int, MultiCollisionTester> disposableCollisionTesters = new(4, 16);

    public override bool ShouldLoad(ICoreAPI api) => true;
    public override double ExecuteOrder() => 0;

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Event.LeaveWorld += DisposeCollisionTesters;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, DisposeCollisionTesters);
    }

    private void DisposeCollisionTesters()
    {
        disposableCollisionTesters.Clear();
    }

}



/// <summary>
/// Passive physics system for entities with support for multiple collision boxes.
/// <br/>Uses the "passivephysicsmultibox" code
/// </summary>
/// <example><code lang="json">
/// "behaviors": [
///  {
///     "code": "passivephysicsmultibox"
/// 	"collisionBoxes": [
/// 		{ "x1": -1.4, "y1": 0, "z1": 0.1, "x2": 1.4, "y2": 1.2, "z2": 2.9 },
/// 		{ "x1": -1.4, "y1": 0, "z1": -2.9, "x2": 1.4, "y2": 1.2, "z2": -0.1 },
/// 		{ "x1": -1.4, "y1": 0, "z1": -5.9, "x2": 1.4, "y2": 1.2, "z2": -2.9 }
/// 	],
/// 	"groundDragFactor": 1,
/// 	"airDragFallingFactor": 0.5,
/// 	"gravityFactor": 1.0
///  }
/// ]
/// </code></example>
[DocumentAsJson]
public class EntityBehaviorPassivePhysicsMultiBox : EntityBehaviorPassivePhysics, IRenderer
{
    /// <summary>
    /// <!--<jsonalias>CollisionBoxes</jsonalias>-->
    /// Collision boxes for this entity
    /// </summary>
    [DocumentAsJson]
    protected Cuboidf[] OrigCollisionBoxes;

    protected Cuboidf[] CollisionBoxes;
    WireframeCube entityWf;

    protected internal static MultiCollisionTester mcollisionTester => ModSystemThreadStaticCollisionTester.CollisionTester;

    public EntityBehaviorPassivePhysicsMultiBox(Entity entity) : base(entity) { }


    public double RenderOrder => 0.5;
    public int RenderRange => 99;


    public void Dispose()
    {
        entityWf?.Dispose();
        capi?.Event.UnregisterRenderer(this, EnumRenderStage.AfterOIT);
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterOIT, "PassivePhysicsMultiBoxWf");
            entityWf = WireframeCube.CreateCenterOriginCube(capi, ColorUtil.WhiteArgb);
        }

        base.Initialize(properties, attributes);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        base.AfterInitialized(onFirstSpawn);
        AdjustCollisionBoxesToYaw(1, false, entity.Pos.Yaw);
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
                AdjustCollisionBoxesToYaw(deltaTime * 60, false, entity.Pos.Yaw);
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
        AdjustCollisionBoxesToYaw(dtFactor, true, entity.Pos.Yaw);

        mcollisionTester.ApplyTerrainCollision(CollisionBoxes, CollisionBoxes.Length, entity, pos, dtFactor, ref newPos, 0, CollisionYExtra);
    }

    // The multibox solver does not publish contacts through the single-body tester.
    protected override Collider getSupportingCollider() => null;

    protected override bool slidingOnSteepCollider => false;

    Vec3d tmpPos = new Vec3d();
    float pushVelocityMul = 1;

    public bool AdjustCollisionBoxesToYaw(float dtFac, bool push, float newYaw)
    {
        CollisionQueryFilter filter = new(CollisionLayer.Entity, excludedOwnerEntityId: entity.EntityId);
        if (push && !mcollisionTester.CanRotate(entity.World.BlockAccessor, entity.World.CollisionScene, OrigCollisionBoxes, entity.Pos, newYaw, filter)) return false;
        adjustBoxesToYaw(newYaw);

        if (push && newYaw == entity.Pos.Yaw)
        {
            tmpPos.Set(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            Cuboidd ccollbox = mcollisionTester.GetCollidingCollisionBox(entity.World.BlockAccessor, CollisionBoxes, CollisionBoxes.Length, tmpPos, false);
            if (ccollbox != null)
            {
                return PushoutOfCollisionbox(dtFac / 60f, ccollbox);
            }
        }

        return true;
    }

    private void adjustBoxesToYaw(float newYaw)
    {
        double cosYaw = Math.Cos(newYaw + GameMath.PI);
        double sinYaw = Math.Sin(newYaw + GameMath.PI);
        for (int i = 0; i < OrigCollisionBoxes.Length; i++)
        {
            var box = OrigCollisionBoxes[i];
            // Multibox physics rotates the centers while keeping the boxes upright.
            float x = (float)(box.MidX * cosYaw + box.MidZ * sinYaw);
            float z = (float)(-box.MidX * sinYaw + box.MidZ * cosYaw);
            float halfWidth = box.HalfExtentX;
            float halfLength = box.HalfExtentZ;
            CollisionBoxes[i].Set(x - halfWidth, box.Y1, z - halfLength, x + halfWidth, box.Y2, z + halfLength);
        }
    }


    private bool PushoutOfCollisionbox(float dt, Cuboidd collBox)
    {
        double posX = entity.Pos.X;
        double posY = entity.Pos.Y;
        double posZ = entity.Pos.Z;

        var ba = entity.World.BlockAccessor;
        var collTester = mcollisionTester;


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

                Cuboidd ccollbox = collTester.GetCollidingCollisionBox(ba, CollisionBoxes, CollisionBoxes.Length, tmpPos.Set(posX + cardinal.Normali.X * r, posY, posZ + cardinal.Normali.Z * r), false);
                if (ccollbox == null && collTester.CanTranslate(entity.World.CollisionScene, CollisionBoxes, entity.Pos, cardinal.Normali.X * r, 0, cardinal.Normali.Z * r, new CollisionQueryFilter(CollisionLayer.Entity, excludedOwnerEntityId: entity.EntityId)))
                {
                    if (r < shortestDist)
                    {
                        // Make going diagonal a bit more costly
                        shortestDist = r + (cardinal.IsDiagonal ? 0.1f : 0);
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

        entity.Pos.X += pushDir.X * dt * 1.5f;
        entity.Pos.Z += pushDir.Z * dt * 1.5f;

        entity.Pos.Motion.X = pushVelocityMul * pushDir.X * dt;
        entity.Pos.Motion.Z = pushVelocityMul * pushDir.Z * dt;

        return true;
    }

}
