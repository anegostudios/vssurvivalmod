using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;


namespace Vintagestory.GameContent
{
    /// <summary>
    /// Collectible that flips between items on hotkey inside character inventory
    /// </summary>
    public interface IFlippableWearable
    {
        /// <summary>
        /// Flips item code in provided slot to next in chain of items
        /// </summary>
        bool Flip(Entity wearer, ItemSlot wearableSlot);

        /// <summary>
        /// Flips item code in provided slot to default state
        /// </summary>
        bool Unflip(Entity wearer, ItemSlot wearableSlot);
    }

    /// <summary>
    /// Provides hotkeys to activate <see cref="IFlippableWearable"/> inside character inventory of the played pressed the hotkey
    /// </summary>
    public class ModSystemFlippableWearable : ModSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private sealed class WearableFlipPacket { }


        private IClientNetworkChannel? clientChannel;
        private IServerNetworkChannel? serverChannel;
        private ICoreClientAPI? clientApi;



        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Input.RegisterHotKey("flip-armor-bit", "Rise/Lower visor", GlKeys.U);
            api.Input.SetHotKeyHandler("flip-armor-bit", TryFlipClientSide);

            clientChannel = api.Network.RegisterChannel("flippable-wearable-hotkey")
                .RegisterMessageType<WearableFlipPacket>();

            clientApi = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverChannel = api.Network.RegisterChannel("flippable-wearable-hotkey")
                .RegisterMessageType<WearableFlipPacket>()
                .SetMessageHandler<WearableFlipPacket>(handleFlipPacket);
        }

        public bool TryFlipClientSide(KeyCombination keys)
        {
            if (clientApi != null)
            {
                TryFlip(clientApi, clientApi.World.Player);
            }

            clientChannel?.SendPacket(new WearableFlipPacket());

            return true;
        }

        public static bool TryFlip(ICoreAPI api, IPlayer wearer)
        {
            IInventory? inventory = wearer.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);

            if (inventory == null) return false;

            bool flippedAny = false;

            foreach (ItemSlot inventorySlot in inventory)
            {
                if (inventorySlot.Itemstack == null) continue;

                IFlippableWearable? flippable = inventorySlot.Itemstack.Collectible.GetCollectibleInterface<IFlippableWearable>();
                if (flippable != null)
                {
                    bool flipped = flippable.Flip(wearer.Entity, inventorySlot);

                    if (flipped)
                    {
                        inventorySlot.MarkDirty();
                        flippedAny = true;
                    }
                    continue;
                }

                ICombinedWearable? combined = inventorySlot.Itemstack.Collectible.GetCollectibleInterface<ICombinedWearable>();
                if (combined != null)
                {
                    bool flippedAnyBits = FlipCombined(combined, inventorySlot, wearer);

                    if (flippedAnyBits)
                    {
                        flippedAny = true;
                    }
                }
            }

            return flippedAny;
        }

        public static bool FlipCombined(ICombinedWearable combined, ItemSlot inventorySlot, IPlayer wearer)
        {
            if (inventorySlot.Itemstack == null) return false;

            bool flippedAnyBits = false;
            IEnumerable<WearableBitEntry> bits = combined.RetrieveBits(inventorySlot.Itemstack);
            List<WearableBitEntry> resultingBits = [];
            foreach (WearableBitEntry bit in bits)
            {
                IFlippableWearable? flippable = bit.Stack.Collectible.GetCollectibleInterface<IFlippableWearable>();

                if (flippable == null)
                {
                    resultingBits.Add(bit);
                    continue;
                }

                DummySlot dummySlot = new(bit.Stack);

                bool flipped = flippable.Flip(wearer.Entity, dummySlot);

                if (flipped)
                {
                    flippedAnyBits = true;
                    if (dummySlot.Itemstack == null) continue;
                    WearableBitEntry newBit = bit;
                    newBit.Stack = dummySlot.Itemstack;
                    resultingBits.Add(newBit);
                }
                else
                {
                    resultingBits.Add(bit);
                }
            }

            if (flippedAnyBits)
            {
                combined.StoreBits(inventorySlot.Itemstack, resultingBits);
                inventorySlot.MarkDirty();
            }

            return flippedAnyBits;
        }



        private static void handleFlipPacket(IServerPlayer player, WearableFlipPacket packet) => TryFlip(player.Entity.Api, player);
    }
}
