using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using System;
using Vintagestory.API.Util;
using System.Linq;
using Vintagestory.API.Datastructures;
using System.IO;
using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Server;
using System.Reflection;
using System.Collections.Generic;
using System.Numerics;

// Requirements:
// Activity Collections
// List/Add/Remove
// Modify

// Activity Collection
// List/Add/Remove collection
// Modify Activity

// Activity
// Slot field
// Priority field
// Name field
// List of Actions + Add/Remove
// List of conditions + Add/Remove
// Visualize button (shows path like cinematic camera)

// Action
// Action specific config fields

// Triger
// condition specific config fields
namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class ApplyConfigPacket
    {
        [ProtoMember(1)]
        public long EntityId;
        [ProtoMember(2)]
        public string ActivityCollectionName;
    }

    [ProtoContract]
    public class ActivityCollectionsJsonPacket
    {
        [ProtoMember(1)]
        public List<string> Collections;
    }

    public class ActivityEditorSystem : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        GuiDialogActivityCollections dlg;
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("activityEditor")
                .RegisterMessageType<ApplyConfigPacket>()
                .RegisterMessageType<ActivityCollectionsJsonPacket>()
            ;

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("activityEditor").SetMessageHandler<ActivityCollectionsJsonPacket>(storeActivityCollectionPacket);

            this.capi = api;
            api.ChatCommands.GetOrCreate("dev")
                .BeginSub("aedit")
                .HandleWith(onCmdAedit)
                .EndSub()
            ;
        }

        private void storeActivityCollectionPacket(ActivityCollectionsJsonPacket packet)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            foreach (var json in packet.Collections)
            {
                // Deserialize and Re-serialize the json so that we don't just write any data to the disk without checking its contents
                var collection = JsonUtil.ToObject<EntityActivityCollection>(json, "", settings);
                if (collection != null)
                {
                    saveCollection(collection);
                }
            }
        }

        private static void saveCollection(EntityActivityCollection collection)
        {
            string filepath = Path.Combine(GamePaths.AssetsPath, "survival", "config", "activitycollections", GamePaths.ReplaceInvalidChars(collection.Name) + ".json");
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            string json = JsonConvert.SerializeObject(collection, Formatting.Indented, settings);
            File.WriteAllText(filepath, json);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;

            api.Network.GetChannel("activityEditor")
                .SetMessageHandler<ApplyConfigPacket>(onPacket)
                .SetMessageHandler<ActivityCollectionsJsonPacket>((player, packet) =>
                {
                    if (!player.HasPrivilege(Privilege.controlserver))
                    {
                        // Only admins can store activities
                        player.SendMessage(GlobalConstants.GeneralChatGroup, "No privilege to save activity collections to server", EnumChatType.CommandError);
                        return;
                    }

                    storeActivityCollectionPacket(packet);

                    // Send this change to other players
                    api.Network.GetChannel("activityEditor").BroadcastPacket(packet, player);
                })
            ;

            api.ChatCommands
                .GetOrCreate("dev")
                .BeginSub("aee")
                    .WithArgs(api.ChatCommands.Parsers.Entities("target entity"))
                    .BeginSub("unmount")
                    .HandleWith((args) => CmdUtil.EntityEach(args, (e) => {
                        (e as EntityAgent)?.TryUnmount();
                        return TextCommandResult.Success();
                    }))
                    .EndSub()
                    .BeginSub("startanim")
                    .WithArgs(api.ChatCommands.Parsers.Word("anim"))
                    .HandleWith((args) => CmdUtil.EntityEach(args, (e) => {
                        e.StartAnimation(args[1] as string);
                        return TextCommandResult.Success();
                    }))
                    .EndSub()
                    .BeginSub("runa")
                        .WithArgs(api.ChatCommands.Parsers.Word("activity name"))
                        .HandleWith((args) => CmdUtil.EntityEach(args, (e) => {
                            var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
                            if (ebh != null)
                            {
                                if (ebh.ActivitySystem.StartActivity(args[1] as string))
                                {
                                    return TextCommandResult.Success("Acitivty started");
                                } else
                                {
                                    return TextCommandResult.Error("Target entity has no such activity");
                                }
                            }

                            return TextCommandResult.Error("Target entity has no EntityBehaviorActivityDriven");
                        }))
                    .EndSub()
                    .BeginSub("pause")
                        .WithArgs(api.ChatCommands.Parsers.Bool("paused"))
                        .HandleWith(onCmdPause)
                    .EndSub()
                    .BeginSub("stop")
                    .HandleWith((args) => CmdUtil.EntityEach(args, (e) => {
                        var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
                        if (ebh != null)
                        {
                            if (ebh.ActivitySystem.CancelAll())
                            {
                                return TextCommandResult.Success("Acitivties stopped");
                            }
                            else
                            {
                                return TextCommandResult.Error("No activity was running");
                            }
                        }

                        return TextCommandResult.Error("Target entity has no EntityBehaviorActivityDriven");
                    }))
                    .EndSub()
                .HandleWith((args) => CmdUtil.EntityEach(args, (e) => {
                    var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
                    if (ebh != null)
                    {
                        var vals = ebh.ActivitySystem.ActiveActivitiesBySlot.Values;
                        if (vals.Count == 0)
                        {
                            return TextCommandResult.Success("No active activities");
                        }
                        return TextCommandResult.Success("Active activities: " + string.Join(", ", vals));
                    }

                    return TextCommandResult.Error("Target entity has no EntityBehaviorActivityDriven");
                }))
            ;
        }

        private void Event_SaveGameLoaded()
        {
            if (sapi.World.Config.GetBool("syncActivityCollections"))
            {
                sapi.Event.PlayerJoin += Event_PlayerJoin;
            }
        }

        public OrderedDictionary<AssetLocation, EntityActivityCollection> collections = new OrderedDictionary<AssetLocation, EntityActivityCollection>();
        private void Event_PlayerJoin(IServerPlayer player)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            sapi.Assets.Reload(AssetCategory.config);
            var files = sapi.Assets.GetMany("config/activitycollections/");

            ActivityCollectionsJsonPacket packet = new ActivityCollectionsJsonPacket();
            packet.Collections = new List<string>();

            collections.Clear();
            foreach (var file in files)
            {
                packet.Collections.Add(file.ToText());
            }

            sapi.Network.GetChannel("activityEditor").SendPacket(packet, player);
        }

        private TextCommandResult onCmdPause(TextCommandCallingArgs args)
        {
            var paused = (bool)args[1];

            return CmdUtil.EntityEach(args, (e) => {
                var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
                if (ebh != null)
                {
                    ebh.ActivitySystem.PauseAutoSelection(paused);
                    if (paused) ebh.ActivitySystem.CancelAll();
                    return TextCommandResult.Success(paused ? "Activity selection paused" : "Activity selection resumed");                    
                }

                return TextCommandResult.Error("Target entity has no EntityBehaviorActivityDriven");
            });
        }

        private void onPacket(IServerPlayer fromPlayer, ApplyConfigPacket packet)
        {
            var e = sapi.World.GetEntityById(packet.EntityId);
            if (e==null)
            {
                fromPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "No such entity id loaded", EnumChatType.Notification);
                return;
            }
            var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
            if (ebh == null)
            {
                fromPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "This entity is lacking the ActivityDriven behavior", EnumChatType.Notification);
                return;
            }

            sapi.Assets.Reload(AssetCategory.config);

            if (ebh.load(packet.ActivityCollectionName))
            {
                fromPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "ActivityCollection loaded on entity " + e.EntityId, EnumChatType.Notification);
            } else
            {
                fromPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Failed to load ActivityCollection", EnumChatType.Notification);
            }
        }

        private TextCommandResult onCmdAedit(TextCommandCallingArgs args)
        {
            if (dlg == null)
            {
                dlg = new GuiDialogActivityCollections(capi);
                dlg.OnClosed += Dlg_OnClosed;
                dlg.TryOpen();
            }
            return TextCommandResult.Success();
        }

        private void Dlg_OnClosed()
        {
            dlg = null;
        }
    }

    public class GuiDialogActivityCollections : GuiDialog
    {
        public OrderedDictionary<AssetLocation, EntityActivityCollection> collections = new OrderedDictionary<AssetLocation, EntityActivityCollection> ();
        protected ElementBounds clipBounds;
        protected GuiElementCellList<EntityActivityCollection> listElem;
        int selectedIndex = -1;

        EntityActivitySystem vas;

        public static long EntityId;
        public static bool AutoApply=true;

        public GuiDialogActivityCollections(ICoreClientAPI capi) : base(capi)
        {
            vas = new EntityActivitySystem(capi.World.Player.Entity);
            Compose();            
        }

        private void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);

            double listHeight = 200;
            ElementBounds listBounds = ElementBounds.Fixed(0, 25, 400, listHeight);
            clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds.FlatCopy().FixedGrow(3).WithFixedOffset(0, 0);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + listBounds.fixedWidth + 7).WithFixedWidth(20);

            var btnFont = CairoFont.SmallButtonText(EnumButtonStyle.Small);
            ElementBounds mbBounds = leftButton.FlatCopy().FixedUnder(clipBounds, 10);
            ElementBounds textfieldbounds = ElementBounds.FixedSize(90, 21).WithAlignment(EnumDialogArea.RightFixed).FixedUnder(clipBounds, 10);

            var offx = btnFont.GetTextExtents("Modify Activity").Width / RuntimeEnv.GUIScale + 20;

            SingleComposer =
                capi.Gui
                .CreateCompo("activitycollections", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Activity collections", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                    .AddInset(insetBounds, 3)
                    .AddCellList(listBounds, createCell, collections.Values, "collections")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .AddSmallButton(Lang.Get("Modify Activity collection"), OnModifyCollection, mbBounds, EnumButtonStyle.Small, "modifycollection")
                .AddTextInput(textfieldbounds.FlatCopy().WithFixedOffset(-offx, 0), null, CairoFont.WhiteDetailText(), "entityid")
                .AddSmallButton(Lang.Get("Apply to entity"), ApplyToEntityId, textfieldbounds.FlatCopy().WithFixedPadding(4, 1), EnumButtonStyle.Small, "applytoentityid")

                .AddSwitch(onAutoApply, textfieldbounds.BelowCopy(0,7).WithFixedOffset(-offx - 69, 0), "autocopy", 20)
                .AddStaticText("Autoapply modifications", CairoFont.WhiteDetailText().WithFontSize(14), textfieldbounds.BelowCopy(0,9).WithFixedOffset(-00, 0).WithFixedWidth(178))

                .AddSmallButton(Lang.Get("Close"), OnClose, leftButton.FixedUnder(clipBounds, 80))
                .AddSmallButton(Lang.Get("Create collection"), OnCreateCollection, rightButton.FixedUnder(clipBounds, 80), EnumButtonStyle.Normal, "create")
            ;

            
            if (EntityId != 0) SingleComposer.GetTextInput("entityid").SetValue(EntityId);
            else SingleComposer.GetTextInput("entityid").SetPlaceHolderText("entity id");

            SingleComposer.GetSwitch("autocopy").On = AutoApply;


            listElem = SingleComposer.GetCellList<EntityActivityCollection>("collections");
            listElem.BeforeCalcBounds();
            listElem.UnscaledCellVerPadding = 0;
            listElem.unscaledCellSpacing = 5;
            SingleComposer.EndChildElements().Compose();
            ReloadCells();
            updateScrollbarBounds();
            updateButtonStates();
        }

        private void onAutoApply(bool on)
        {
            AutoApply = on;
        }

        public bool ApplyToEntityId()
        {
            if (selectedIndex < 0) return true;

            capi.Network.GetChannel("activityEditor").SendPacket(new ApplyConfigPacket()
            {
                ActivityCollectionName = collections.GetValueAtIndex(selectedIndex).Name,
                EntityId = EntityId = SingleComposer.GetTextInput("entityid").GetText().ToInt()
            });
            return true;
        }

        private void updateButtonStates()
        {
            SingleComposer.GetButton("modifycollection").Enabled = selectedIndex >= 0;
            SingleComposer.GetButton("applytoentityid").Enabled = selectedIndex >= 0;
        }


        GuiDialogActivityCollection editDlg;
        private bool OnModifyCollection()
        {
            if (selectedIndex < 0) return true;

            var key = collections.GetKeyAtIndex(selectedIndex);
            editDlg = new GuiDialogActivityCollection(capi, this, collections[key], vas, key);
            editDlg.TryOpen();
            return true;
        }


        public void ReloadCells()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            capi.Assets.Reload(AssetCategory.config);
            var files = capi.Assets.GetMany("config/activitycollections/");
            collections.Clear();
            foreach (var file in files)
            {
                var c = collections[file.Location] = file.ToObject<EntityActivityCollection>(settings);
                c.OnLoaded(vas);
            }

            listElem.ReloadCells(collections.Values);
        }

        GuiDialogActivityCollection createDlg;
        private bool OnCreateCollection()
        {
            if (createDlg != null && createDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "alreadyopened", Lang.Get("Close the other activity collection dialog first"));
                return false;
            }

            createDlg = new GuiDialogActivityCollection(capi, this, null, vas, null);
            createDlg.TryOpen();
            return true;
        }

        private bool OnClose()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private IGuiElementCell createCell(EntityActivityCollection collection, ElementBounds bounds)
        {
            bounds.fixedPaddingY = 0;
            var cellElem = new ActivityCellEntry(capi, bounds, collection.Name, collection.Activities.Count +" activities", didClickCell);
            return cellElem;
        }

        private void didClickCell(int index)
        {
            foreach (var val in listElem.elementCells) (val as ActivityCellEntry).Selected = false;
            selectedIndex = index;
            (listElem.elementCells[index] as ActivityCellEntry).Selected = true;

            updateButtonStates();
        }

        void updateScrollbarBounds()
        {
            if (listElem == null) return;
            SingleComposer.GetScrollbar("scrollbar")?.Bounds.CalcWorldBounds();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights(
                (float)(clipBounds.fixedHeight),
                (float)(listElem.Bounds.fixedHeight)
            );
        }

        private void OnNewScrollbarValue(float value)
        {
            listElem = SingleComposer.GetCellList<EntityActivityCollection>("collections");
            listElem.Bounds.fixedY = 0 - value;
            listElem.Bounds.CalcWorldBounds();
        }

        public override string ToggleKeyCombinationCode => null;
    }


    // Name field
    // List of activities
    public class GuiDialogActivityCollection : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        GuiDialogActivityCollections dlg;
        public EntityActivityCollection collection;
        private EntityActivitySystem vas;
        public AssetLocation assetpath;
        bool isNew = false;
        int selectedIndex = -1;

        public GuiDialogActivityCollection(ICoreClientAPI capi, GuiDialogActivityCollections dlg, EntityActivityCollection collection, EntityActivitySystem vas, AssetLocation assetpath) : base(capi)
        {
            if (collection == null)
            {
                isNew= true;
                collection = new EntityActivityCollection();
            }
            this.vas = vas;
            this.assetpath = assetpath;
            this.dlg = dlg;
            this.collection = collection.Clone();
            Compose();
        }

        protected ElementBounds clipBounds;
        protected GuiElementCellList<EntityActivity> listElem;


        public GuiDialogActivityCollection(ICoreClientAPI capi) : base(capi)
        {
            Compose();
        }

        private void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding+150, 20);


            ElementBounds textlabelBounds = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 20, 200, 20);
            ElementBounds textBounds = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 200, 25).FixedUnder(textlabelBounds);
            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);

            double listHeight = 300;
            ElementBounds listBounds = ElementBounds.Fixed(0, 0, 400, listHeight).FixedUnder(textBounds, 10);
            clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds.FlatCopy().FixedGrow(3);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + listBounds.fixedWidth + 7).WithFixedWidth(20);

            var btnFont = CairoFont.SmallButtonText(EnumButtonStyle.Small);

            ElementBounds dbBounds = leftButton.FlatCopy().FixedUnder(clipBounds, 10);
            ElementBounds mbBounds = dbBounds.FlatCopy().WithFixedOffset(btnFont.GetTextExtents("Delete Activity").Width / RuntimeEnv.GUIScale + 16 + 10,0);
            ElementBounds abBounds = dbBounds.FlatCopy().FixedRightOf(mbBounds, 3).WithFixedOffset(btnFont.GetTextExtents("Modify Activity").Width / RuntimeEnv.GUIScale + 16 + 10, 0);

            ElementBounds exeLabelBounds = ElementBounds.Fixed(0, 0, 200, 25).WithFixedPadding(4, 2).FixedUnder(dbBounds, 35);
            ElementBounds exeBounds = ElementBounds.Fixed(0,0).WithFixedPadding(4,2).FixedUnder(exeLabelBounds, 0);
            var len = (int)(btnFont.GetTextExtents("Execute Activity").Width / RuntimeEnv.GUIScale + 16);
            ElementBounds exe2Bounds = ElementBounds.Fixed(len, 0).WithFixedPadding(4, 2).FixedUnder(exeLabelBounds, 0);
            ElementBounds exe3Bounds = ElementBounds.Fixed(len + (int)(btnFont.GetTextExtents("Stop actions").Width / RuntimeEnv.GUIScale + 16), 0).WithFixedPadding(4, 2).FixedUnder(exeLabelBounds, 0);

            collection.Activities = collection.Activities.OrderByDescending(a => a.Priority).ToList();

            SingleComposer =
                capi.Gui
                .CreateCompo("activitycollection-" + (this.assetpath?.ToShortString() ?? "new"), dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Create/Modify Activity collection", OnTitleBarClose)
                .BeginChildElements(bgBounds) 

                .AddStaticText("Collection Name", CairoFont.WhiteDetailText(), textlabelBounds)
                .AddTextInput(textBounds, onNameChanced, CairoFont.WhiteDetailText(), "name")
                .BeginClip(clipBounds)
                    .AddInset(insetBounds, 3)
                    .AddCellList(listBounds, createCell, collection.Activities, "activities")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")

                .AddSmallButton(Lang.Get("Delete Activity"), OnDeleteActivity, dbBounds, EnumButtonStyle.Small, "deleteactivity")
                .AddSmallButton(Lang.Get("Modify Activity"), OnModifyActivity, mbBounds, EnumButtonStyle.Small, "modifyactivity")
                .AddSmallButton(Lang.Get("Add Activity"), OnCreateActivity, abBounds, EnumButtonStyle.Small)

                .AddIf(GuiDialogActivityCollections.EntityId > 0)
                    .AddStaticText("For entity with id " + GuiDialogActivityCollections.EntityId, CairoFont.WhiteDetailText(), exeLabelBounds)
                    .AddSmallButton(Lang.Get("Execute Activity"), OnExecuteActivity, exeBounds, EnumButtonStyle.Small, "exec")
                    .AddSmallButton(Lang.Get("Stop actions"), OnStopActivity, exe2Bounds, EnumButtonStyle.Small, "stop")
                    .AddSmallButton(Lang.Get("Toggle Autorun"), OnTogglePauseActivity, exe3Bounds, EnumButtonStyle.Small, "pause")
                .EndIf()

                .AddSmallButton(Lang.Get("Close"), OnCancel, leftButton = leftButton.FlatCopy().FixedUnder(exeBounds, 60))
                .AddSmallButton(Lang.Get("Save Edits"), OnSave, rightButton.FixedUnder(exeBounds, 60), EnumButtonStyle.Normal, "create")
            ;


            listElem = SingleComposer.GetCellList<EntityActivity>("activities");
            listElem.BeforeCalcBounds();
            listElem.UnscaledCellVerPadding = 0;
            listElem.unscaledCellSpacing = 5;
            SingleComposer.EndChildElements().Compose();

            SingleComposer.GetTextInput("name").SetValue(collection.Name);

            updateScrollbarBounds();
            updateButtonStates();
        }

        private bool OnStopActivity()
        {
            capi.SendChatMessage("/dev aee e[id=" + GuiDialogActivityCollections.EntityId + "] stop");
            return true;
        }

        bool pause = false;
        private bool OnTogglePauseActivity()
        {
            pause= !pause;
            capi.SendChatMessage("/dev aee e[id=" + GuiDialogActivityCollections.EntityId + "] pause " + pause);
            return true;
        }

        private bool OnExecuteActivity()
        {
            capi.SendChatMessage("/dev aee e[id="+GuiDialogActivityCollections.EntityId+"] runa " + collection.Activities[selectedIndex].Name);
            return true;
        }

        public int SaveActivity(EntityActivity activity, int index)
        {
            if (index >= collection.Activities.Count)
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to save, out of index bounds");
                return -1;
            }

            if (index < 0) collection.Activities.Add(activity);
            else collection.Activities[index] = activity;

            collection.Activities = collection.Activities.OrderByDescending(a => a.Priority).ToList();

            listElem.ReloadCells(collection.Activities);
            OnSave();

            return index < 0 ? collection.Activities.Count - 1 : index;            
        }

        private void updateButtonStates()
        {
            SingleComposer.GetButton("deleteactivity").Enabled = selectedIndex >= 0;
            SingleComposer.GetButton("modifyactivity").Enabled = selectedIndex >= 0;
            if (SingleComposer.GetButton("exec") != null)
            {
                SingleComposer.GetButton("exec").Enabled = GuiDialogActivityCollections.EntityId != 0 && selectedIndex >= 0;
            }
        }

        GuiDialogActivity activityDlg;
        private bool OnDeleteActivity()
        {
            if (activityDlg != null && activityDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to delete, place close any currently opened activity dialogs first");
                return false;
            }

            if (selectedIndex < 0) return true;
            collection.Activities.RemoveAt(selectedIndex);
            listElem.ReloadCells(collection.Activities);

            return true;
        }

        private bool OnModifyActivity()
        {
            if (selectedIndex < 0) return true;
            if (activityDlg != null && activityDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to modify. Close any currently opened activity dialogs first");
                return false;
            }

            activityDlg = new GuiDialogActivity(capi, this, vas, collection.Activities[selectedIndex], selectedIndex);
            activityDlg.TryOpen();
            activityDlg.OnClosed += () => activityDlg = null;

            return true;
        }

        private bool OnSave()
        {
            collection.Name = SingleComposer.GetTextInput("name").GetText();
            if (collection.Name == null || collection.Name.Length == 0 || collection.Activities.Count == 0)
            {
                capi.TriggerIngameError(this, "missingfields", "Requires at least one activity and a name");
                return false;
            }

            string filepath;
            if (isNew)
            {
                filepath = Path.Combine(GamePaths.AssetsPath, "survival", "config", "activitycollections", GamePaths.ReplaceInvalidChars(collection.Name) + ".json");
            } else
            {
                filepath = Path.Combine(GamePaths.AssetsPath, "survival", this.assetpath.Path);
            }

            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            string json = JsonConvert.SerializeObject(collection, Formatting.Indented, settings);
            File.WriteAllText(filepath, json);

            dlg.ReloadCells();

            capi.Network.GetChannel("activityEditor").SendPacket(new ActivityCollectionsJsonPacket()
            {
                Collections = new List<string>() { json }
            });

            if (GuiDialogActivityCollections.AutoApply && GuiDialogActivityCollections.EntityId != 0)
            {
                capi.SendChatMessage("/dev aee e[id=" + GuiDialogActivityCollections.EntityId + "] stop");
                dlg.ApplyToEntityId();
            }

            return true;
        }

        private void onNameChanced(string name)
        {
            collection.Name = name;
        }

        private bool OnCreateActivity()
        {
            activityDlg = new GuiDialogActivity(capi, this, vas, null, -1);
            activityDlg.TryOpen();
            activityDlg.OnClosed += () => activityDlg = null;
                
            return true;
        }

        private bool OnCancel()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private IGuiElementCell createCell(EntityActivity collection, ElementBounds bounds)
        {
            bounds.fixedPaddingY = 0;
            var cellElem = new ActivityCellEntry(capi, bounds, "P" + Math.Round(collection.Priority,2) + "  " + collection.Name, collection.Actions.Length + " actions, " + collection.Conditions.Length + " conditions", didClickCell);
            return cellElem;
        }

        private void didClickCell(int index)
        {
            foreach (var val in listElem.elementCells) (val as ActivityCellEntry).Selected = false;
            selectedIndex = index;
            (listElem.elementCells[index] as ActivityCellEntry).Selected = true;
            updateButtonStates();
        }

        void updateScrollbarBounds()
        {
            if (listElem == null) return;
            SingleComposer.GetScrollbar("scrollbar")?.Bounds.CalcWorldBounds();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights(
                (float)(clipBounds.fixedHeight),
                (float)(listElem.Bounds.fixedHeight)
            );
        }

        private void OnNewScrollbarValue(float value)
        {
            listElem = SingleComposer.GetCellList<EntityActivity>("activities");
            listElem.Bounds.fixedY = 0 - value;
            listElem.Bounds.CalcWorldBounds();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            activityDlg?.TryClose();
            activityDlg = null;
        }
    }


    // Name
    // Priority
    // Slot
    // List of actions
    // List of conditions
    public class GuiDialogActivity : GuiDialog
    {
        private GuiDialogActivityCollection guiDialogActivityCollection;
        public EntityActivity entityActivity;
        public bool Saved;

        protected ElementBounds actionsClipBounds;
        protected ElementBounds conditionsClipBounds;
        protected GuiElementCellList<IEntityAction> actionListElem;
        protected GuiElementCellList<IActionCondition> conditionsListElem;
        bool isNew = false;
        public override string ToggleKeyCombinationCode => null;

        int selectedActionIndex = -1;
        int selectedConditionIndex = -1;

        int collectionIndex;

        public GuiDialogActivity(ICoreClientAPI capi, GuiDialogActivityCollection guiDialogActivityCollection, EntityActivitySystem vas, EntityActivity entityActivity, int collectionIndex) : base(capi)
        {
            if (entityActivity == null)
            {
                isNew = true;
                entityActivity = new EntityActivity();
            }

            this.guiDialogActivityCollection = guiDialogActivityCollection;
            this.entityActivity = entityActivity.Clone();
            this.entityActivity.OnLoaded(vas);
            this.collectionIndex = collectionIndex;
            Compose();
        }

        private void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding+550, 40);

            var btnFont = CairoFont.SmallButtonText(EnumButtonStyle.Small);

            ElementBounds namelabelBounds = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 20, 200, 20);
            ElementBounds nameBounds = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 200, 25).FixedUnder(namelabelBounds);

            ElementBounds prioritylabelBounds = namelabelBounds.RightCopy(15).WithFixedWidth(80);
            ElementBounds priorityBounds = nameBounds.RightCopy(15).WithFixedWidth(80);

            ElementBounds slotlabelBounds = prioritylabelBounds.RightCopy(15).WithFixedWidth(80);
            ElementBounds slotBounds = priorityBounds.RightCopy(15).WithFixedWidth(80);

            ElementBounds conditionOplabelBounds = slotlabelBounds.RightCopy(15).WithFixedWidth(100);
            ElementBounds opDropBounds = slotBounds.RightCopy(15).WithFixedWidth(80);

            ElementBounds actionsLabelBounds = nameBounds.BelowCopy(0, 15);

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);

            
            ElementBounds actionsListBounds = ElementBounds.Fixed(0, 0, 500, 200);
            actionsClipBounds = actionsListBounds.ForkBoundingParent().FixedUnder(actionsLabelBounds, -3);
            ElementBounds actionsInsetBounds = actionsListBounds.FlatCopy().FixedGrow(3);
            ElementBounds actionsScrollbarBounds = actionsInsetBounds.CopyOffsetedSibling(3 + actionsListBounds.fixedWidth + 7).WithFixedWidth(20);

            ElementBounds daBounds = leftButton.FlatCopy().FixedUnder(actionsClipBounds, 10);
            ElementBounds maBounds = daBounds.FlatCopy().WithFixedOffset(btnFont.GetTextExtents("Delete Action").Width / RuntimeEnv.GUIScale + 16 + 10, 0);
            ElementBounds aaBounds = daBounds.FlatCopy().FixedRightOf(maBounds).WithFixedOffset(btnFont.GetTextExtents("Modify Action").Width / RuntimeEnv.GUIScale + 16 + 10, 0);

            ElementBounds upBounds = daBounds.FlatCopy().FixedRightOf(aaBounds).WithFixedOffset(btnFont.GetTextExtents("Add Action").Width / RuntimeEnv.GUIScale + 16 + 20, 0);
            ElementBounds downBounds = daBounds.FlatCopy().FixedRightOf(upBounds).WithFixedOffset(btnFont.GetTextExtents("M. Up").Width / RuntimeEnv.GUIScale + 16 + 2, 0);


            ElementBounds conditionsLabelBounds = nameBounds.FlatCopy().FixedUnder(aaBounds, 10);

            ElementBounds conditionsListBounds = ElementBounds.Fixed(0, 0, 500, 100);
            conditionsClipBounds = conditionsListBounds.ForkBoundingParent().FixedUnder(conditionsLabelBounds, 0);
            ElementBounds conditionsInsetBounds = conditionsListBounds.FlatCopy().FixedGrow(3);
            ElementBounds conditionsScrollbarBounds = conditionsInsetBounds.CopyOffsetedSibling(3 + conditionsListBounds.fixedWidth + 7).WithFixedWidth(20);


            ElementBounds dtBounds = leftButton.FlatCopy().FixedUnder(conditionsClipBounds, 10);
            ElementBounds mtBounds = dtBounds.FlatCopy().WithFixedOffset(btnFont.GetTextExtents("Delete Condition").Width / RuntimeEnv.GUIScale + 16 + 10, 0);
            ElementBounds atBounds = dtBounds.FlatCopy().FixedRightOf(mtBounds).WithFixedOffset(btnFont.GetTextExtents("Modify Condition").Width / RuntimeEnv.GUIScale + 16 + 10, 0);

            ElementBounds vsBounds = ElementBounds.Fixed(0,0,25,25).WithAlignment(EnumDialogArea.RightFixed).FixedUnder(conditionsClipBounds, 10);

            string key = "activityedit-" + (guiDialogActivityCollection.assetpath?.ToShortString() ?? "new")  + "-" + collectionIndex;
            SingleComposer =
                capi.Gui
                .CreateCompo(key, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Create/Modify Activity", OnTitleBarClose)
                .BeginChildElements(bgBounds)

                .AddStaticText("Activity Name", CairoFont.WhiteDetailText(), namelabelBounds)
                .AddTextInput(nameBounds, onNameChanged, CairoFont.WhiteDetailText(), "name")

                .AddStaticText("Priority", CairoFont.WhiteDetailText(), prioritylabelBounds)
                .AddNumberInput(priorityBounds, onPrioChanged, CairoFont.WhiteDetailText(), "priority")

                .AddStaticText("Slot", CairoFont.WhiteDetailText(), slotlabelBounds)
                .AddNumberInput(slotBounds, onSlotChanged, CairoFont.WhiteDetailText(), "slot")

                .AddStaticText("Conditions OP", CairoFont.WhiteDetailText(), conditionOplabelBounds)
                .AddDropDown(new string[] { "OR", "AND" }, new string[] { "OR", "AND" }, (int)entityActivity.ConditionsOp, onDropOpChanged, opDropBounds, "opdropdown")

                .AddStaticText("Actions", CairoFont.WhiteDetailText(), actionsLabelBounds)

                .BeginClip(actionsClipBounds)
                    .AddInset(actionsInsetBounds, 3)
                    .AddCellList(actionsListBounds, createActionCell, entityActivity.Actions, "actions")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueActions, actionsScrollbarBounds, "actionsScrollbar")

                .AddSmallButton(Lang.Get("Delete Action"), OnDeleteAction, daBounds, EnumButtonStyle.Small, "deleteaction")
                .AddSmallButton(Lang.Get("Modify Action"), () => OpenActionDlg(entityActivity.Actions[selectedActionIndex]), maBounds, EnumButtonStyle.Small, "modifyaction")
                .AddSmallButton(Lang.Get("Add Action"), () => OpenActionDlg(null), aaBounds, EnumButtonStyle.Small, "addaction")

                .AddSmallButton(Lang.Get("M. Up"), moveUp, upBounds, EnumButtonStyle.Small, "moveup")
                .AddSmallButton(Lang.Get("M. Down"), moveDown, downBounds, EnumButtonStyle.Small, "movedown")

                .AddStaticText("Conditions", CairoFont.WhiteDetailText(), conditionsLabelBounds)
                .BeginClip(conditionsClipBounds)
                    .AddInset(conditionsInsetBounds, 3)
                    .AddCellList(conditionsListBounds, createConditionCell, entityActivity.Conditions, "conditions")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueconditions, conditionsScrollbarBounds, "conditionsScrollbar")

                .AddSmallButton(Lang.Get("Delete condition"), OnDeletecondition, dtBounds, EnumButtonStyle.Small, "deletecondition")
                .AddSmallButton(Lang.Get("Modify condition"), () => OpenconditionDlg(entityActivity.Conditions[selectedConditionIndex]), mtBounds, EnumButtonStyle.Small, "modifycondition")
                .AddSmallButton(Lang.Get("Add condition"), () => OpenconditionDlg(null), atBounds, EnumButtonStyle.Small, "addcondition")

                .AddIconButton("line", OnVisualize, vsBounds, "visualize")

                .AddSmallButton(Lang.Get("Close"), OnCancel, leftButton.FlatCopy().FixedUnder(vsBounds, 40))
                .AddSmallButton(Lang.Get("Save"), OnSaveActivity, rightButton.FixedUnder(vsBounds, 40), EnumButtonStyle.Normal, "create")
            ;


            SingleComposer.GetToggleButton("visualize").Toggleable = true;

            actionListElem = SingleComposer.GetCellList<IEntityAction>("actions");
            actionListElem.BeforeCalcBounds();
            actionListElem.UnscaledCellVerPadding = 0;
            actionListElem.unscaledCellSpacing = 5;


            conditionsListElem = SingleComposer.GetCellList<IActionCondition>("conditions");
            conditionsListElem.BeforeCalcBounds();
            conditionsListElem.UnscaledCellVerPadding = 0;
            conditionsListElem.unscaledCellSpacing = 5;

            SingleComposer.EndChildElements().Compose();

            updateButtonStates();
            updateScrollbarBounds();

            SingleComposer.GetTextInput("name").SetValue(entityActivity.Name);
            SingleComposer.GetNumberInput("priority").SetValue((float)entityActivity.Priority);
            SingleComposer.GetNumberInput("slot").SetValue(entityActivity.Slot + "");
        }

        private bool moveDown()
        {
            if (selectedActionIndex >= entityActivity.Actions.Length - 1) return false;

            if (editActionDlg != null && editActionDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to delete, place close any currently opened action dialogs first");
                return false;
            }

            var cur = entityActivity.Actions[selectedActionIndex];
            var next = entityActivity.Actions[selectedActionIndex + 1];

            entityActivity.Actions[selectedActionIndex] = next;
            entityActivity.Actions[selectedActionIndex + 1] = cur;

            actionListElem.ReloadCells(entityActivity.Actions);
            didClickActionCell(selectedActionIndex+1);
            return true;
        }

        private bool moveUp()
        {
            if (selectedActionIndex == 0) return false;

            if (editActionDlg != null && editActionDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to delete, place close any currently opened action dialogs first");
                return false;
            }

            var cur = entityActivity.Actions[selectedActionIndex];
            var prev = entityActivity.Actions[selectedActionIndex - 1];

            entityActivity.Actions[selectedActionIndex] = prev;
            entityActivity.Actions[selectedActionIndex - 1] = cur;

            actionListElem.ReloadCells(entityActivity.Actions);
            didClickActionCell(selectedActionIndex-1);
            return true;
        }

        private void onDropOpChanged(string code, bool selected)
        {
            entityActivity.ConditionsOp = code == "AND" ? EnumConditionLogicOp.AND : EnumConditionLogicOp.OR;
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            visualizer?.Dispose();

            editActionDlg?.TryClose();
            editCondDlg?.TryClose();
        }

        ActivityVisualizer visualizer;
        private void OnVisualize(bool on)
        {
            visualizer?.Dispose();
            if (on)
            {
                visualizer = new ActivityVisualizer(capi, entityActivity);
            }
        }

        private void updateButtonStates()
        {
            SingleComposer.GetButton("deleteaction").Enabled = selectedActionIndex >= 0;
            SingleComposer.GetButton("modifyaction").Enabled = selectedActionIndex >= 0;
            SingleComposer.GetButton("moveup").Enabled = selectedActionIndex >= 0;
            SingleComposer.GetButton("movedown").Enabled = selectedActionIndex >= 0;

            SingleComposer.GetButton("deletecondition").Enabled = selectedConditionIndex >= 0;
            SingleComposer.GetButton("modifycondition").Enabled = selectedConditionIndex >= 0;
        }


        GuiDialogEditcondition editCondDlg;
        private bool OpenconditionDlg(IActionCondition condition)
        {
            editCondDlg?.TryClose();
            editCondDlg = new GuiDialogEditcondition(capi, guiDialogActivityCollection, condition);
            editCondDlg.TryOpen();
            editCondDlg.OnClosed += () =>
            {
                if (editCondDlg.Saved)
                {
                    if (condition == null) entityActivity.Conditions = entityActivity.Conditions.Append(editCondDlg.actioncondition);
                    else entityActivity.Conditions[selectedConditionIndex] = editCondDlg.actioncondition;
                    conditionsListElem.ReloadCells(entityActivity.Conditions);
                    updateScrollbarBounds();
                }
            };
            return true;
        }

        GuiDialogEditAction editActionDlg;
        private bool OpenActionDlg(IEntityAction entityAction)
        {
            editActionDlg?.TryClose();
            editActionDlg = new GuiDialogEditAction(capi, guiDialogActivityCollection, entityAction);
            editActionDlg.TryOpen();
            editActionDlg.OnClosed += () =>
            {
                if (editActionDlg.Saved)
                {
                    if (entityAction == null)
                    {
                        if (selectedActionIndex != -1 && selectedActionIndex < entityActivity.Actions.Length-1)
                        {
                            entityActivity.Actions = entityActivity.Actions.InsertAt(editActionDlg.entityAction, selectedActionIndex+1);
                        } else
                        {
                            entityActivity.Actions = entityActivity.Actions.Append(editActionDlg.entityAction);
                        }
                        
                    }
                    else entityActivity.Actions[selectedActionIndex] = editActionDlg.entityAction;
                    actionListElem.ReloadCells(entityActivity.Actions);
                    updateScrollbarBounds();
                }
            };
            return true;
        }

        private bool OnDeletecondition()
        {
            if (editCondDlg != null && editCondDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to delete, place close any currently opened condition dialogs first");
                return false;
            }

            entityActivity.Conditions = entityActivity.Conditions.RemoveAt(selectedConditionIndex);
            selectedConditionIndex = Math.Max(0, selectedConditionIndex-1);
            conditionsListElem.ReloadCells(entityActivity.Conditions);
            if (entityActivity.Conditions.Length > 0) didClickconditionCell(selectedConditionIndex);
            else selectedConditionIndex = -1;
            updateButtonStates();
            return true;
        }
        private bool OnDeleteAction()
        {
            if (editActionDlg != null && editActionDlg.IsOpened())
            {
                capi.TriggerIngameError(this, "cantsave", "Unable to delete, place close any currently opened action dialogs first");
                return false;
            }

            entityActivity.Actions = entityActivity.Actions.RemoveAt(selectedActionIndex);
            selectedActionIndex = Math.Max(0, selectedActionIndex-1);
            actionListElem.ReloadCells(entityActivity.Actions);
            if (entityActivity.Actions.Length > 0) didClickActionCell(selectedActionIndex);
            else selectedActionIndex = -1;
            updateButtonStates();
            return true;
        }

        private bool OnSaveActivity()
        {
            entityActivity.Priority = SingleComposer.GetNumberInput("priority").GetValue();

            bool canSave = entityActivity.Actions.Length > 0 && entityActivity.Conditions.Length > 0 && entityActivity.Name != null && entityActivity.Name.Length > 0;
            if (!canSave)
            {
                capi.TriggerIngameError(this, "missingfields", "Requires at least 1 action, 1 condition and activity name");
            }

            if (canSave)
            {
                collectionIndex = guiDialogActivityCollection.SaveActivity(entityActivity, collectionIndex);
            }

            return canSave;
        }

        private void onSlotChanged(string text)
        {
            entityActivity.Slot = text.ToInt();
        }

        private void onPrioChanged(string text)
        {
            entityActivity.Priority = text.ToDouble();
        }

        private void onNameChanged(string text)
        {
            entityActivity.Name = text;
        }

        private IGuiElementCell createActionCell(IEntityAction action, ElementBounds bounds)
        {
            bounds.fixedPaddingY = 0;
            var cellElem = new ActivityCellEntry(capi, bounds, action.Type, action.ToString(), didClickActionCell);

            return cellElem;
        }
        private IGuiElementCell createConditionCell(IActionCondition condition, ElementBounds bounds)
        {
            bounds.fixedPaddingY = 0;
            var cellElem = new ActivityCellEntry(capi, bounds, condition.Type, condition.ToString(), didClickconditionCell);
            return cellElem;
        }

        private void didClickconditionCell(int index)
        {
            foreach (var val in conditionsListElem.elementCells) (val as ActivityCellEntry).Selected = false;
            selectedConditionIndex = index;
            (conditionsListElem.elementCells[index] as ActivityCellEntry).Selected = true;

            updateButtonStates();
        }

        private void didClickActionCell(int index)
        {
            foreach (var val in actionListElem.elementCells) (val as ActivityCellEntry).Selected = false;
            selectedActionIndex = index;
            (actionListElem.elementCells[index] as ActivityCellEntry).Selected = true;

            updateButtonStates();
        }

        private bool OnCancel()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }
        void updateScrollbarBounds()
        {
            if (actionListElem == null) return;
            SingleComposer.GetScrollbar("actionsScrollbar")?.Bounds.CalcWorldBounds();

            SingleComposer.GetScrollbar("actionsScrollbar")?.SetHeights(
                (float)(actionsClipBounds.fixedHeight),
                (float)(actionListElem.Bounds.fixedHeight)
            );


            SingleComposer.GetScrollbar("conditionsScrollbar")?.Bounds.CalcWorldBounds();

            SingleComposer.GetScrollbar("conditionsScrollbar")?.SetHeights(
                (float)(conditionsClipBounds.fixedHeight),
                (float)(conditionsListElem.Bounds.fixedHeight)
            );
        }

        private void OnNewScrollbarValueActions(float value)
        {
            actionListElem = SingleComposer.GetCellList<IEntityAction>("actions");
            actionListElem.Bounds.fixedY = 0 - value;
            actionListElem.Bounds.CalcWorldBounds();
        }

        private void OnNewScrollbarValueconditions(float value)
        {
            conditionsListElem = SingleComposer.GetCellList<IActionCondition>("conditions");
            conditionsListElem.Bounds.fixedY = 0 - value;
            conditionsListElem.Bounds.CalcWorldBounds();
        }

    }


    // Action Type
    // Action specific edit fields
    public class GuiDialogEditAction : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        GuiDialogActivityCollection guiDialogActivityCollection;

        public IEntityAction entityAction;
        public bool Saved;

        public GuiDialogEditAction(ICoreClientAPI capi, GuiDialogActivityCollection guiDialogActivityCollection, IEntityAction entityAction) : base(capi)
        {
            this.entityAction = entityAction?.Clone();
            this.guiDialogActivityCollection = guiDialogActivityCollection;
            Compose();
        }

        private void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);

            var dropDownBounds = ElementBounds.Fixed(0, 30, 160, 25);
            var chBounds = ElementBounds.Fixed(0, 70, 300, 400);
            chBounds.verticalSizing = ElementSizing.FitToChildren;
            chBounds.AllowNoChildren = true;

            var actionTypes = ActivityModSystem.ActionTypes;
            string[] values = actionTypes.Keys.ToArray();
            string[] names = actionTypes.Keys.ToArray();

            SingleComposer =
                capi.Gui
                .CreateCompo("editaction", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Edit Action", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .AddDropDown(values, names, values.IndexOf(entityAction?.Type ?? ""), onSelectionChanged, dropDownBounds)
                .BeginChildElements(chBounds)
            ;

            if (entityAction != null)
            {   
                entityAction.AddGuiEditFields(capi, SingleComposer);
            }

            var b = SingleComposer.LastAddedElementBounds;
            SingleComposer
                .EndChildElements()
                .AddSmallButton(Lang.Get("Cancel"), OnClose, leftButton.FixedUnder(b, 80))
                .AddSmallButton(Lang.Get("Confirm"), OnSave, rightButton.FixedUnder(b, 80), EnumButtonStyle.Normal, "confirm")
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetButton("confirm").Enabled = entityAction != null;
        }

        private bool OnClose()
        {
            TryClose();
            return true;
        }

        private bool OnSave()
        {
            if (!entityAction.StoreGuiEditFields(capi, SingleComposer))
            {
                return true;
            }
            Saved = true;
            TryClose();
            return true;
        }

        private void onSelectionChanged(string code, bool selected)
        {
            var actionTypes = ActivityModSystem.ActionTypes;
            entityAction = (IEntityAction)Activator.CreateInstance(actionTypes[code]);
            SingleComposer.GetButton("confirm").Enabled = entityAction != null;
            Compose();
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }
    }


    // condition specific edit fields
    public class GuiDialogEditcondition : GuiDialog
    {
        private GuiDialogActivityCollection guiDialogActivityCollection;
        public IActionCondition actioncondition;
        public bool Saved;

        public override string ToggleKeyCombinationCode => null;
        public GuiDialogEditcondition(ICoreClientAPI capi) : base(capi)
        {
        }

        public GuiDialogEditcondition(ICoreClientAPI capi, GuiDialogActivityCollection guiDialogActivityCollection, IActionCondition actioncondition) : this(capi)
        {
            this.guiDialogActivityCollection = guiDialogActivityCollection;
            this.actioncondition = actioncondition?.Clone();
            Compose();
        }

        private void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);

            var dropDownBounds = ElementBounds.Fixed(0, 30, 160, 25);
            var chBounds = ElementBounds.Fixed(0, 100, 300, 400);
            chBounds.verticalSizing = ElementSizing.FitToChildren;
            chBounds.AllowNoChildren = true;

            var conditionTypes = ActivityModSystem.ConditionTypes;
            string[] values = conditionTypes.Keys.ToArray();
            string[] names = conditionTypes.Keys.ToArray();

            SingleComposer =
                capi.Gui
                .CreateCompo("editcondition", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Edit condition", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .AddDropDown(values, names, values.IndexOf(actioncondition?.Type ?? ""), onSelectionChanged, dropDownBounds)
                .AddSwitch(null, ElementBounds.FixedSize(20,20).FixedUnder(dropDownBounds, 10), "invert", 20, 4)
                .AddStaticText("Invert Condition", CairoFont.WhiteDetailText(), ElementBounds.Fixed(30, 10, 200, 25).FixedUnder(dropDownBounds))
                .BeginChildElements(chBounds)
            ;

            if (actioncondition != null)
            {
                actioncondition.AddGuiEditFields(capi, SingleComposer);
            }

            var b = SingleComposer.LastAddedElementBounds;
            SingleComposer
                .EndChildElements()
                .AddSmallButton(Lang.Get("Cancel"), OnClose, leftButton.FixedUnder(b, 110))
                .AddSmallButton(Lang.Get("Confirm"), OnSave, rightButton.FixedUnder(b, 110), EnumButtonStyle.Normal, "confirm")
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetButton("confirm").Enabled = actioncondition != null;
            SingleComposer.GetSwitch("invert").On = actioncondition?.Invert ?? false;
        }



        private void onSelectionChanged(string code, bool selected)
        {
            var conditionTypes = ActivityModSystem.ConditionTypes;
            actioncondition = (IActionCondition)Activator.CreateInstance(conditionTypes[code]);
            SingleComposer.GetButton("confirm").Enabled = actioncondition != null;
            Compose();
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }


        private bool OnClose()
        {
            TryClose();
            return true;
        }

        private bool OnSave()
        {
            Saved = true;
            actioncondition.Invert = SingleComposer.GetSwitch("invert").On;
            actioncondition.StoreGuiEditFields(capi, SingleComposer);
            TryClose();
            return true;
        }
    }
}