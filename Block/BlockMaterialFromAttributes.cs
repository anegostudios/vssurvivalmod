using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent;

public class BlockMaterialFromAttributes : Block
{
    public virtual string MeshKey => "BMA";
    public virtual string MeshKeyInventory => MeshKey + "Inventory";
    public Dictionary<string, CompositeTexture> TexturesBMFA = null!;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        LoadTypes();
    }

    public virtual void LoadTypes()
    {
        var grp = Attributes["materials"].AsObject<RegistryObjectVariantGroup>();
        TexturesBMFA = Attributes["textures"].AsObject<Dictionary<string, CompositeTexture>>();
        var materials = grp.States;
        if (grp.LoadFromProperties != null)
        {
            var prop = api.Assets.TryGet(grp.LoadFromProperties.WithPathPrefixOnce("worldproperties/").WithPathAppendixOnce(".json"))
                .ToObject<StandardWorldProperty>();
            materials = prop.Variants.Select(p => p.Code.Path).ToArray().Append(materials);
        }

        List<JsonItemStack> stacks = new List<JsonItemStack>();

        foreach (var material in materials)
        {
            var jstack = new JsonItemStack()
            {
                Code = Code,
                Type = EnumItemClass.Block,
                Attributes = new JsonObject(JToken.Parse("{ \"material\": \"" + material + "\" }"))
            };

            jstack.Resolve(api.World, Code + " type");
            stacks.Add(jstack);
        }

        CreativeInventoryStacks =
        [
            new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = ["general", "decorative"] }
        ];
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        var meshes = ObjectCacheUtil.TryGet<Dictionary<string, MeshData>>(api, MeshKey);
        if (meshes?.Count > 0)
        {
            foreach (var (_, meshref) in meshes)
            {
                meshref.Dispose();
            }

            ObjectCacheUtil.Delete(api, MeshKey);
        }

        var invMeshes = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, MeshKeyInventory);
        if (invMeshes?.Count > 0)
        {
            foreach (var (_, meshref) in invMeshes)
            {
                meshref.Dispose();
            }

            ObjectCacheUtil.Delete(api, MeshKeyInventory);
        }

        base.OnUnloaded(api);
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        var meshRefs = ObjectCacheUtil.GetOrCreate(capi, MeshKeyInventory, () => new Dictionary<string, MultiTextureMeshRef>());

        var material = itemstack.Attributes.GetString("material", "");

        var key = Variant["type"] + material;
        if (!meshRefs.TryGetValue(key, out MultiTextureMeshRef? meshref))
        {
            var mesh = GetOrCreateMesh(material);
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
    }

    public virtual MeshData GetOrCreateMesh(string material, ITexPositionSource? overrideTexturesource = null)
    {
        var cMeshes = ObjectCacheUtil.GetOrCreate(api, MeshKey, () => new Dictionary<string, MeshData>());
        ICoreClientAPI capi = (ICoreClientAPI)api;

        string key = Variant["type"] + material;
        if (overrideTexturesource != null || !cMeshes.TryGetValue(key, out var mesh))
        {
            var rcshape = Shape.Clone();

            mesh = capi.TesselatorManager.CreateMesh(
                Code + " block",
                rcshape,
                (shape, name) => new ShapeTextureSource(capi, shape, name, TexturesBMFA, (p) => p.Replace("{material}", material)),
                overrideTexturesource
            );

            if (overrideTexturesource == null)
            {
                cMeshes[key] = mesh;
            }
        }

        return mesh;
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

        if (val)
        {
            var bect = world.BlockAccessor.GetBlockEntity(blockSel.Position).GetBehavior<BEBehaviorMaterialFromAttributes>();
            if (bect != null)
            {
                BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                float angleHor = (float)Math.Atan2(dx, dz);

                float intervalRad = GameMath.PIHALF;
                float roundRad = ((int)Math.Round(angleHor / intervalRad)) * intervalRad;
                bect.MeshAngleY = roundRad;
                bect.Init();
            }
        }

        return val;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        return new ItemStack[] { OnPickBlock(world, pos) };
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        var stack = base.OnPickBlock(world, pos);
        var beshelf = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMaterialFromAttributes>();
        if (beshelf != null)
        {
            stack.Attributes.SetString("material", beshelf.Material);
        }

        return stack;
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        return $"block-{Code.Path}-" + itemStack.Attributes.GetString("material");
    }
}
