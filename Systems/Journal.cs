using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

// Each player has a Journal
// Each Journal has journal entries
// Each journal entry has one or more chapters

// Multiple Journal entries can belong to a category
// Each journal entry has a unique code
namespace Vintagestory.GameContent
{
    #region pre v1.14
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalChapterOld
    {
        /// <summary>
        /// The journal entry id
        /// </summary>
        public int EntryId;
        public string Text;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalEntryOld
    {
        public int EntryId;
        public string LoreCode;

        public string Title;
        public bool Editable;
        public List<JournalChapterOld> Chapters = new List<JournalChapterOld>();
    }


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalOld
    {
        public List<JournalEntryOld> Entries = new List<JournalEntryOld>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class LoreDiscoveryOld
    {
        public string Code;
        public List<int> PieceIds;
    }
    #endregion


    #region v1.14+


    [ProtoContract]
    public class Journal
    {
        [ProtoMember(1)]
        public List<JournalEntry> Entries = new List<JournalEntry>();
    }


    [ProtoContract]
    public class JournalEntry
    {
        [ProtoMember(1)]
        public int EntryId;
        [ProtoMember(2)]
        public string LoreCode;
        [ProtoMember(3)]
        public string Title;
        [ProtoMember(4)]
        public bool Editable;
        [ProtoMember(5)]
        public List<JournalChapter> Chapters = new List<JournalChapter>();
    }

    [ProtoContract]
    public class JournalChapter
    {
        /// <summary>
        /// The journal entry id
        /// </summary>
        [ProtoMember(1)]
        public int EntryId;
        /// <summary>
        /// The id of this chapter
        /// </summary>
        [ProtoMember(2)]
        public int ChapterId;

        [ProtoMember(3)]
        public string Text;
    }

    [ProtoContract]
    public class LoreDiscovery
    {
        [ProtoMember(1)]
        public string Code;
        [ProtoMember(2)]
        public List<int> ChapterIds;
    }
    #endregion


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JournalAsset
    {
        public string Code;
        public string Title;
        public string[] Pieces;
        public string Category;
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

            capi.Input.RegisterHotKey("journal", Lang.Get("Journal"), GlKeys.J, HotkeyType.CharacterControls);
            capi.Input.SetHotKeyHandler("journal", OnHotkeyJournal);

            clientChannel =
                api.Network.RegisterChannel("journal")
               .RegisterMessageType(typeof(JournalEntry))
               .RegisterMessageType(typeof(Journal))
               .RegisterMessageType(typeof(JournalChapter))
               .SetMessageHandler<Journal>(OnJournalItemsReceived)
               .SetMessageHandler<JournalEntry>(OnJournalItemReceived)
               .SetMessageHandler<JournalChapter>(OnJournalPieceReceived)
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


        private void OnJournalPieceReceived(JournalChapter entryPiece)
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
               .RegisterMessageType(typeof(JournalChapter))
            ;

            api.Event.RegisterEventBusListener(OnLoreDiscovery, 0.5, "loreDiscovery");

#if DEBUG
            api.ChatCommands.GetOrCreate("debug")
                 .BeginSubCommand("alllore")
                     .WithDescription("Discover all lore content in the journal")
                     .RequiresPrivilege(Privilege.controlserver)
                     .HandleWith(DiscoverEverything)
            
                     .BeginSubCommand("clear")
                         .WithDescription("Clear the journal")
                         .RequiresPlayer()
                         .HandleWith(args =>
                         {
                             if (journalsByPlayerUid.TryGetValue(args.Caller.Player.PlayerUID, out var journal))
                             {
                                 journal.Entries.Clear();
                             }
                             else
                             {
                                 journalsByPlayerUid[args.Caller.Player.PlayerUID] = new Journal();
                             }

                             if (loreDiscoveryiesByPlayerUid.TryGetValue(args.Caller.Player.PlayerUID, out var loreDiscovery))
                             {
                                 loreDiscovery.Clear();
                             }
                             else
                             {
                                 loreDiscoveryiesByPlayerUid[args.Caller.Player.PlayerUID] = new Dictionary<string, LoreDiscovery>();
                             }
                             return TextCommandResult.Success("Cleared.");
                         })
                     .EndSubCommand()
                 .EndSubCommand();
#endif

        }

        private void OnGameGettingSaved()
        {
            using FastMemoryStream ms = new();
            sapi.WorldManager.SaveGame.StoreData("journalItemsByPlayerUid", SerializerUtil.Serialize(journalsByPlayerUid, ms));
            sapi.WorldManager.SaveGame.StoreData("loreDiscoveriesByPlayerUid", SerializerUtil.Serialize(loreDiscoveryiesByPlayerUid, ms));
        }


        private void OnSaveGameLoaded()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("journalItemsByPlayerUid");

                if (GameVersion.IsLowerVersionThan(sapi.WorldManager.SaveGame.LastSavedGameVersion, "1.14-pre.1"))
                {
                    if (data != null) journalsByPlayerUid = upgrade(SerializerUtil.Deserialize<Dictionary<string, JournalOld>>(data));
                    sapi.World.Logger.Notification("Upgraded journalItemsByPlayerUid from v1.13 format to v1.14 format");
                } else
                {
                    if (data != null) journalsByPlayerUid = SerializerUtil.Deserialize<Dictionary<string, Journal>>(data);
                }
                
            } catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading journalItemsByPlayerUid. Resetting.");
                sapi.World.Logger.Error(e);
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

        private Dictionary<string, Journal> upgrade(Dictionary<string, JournalOld> dict)
        {
            Dictionary<string, Journal> newdict = new Dictionary<string, Journal>();

            foreach (var val in dict)
            {
                var entries = new List<JournalEntry>();

                foreach (var entry in val.Value.Entries)
                {
                    var chapters = new List<JournalChapter>();

                    foreach (var chapter in entry.Chapters)
                    {
                        chapters.Add(new JournalChapter()
                        {
                            Text = chapter.Text,
                            EntryId = chapter.EntryId
                        });
                    }

                    entries.Add(new JournalEntry()
                    {
                        Chapters = chapters,
                        Editable = entry.Editable,
                        EntryId = entry.EntryId,
                        LoreCode = entry.LoreCode,
                        Title = entry.Title
                    });
                }

                newdict[val.Key] = new Journal()
                {
                    Entries = entries,
                };
            }

            return newdict;
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (journalsByPlayerUid.TryGetValue(byPlayer.PlayerUID, out Journal journal))
            {
                serverChannel.SendPacket(journal, byPlayer);
            }
        }



        public void AddOrUpdateJournalEntry(IServerPlayer forPlayer, JournalEntry entry)
        {
            if (!journalsByPlayerUid.TryGetValue(forPlayer.PlayerUID, out Journal journal))
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

            ItemSlot itemslot = plr.InventoryManager.ActiveHotbarSlot;
            LoreDiscovery discovery;

            string journalEntryCode = itemslot.Itemstack.Attributes.GetString("discoveryCode", null);
            if (journalEntryCode != null)
            {
                var chapters = (itemslot.Itemstack.Attributes["chapterIds"] as IntArrayAttribute).value;
                discovery = new LoreDiscovery()
                {
                    Code = journalEntryCode,
                    ChapterIds = new List<int>(chapters)
                };
            }
            else
            {
                discovery = getRandomLoreDiscovery(sapi.World, plr, category);
            }

            if (discovery == null)
            {
                if (journalEntryCode == null) plr.SendIngameError("alreadydiscovered", Lang.Get("Nothing new in these pages"));
                return;
            }

            if (TryDiscoverLore(discovery, plr, itemslot))
            {
                itemslot.MarkDirty();
                plr.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/writing"), plr.Entity);
                handling = EnumHandling.PreventDefault;
            }
        }

        public bool TryDiscoverLore(LoreDiscovery newdiscovery, IServerPlayer plr, ItemSlot slot = null)
        {
            string playerUid = plr.PlayerUID;

            if (!journalsByPlayerUid.TryGetValue(playerUid, out Journal journal))
            {
                journalsByPlayerUid[playerUid] = journal = new Journal();
            }

            JournalEntry entry = null;
            ensureJournalAssetsLoaded();
            JournalAsset asset = journalAssetsByCode[newdiscovery.Code];

            for (int i = 0; i < journal.Entries.Count; i++)
            {
                if (journal.Entries[i].LoreCode == newdiscovery.Code)
                {
                    entry = journal.Entries[i];
                    break;
                }
            }

            bool isNew = false;

            if (entry == null)
            {
                journal.Entries.Add(entry = new JournalEntry() { Editable = false, Title = asset.Title, LoreCode = newdiscovery.Code, EntryId = journal.Entries.Count });
                isNew = true;
            }

            bool anyAdded = false;

            loreDiscoveryiesByPlayerUid.TryGetValue(plr.PlayerUID, out Dictionary<string, LoreDiscovery> discoveredLore);
            if (discoveredLore == null)
            {
                loreDiscoveryiesByPlayerUid[plr.PlayerUID] = discoveredLore = new Dictionary<string, LoreDiscovery>();
            }
            if (discoveredLore.TryGetValue(asset.Code, out var olddisc))
            {
                foreach (int newChapterId in newdiscovery.ChapterIds)
                {
                    if (!olddisc.ChapterIds.Contains(newChapterId))
                    {
                        olddisc.ChapterIds.Add(newChapterId);
                        anyAdded = true;
                    }
                }
            } else {
                discoveredLore[asset.Code] = newdiscovery;
                anyAdded = true;
            }

            if (!anyAdded) return false;

            int partnum = 0;
            int partcount = asset.Pieces.Length;



            for (int i = 0; i < newdiscovery.ChapterIds.Count; i++)
            {
                JournalChapter chapter = new JournalChapter() { Text = asset.Pieces[newdiscovery.ChapterIds[i]], EntryId = entry.EntryId, ChapterId = newdiscovery.ChapterIds[i] };
                entry.Chapters.Add(chapter);
                if (!isNew) serverChannel.SendPacket(chapter, plr);

                partnum = newdiscovery.ChapterIds[i];
            }

            if (slot != null)
            {
                slot.Itemstack.Attributes.SetString("discoveryCode", newdiscovery.Code);
                slot.Itemstack.Attributes["chapterIds"] = new IntArrayAttribute(newdiscovery.ChapterIds.ToArray());

                slot.Itemstack.Attributes["textCodes"] = new StringArrayAttribute(newdiscovery.ChapterIds.Select(id => asset.Pieces[id]).ToArray());
                slot.Itemstack.Attributes.SetString("titleCode", entry.Title);
                slot.MarkDirty();
            }

            if (isNew)
            {
                serverChannel.SendPacket(entry, plr);
            }

            sapi.SendIngameDiscovery(plr, "lore-" + newdiscovery.Code, null, partnum + 1, partcount);
            sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/deepbell"), plr.Entity, null, false, 5, 0.5f);

            return true;
        }

        protected TextCommandResult DiscoverEverything(TextCommandCallingArgs args)
        {
            var plr = args.Caller.Player as IServerPlayer;
            JournalAsset[] journalAssets = sapi.World.AssetManager.GetMany<JournalAsset>(sapi.World.Logger, "config/lore/").Values.ToArray();

            if (!journalsByPlayerUid.TryGetValue(plr.PlayerUID, out Journal journal))
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
                    JournalChapter piece = new JournalChapter() { Text = part, EntryId = entry.EntryId };
                    entry.Chapters.Add(piece);
                    serverChannel.SendPacket(piece, plr);
                }
            }
            return TextCommandResult.Success("All lore added");
        }


        LoreDiscovery getRandomLoreDiscovery(IWorldAccessor world, IPlayer serverplayer, string category)
        {
            JournalAsset[] journalAssets;

            ensureJournalAssetsLoaded();
            journalAssets = journalAssetsByCode.Values.ToArray();
            journalAssets.Shuffle(world.Rand);

            for (int i = 0; i < journalAssets.Length; i++)
            {
                JournalAsset journalAsset = journalAssets[i];
                if (category != null && journalAsset.Category != category) continue;

                LoreDiscovery dis = getNextUndiscoveredChapter(serverplayer, journalAsset);
                if (dis != null) return dis;
            }

            return null;
        }



        /// <summary>
        /// Marks the first undiscovered chapter of this lore as discovered and returns it
        /// </summary>
        /// <param name="plr"></param>
        /// <param name="asset"></param>
        /// <returns></returns>
        private LoreDiscovery getNextUndiscoveredChapter(IPlayer plr, JournalAsset asset)
        {
            loreDiscoveryiesByPlayerUid.TryGetValue(plr.PlayerUID, out Dictionary<string, LoreDiscovery> discoveredLore);
            if (discoveredLore == null)
            {
                loreDiscoveryiesByPlayerUid[plr.PlayerUID] = discoveredLore = new Dictionary<string, LoreDiscovery>();
            }

            if (!discoveredLore.ContainsKey(asset.Code))
            {
                return new LoreDiscovery() { Code = asset.Code, ChapterIds = new List<int>() { 0 } };
            }

            LoreDiscovery ld = discoveredLore[asset.Code];

            for (int p = 0; p < asset.Pieces.Length; p++)
            {
                if (!ld.ChapterIds.Contains(p))
                {
                    return new LoreDiscovery()
                    {
                        ChapterIds = new List<int>() { p },
                        Code = ld.Code
                    };
                }
            }

            return null;
        }


        void ensureJournalAssetsLoaded()
        {
            if (journalAssetsByCode == null)
            {
                journalAssetsByCode = new Dictionary<string, JournalAsset>();

                var journalAssets = sapi.World.AssetManager.GetMany<JournalAsset>(sapi.World.Logger, "config/lore/").Values.ToArray();

                foreach (JournalAsset asset in journalAssets)
                {
                    journalAssetsByCode[asset.Code] = asset;
                }
            }
        }



        public bool DidDiscoverLore(string playerUid, string code, int chapterId)
        {
            if (!journalsByPlayerUid.TryGetValue(playerUid, out Journal journal))
            {
                return false;
            }

            for (int i = 0; i < journal.Entries.Count; i++)
            {
                if (journal.Entries[i].LoreCode == code)
                {
                    JournalEntry entry = journal.Entries[i];
                    for (int j = 0; j < entry.Chapters.Count; j++)
                    {
                        if (entry.Chapters[j].ChapterId == chapterId) return true;
                    }

                    break;
                }
            }

            return false;
        }
    }


}
