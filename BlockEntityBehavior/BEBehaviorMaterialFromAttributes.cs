using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BEBehaviorMaterialFromAttributes : BlockEntityBehavior, IRotatable
{
    private MeshData? mesh;

    private float[] mat = null!;
    public string? Material { get; private set; }

    public float MeshAngleY { get; set; }
    public float MeshAngleX { get; set; }
    public float MeshAngleZ { get; set; }

    public BEBehaviorMaterialFromAttributes(BlockEntity blockEntity) : base(blockEntity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        if (mesh == null)
        {
            Init();
        }
    }

    public void Init()
    {
        if (Api == null || Material == null || !(Block is BlockMaterialFromAttributes)) return;

        if (Api.Side == EnumAppSide.Client)
        {
            mesh = (Block as BlockMaterialFromAttributes)?.GetOrCreateMesh(Material);
            mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).Rotate(MeshAngleX, MeshAngleY, MeshAngleZ)
                .Translate(-0.5f, -0.5f, -0.5f)
                .Values;
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetString("material", Material);
        tree.SetFloat("meshAngleRad", MeshAngleY);
        tree.SetFloat("meshAngleX", MeshAngleX);
        tree.SetFloat("meshAngleZ", MeshAngleZ);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        Material = tree.GetString("material");
        MeshAngleY = tree.GetFloat("meshAngleRad");
        MeshAngleX = tree.GetFloat("meshAngleX");
        MeshAngleZ = tree.GetFloat("meshAngleZ");
        Init();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        Material ??= byItemStack?.Attributes.GetString("material");
        Init();
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        mesher.AddMeshData(mesh, mat);
        return true;
    }

    public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation,
        Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
    {
        MeshAngleY = tree.GetFloat("meshAngleRad");
        MeshAngleY -= degreeRotation * GameMath.DEG2RAD;
        tree.SetFloat("meshAngleRad", MeshAngleY);
    }

    public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
    {
        if (byEntity.Controls.ShiftKey)
        {
            if (blockSel.Face.Axis == EnumAxis.X)
            {
                MeshAngleX += GameMath.PIHALF * dir;
            }

            if (blockSel.Face.Axis == EnumAxis.Y)
            {
                MeshAngleY += GameMath.PIHALF * dir;
            }

            if (blockSel.Face.Axis == EnumAxis.Z)
            {
                MeshAngleZ += GameMath.PIHALF * dir;
            }
        }
        else
        {
            float deg22dot5rad = GameMath.PIHALF / 4;
            MeshAngleY += deg22dot5rad * dir;
        }

        Init();
        Blockentity.MarkDirty(true);
    }
}