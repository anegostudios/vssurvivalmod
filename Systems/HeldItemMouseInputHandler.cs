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
    /// <summary>
    /// The rough change in time since last called.
    /// </summary>
    public int Delta { get; set; }

    /// <summary>
    /// The precise change in time since last called.
    /// </summary>
    public float DeltaPrecise { get; set; }

    /// <summary>
    /// The rough change in value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// The precise change in value.
    /// </summary>
    public float ValuePrecise { get; set; }
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
            Delta = args.delta,
            DeltaPrecise = args.deltaPrecise,
            Value = args.value,
            ValuePrecise = args.valuePrecise
        };

        clientChannel?.SendPacket(packet);
    }



    private void PacketHandler(IServerPlayer player, HeldItemMouseWheelEventPacket packet)
    {
        if (player.Entity is not { ActiveHandItemSlot: { } activeSlot } playerEntity ||
            activeSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldItemOnMouseWheel>() is not { } objInterface)
        {
            return;
        }

        MouseWheelEventArgs args = new()
        {
            delta = packet.Delta,
            deltaPrecise = packet.DeltaPrecise,
            value = packet.Value,
            valuePrecise = packet.ValuePrecise
        };

        objInterface.OnMouseWheel(playerEntity, activeSlot, args);
    }
}
