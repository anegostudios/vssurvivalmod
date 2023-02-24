using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class EditbookPacket
    {
        [ProtoMember(1)]
        public bool DidSave;
        [ProtoMember(2)]
        public bool DidSign;
        [ProtoMember(3)]
        public string Text;
        [ProtoMember(4)]
        public string Title;
    }

    [ProtoContract]
    public class TranscribePacket
    {
        [ProtoMember(1)]
        public string Text;
        [ProtoMember(2)]
        public string Title;
        [ProtoMember(3)]
        public int PageNumber;
    }

    public class ModSystemEditableBook : ModSystem
    {
        Dictionary<string, ItemSlot> nowEditing = new Dictionary<string, ItemSlot>();
        ICoreAPI api;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public void Transcribe(IPlayer player, string pageText, string bookTitle, int pageNumber, ItemSlot bookSlot)
        {
            ItemSlot matSlot = null;
            player.Entity.WalkInventory((slot) =>
            {
                if (slot.Empty) return true;
                if (slot.Itemstack.Collectible.Attributes?["canTranscribeOn"].AsBool(false) == true && !slot.Itemstack.Attributes.HasAttribute("text"))
                {
                    matSlot = slot;
                    return false;
                }

                return true;
            });

            if (matSlot == null)
            {
                (player as IServerPlayer)?.SendIngameError("nomats", Lang.Get("Need something to transcribe it on first, such as parchment"));
                (api as ICoreClientAPI)?.TriggerIngameError(this, "nomats", Lang.Get("Need something to transcribe it on first, such as parchment"));
                return;
            }

            if (!ItemBook.isWritingTool(player.Entity.LeftHandItemSlot))
            {
                (player as IServerPlayer)?.SendIngameError("noink", Lang.Get("Need ink and quill in my off hand"));
                (api as ICoreClientAPI)?.TriggerIngameError(this, "noink", Lang.Get("Need ink and quill in my off hand"));
                return;
            }

            if (api is ICoreClientAPI capi)
            {
                capi.Network.GetChannel("editablebook").SendPacket(new TranscribePacket() { 
                    Title = bookTitle, 
                    Text = pageText,
                    PageNumber = pageNumber
                });
            } else
            {
                var paperStack = matSlot.TakeOut(1);
                paperStack.Attributes.SetString("text", pageText);
                paperStack.Attributes.SetString("title", bookTitle);
                paperStack.Attributes.SetInt("pageNumber", pageNumber);
                paperStack.Attributes.SetString("signedby", bookSlot.Itemstack.Attributes.GetString("signedby"));
                paperStack.Attributes.SetString("signedbyuid", bookSlot.Itemstack.Attributes.GetString("signedbyuid"));
                paperStack.Attributes.SetString("transcribedby", player.PlayerName);
                paperStack.Attributes.SetString("transcribedbyuid", player.PlayerUID);

                if (!player.InventoryManager.TryGiveItemstack(paperStack, true))
                {
                    api.World.SpawnItemEntity(paperStack, player.Entity.Pos.XYZ);
                }

                api.World.PlaySoundAt(new AssetLocation("sounds/effect/writing"), player.Entity);
            }
        }

        public void BeginEdit(IPlayer player, ItemSlot slot)
        {
            nowEditing[player.PlayerUID] = slot;
        }


        public void EndEdit(IPlayer player, string text, string title, bool didSign)
        {
            if (nowEditing.TryGetValue(player.PlayerUID, out var slot))
            {
                slot.Itemstack.Attributes.SetString("text", text);
                slot.Itemstack.Attributes.SetString("title", title);

                if (didSign)
                {
                    slot.Itemstack.Attributes.SetString("signedby", player.PlayerName);
                    slot.Itemstack.Attributes.SetString("signedbyuid", player.PlayerUID);
                }

                slot.MarkDirty();

                if (api is ICoreClientAPI capi)
                {
                    capi.Network.GetChannel("editablebook").SendPacket(new EditbookPacket() { DidSave = true, DidSign = didSign, Text = text, Title = title });
                }
            }

            nowEditing.Remove(player.PlayerUID);
        }

        public void CancelEdit(IPlayer player)
        {
            nowEditing.Remove(player.PlayerUID);

            if (api is ICoreClientAPI capi)
            {
                capi.Network.GetChannel("editablebook").SendPacket(new EditbookPacket() { DidSave = false });
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;
            api.Network.RegisterChannel("editablebook").RegisterMessageType<EditbookPacket>().RegisterMessageType<TranscribePacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network.GetChannel("editablebook").SetMessageHandler<EditbookPacket>(onEditBookPacket).SetMessageHandler<TranscribePacket>(onTranscribePacket);
        }

        private void onTranscribePacket(IServerPlayer fromPlayer, TranscribePacket packet)
        {
            if (nowEditing.TryGetValue(fromPlayer.PlayerUID, out var slot))
            {
                Transcribe(fromPlayer, packet.Text, packet.Title, packet.PageNumber, slot);
            }
        }

        private void onEditBookPacket(IServerPlayer fromPlayer, EditbookPacket packet)
        {
            if (nowEditing.TryGetValue(fromPlayer.PlayerUID, out var slot))
            {
                if (packet.DidSave) EndEdit(fromPlayer, packet.Text, packet.Title, packet.DidSign);
                else CancelEdit(fromPlayer);
            }
        }
    }
}
