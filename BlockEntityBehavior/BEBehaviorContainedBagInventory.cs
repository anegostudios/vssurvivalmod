using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class BEBehaviorContainedBagInventory : BlockEntityBehavior
{
    public const int PacketIdBitShift = 16;    // magic number; see also IClientNetworkAPI.SendEntityPacketWithOffset() which enables such tricks

    readonly BlockEntityContainer containerEntity;
    public BlockEntityContainedBagWorkspace[] BagInventories { get; private set; }

    public BEBehaviorContainedBagInventory(BlockEntity blockEntity) : base(blockEntity)
    {
        containerEntity = (BlockEntityContainer)blockEntity;
        BagInventories = new BlockEntityContainedBagWorkspace[containerEntity.Inventory.Count];
        containerEntity.Inventory.SlotModified += ContainerInv_SlotModified;
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        for (int i = 0; i < containerEntity.Inventory.Count; i++)
        {
            BagInventories[i] ??= new BlockEntityContainedBagWorkspace(containerEntity, i);
        }
    }

    private void ContainerInv_SlotModified(int slotid)
    {
        if (containerEntity.Inventory[slotid] is not ItemSlot bagSlot ||
            !BagInventories[slotid].TryLoadBagInv(bagSlot, bagSlot.Itemstack?.Collectible.GetBehavior<CollectibleBehaviorGroundStoredHeldBag>()))
        {
            BagInventories[slotid].Dispose();
        }
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        int targetSlotId = packetid >> PacketIdBitShift;
        int first15Bits = (1 << PacketIdBitShift) - 1;
        packetid = packetid & first15Bits;

        BagInventories[targetSlotId].OnReceivedClientPacket(player, packetid, data);
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        int targetSlotId = packetid >> PacketIdBitShift;
        int first15Bits = (1 << PacketIdBitShift) - 1;
        packetid = packetid & first15Bits;

        BagInventories[targetSlotId].OnReceivedServerPacket(packetid, data);
    }

    public virtual void Dispose()
    {
        for (int i = 0; i < BagInventories.Length; i++)
        {
            BagInventories[i]?.Dispose();
        }

        containerEntity.Inventory.SlotModified -= ContainerInv_SlotModified;
    }
}

public class BlockEntityContainedBagWorkspace
{

    GuiDialogBlockEntityInventory? invDialog;
    BlockEntityContainer be;
    BagInventory bagInv;
    InventoryGeneric wrapperInv;

    string dialogTitleLangCode = string.Empty;
    SoundAttributes? OpenSound;
    SoundAttributes? CloseSound;

    int slotId = -1;

    public BlockEntityContainedBagWorkspace(BlockEntityContainer be, int bagSlot)
    {
        this.be = be;
        slotId = bagSlot;
        bagInv = new BagInventory(be.Api, [be.Inventory[bagSlot]]);
        wrapperInv = new InventoryGeneric(be.Api);
    }

    public bool TryLoadBagInv(ItemSlot bagSlot, CollectibleBehaviorGroundStoredHeldBag? bagBh)
    {
        if (bagBh == null || bagSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>() == null) return false;

        dialogTitleLangCode = bagBh.DialogTitleLangCode;
        OpenSound = bagBh.OpenSound;
        CloseSound = bagBh.CloseSound;

        bagInv.ReloadBagInventory(wrapperInv, [bagSlot]);
        wrapperInv.Init(bagInv.Count, "blockcontainedbaginv", "blockcontainedbag-slot" + slotId + "-" + be.Pos, onNewBagSlot);

        if (be.Api.World.Side == EnumAppSide.Server)
        {
            wrapperInv.SlotModified += BagInv_SlotModified;
        }

        return true;
    }

    private ItemSlot onNewBagSlot(int slotId, InventoryGeneric self)
    {
        return bagInv[slotId];
    }

    private void BagInv_SlotModified(int slotid)
    {
        var slot = wrapperInv[slotid];
        bagInv.SaveSlotIntoBag((ItemSlotBagContent)slot);
    }

    protected void OnBagInvDialogClosed(IPlayer player)
    {
        // This is already handled elsewhere and also causes a stackoverflowexception, but seems needed somehow?
        var inv = invDialog;
        invDialog = null; // Weird handling because to prevent endless recursion
        if (inv?.IsOpened() == true) inv?.TryClose();
        inv?.Dispose();
    }

    public bool OpenHeldBag(IPlayer byPlayer)
    {
        if (be.Api.World is IServerWorldAccessor)
        {
            var data = BlockEntityContainerOpen.ToBytes("BlockEntityInventory", dialogTitleLangCode, 4, wrapperInv);
            ((ICoreServerAPI)be.Api).Network.SendBlockEntityPacket(
                (IServerPlayer)byPlayer,
                be.Pos,
                (int)EnumBlockContainerPacketId.OpenInventory + (slotId << BEBehaviorContainedBagInventory.PacketIdBitShift),
                data
            );
        }

        return true;
    }

    public void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        if (be.Inventory[slotId].Itemstack?.Collectible is not CollectibleObject obj ||
            obj.GetBehavior<CollectibleBehaviorGroundStoredHeldBag>() == null ||
            obj.GetCollectibleInterface<IHeldBag>() == null)
        {
            return;
        }

        if (!be.Api.World.Claims.TryAccess(player, be.Pos, EnumBlockAccessFlags.Use))
        {
            be.Api.World.Logger.Audit("Player {0} sent an inventory packet to ground stored held bag at {1} but has no claim access. Rejected.", player.PlayerName, be.Pos);
            return;
        }

        if (packetid < 1000)
        {
            wrapperInv.InvNetworkUtil.HandleClientPacket(player, packetid, data);

            // Tell server to save this chunk to disk again
            be.Api.World.BlockAccessor.GetChunkAtBlockPos(be.Pos).MarkModified();

            return;
        }

        if (packetid == (int)EnumBlockEntityPacketId.Close)
        {
            player.InventoryManager?.CloseInventory(wrapperInv);
        }

        if (packetid == (int)EnumBlockEntityPacketId.Open)
        {
            player.InventoryManager?.OpenInventory(wrapperInv);
        }
    }

    public void OnReceivedServerPacket(int packetid, byte[] data)
    {
        if (be.Inventory[slotId].Itemstack?.Collectible is not CollectibleObject obj ||
            obj.GetBehavior<CollectibleBehaviorGroundStoredHeldBag>() == null ||
            obj.GetCollectibleInterface<IHeldBag>() == null)
        {
            return;
        }

        IClientWorldAccessor clientWorld = (IClientWorldAccessor)be.Api.World;

        if (packetid == (int)EnumBlockContainerPacketId.OpenInventory)
        {
            if (invDialog != null)
            {
                if (invDialog?.IsOpened() == true) invDialog.TryClose();
                invDialog?.Dispose();
                invDialog = null;
                return;
            }

            var blockContainer = BlockEntityContainerOpen.FromBytes(data);
            wrapperInv.FromTreeAttributes(blockContainer.Tree);
            wrapperInv.ResolveBlocksOrItems();

            invDialog = new (Lang.Get(blockContainer.DialogTitle), wrapperInv, be.Pos, slotId << BEBehaviorContainedBagInventory.PacketIdBitShift, blockContainer.Columns, be.Api as ICoreClientAPI)
            {
                OpenSound = OpenSound ?? new SoundAttributes(),
                CloseSound = CloseSound ?? new SoundAttributes()
            };

            if (invDialog.TryOpen())
            {
                clientWorld.Player.InventoryManager.OpenInventory(wrapperInv);
                (be.Api as ICoreClientAPI)?.Network?.SendBlockEntityPacket(be.Pos, (int)EnumBlockEntityPacketId.Open + invDialog.packetIdOffset, null);
            }

            invDialog.OnClosed += () => OnBagInvDialogClosed(clientWorld.Player);
        }

        if (packetid == (int)EnumBlockEntityPacketId.Close)
        {
            clientWorld.Player.InventoryManager.CloseInventoryAndSync(wrapperInv);
            if (invDialog?.IsOpened() == true) invDialog?.TryClose();
            invDialog?.Dispose();
            invDialog = null;
        }
    }

    public virtual void Dispose()
    {
        if (invDialog?.IsOpened() == true) invDialog?.TryClose();
        invDialog?.Dispose();

        dialogTitleLangCode = string.Empty;
        OpenSound = null;
        CloseSound = null;

        if (be.Api.Side == EnumAppSide.Server)
        {
            wrapperInv?.openedByPlayerGUIds?.Clear();
        }
    }
}
