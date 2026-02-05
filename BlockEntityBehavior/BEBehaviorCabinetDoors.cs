using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class TypedTextureSource : ITexPositionSource
{
    protected ICoreClientAPI capi;
    protected ITextureAtlasAPI atlas;
    protected string domain;
    public Dictionary<string, string> materials;
    public Dictionary<string, string> textureMapping;

    public TypedTextureSource(ICoreClientAPI capi, ITextureAtlasAPI atlas, string defaultDomain, Dictionary<string, string> textureMapping, Dictionary<string, string> materials)
    {
        this.capi = capi;
        this.atlas = atlas;
        this.domain = defaultDomain;
        this.textureMapping = textureMapping;
        this.materials = materials;
    }

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            var texturePath = textureMapping[textureCode];
            foreach (var mat in materials)
            {
                texturePath = texturePath.Replace("{" + mat.Key + "}", mat.Value);
            }
            var loc = AssetLocation.Create(texturePath, domain);
            atlas.GetOrInsertTexture(loc, out _, out var texPos);
            return texPos;
        }
    }


    public Size2i AtlasSize => atlas.Size;
}

public class BlockBehaviorCabinetDoors : StrongBlockBehavior
{
    public Cuboidf[]? DoorSelectionBoxOpened;
    public Cuboidf[]? DoorSelectionBoxClosed;
    public string?[]? DoorElements;

    public BlockBehaviorCabinetDoors(Block block) : base(block)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        DoorSelectionBoxOpened = properties["selectionBoxesOpened"].AsObject<Cuboidf[]>();
        DoorSelectionBoxClosed = properties["selectionBoxesClosed"].AsObject<Cuboidf[]>();


        if (DoorSelectionBoxClosed == null) throw new NullReferenceException("selectionBoxesClosed cannot be null! Block code " + block.Code);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
    {
        handled = EnumHandling.Handled;
        var be = blockAccessor.GetBlockEntity(pos);
        if (be == null) return base.GetSelectionBoxes(blockAccessor, pos, ref handled);

        return be.GetBehavior<BEBehaviorCabinetDoors>().GetSelectionBoxes();
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
    {
        if (selection.SelectionBoxId == null)
        {
            var doorStacks = ObjectCacheUtil.GetOrCreate(world.Api, "cabinetDoors", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();
                foreach (var collObj in world.Collectibles)
                {
                    if (collObj.Code.PathStartsWith("cabinetdoor"))
                    {
                        stacks.Add(new ItemStack(collObj));
                    }
                }
                return stacks.ToArray();
            });

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling).Append(new WorldInteraction()
            {
                ActionLangCode = "addcabinetdoors",
                HotKeyCode = "sprint",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = doorStacks
            });
        }

        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);
    }
}

public class BEBehaviorCabinetDoors : BEBehaviorAnimatable, IInteractable
{
    protected bool opened = false;
    protected Cuboidf[]? selectionBoxesOpened;
    protected Cuboidf[]? selectionBoxesClosed;
    protected ICoreClientAPI? capi;
    protected ItemStack? doorStack;
    public BEBehaviorCabinetDoors(BlockEntity blockentity) : base(blockentity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        capi = api as ICoreClientAPI;
        updateAnimationState();
    }

    private void updateAnimationState()
    {
        if (!opened) animUtil?.StopAnimation("opened");
        else animUtil?.StartAnimation(new AnimationMetaData() { Animation = "opened", Code = "opened", AnimationSpeed = 0.7f, EaseOutSpeed = 4 });
    }

    public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (doorStack == null)
        {
            if (byPlayer.Entity.Controls.CtrlKey)
            {
                var slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
                var titem = slot?.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorTypedTexture>(true);
                if (titem != null)
                {
                    if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        doorStack = slot.Itemstack.Clone();
                        doorStack.StackSize = 1;
                    } else
                    {
                        doorStack = slot.TakeOut(1);
                    }

                    slot.MarkDirty();
                    updateAnimationState();
                    handling = EnumHandling.PreventSubsequent;
                    Blockentity.MarkDirty(true);
                    return true;
                }
            }

            return false;
        }

        if (blockSel.SelectionBoxId == "door")
        {
            opened = !opened;
            updateAnimationState();
            handling = EnumHandling.PreventDefault;
            return true;
        }

        return false;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        opened = tree.GetBool("opened");

        var doorStackWasNull = doorStack == null;
        doorStack = tree.GetItemstack("doorStack");
        doorStack?.ResolveBlockOrItem(worldAccessForResolve);

        if (doorStack != null && doorStackWasNull && worldAccessForResolve.Side == EnumAppSide.Client)
        {
            Blockentity.MarkDirty(true);
        }
        updateAnimationState();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBool("opened", opened);
        tree.SetItemstack("doorStack", doorStack);
    }

    public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
    {
        if (doorStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings) == false)
        {
            doorStack = null;
        }
        else
        {
            doorStack?.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(doorStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }
    }

    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
        doorStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(doorStack), blockIdMapping, itemIdMapping);
    }

    MeshData doorMesh;

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        if (doorStack == null) return false;

        ensureMeshExists(tessThreadTesselator);

        if (animUtil.activeAnimationsByAnimCode.Count > 0 || (animUtil.animator != null && animUtil.animator.ActiveAnimationCount > 0)) return false;

        float angleRad = Blockentity.GetBehavior<IRotatablePlaceable>()?.MeshAngleRad ?? 0;
        var mat =
            new Matrixf()
           .Translate(0.5f, 0, 0.5f)
            .RotateYDeg(Block.Shape.rotateY)
            .RotateY(angleRad)
            .Translate(-0.5f, 0, -0.5f)
            .Values
        ;

        mesher.AddMeshData(doorMesh, mat);
        return false;
    }


    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        if (doorStack != null)
        {
            Api.World.SpawnItemEntity(doorStack, Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            doorStack = null;
            Blockentity.MarkDirty(true);
        }
    }

    private void ensureMeshExists(ITesselatorAPI tesselator)
    {
        var titem = doorStack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorTypedTexture>(true);

        if (titem == null || capi == null || doorStack == null) return;
        if (doorMesh != null) return;

        // I'm not sure how to best do this in a way that is not hardcoded
        AssetLocation shapeLoc = titem.CompositeShape.Base.Clone();
        shapeLoc.Path = shapeLoc.Path.Replace("middle", Block.Variant["section"]);
        var shape = capi.Assets.Get<Shape>(shapeLoc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));

        var dictKey = "cabinetdoors-" + Block.Code;
        var rot = new Vec3f(0, GameMath.RAD2DEG * Blockentity.GetBehavior<IRotatablePlaceable>()?.MeshAngleRad ?? 0, 0);
        
        string animkey = Block.Shape.ToString();

        var materials = titem.GetMaterials(doorStack);
        materials["section"] = Block.Variant["section"];
        var texSource = new TypedTextureSource(capi, capi.BlockTextureAtlas, doorStack!.Collectible.Code.Domain, titem.TextureMapping, materials);

        doorMesh = animUtil.CreateMesh(animkey, shape, out Shape animatableShape, texSource, new TesselationMetaData() {  });
        animUtil.InitializeAnimator(dictKey, doorMesh, animatableShape, rot);
        updateAnimationState();
    }

    public Cuboidf[]? GetSelectionBoxes()
    {
        if (doorStack == null) return Array.Empty<Cuboidf>();

        if (selectionBoxesClosed == null)
        {
            selectionBoxesOpened = rotatedCopy(Block.GetBehavior<BlockBehaviorCabinetDoors>().DoorSelectionBoxOpened);
            selectionBoxesClosed = rotatedCopy(Block.GetBehavior<BlockBehaviorCabinetDoors>().DoorSelectionBoxClosed);
        }

        return opened ? selectionBoxesOpened : selectionBoxesClosed;
    }

    private Cuboidf[]? rotatedCopy(Cuboidf[]? boxes)
    {
        float angleRad = Blockentity.GetBehavior<IRotatablePlaceable>()?.MeshAngleRad ?? 0;

        var rotCubes = new CuboidfWithId[boxes.Length];
        for (int i = 0; i < boxes.Length; i++)
        {
            rotCubes[i] = new CuboidfWithId(boxes[i].RotatedCopyRad(0, angleRad, 0, new Vec3d(0.5, 0, 0.5))) { Id = "door" };
        }

        return rotCubes;
    }
}
