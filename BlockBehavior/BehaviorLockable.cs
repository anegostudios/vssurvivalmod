using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Systems;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to be locked by a player with a padlock, preventing any interaction from any other players.
    /// Uses the "lockable" code. This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "Lockable"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorLockable : BlockBehavior
    {
        public BlockBehaviorLockable(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            ModSystemBlockReinforcement bre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            if (bre.IsLockedForInteract(blockSel.Position, byPlayer))
            {
                if (world.Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "locked", Lang.Get("ingameerror-locked"));
                }

                handling = EnumHandling.PreventSubsequent;
                return false;
            }

            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            var bed = blockEntity?.GetBehavior<BEBehaviorDoor>();
            if (bed?.StoryLockedCode != null)
            {
                // only open client side
                if (world.Api is not ICoreClientAPI capi)
                {
                    handling = EnumHandling.PreventSubsequent;
                    return false;
                }
                handling = EnumHandling.Handled;

                var stl = capi.ModLoader.GetModSystem<StoryLockableDoor>();
                if (stl.StoryLockedLocationCodes.TryGetValue(bed.StoryLockedCode, out var list) && list.Contains(byPlayer.PlayerUID))
                {
                    return true;
                }
                capi.TriggerIngameError(this, "locked", Lang.Get("ingameerror-locked"));

                handling = EnumHandling.PreventSubsequent;
                return false;
            }

            if (bed?.LockedByKey == true)
            {
                var handItemStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
                var capi = world.Api as ICoreClientAPI;
                if (handItemStack?.Item is ItemDoorKey)
                {
                    var doorKeyRoomCode = handItemStack.Attributes.GetString("roomCode");
                    var doorKeyId = handItemStack.Attributes.GetString("keyId");
                    if (bed.RoomCode == doorKeyRoomCode && bed.KeyId == doorKeyId)
                    {
                        world.PlaySoundAt(new SoundAttributes("sounds/tool/padlock.ogg", false), byPlayer, byPlayer);
                        bed.LockedByKey = false;
                        blockEntity.MarkDirty();
                        handling = EnumHandling.PreventSubsequent;
                        return true;
                    }

                    capi?.TriggerIngameError(this, "wrongkey", Lang.Get("ingameerror-locked-wrongkey"));
                    handling = EnumHandling.PreventSubsequent;
                    return false;
                }

                capi?.TriggerIngameError(this, "locked", Lang.Get("ingameerror-locked"));

                handling = EnumHandling.PreventSubsequent;
                return false;
            }

            var bedbl = blockEntity?.GetBehavior<BEBehaviorDoorBarLock>();
            if (bedbl != null)
            {
                return bedbl.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }
    }
}
