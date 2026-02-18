using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BEBehaviorMannequin : BEBehaviorRotatablePlaceable, IInteractable, ITexPositionSource, IRotatablePlaceable
{
    public InventoryBase Inventory { get; set; }
    public Size2i AtlasSize => capi!.BlockTextureAtlas.Size;


    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            if (textureCode == "seraph")
            {
                capi!.BlockTextureAtlas.GetOrInsertTexture(ttas, out _, out var texpos);
                return texpos;
            }

            return capi!.BlockTextureAtlas.Positions[collectedTextures![textureCode].Baked.TextureSubId];
        }
    }

    protected static AssetLocation ttas = new AssetLocation("block/transparent");
    protected InWorldContainer container;
    protected string InventoryClassName = "mannequin";
    protected ICoreClientAPI? capi;
    protected MeshData? mesh;
    protected IDictionary<string, CompositeTexture>? collectedTextures;
    protected Vec3f? shapeOffset;
    protected string[] allowedDresstypes;

    protected int[] placementOrder;
    protected int placementIndex;

    public BEBehaviorMannequin(BlockEntity blockentity) : base(blockentity)
    {
        allowedDresstypes = Block.Attributes?["allowedDresstypes"].AsArray<string>();
        if (allowedDresstypes == null)
        {
            Api.Logger.Warning("Mannequin block " + Block.Code + " has no allowed dress types.");
            allowedDresstypes = [];
        }

        int qslots = allowedDresstypes?.Length ?? 0;
        placementOrder = new int[qslots].Fill(-1);
        placementIndex = 0;
        Inventory = new InventoryGeneric(qslots, InventoryClassName+"-0", null, (id,inv) => new ItemSlotWearable(inv, [allowedDresstypes[id]]));
        container = new InWorldContainer(() => Inventory, "inventory");
    }

    CompositeShape? dressableEntityShape;

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        capi = api as ICoreClientAPI;

        dressableEntityShape = properties["dressableEntityShape"].AsObject<CompositeShape>();
        shapeOffset = properties["shapeOffset"]?.AsObject<Vec3f>();

        var inventoryClassName = InventoryClassName + "-" + Pos;
        Inventory.LateInitialize(inventoryClassName, api);
        Inventory.Pos = Pos;
        container.Init(Api, () => Pos, () => Blockentity.MarkDirty(true));
        regenMesh();
    }
    

    protected virtual void regenMesh()
    {
        if (dressableEntityShape == null || capi == null) return;

        var loc = dressableEntityShape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        var asset = capi.Assets.TryGet(loc);
        if (asset == null)
        {
            capi.World.Logger.Warning("Mannequin block "+Block.Code+" dressable shape " + loc + " not found.");
            return;
        }
        Shape shape = asset.ToObject<Shape>();

        string[] willDeleEles = new string[0];
        addGearToShape(shape, dressableEntityShape.Base, ref willDeleEles);

        capi.Tesselator.TesselateShape(new TesselationMetaData()
        {
            TexSource = this,
            IgnoreElements = willDeleEles
        }, shape, out mesh);

        for (int i = 0; i < mesh.RenderPassCount; i++) mesh.RenderPassesAndExtraBits[i] = (int)EnumChunkRenderPass.BlendNoCull;

        if (shapeOffset != null) mesh.Translate(shapeOffset);
    }


    protected virtual Shape addGearToShape(Shape entityShape, string shapePathForLogging, ref string[] willDeleteElements)
    {
        IInventory inv = Inventory;
        if (inv == null || inv.Empty) return entityShape;

        collectedTextures = new Dictionary<string, CompositeTexture>();

        foreach (var gearslot in inv)
        {
            if (gearslot.Empty) continue;
            var iatta = IAttachableToEntity.FromCollectible(gearslot.Itemstack.Collectible);
            if (iatta == null || !iatta.IsAttachable(null, gearslot.Itemstack)) return entityShape;

            string slotCode = "default";
            EntityBehaviorContainer.addGearToShape(capi!, null, capi!.BlockTextureAtlas, entityShape, gearslot.Itemstack, iatta, slotCode, shapePathForLogging, ref willDeleteElements, collectedTextures, null);
        }

        return entityShape;
    }

    public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        if (byPlayer.Entity.Controls.ShiftKey)
        {
            int i = 0;
            var stackMoved = false;
            while (i < placementOrder.Length)
            {
                var index = placementOrder[GameMath.Mod(placementIndex - i - 1, placementOrder.Length)];
                i++;
                if (index == -1) break;

                var slot = Inventory[index];
                if (!slot.Empty)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                    {
                        Api.World.SpawnItemEntity(slot.Itemstack, Pos);
                    }
                    slot.Itemstack = null;
                    stackMoved = true;
                    break;
                }
            }

            if (!stackMoved)
            {
                var slot = Inventory.FirstNonEmptySlot;
                if (slot != null)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                    {
                        Api.World.SpawnItemEntity(slot.Itemstack, Pos);
                    }
                    slot.Itemstack = null;
                }                
            }
        }
        else
        {
            var hslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (!hslot.Empty)
            {
                var cat = hslot.Itemstack.ItemAttributes?["clothescategory"].AsString();

                if (hslot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is IWearableStatsSupplier wearableStats)
                {
                    cat = wearableStats.GetDressType(hslot).ToString().ToLowerInvariant();
                }

                if (!allowedDresstypes.Contains(cat)) return false;
                int i = 0;
                foreach (var slot in Inventory)
                {
                    if (slot.Empty)
                    {
                        if (hslot.TryPutInto(Api.World, slot) > 0)
                        {
                            placementOrder[placementIndex] = i;
                            placementIndex = (placementIndex + 1) % placementOrder.Length;
                            break;
                        }
                    }
                    i++;
                }
            }
        }
        regenMesh();
        Blockentity.MarkDirty(true);
        return true;
    }

    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        base.OnBlockBroken(byPlayer);
        Inventory.DropAll(Pos.ToVec3d().AddCopy(0.5, 0.5, 0.5));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        container.FromTreeAttributes(tree, worldAccessForResolve);
        placementIndex = tree.GetInt("placementIndex");
        placementOrder = (tree["placementOrder"] as IntArrayAttribute)?.value ?? new int[Inventory.Count].Fill(-1);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        container.ToTreeAttributes(tree);
        tree.SetInt("placementIndex", placementIndex);
        tree["placementOrder"] = new IntArrayAttribute(placementOrder);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        var tfMatrix = new Matrixf().Translate(0.5f, 0, 0.5f).RotateY(MeshAngleRad + (Block.GetBehavior<BlockBehaviorRotateablePlaceable>()?.OffsetRad ?? 0)).Translate(-0.5f, 0, -0.5f).Values;

        if (!Inventory.Empty)
        {
            mesher.AddMeshData(mesh, tfMatrix);
        }

        mesher.AddMeshData(capi!.TesselatorManager.GetDefaultBlockMesh(Block), tfMatrix);

        return true;
    }
}
