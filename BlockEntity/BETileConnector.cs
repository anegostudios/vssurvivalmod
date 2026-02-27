using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent;

public class BETileConnector : BlockEntity, IRotatable
{
    public string Name = "";
    public string Target = "";
    public BlockFacing Direction = BlockFacing.NORTH;

    public static float[][] tfMatrix;

    static BETileConnector() {
        tfMatrix = new float[6][];
        for (int i = 0; i < 4; i++)
        {
            tfMatrix[i] = new Matrixf().Translate(0.5f, 0f, 0.5f).RotateYDeg(-i * 90f).Translate(-0.5f, 0f, -0.5f).Values;
        }

        tfMatrix[4] = new Matrixf().Translate(0.5f, 0.5f, 0.5f).RotateXDeg(90).Translate(-0.5f, -0.5f, -0.5f).Values;
        tfMatrix[5] = new Matrixf().Translate(0.5f, 0.5f, 0.5f).RotateXDeg(-90).Translate(-0.5f, -0.5f, -0.5f).Values;
    }

    public void OnInteract(IPlayer byPlayer)
    {
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

        if (Api is ICoreClientAPI api)
        {
            var dlg = new GuiDialogTiledDungeon("Dungeon Tile Constraint", Name, Target, api);
            dlg.TryOpen();
            dlg.OnClosed += () => DidCloseDialog(dlg, api);
        }
    }

    private void DidCloseDialog(GuiDialogTiledDungeon dialog, ICoreClientAPI capi)
    {
        var attr = dialog.Attributes;
        if (attr.GetInt("save") == 0) return;

        using var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        attr.ToBytes(writer);

        capi.Network.SendBlockEntityPacket(Pos, 0, ms.ToArray());
    }

    public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
    {
        if (packetid != 0) return;

        var tree = new TreeAttribute();
        tree.FromBytes(data);
        Target = tree.GetString("target","");
        Name = tree.GetString("name", "");

        MarkDirty();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("name", Name);
        tree.SetString("target", Target);
        tree.SetInt("direction", Direction.Index);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        Name = tree.GetString("name", "");
        Target = tree.GetString("target", "");
        Direction = BlockFacing.ALLFACES[tree.GetInt("direction")];
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        var mesh = (Api as ICoreClientAPI)!.TesselatorManager.GetDefaultBlockMesh(Block);
        mesher.AddMeshData(mesh, tfMatrix[Direction.Index]);
        return true;
    }

    public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
    {
        var dirIndex = tree.GetAsInt("direction");

        var face = BlockFacing.ALLFACES[dirIndex];
        var rotated = face.GetHorizontalRotated(degreeRotation);
        tree.SetInt("direction", rotated.Index);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine("Name:" + Name);
        dsc.AppendLine("Schematics connect " + Direction.ToString() + " of this block");
        base.GetBlockInfo(forPlayer, dsc);
    }
}
