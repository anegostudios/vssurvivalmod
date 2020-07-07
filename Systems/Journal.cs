using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalPiece
    {
        public int EntryId;
        public string Text;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalEntry
    {
        public int EntryId;
        public string LoreCode;

        public string Title;
        public bool Editable;
        public List<JournalPiece> Chapters = new List<JournalPiece>();
    }
    

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class Journal
    {
        public List<JournalEntry> Entries = new List<JournalEntry>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalAsset
    {
        public string Code;
        public string Title;
        public string[] Pieces;
        public string Category;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class LoreDiscovery
    {
        public string Code;
        public List<int> PieceIds;
    }
    
    public class ModJournal : ModSystem
    {
        // Server
        ICoreServerAPI sapi;
        Dictionary<string, Journal> journalsByPlayerUid = new Dictionary<string, Journal>();

        Dictionary<string, Dictionary<string, LoreDiscovery>> loreDiscoveryiesByPlayerUid = new Dictionary<string, Dictionary<string, LoreDiscovery>>();

        Dictionary<string, JournalAsset> journalAssetsByCode;

        IServerNetworkChannel serverChannel;

        // Client
        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;
        Journal ownJournal = new Journal();
        GuiDialogJournal dialog;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

        }

       

        


        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            capi.Input.RegisterHotKey("journal", "Journal", GlKeys.J, HotkeyType.GUIOrOtherControls);
            capi.Input.SetHotKeyHandler("journal", OnHotkeyJournal);

            clientChannel =
                api.Network.RegisterChannel("journal")
               .RegisterMessageType(typeof(JournalEntry))
               .RegisterMessageType(typeof(Journal))
               .RegisterMessageType(typeof(JournalPiece))
               .SetMessageHandler<Journal>(OnJournalItemsReceived)
               .SetMessageHandler<JournalEntry>(OnJournalItemReceived)
               .SetMessageHandler<JournalPiece>(OnJournalPieceReceived)
            ;
        }

        
        private bool OnHotkeyJournal(KeyCombination comb)
        {
            if (dialog != null)
            {
                dialog.TryClose();
                dialog = null;
                return true;
            }

            dialog = new GuiDialogJournal(ownJournal.Entries, capi);
            dialog.TryOpen();
            dialog.OnClosed += () => dialog = null;

            return true;
        }


        private void OnJournalPieceReceived(JournalPiece entryPiece)
        {   
            ownJournal.Entries[entryPiece.EntryId].Chapters.Add(entryPiece);
        }

        private void OnJournalItemReceived(JournalEntry entry)
        {
            if (entry.EntryId >= ownJournal.Entries.Count)
            {
                ownJournal.Entries.Add(entry);
            } else
            {
                ownJournal.Entries[entry.EntryId] = entry;
            }
        }

        private void OnJournalItemsReceived(Journal fullJournal)
        {
            ownJournal = fullJournal;
        }





        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameGettingSaved;

            serverChannel =
               api.Network.RegisterChannel("journal")
               .RegisterMessageType(typeof(JournalEntry))
               .RegisterMessageType(typeof(Journal))
               .RegisterMessageType(typeof(JournalPiece))
            ;

            api.Event.RegisterEventBusListener(OnLoreDiscovery, 0.5, "loreDiscovery");

            //api.RegisterCommand("alllore", "", "", onCmdAllLore, Privilege.controlserver);
        }

        private void onCmdAllLore(IServerPlayer player, int groupId, CmdArgs args)
        {
            DiscoverEverything(player);
        }

        private void OnGameGettingSaved()
        {
            sapi.WorldManager.SaveGame.StoreData("journalItemsByPlayerUid", SerializerUtil.Serialize(journalsByPlayerUid));
            sapi.WorldManager.SaveGame.StoreData("loreDiscoveriesByPlayerUid", SerializerUtil.Serialize(loreDiscoveryiesByPlayerUid));
        }


        private void OnSaveGameLoaded()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("journalItemsByPlayerUid");
                if (data != null) journalsByPlayerUid = SerializerUtil.Deserialize<Dictionary<string, Journal>>(data);
            } catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading journalItemsByPlayerUid. Resetting. Exception: {0}", e);
            }
            if (journalsByPlayerUid == null) journalsByPlayerUid = new Dictionary<string, Journal>();

            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("loreDiscoveriesByPlayerUid");
                if (data != null) loreDiscoveryiesByPlayerUid = SerializerUtil.Deserialize<Dictionary<string, Dictionary<string, LoreDiscovery>>>(data);
            }
            catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading loreDiscoveryiesByPlayerUid. Resetting. Exception: {0}", e);
            }

            if (loreDiscoveryiesByPlayerUid == null) loreDiscoveryiesByPlayerUid = new Dictionary<string, Dictionary<string, LoreDiscovery>>();
        }



        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            Journal journal;
            if (journalsByPlayerUid.TryGetValue(byPlayer.PlayerUID, out journal))
            {
                serverChannel.SendPacket(journal, byPlayer);
            }
        }



        public void AddOrUpdateJournalEntry(IServerPlayer forPlayer, JournalEntry entry)
        {
            Journal journal;
            if (!journalsByPlayerUid.TryGetValue(forPlayer.PlayerUID, out journal))
            {
                journalsByPlayerUid[forPlayer.PlayerUID] = journal = new Journal();
            }

            for (int i = 0; i < journal.Entries.Count; i++)
            {
                JournalEntry exentry = journal.Entries[i];

                if (exentry.LoreCode == entry.LoreCode)
                {
                    journal.Entries[i] = entry;
                    serverChannel.SendPacket(entry, forPlayer);
                    return;
                }
            }

            journal.Entries.Add(entry);
            serverChannel.SendPacket(entry, forPlayer);
        }



        private void OnLoreDiscovery(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            string playerUid = tree.GetString("playeruid");
            string category = tree.GetString("category");

            IServerPlayer plr = sapi.World.PlayerByUid(playerUid) as IServerPlayer;

            LoreDiscovery discovery = TryGetRandomLoreDiscovery(sapi.World, plr, category);
            if (discovery == null)
            {
                plr.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("Nothing of significance in these pages"), EnumChatType.Notification);
                return;
            }


            handling = EnumHandling.PreventDefault;

            Journal journal;
            if (!journalsByPlayerUid.TryGetValue(playerUid, out journal))
            {
                journalsByPlayerUid[playerUid] = journal = new Journal();
            }

            JournalEntry entry = null;
            JournalAsset asset = journalAssetsByCode[discovery.Code];

            for (int i = 0; i < journal.Entries.Count; i++)
            {
                if (journal.Entries[i].LoreCode == discovery.Code)
                {
                    entry = journal.Entries[i];
                    break;
                }
            }

            bool isNew = false;

            if (entry == null)
            {
                journal.Entries.Add(entry = new JournalEntry() { Editable = false, Title = asset.Title, LoreCode = discovery.Code, EntryId = journal.Entries.Count });
                isNew = true;
            }

            int partnum = 0;
            int partcount = asset.Pieces.Length;

            for (int i = 0; i < discovery.PieceIds.Count; i++)
            {
                JournalPiece piece = new JournalPiece() { Text = asset.Pieces[discovery.PieceIds[i]], EntryId = entry.EntryId };
                entry.Chapters.Add(piece);
                if (!isNew) serverChannel.SendPacket(piece, plr);

                partnum = discovery.PieceIds[i];
            }

            if (isNew)
            {
                serverChannel.SendPacket(entry, plr);
            }

            //plr.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("lorediscovery", entry.Title), EnumChatType.Notification);
            
            sapi.SendIngameDiscovery(plr, "lore-" + discovery.Code, null, partnum, partcount);

            sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/deepbell"), plr.Entity, null, false, 32, 0.5f);
        }


        protected void DiscoverEverything(IServerPlayer plr)
        {
            JournalAsset[] journalAssets = sapi.World.AssetManager.GetMany<JournalAsset>(sapi.World.Logger, "config/lore/").Values.ToArray();

            Journal journal;
            if (!journalsByPlayerUid.TryGetValue(plr.PlayerUID, out journal))
            {
                journalsByPlayerUid[plr.PlayerUID] = journal = new Journal();
            }

            journal.Entries.Clear();

            foreach (var val in journalAssets)
            {
                JournalEntry entry = null;
                journal.Entries.Add(entry = new JournalEntry() { Editable = false, Title = val.Title, LoreCode = val.Code, EntryId = journal.Entries.Count });
                serverChannel.SendPacket(entry, plr);

                foreach (var part in val.Pieces)
                {
                    JournalPiece piece = new JournalPiece() { Text = part, EntryId = entry.EntryId };
                    entry.Chapters.Add(piece);
                    serverChannel.SendPacket(piece, plr);
                }
            }
        }


        LoreDiscovery TryGetRandomLoreDiscovery(IWorldAccessor world, IPlayer serverplayer, string category)
        {
            Dictionary<string, LoreDiscovery> discoveredLore;
            loreDiscoveryiesByPlayerUid.TryGetValue(serverplayer.PlayerUID, out discoveredLore);

            if (discoveredLore == null)
            {
                loreDiscoveryiesByPlayerUid[serverplayer.PlayerUID] = discoveredLore = new Dictionary<string, LoreDiscovery>();
            }

            JournalAsset[] journalAssets;
            
            if (journalAssetsByCode == null)
            {
                journalAssetsByCode = new Dictionary<string, JournalAsset>();

                journalAssets = world.AssetManager.GetMany<JournalAsset>(world.Logger, "config/lore/").Values.ToArray();

                foreach (JournalAsset asset in journalAssets)
                {
                    journalAssetsByCode[asset.Code] = asset;
                }
            } else
            {
                journalAssets = journalAssetsByCode.Values.ToArray();
            }

            journalAssets.Shuffle(world.Rand);


            for (int i = 0; i < journalAssets.Length; i++)
            {
                JournalAsset journalAsset = journalAssets[i];
                if (journalAsset.Category != category) continue;

                if (!discoveredLore.ContainsKey(journalAsset.Code))
                {
                    return discoveredLore[journalAsset.Code] = new LoreDiscovery() { Code = journalAsset.Code, PieceIds = new List<int>() { 0 } };
                }

                LoreDiscovery ld = discoveredLore[journalAsset.Code];

                for (int p = 0; p < journalAsset.Pieces.Length; p++)
                {
                    if (!ld.PieceIds.Contains(p))
                    {
                        ld.PieceIds.Add(p);
                        return new LoreDiscovery() { Code = journalAsset.Code, PieceIds = new List<int>() { p } };
                    }
                }
            }

            return null;
        }
        
    }


}
