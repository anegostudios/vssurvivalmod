using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;


public class ItemDoorKey : Item
{
    public override bool OnPlacementBySchematicCustom(ICoreServerAPI sapi, ItemSlot slot, ITreeAttribute schematicTreeData)
    {
        if (slot.Itemstack == null) return false;

        var roomCode = slot.Itemstack.Attributes.GetString("roomCode");
        var keyId = schematicTreeData.GetString("keyId");
        if (roomCode == null)
        {
            sapi.Logger.Warning($"ItemDoorKey: roomCode is null for keyId: {keyId}");
            return false;
        }

        var keyCountTree = schematicTreeData.GetTreeAttribute("keyCountByRoomCode");
        var count = keyCountTree?.GetInt(roomCode);
        if (keyCountTree != null)
        {
            if (count > 0)
            {
                keyCountTree.SetInt(roomCode, count.Value - 1);
                slot.Itemstack.Attributes.SetString("keyId", keyId);
                return true;
            }

            slot.Itemstack = null;
            return false;
        }
        slot.Itemstack.Attributes.SetString("keyId", keyId);
        return false;
    }
}
