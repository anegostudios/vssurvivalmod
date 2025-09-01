using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class GroundStorageRenderer : IRenderer
{
    private readonly ICoreClientAPI capi;
    private readonly BlockEntityGroundStorage groundStorage;
    public Matrixf ModelMat = new Matrixf();

    public double RenderOrder => 0.5;

    public int RenderRange => 30;

    private int[] itemTemps;
    private float accumDelta;
    private bool check500;
    private bool check450;

    public GroundStorageRenderer(ICoreClientAPI capi, BlockEntityGroundStorage groundStorage)
    {
        this.capi = capi;
        this.groundStorage = groundStorage;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        itemTemps = new int[groundStorage.Inventory.Count];
        UpdateTemps();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        accumDelta += deltaTime;
        var pos = capi.World.Player.Entity.Pos;
        var dist = groundStorage.Pos.DistanceSqTo(pos.X, pos.Y, pos.Z);
        var outOfRange = RenderRange * RenderRange < dist;

        // update temp only every second
        if (accumDelta > 1)
        {
            UpdateTemps();
        }

        if (!groundStorage.UseRenderer || groundStorage.Inventory.Empty || outOfRange || groundStorage.StorageProps == null) return;

        var rpi = capi.Render;
        var camPos = capi.World.Player.Entity.CameraPos;

        var prog = rpi.PreparedStandardShader(groundStorage.Pos.X, groundStorage.Pos.Y, groundStorage.Pos.Z);

        var offs = new Vec3f[groundStorage.DisplayedItems];
        groundStorage.GetLayoutOffset(offs);
        var lightrgbs = capi.World.BlockAccessor.GetLightRGBs(groundStorage.Pos.X, groundStorage.Pos.Y, groundStorage.Pos.Z);
        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true);

        prog.ViewMatrix = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

        var meshes = groundStorage.MeshRefs;
        for (var index = 0; index < meshes.Length; index++)
        {
            var stack = groundStorage.Inventory[index]?.Itemstack;
            var meshRef = groundStorage.MeshRefs[index];

            if (stack == null || meshRef == null || meshRef.Disposed) continue;


            var glowColor = ColorUtil.GetIncandescenceColorAsColor4f(itemTemps[index]);
            var gi = GameMath.Clamp((itemTemps[index] - 500) / 3, 0, 255);

            ModelMat
                .Identity()
                .Translate(groundStorage.Pos.X - camPos.X, groundStorage.Pos.Y - camPos.Y, groundStorage.Pos.Z - camPos.Z)
                .Translate(0.5f, 0.5f, 0.5f)
                .RotateY(groundStorage.MeshAngle)
                .Translate(-0.5f, -0.5f, -0.5f)
                .Translate(offs[index].X, offs[index].Y, offs[index].Z)
                ;

            var transform = groundStorage.ModelTransformsRenderer[index];
            if (transform != null)
            {
                ModelMat
                    .Translate(0.5f, 0.5f, 0.5f)
                    .RotateY(transform.Rotation.Y)
                    .Translate(-0.5f, -0.5f, -0.5f)
                    .Translate(0.5f, 0.0f, 0.5f)
                    .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                    .Translate(-0.5f, -0.0f, -0.5f)
                    ;
            }

            prog.ModelMatrix = ModelMat.Values;

            prog.TempGlowMode = 1; // stack.ItemAttributes?["tempGlowMode"].AsInt() ?? 0;
            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], gi / 255f);
            prog.ExtraGlow = gi;
            prog.AverageColor = ColorUtil.ToRGBAVec4f(capi.BlockTextureAtlas.GetAverageColor((stack.Item?.FirstTexture ?? stack.Block.FirstTextureInventory).Baked.TextureSubId));

            rpi.RenderMultiTextureMesh(meshRef, "tex");
        }

        prog.TempGlowMode = 0;
        prog.Stop();
    }

    public void UpdateTemps()
    {
        accumDelta = 0;
        float maxTemp = 0;
        for (var index = 0; index < groundStorage.Inventory.Count; index++)
        {
            var itemStack = groundStorage.Inventory[index].Itemstack;
            itemTemps[index] = (int)(itemStack?.Collectible.GetTemperature(capi.World, itemStack) ?? 0f);
            maxTemp = Math.Max(maxTemp,itemTemps[index]);
        }

        // update to not use the custom renderer on next render
        if (!groundStorage.NeedsRetesselation)
        {
            if (maxTemp < 500 && !check500)
            {
                check500 = true;
                groundStorage.NeedsRetesselation = true;
                groundStorage.MarkDirty(true);
            }
            if (maxTemp < 450 && !check450)
            {
                check450 = true;
                groundStorage.NeedsRetesselation = true;
                groundStorage.MarkDirty(true);
            }
        }

        if (maxTemp > 500 && (check500 || check450))
        {
            check500 = false;
            check450 = false;
        }
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
    }
}
