using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class FruitPressContentsRenderer : IRenderer, ITexPositionSource
{
    private readonly ICoreClientAPI capi;
    public double RenderOrder => 0.65;
    public int RenderRange => 0;

    private readonly BlockPos blockPos;
    private readonly Matrixf modelMat = new();

    private MeshRef? juiceMeshRef;
    private MeshRef? mashMeshRef;
    private AssetLocation? textureLocation;
    private TextureAtlasPosition? mashTexPos;
    public TextureAtlasPosition? juiceTexPos;

    private readonly BlockEntityFruitPress fruitPressEntity;
    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

    public FruitPressContentsRenderer(BlockPos blockPos, BlockEntityFruitPress fruitPressEntity)
    {
        this.blockPos = blockPos;
        this.fruitPressEntity = fruitPressEntity;
        capi = (ICoreClientAPI)fruitPressEntity.Api;
    }

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            AssetLocation? texturePath = textureLocation;
            TextureAtlasPosition? texPos;
            if (texturePath == null)
            {
                texPos = mashTexPos;
            }
            else
            {
                texPos = capi.BlockTextureAtlas[texturePath];
            }

            if (texPos == null)
            {
                IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    BitmapRef bmp = texAsset.ToBitmap(capi);
                    capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texPos, () => bmp);
                }
                else
                {
                    texPos = capi.BlockTextureAtlas.UnknownTexturePosition;
                }
            }

            return texPos;
        }
    }

    /// <summary>
    /// Retessellate contents based on the current press inventory.
    /// </summary>
    public void ReloadMeshes()
    {
        JuiceableProperties? props = fruitPressEntity.GetJuiceableProps(fruitPressEntity.MashSlot.Itemstack);

        juiceMeshRef?.Dispose();
        mashMeshRef?.Dispose();

        if (props == null)
        {
            juiceMeshRef = null;
            mashMeshRef = null;
            return;
        }

        ItemStack mashStack = fruitPressEntity.MashSlot.Itemstack;

        // Tessellate mash.
        int y;
        if (mashStack.Collectible.Code.Path == "rot")
        {
            textureLocation = new AssetLocation("block/wood/barrel/rot");
            y = GameMath.Clamp(mashStack.StackSize / 2, 1, 9);
        }
        else
        {
            KeyValuePair<string, CompositeTexture> tex = props.PressedStack.ResolvedItemstack.Item.Textures.First();
            textureLocation = tex.Value.Base;

            if (mashStack.Attributes.HasAttribute("juiceableLitresLeft"))
            {
                float availableLitres = (float)mashStack.Attributes.GetDecimal("juiceableLitresLeft") + (float)mashStack.Attributes.GetDecimal("juiceableLitresTransfered");
                y = (int)GameMath.Clamp(availableLitres, 1, 9);
            }
            else y = GameMath.Clamp(mashStack.StackSize, 1, 9);
        }

        Shape? mashShape = Shape.TryGet(capi, "shapes/block/wood/fruitpress/part-mash-" + y + ".json");
        capi.Tesselator.TesselateShape("fruitpress-mash", mashShape, out MeshData mashMesh, this);
        juiceTexPos = capi.BlockTextureAtlas[textureLocation];

        // Tessellate juice.
        if (mashStack.Collectible.Code.Path != "rot")
        {
            Shape? juiceShape = Shape.TryGet(capi, "shapes/block/wood/fruitpress/part-juice.json");
            AssetLocation location = AssetLocation.Create("juiceportion-" + mashStack.Collectible.Variant["fruit"], mashStack.Collectible.Code.Domain);
            Item? item = capi.World.GetItem(location);
            textureLocation = null;
            if (item?.FirstTexture.Baked == null)
            {
                mashTexPos = capi.BlockTextureAtlas.UnknownTexturePosition;
            }
            else
            {
                mashTexPos = capi.BlockTextureAtlas.Positions[item.FirstTexture.Baked.TextureSubId];
            }

            capi.Tesselator.TesselateShape("fruitpress-juice", juiceShape, out MeshData juiceMesh, this);
            juiceMeshRef = capi.Render.UploadMesh(juiceMesh);
        }

        mashMeshRef = capi.Render.UploadMesh(mashMesh);
    }

    // Needs to render mash, juice quad, and bucket
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (mashMeshRef == null || mashMeshRef.Disposed) return;

        // Updated every frame now.
        UpdateSqueezeRelBasedOnAnimation();

        IRenderAPI rpi = capi.Render;
        Vec3d camPos = capi.World.Player.Entity.CameraPos;

        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true);

        IStandardShaderProgram prog = rpi.StandardShader;
        prog.Use();
        prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
        prog.DontWarpVertices = 0;
        prog.AddRenderFlags = 0;
        prog.RgbaAmbientIn = rpi.AmbientColor;
        prog.RgbaFogIn = rpi.FogColor;
        prog.FogMinIn = rpi.FogMin;
        prog.FogDensityIn = rpi.FogDensity;
        prog.RgbaTint = ColorUtil.WhiteArgbVec;
        prog.NormalShaded = 1;
        prog.ExtraGodray = 0;
        prog.ExtraGlow = 0;
        prog.SsaoAttn = 0;
        prog.AlphaTest = 0.05f;
        prog.OverlayOpacity = 0;

        Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(blockPos.X, blockPos.Y, blockPos.Z);
        prog.RgbaLightIn = lightrgbs;

        double squeezeRel = fruitPressEntity.MashSlot.Itemstack?.Attributes?.GetDouble("squeezeRel", 1) ?? 1;

        prog.ModelMatrix = modelMat
            .Identity()
            .Translate(blockPos.X - camPos.X, blockPos.Y - camPos.Y, blockPos.Z - camPos.Z)
            .Translate(0, 0.8f, 0)
            .Scale(1, (float)squeezeRel, 1)
            .Values
        ;

        prog.ViewMatrix = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

        rpi.RenderMesh(mashMeshRef);

        prog.Stop();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

        mashMeshRef?.Dispose();
        juiceMeshRef?.Dispose();
    }

    private void UpdateSqueezeRelBasedOnAnimation()
    {
        RunningAnimation? anim = fruitPressEntity.AnimUtil?.animator.GetAnimationState("compress");
        ItemStack? mashStack = fruitPressEntity.MashSlot.Itemstack;

        if (anim == null || mashStack == null) return;

        double squeezeRel = Math.Clamp(1f - (anim.CurrentFrame / (anim.Animation.QuantityFrames - 1) / 2f), 0.1f, 1f);
        float selfHeight = (float)(fruitPressEntity.JuiceableLitresTransferred + fruitPressEntity.JuiceableLitresLeft) / 10f;

        squeezeRel += Math.Max(0, 0.9f - selfHeight);
        squeezeRel = Math.Clamp(Math.Min(mashStack.Attributes.GetDouble("squeezeRel", 1), squeezeRel), 0.1f, 1f);

        mashStack.Attributes.SetDouble("squeezeRel", squeezeRel);
    }
}