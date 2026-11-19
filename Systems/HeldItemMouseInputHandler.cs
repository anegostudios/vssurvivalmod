using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public interface IHeldItemOnMouseWheel
{
    void OnMouseWheel(EntityPlayer byPlayer, ItemSlot inSlot, MouseWheelEventArgs args);
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class HeldItemMouseWheelEventPacket
{
    [Obsolete("Please use DeltaYInt instead.")]
    public int Delta { get => (int)DeltaY; set => DeltaY = value; }
    [Obsolete("Please use DeltaY instead.")]
    public float DeltaPrecise { get => DeltaY; set => DeltaY = value; }
    [Obsolete("Use IsFresh to determine if this is the first event of a continuous scroll instead.")]
    public int Value { get => (int)ValuePrecise; set => ValuePrecise = value; }
    [Obsolete("Use IsFresh to determine if this is the first event of a continuous scroll instead.")]
    public float ValuePrecise { get; set; }
    /// <summary>
    /// The X delta of the scroll.
    /// </summary>
    public float DeltaX { get; set; }

    /// <summary>
    /// The Y delta of the scroll.
    /// </summary>
    public float DeltaY { get; set; }

    /// <summary>
    /// The X delta of the scroll as an int, accumulated every 50ms to simulate sensitivity.
    /// </summary>
    public int DeltaXInt { get; set; }

    /// <summary>
    /// The X delta of the scroll as an int, accumulated every 50ms to simulate sensitivity.
    /// </summary>
    public int DeltaYInt { get; set; }

    /// <summary>
    /// Whether or not this is the first event to trigger in the last 50ms.
    /// </summary>
    public bool IsFresh { get; set; }
}

public sealed class HeldItemMouseInputHandler : ModSystem
{
    private ICoreClientAPI? clientApi;
    private const string networkChannelId = "HeldItemMouseInputHandler";
    private IClientNetworkChannel? clientChannel;



    public override void StartClientSide(ICoreClientAPI api)
    {
        clientApi = api;
        clientChannel = api.Network.RegisterChannel(networkChannelId)
            .RegisterMessageType<HeldItemMouseWheelEventPacket>();

        clientApi.Event.MouseWheelMove += OnMouseWheel;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Network.RegisterChannel(networkChannelId)
            .RegisterMessageType<HeldItemMouseWheelEventPacket>()
            .SetMessageHandler<HeldItemMouseWheelEventPacket>(PacketHandler);
    }

    public void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (clientApi?.World.Player.Entity is not { ActiveHandItemSlot: { } activeSlot } playerEntity ||
            activeSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldItemOnMouseWheel>() is not { } objInterface)
        {
            return;
        }

        objInterface.OnMouseWheel(playerEntity, activeSlot, args);
        if (!args.IsHandled) return;

        HeldItemMouseWheelEventPacket packet = new()
        {
#pragma warning disable CS0618 // We will remove this in 1.24 or 1.25 hopefully, but this keeps old mods mostly working for now
            ValuePrecise = args.valuePrecise,
#pragma warning restore CS0618
            DeltaX = args.DeltaX,
            DeltaY = args.DeltaY,
            DeltaXInt = args.DeltaXInt,
            DeltaYInt = args.DeltaYInt,
            IsFresh = args.IsFresh
        };

        clientChannel?.SendPacket(packet);
    }



    private void PacketHandler(IServerPlayer player, HeldItemMouseWheelEventPacket packet)
    {
        if (player.Entity is not EntityPlayer playerEntity) return;

        var activeSlot = playerEntity.ActiveHandItemSlot;
        var objInterface = activeSlot?.Itemstack?.Collectible.GetCollectibleInterface<IHeldItemOnMouseWheel>();
        if (objInterface == null) return;

        MouseWheelEventArgs args = new()
        {
#pragma warning disable CS0618 // We will remove this in 1.24 or 1.25 hopefully, but this keeps old mods mostly working for now
            delta = packet.Delta,
            deltaPrecise = packet.DeltaPrecise,
            value = packet.Value,
            valuePrecise = packet.ValuePrecise,
#pragma warning restore CS0618
            DeltaX = packet.DeltaX,
            DeltaY = packet.DeltaY,
            DeltaXInt = packet.DeltaXInt,
            DeltaYInt = packet.DeltaYInt,
            IsFresh = packet.IsFresh
        };

        objInterface.OnMouseWheel(playerEntity, activeSlot, args);
    }
}
