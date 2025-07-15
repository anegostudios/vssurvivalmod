using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent;


public sealed class EntityErel : EntityAgent
{
    public override bool CanSwivel => true;
    public override bool CanSwivelNow => true;
    public override bool StoreWithChunk => false;
    public override bool AllowOutsideLoadedRange => true;
    public override bool AlwaysActive => true;
    public long LastAttackTime { get; set; } = 0;

    public double LastAnnoyedTotalDays
    {
        get { return WatchedAttributes.GetDouble("lastannoyedtotaldays", -9999999); }
        set { WatchedAttributes.SetDouble("lastannoyedtotaldays", value); }
    }
    public bool Annoyed
    {
        get { return WatchedAttributes.GetBool("annoyed", false); }
        set { WatchedAttributes.SetBool("annoyed", value); }
    }


    static EntityErel()
    {
        AiTaskRegistry.Register("flycircle", typeof(AiTaskFlyCircle));
        AiTaskRegistry.Register("flycircleifentity", typeof(AiTaskFlyCircleIfEntity));
        AiTaskRegistry.Register("flycircletarget", typeof(AiTaskFlyCircleTarget));
        AiTaskRegistry.Register("flywander", typeof(AiTaskFlyWander));
        AiTaskRegistry.Register("flyswoopattack", typeof(AiTaskFlySwoopAttack));
        AiTaskRegistry.Register("flydiveattack", typeof(AiTaskFlyDiveAttack));
        AiTaskRegistry.Register("firefeathersattack", typeof(AiTaskFireFeathersAttack));
        AiTaskRegistry.Register("flyleave", typeof(AiTaskFlyLeave));
    }
    public EntityErel()
    {
        SimulationRange = 1024;
    }

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);

        WatchedAttributes.SetBool("showHealthbar", true);

        if (api is ICoreClientAPI capi)
        {
            aliveSound = capi.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = new AssetLocation("sounds/creature/erel/alive"), ShouldLoop = true, Range = 48 });
            aliveSound.Start();
            glideSound = capi.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = new AssetLocation("sounds/creature/erel/glide"), ShouldLoop = true, Range = 24 });
        }

        healthBehavior = GetBehavior<EntityBehaviorHealth>();
    }
    public override void AfterInitialized(bool onFirstSpawn)
    {
        base.AfterInitialized(onFirstSpawn);

        Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().SetErelAnnoyed(Annoyed);

        if (World.Side == EnumAppSide.Server)
        {
            taskManager = GetBehavior<EntityBehaviorTaskAI>().TaskManager;
            taskManager.OnShouldExecuteTask += TaskManager_OnShouldExecuteTask;

            if (Annoyed) taskManager.GetTask<AiTaskFlyLeave>().AllowExecute = true;

            attackCooldowns["swoop"] = [taskManager.GetTask<AiTaskFlySwoopAttack>().Mincooldown, taskManager.GetTask<AiTaskFlySwoopAttack>().Maxcooldown];
            attackCooldowns["dive"] = [taskManager.GetTask<AiTaskFlyDiveAttack>().Mincooldown, taskManager.GetTask<AiTaskFlyDiveAttack>().Maxcooldown];
            attackCooldowns["feathers"] = [taskManager.GetTask<AiTaskFireFeathersAttack>().Mincooldown, taskManager.GetTask<AiTaskFireFeathersAttack>().Maxcooldown];
        }

        updateAnnoyedState();
    }
    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        base.OnEntityDespawn(despawn);
        aliveSound?.Dispose();
    }
    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);

        if (Api.Side == EnumAppSide.Server)
        {
            doOccasionalFlapping(dt);

            annoyCheckAccum += dt;
            if (annoyCheckAccum > 1)
            {
                annoyCheckAccum = 0;
                updateAnnoyedState();
                toggleBossFightModeNearTower();
                stopAttacksOutsideDevaRange();
            }

        }
        else
        {
            aliveSound.SetPosition((float)Pos.X, (float)Pos.InternalY, (float)Pos.Z);
            glideSound.SetPosition((float)Pos.X, (float)Pos.InternalY, (float)Pos.Z);

            if (AnimManager.IsAnimationActive("fly-flapactive", "fly-flapactive-fast") && glideSound.IsPlaying)
            {
                glideSound.Stop();
            }
            else
            {
                if ((AnimManager.IsAnimationActive("fly-idle", "fly-flapcruise") || AnimManager.ActiveAnimationsByAnimCode.Count == 0) && !glideSound.IsPlaying)
                {
                    glideSound.Start();
                }
            }

            setCurrentShape(Pos.Dimension);
        }

        if (AnimManager.IsAnimationActive("dive", "slam")) return;
        double speed = ServerPos.Motion.Length();
        if (speed > 0.01)
        {
            //ServerPos.Roll = (float)Math.Asin(GameMath.Clamp(-ServerPos.Motion.Y / speed, -1, 1));
        }
    }

    public override bool ReceiveDamage(DamageSource damageSource, float damage)
    {
        if (!inTowerRange())
        {
            damage /= 2;
        }

        if (World.Side == EnumAppSide.Server)
        {
            // 1/(1+sqrt((x-1)/4))
            // https://www.toolfk.com/online-plotter-frame#W3sidHlwZSI6MCwiZXEiOiIxLygxK3NxcnQoKHgtMSkvMykpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjEwIiwiMCIsIjEuMSJdLCJzaXplIjpbNjQ3LDM5N119XQ--
            // Scale damage based on how many are fighting the erel
            int x = nearbyPlayerCount();
            damage *= 1f / (1 + (float)Math.Sqrt((x - 1) / 4));
        }

        // Cannot die
        if (healthBehavior != null && healthBehavior.Health - damage < 0)
        {
            float reducedDamage = MathF.Max(healthBehavior.Health - 1, 0);
            return base.ReceiveDamage(damageSource, reducedDamage);
        }

        return base.ReceiveDamage(damageSource, damage);
    }

    public void ChangeDimension(int dim)
    {
        if (ServerPos.Dimension != dim)
        {
            spawnTeleportParticles(Pos);
        }

        Pos.Dimension = dim;
        ServerPos.Dimension = dim;

        long newchunkindex3d = Api.World.ChunkProvider.ChunkIndex3D(Pos);
        Api.World.UpdateEntityChunk(this, newchunkindex3d);
    }
    public void ChangeDimensionNoParticles(int dim)
    {
        Pos.Dimension = dim;
        ServerPos.Dimension = dim;

        long newchunkindex3d = Api.World.ChunkProvider.ChunkIndex3D(Pos);
        Api.World.UpdateEntityChunk(this, newchunkindex3d);
    }
    protected override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned)
    {
        base.OnTesselation(ref  entityShape, shapePathForLogging, ref shapeIsCloned);

        AnimManager.LoadAnimator(World.Api, this, entityShape, AnimManager.Animator?.Animations, requirePosesOnServer, "head");
    }


    private ILoadedSound aliveSound;
    private ILoadedSound glideSound;
    private AiTaskManager taskManager;
    private readonly Dictionary<string, int[]> attackCooldowns = new();
    private float nextFlyIdleSec = -1;
    private float nextFlapCruiseSec = -1;
    private float prevYaw = 0;
    private float annoyCheckAccum;
    private bool wasAtBossFightArea;
    private EntityBehaviorHealth healthBehavior;
    private int previousTickDimension = 0;


    private void spawnTeleportParticles(EntityPos pos)
    {
        int r = 53;
        int g = 221;
        int b = 172;

        SimpleParticleProperties teleportParticles = new(
            300, 400,
            (r << 16) | (g << 8) | (b << 0) | (100 << 24),
            new Vec3d(pos.X - 2.5, pos.Y, pos.Z - 2.5),
            new Vec3d(pos.X + 2.5, pos.Y + 5.8, pos.Z + 2.5),
            new Vec3f(-0.7f, -0.7f, -0.7f),
            new Vec3f(1.4f, 1.4f, 1.4f),
            2f,
            0,
            0.15f,
            0.3f,
            EnumParticleModel.Quad
        );

        teleportParticles.addLifeLength = 1f;
        teleportParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -10f);

        int dim = pos.Dimension;
        // Spawn in dim 1
        Api.World.SpawnParticles(teleportParticles);
        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/timeswitch"), pos.X, pos.Y, pos.Z, null, false, range: 128, volume: 1);

        // Spawn in dim 2
        teleportParticles.MinPos.Y += dim * BlockPos.DimensionBoundary;
        Api.World.SpawnParticles(teleportParticles);
        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/timeswitch"), pos.X, pos.Y + dim * BlockPos.DimensionBoundary, pos.Z, null, false, range: 128, volume: 1);
    }
    private int nearbyPlayerCount()
    {
        int cnt = 0;
        foreach (IServerPlayer plr in World.AllOnlinePlayers)
        {
            if (plr.ConnectionState != EnumClientState.Playing) continue;
            if (plr.WorldData.CurrentGameMode != EnumGameMode.Survival) continue;

            double dx = plr.Entity.Pos.X - Pos.X;
            double dz = plr.Entity.Pos.Z - Pos.Z;

            if (Math.Abs(dx) <= 7 && Math.Abs(dz) <= 7)
            {
                cnt++;
            }
        }

        return cnt;
    }
    private void toggleBossFightModeNearTower()
    {
        ModSystemDevastationEffects msdevaeff = Api.ModLoader.GetModSystem<ModSystemDevastationEffects>();
        Vec3d loc = ServerPos.Dimension == 0 ? msdevaeff.DevaLocationPresent : msdevaeff.DevaLocationPast;
        WatchedAttributes.SetBool("showHealthbar", ServerPos.InternalY > loc.Y + 70);

        AiTaskFlyCircleIfEntity ctask = taskManager.GetTask<AiTaskFlyCircleIfEntity>();
        Entity nearTownerEntity = ctask.getEntity();
        bool atBossFightArea = nearTownerEntity != null && ServerPos.XYZ.HorizontalSquareDistanceTo(ctask.CenterPos) < 70 * 70;

        AiTaskFlySwoopAttack swoopAtta = taskManager.GetTask<AiTaskFlySwoopAttack>();
        AiTaskFlyDiveAttack diveAtta = taskManager.GetTask<AiTaskFlyDiveAttack>();
        AiTaskFireFeathersAttack feathersAtta = taskManager.GetTask<AiTaskFireFeathersAttack>();

        feathersAtta.Enabled = atBossFightArea;
        diveAtta.Enabled = atBossFightArea;

        if (wasAtBossFightArea && !atBossFightArea)
        {
            swoopAtta.Mincooldown = attackCooldowns["swoop"][0];
            swoopAtta.Maxcooldown = attackCooldowns["swoop"][1];
            diveAtta.Mincooldown = attackCooldowns["dive"][0];
            diveAtta.Maxcooldown = attackCooldowns["dive"][1];
            feathersAtta.Mincooldown = attackCooldowns["feathers"][0];
            feathersAtta.Maxcooldown = attackCooldowns["feathers"][1];
        }
        if (!wasAtBossFightArea && atBossFightArea)
        {
            swoopAtta.Mincooldown = attackCooldowns["swoop"][0] / 2;
            swoopAtta.Maxcooldown = attackCooldowns["swoop"][1] / 2;
            diveAtta.Mincooldown = attackCooldowns["dive"][0] / 2;
            diveAtta.Maxcooldown = attackCooldowns["dive"][1] / 2;
            feathersAtta.Mincooldown = attackCooldowns["feathers"][0] / 2;
            feathersAtta.Maxcooldown = attackCooldowns["feathers"][1] / 2;
        }

        wasAtBossFightArea = atBossFightArea;
    }
    private void updateAnnoyedState()
    {
        if (Api.Side == EnumAppSide.Client) return;

        if (!Annoyed)
        {
            if (healthBehavior.Health / healthBehavior.MaxHealth < 0.6)
            {
                Api.World.PlaySoundAt("sounds/creature/erel/annoyed", this, null, false, 1024, 1);
                AnimManager.StartAnimation("defeat");
                LastAnnoyedTotalDays = Api.World.Calendar.TotalDays;
                Annoyed = true;
                taskManager.GetTask<AiTaskFlyLeave>().AllowExecute = true;

                Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().SetErelAnnoyed(true);
            }
        }
        else
        {
            if (Api.World.Calendar.TotalDays - LastAnnoyedTotalDays > 14)
            {
                Annoyed = false;
                healthBehavior.Health = healthBehavior.MaxHealth;
                taskManager.GetTask<AiTaskFlyLeave>().AllowExecute = false;
                Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().SetErelAnnoyed(false);
            }
        }
    }
    private void doOccasionalFlapping(float dt)
    {
        float turnSpeed = Math.Abs(GameMath.AngleRadDistance(prevYaw, ServerPos.Yaw));
        double flyspeed = ServerPos.Motion.Length();

        if (AnimManager.IsAnimationActive("dive", "slam")) return;

        if ((ServerPos.Motion.Y >= 0.03 || turnSpeed > 0.05 || flyspeed < 0.15) && (AnimManager.IsAnimationActive("fly-idle", "fly-flapcruise") || AnimManager.ActiveAnimationsByAnimCode.Count == 0))
        {
            AnimManager.StopAnimation("fly-flapcruise");
            AnimManager.StopAnimation("fly-idle");
            AnimManager.StartAnimation("fly-flapactive-fast");
            return;
        }

        if (ServerPos.Motion.Y <= 0.01 && turnSpeed < 0.03 && flyspeed >= 0.35 && AnimManager.IsAnimationActive("fly-flapactive", "fly-flapactive-fast"))
        {
            AnimManager.StopAnimation("fly-flapactive");
            AnimManager.StopAnimation("fly-flapactive-fast");
            AnimManager.StartAnimation("fly-idle");
        }

        prevYaw = ServerPos.Yaw;

        if (nextFlyIdleSec > 0)
        {
            nextFlyIdleSec -= dt;
            if (nextFlyIdleSec < 0)
            {
                AnimManager.StopAnimation("fly-flapcruise");
                AnimManager.StartAnimation("fly-idle");
                return;
            }
        }

        if (nextFlapCruiseSec < 0)
        {
            nextFlapCruiseSec = (float)Api.World.Rand.NextDouble() * 15 + 5;
            return;
        }

        if (AnimManager.IsAnimationActive("fly-idle"))
        {
            nextFlapCruiseSec -= dt;
            if (nextFlapCruiseSec < 0)
            {
                AnimManager.StopAnimation("fly-idle");
                AnimManager.StartAnimation("fly-flapcruise");
                nextFlyIdleSec = (float)(Api.World.Rand.NextDouble() * 4 + 1) * 130 / 30.0f;
            }
        }
    }
    private void stopAttacksOutsideDevaRange()
    {
        if (outSideDevaRange())
        {
            foreach (IAiTask t in taskManager.ActiveTasksBySlot)
            {
                if (t is AiTaskFlySwoopAttack || t is AiTaskFlyDiveAttack || t is AiTaskFireFeathersAttack || t is AiTaskFlyCircleTarget)
                {
                    taskManager.StopTask(t.GetType());
                }
            }
        }
    }
    private bool outSideDevaRange()
    {
        return distanceToTower() > 600;
    }
    private bool inTowerRange() => distanceToTower() < 100;
    private double distanceToTower()
    {
        ModSystemDevastationEffects msdevaeff = Api.ModLoader.GetModSystem<ModSystemDevastationEffects>();
        Vec3d loc = ServerPos.Dimension == 0 ? msdevaeff.DevaLocationPresent : msdevaeff.DevaLocationPast;
        return ServerPos.DistanceTo(loc);
    }
    private void setCurrentShape(int dimension)
    {
        if (Properties.Client.Renderer is not EntityShapeRenderer renderer) return;

        if (dimension == 2)
        {
            renderer.OverrideEntityShape = Properties.Client.LoadedAlternateShapes[0];
        }
        else
        {
            renderer.OverrideEntityShape = Properties.Client.LoadedAlternateShapes[1];
        }

        if (previousTickDimension != dimension)
        {
            renderer.TesselateShape();
            previousTickDimension = dimension;
        }
    }

    /// <summary>
    /// Don't attack or track the player if outside devastation area
    /// </summary>
    private bool TaskManager_OnShouldExecuteTask(IAiTask t)
    {
        if (t is AiTaskFlySwoopAttack || t is AiTaskFlyDiveAttack || t is AiTaskFireFeathersAttack || t is AiTaskFlyCircleTarget)
        {
            if (outSideDevaRange())
            {
                return false;
            }
        }

        return true;
    }
}
