using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent;

public class BlockShapeMaterialFromAttributes : Block
{
    private string[] types = null!;
    private string[] materials = null!;
    public Dictionary<string, CompositeTexture> TexturesBSMFA = null!;
    public CompositeShape Cshape = null!;

    public virtual string MeshKey { get; } = "BSMFA";
    public virtual string MeshKeyInventory => MeshKey + "Inventory";

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        LoadTypes();
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

    public virtual void LoadTypes()
    {
        types = Attributes["types"].AsArray<string>();
        Cshape = Attributes["shape"].AsObject<CompositeShape>();
        TexturesBSMFA = Attributes["textures"].AsObject<Dictionary<string, CompositeTexture>>();
        var grp = Attributes["materials"].AsObject<RegistryObjectVariantGroup>();

        materials = grp.States;
        if (grp.LoadFromProperties != null)
        {
            var prop = api.Assets.TryGet(grp.LoadFromProperties.WithPathPrefixOnce("worldproperties/").WithPathAppendixOnce(".json"))
                .ToObject<StandardWorldProperty>();
            materials = prop.Variants.Select(p => p.Code.Path).ToArray().Append(materials);
        }

        List<JsonItemStack> stacks = new List<JsonItemStack>();

        foreach (var type in types)
        {
            foreach (var material in materials)
            {
                var jstack = new JsonItemStack()
                {
                    Code = this.Code,
                    Type = EnumItemClass.Block,
                    Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + type + "\", \"material\": \"" + material + "\" }"))
                };

                jstack.Resolve(api.World, Code + " type");
                stacks.Add(jstack);
            }
        }

        CreativeInventoryStacks =
        [
            new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = ["general", "decorative"] }
        ];
    }

    public virtual MeshData GetOrCreateMesh(string type, string material, string? cachekeyextra = null, ITexPositionSource? overrideTexturesource = null)
    {
        var cMeshes = ObjectCacheUtil.GetOrCreate(api, MeshKey, () => new Dictionary<string, MeshData>());
        ICoreClientAPI capi = (ICoreClientAPI)api;

        string key = type + "-" + material + cachekeyextra;
        if (overrideTexturesource != null || !cMeshes.TryGetValue(key, out var mesh))
        {
            var rcshape = Cshape.Clone();
            rcshape.Base.Path = rcshape.Base.Path.Replace("{type}", type).Replace("{material}", material);
            mesh = capi.TesselatorManager.CreateMesh(
                Code +" block",
                rcshape,
                (shape, name) => new ShapeTextureSource(capi, shape, name, TexturesBSMFA, (p) => p.Replace("{type}", type).Replace("{material}", material)),
                overrideTexturesource
            );

            if (overrideTexturesource == null)
            {
                cMeshes[key] = mesh;
            }
        }

        return mesh;
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

        Dictionary<string, MultiTextureMeshRef> meshRefs;
        meshRefs = ObjectCacheUtil.GetOrCreate(capi, MeshKeyInventory, () => new Dictionary<string, MultiTextureMeshRef>());

        string type = itemstack.Attributes.GetString("type", "");
        string material = itemstack.Attributes.GetString("material", "");
        string key = type + "-" + material;

        if (!meshRefs.TryGetValue(key, out MultiTextureMeshRef? meshref))
        {
            MeshData mesh = GetOrCreateMesh(type, material);
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
    }
    
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

        if (val)
        {
            var bect = world.BlockAccessor.GetBlockEntity(blockSel.Position).GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
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

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        string wood = inSlot.Itemstack.Attributes.GetString("material", "oak");
        dsc.AppendLine(Lang.Get("Material: {0}", Lang.Get("material-" + wood)));
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        return new ItemStack[] { OnPickBlock(world, pos) };
    }

    public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
    {
        var drops = base.GetDropsForHandbook(handbookStack, forPlayer);
        drops[0] = drops[0].Clone();
        drops[0].ResolvedItemstack.SetFrom(handbookStack);

        return drops;
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        var stack = base.OnPickBlock(world, pos);
        var beshelf = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
        if (beshelf != null)
        {
            stack.Attributes.SetString("type", beshelf.Type);
            stack.Attributes.SetString("material", beshelf.Material);
        }

        return stack;
    }

    public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
    {
        var bect = GetBlockEntity<BlockEntityGeneric>(blockSel.Position)?.GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
        bect?.Rotate(byEntity, blockSel, dir);
    }
}
