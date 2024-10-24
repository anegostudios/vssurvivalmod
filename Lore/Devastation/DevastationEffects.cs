using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

[ProtoContract]
public class DevaLocation
{
    [ProtoMember(1)]
    public BlockPos Pos;

    [ProtoMember(2)]
    public int Radius;
}

public class DevastationEffects : ModSystem
{
    public Vec3d DevaLocation;
    public int Radius;
    private ICoreClientAPI capi;
    private int EffectDist = 5000;

    private static SimpleParticleProperties dustParticles;

    private ICoreServerAPI sapi;

    AmbientModifier towerAmbient;

    public override double ExecuteOrder() => 2;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.Event.OnGetWindSpeed += Event_OnGetWindSpeed;
    }

    private void Event_OnGetWindSpeed(Vec3d pos, ref Vec3d windSpeed)
    {
        if (DevaLocation == null) return;
        var dist = DevaLocation.DistanceTo(pos.X, pos.Y, pos.Z);
        if (dist > EffectDist) return;

        windSpeed.Mul(GameMath.Clamp(dist/Radius - 0.5f, 0, 1));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;

        dustParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(),
            new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.3f, 0.3f, EnumParticleModel.Quad);
        dustParticles.MinQuantity = 0.1f;
        dustParticles.MinVelocity.Set(-0.05f, -0.4f, -0.05f);
        dustParticles.AddVelocity.Set(0, 0, 0);
        dustParticles.ParticleModel = EnumParticleModel.Quad;
        dustParticles.GravityEffect = 0;
        dustParticles.MaxSize = 1f;
        dustParticles.AddPos.Set(0, 0, 0);
        dustParticles.MinSize = 0.2f;
        dustParticles.Color = ColorUtil.ColorFromRgba(75 / 4, 100 / 4, 150 / 4, 255);
        dustParticles.addLifeLength = 0;
        dustParticles.WithTerrainCollision = false;
        dustParticles.SelfPropelled = false;
        dustParticles.LifeLength = 3f;
        dustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);

        // devastation location and radius is send from GenDevastationLayer.cs
        api.Network.RegisterChannel("devastation")
            .RegisterMessageType<DevaLocation>()
            .SetMessageHandler<DevaLocation>(OnDevaLocation);

        api.Event.RegisterAsyncParticleSpawner(asyncParticleSpawn);
        api.Event.RegisterGameTickListener(onClientTick, 10, 0);

        

        towerAmbient = new AmbientModifier()
        {
            FogColor = new WeightedFloatArray(new float[] { 66/255f, 45 / 255f, 25 / 255f }, 0),
            FogDensity = new WeightedFloat(0.05f, 0)
        }.EnsurePopulated();

        api.Ambient.CurrentModifiers["towerAmbient"] = towerAmbient;
    }

    private void onClientTick(float dt)
    {
        if (DevaLocation == null) return;

        Vec3d offsetToTowerCenter = DevaLocation - capi.World.Player.Entity.Pos.XYZ;
        var dist = offsetToTowerCenter.Length();
        if (dist > EffectDist)
        {
            capi.Render.ShaderUniforms.FogSphereQuantity = 0;
            towerAmbient.FogDensity.Weight = 0;
            towerAmbient.FogColor.Weight = 0;
            return;
        }

        towerAmbient.FogColor.Weight = (float)GameMath.Clamp((1 - (dist - Radius / 2) / Radius) * 2f, 0, 1f);
        towerAmbient.FogDensity.Value = 0.05f;
        towerAmbient.FogDensity.Weight = (float)GameMath.Clamp((1 - (dist - Radius / 2) / Radius), 0, 1f);


        // Goes from 1 = at deva tower
        // to 0 = 5000 blocks away
        float f = (float)(1 - dist / EffectDist);
        f = GameMath.Clamp(1.5f * (f - 0.25f), 0, 1);

        // However, lets blend that fog sphere to ambient fog after some distance
        var fogColor = capi.Ambient.BlendedFogColor;
        var fogDense = capi.Ambient.BlendedFogDensity;

        float w = towerAmbient.FogColor.Weight;
        float l = GameMath.Clamp(fogDense*100 + (1-w) - 1, 0, 1);

        var b = capi.Ambient.BlendedFogBrightness * capi.Ambient.BlendedSceneBrightness;

        capi.Render.ShaderUniforms.FogSphereQuantity = 1;
        capi.Render.ShaderUniforms.FogSpheres[0] = (float)offsetToTowerCenter.X;
        capi.Render.ShaderUniforms.FogSpheres[1] = (float)offsetToTowerCenter.Y - 300;
        capi.Render.ShaderUniforms.FogSpheres[2] = (float)offsetToTowerCenter.Z;
        capi.Render.ShaderUniforms.FogSpheres[3] = Radius * 1.6f;
        capi.Render.ShaderUniforms.FogSpheres[4] = 1/800f * f;
        capi.Render.ShaderUniforms.FogSpheres[5] = GameMath.Lerp(66 / 255f * b, fogColor.R, l);
        capi.Render.ShaderUniforms.FogSpheres[6] = GameMath.Lerp(45 / 255f * b, fogColor.G, l);
        capi.Render.ShaderUniforms.FogSpheres[7] = GameMath.Lerp(25 / 255f * b, fogColor.B, l);

    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Event.RegisterGameTickListener(OnGameTick, 1000);
    }

    private void OnGameTick(float obj)
    {
        if (DevaLocation == null) return;

        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            var hasEffect = player.Entity.Stats["gliderLiftMax"].ValuesByKey.TryGetValue("deva", out _);
            if (player.Entity.ServerPos.DistanceTo(DevaLocation) < Radius)
            {
                if (!hasEffect)
                {
                    player.Entity.Stats.Set("gliderSpeedMax", "deva", -0.185f);
                    player.Entity.Stats.Set("gliderLiftMax", "deva", -1.01f);
                }
            }
            else
            {
                if (hasEffect)
                {
                    player.Entity.Stats.Remove("gliderSpeedMax", "deva");
                    player.Entity.Stats.Remove("gliderLiftMax", "deva");
                }
            }
        }
    }

    private bool asyncParticleSpawn(float dt, IAsyncParticleManager manager)
    {
        if (DevaLocation == null) return true;
        var plr = capi.World.Player.Entity;

        float intensity = (float)GameMath.Clamp((1.5 - plr.Pos.DistanceTo(DevaLocation) / Radius)*1.5, 0, 1);
        if (intensity <= 0) return true;

        double offsetx = plr.Pos.Motion.X * 200;
        double offsetz = plr.Pos.Motion.Z * 200;

        for (int dx = -60; dx <= 60; dx++)
        {
            for (int dz = -60; dz <= 60; dz++)
            {
                var pos = plr.Pos.XYZ.Add(dx + offsetx, 0, dz + offsetz);

                float hereintensity = (float)GameMath.Clamp((1 - pos.DistanceTo(DevaLocation) / Radius) * 1.5, 0, 1);
                if (capi.World.Rand.NextDouble() > hereintensity * 0.015) continue;

                pos.Y = capi.World.BlockAccessor.GetRainMapHeightAt((int)pos.X, (int)pos.Z) - 8 + capi.World.Rand.NextDouble() * 25;

                Block block = capi.World.BlockAccessor.GetBlock((int)pos.X, (int)pos.Y, (int)pos.Z);
                if (block.FirstCodePart() != "devastatedsoil") continue;

                Vec3f velocity = DevaLocation.Clone().Sub(pos.X, pos.Y, pos.Z).Normalize().Mul(2f * capi.World.Rand.NextDouble()).ToVec3f() / 2f;
                velocity.Y = 1 + Math.Max(0, 40 - pos.DistanceTo(DevaLocation)) / 20.0f;


                dustParticles.MinPos = pos;
                dustParticles.MinVelocity = velocity;

                manager.Spawn(dustParticles);
            }
        }

        return true;
    }

    private void OnDevaLocation(DevaLocation packet)
    {
        DevaLocation = packet.Pos.ToVec3d();
        Radius = packet.Radius;
    }
}
