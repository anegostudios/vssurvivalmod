using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TpLocations
    {
        public TeleporterLocation ForLocation;
        public Dictionary<BlockPos, TeleporterLocation> Locations;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TeleporterLocation
    {
        public string SourceName;
        public BlockPos SourcePos;
        public string TargetName;
        public BlockPos TargetPos;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class DidTeleport
    {

    }


    public class TeleporterManager : ModSystem
    {
        Dictionary<BlockPos, TeleporterLocation> Locations = new Dictionary<BlockPos, TeleporterLocation>();
        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;

        // Client values
        List<BlockPos> targetPositionsOrdered = new List<BlockPos>();
        List<string> targetNamesOrdered = new List<string>();

        IClientNetworkChannel clientChannel;
        ICoreClientAPI capi;
        GuiJsonDialog dialog;
        JsonDialogSettings dialogSettings;
        TeleporterLocation forLocation = new TeleporterLocation();

        public long lastTeleCollideMsOwnPlayer = 0;
        public long lastTranslocateCollideMsOwnPlayer = 0;
        public long lastTranslocateCollideMsOtherPlayer = 0;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        internal TeleporterLocation GetOrCreateLocation(BlockPos pos)
        {
            TeleporterLocation loc;
            if (Locations.TryGetValue(pos, out loc))
            {
                return loc;
            }

            loc = new TeleporterLocation()
            {
                SourceName = "Location-" + (Locations.Count + 1),
                SourcePos = pos.Copy()
            };

            Locations[loc.SourcePos] = loc;

            return loc;
        }

        public void DeleteLocation(BlockPos pos)
        {
            Locations.Remove(pos);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Event.SaveGameLoaded += OnLoadGame;
            api.Event.GameWorldSave += OnSaveGame;

            api.Event.RegisterEventBusListener(OnConfigEventServer, 0.5, "configTeleporter");

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .GetOrCreate("settlpos")
                .WithDesc("Set translocator target teleport position of currently looked at translocator")
                .RequiresPrivilege(Privilege.setspawn)
                .WithArgs(parsers.WorldPosition("translocator position"), parsers.WorldPosition("target position"), parsers.OptionalBool("relative"))
                .HandleWith(handleSetTlPos)
            ;

            serverChannel =
               api.Network.RegisterChannel("tpManager")
               .RegisterMessageType(typeof(TpLocations))
               .RegisterMessageType(typeof(TeleporterLocation))
               .RegisterMessageType(typeof(DidTeleport))
               .SetMessageHandler<TeleporterLocation>(OnSetLocationReceived)
            ;

        }

        private TextCommandResult handleSetTlPos(TextCommandCallingArgs args)
        {
            var tlpos = (args[0] as Vec3d).AsBlockPos;
            var block = sapi.World.BlockAccessor.GetBlock(tlpos);
            if (block is BlockStaticTranslocator && block.Variant["state"] == "broken")
            {
                sapi.World.BlockAccessor.SetBlock(sapi.World.GetBlock(block.CodeWithVariant("state", "normal")).Id, tlpos);
            }
            BlockEntityStaticTranslocator bet = sapi.World.BlockAccessor.GetBlockEntity(tlpos) as BlockEntityStaticTranslocator;

            if (bet == null)
            {
                return TextCommandResult.Error("Supplied position does not contain a translocator. You can use l[] to use the looked at position.");
            }

            bool wasRepaired = bet.FullyRepaired;
            if (!wasRepaired)
            {
                bet.findNextChunk = false;
                bet.repairState = bet.RepairInteractionsRequired;
            }
            bet.tpLocation = (args[1] as Vec3d).AsBlockPos;
            bet.tpLocationIsOffset = args.Parsers[2].IsMissing ? false : (bool)args[2];
            if (!wasRepaired) bet.setupGameTickers();
            bet.DoActivate();

            return TextCommandResult.Success("Target position set.");
        }

        private void OnSetLocationReceived(IServerPlayer fromPlayer, TeleporterLocation networkMessage)
        {
            Locations[networkMessage.SourcePos].SourcePos = networkMessage.SourcePos;
            Locations[networkMessage.SourcePos].TargetPos = networkMessage.TargetPos;
            Locations[networkMessage.SourcePos].SourceName = networkMessage.SourceName;
            Locations[networkMessage.SourcePos].TargetName = networkMessage.TargetName;

            BlockEntityTeleporter be = sapi.World.BlockAccessor.GetBlockEntity(networkMessage.SourcePos) as BlockEntityTeleporter;
            if (be != null) be.MarkDirty();
        }

        private void OnSaveGame()
        {
            sapi.WorldManager.SaveGame.StoreData("tpLocations", SerializerUtil.Serialize(Locations));
        }

        private void OnLoadGame()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("tpLocations");
                if (data != null) Locations = SerializerUtil.Deserialize<Dictionary<BlockPos, TeleporterLocation>>(data);
            } catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading tp locations: {0}", e);
            }
            
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterEventBusListener(OnConfigEventClient, 0.5, "configTeleporter");

            clientChannel =
                api.Network.RegisterChannel("tpManager")
               .RegisterMessageType(typeof(TpLocations))
               .RegisterMessageType(typeof(TeleporterLocation))
               .RegisterMessageType(typeof(DidTeleport))
               .SetMessageHandler<TpLocations>(OnLocationsReceived)
               .SetMessageHandler<DidTeleport>(OnTranslocateClient)
            ;
        }


        private void OnLocationsReceived(TpLocations networkMessage)
        {
            this.Locations = networkMessage.Locations;
            forLocation = networkMessage.ForLocation;
            dialog.ReloadValues();
        }

        private void OnConfigEventClient(string eventName, ref EnumHandling handling, IAttribute data)
        {
            capi.Assets.Reload(AssetCategory.dialog);
            dialogSettings = capi.Assets.Get<JsonDialogSettings>(new AssetLocation("dialog/tpmanager.json"));
            dialogSettings.OnGet = OnGetValuesDialog;
            dialogSettings.OnSet = OnSetValuesDialog;

            dialog = new GuiJsonDialog(dialogSettings, capi);
            dialog.TryOpen();
        }

        private void OnSetValuesDialog(string elementCode, string newValue)
        {
            switch (elementCode)
            {
                case "name":
                    forLocation.SourceName = newValue;
                    break;
                case "targetlocation":
                    if (newValue.Length > 0)
                    {
                        int pos = int.Parse(newValue);
                        forLocation.TargetPos = targetPositionsOrdered[pos];
                        forLocation.TargetName = targetNamesOrdered[pos];
                    }
                    break;
                case "cancel":
                    dialog.TryClose();
                    break;
                case "save":
                    clientChannel.SendPacket(forLocation);
                    dialog.TryClose();
                    break;
            }
        }

        

        private string OnGetValuesDialog(string elementCode)
        {
            switch (elementCode)
            {
                case "cancel": return "Cancel";
                case "save": return "Save";
                case "name":
                    return forLocation?.SourceName;
                case "targetlocation":
                    {
                        targetPositionsOrdered = Locations.OrderBy(val => val.Value.SourceName).Select(val => val.Value.SourcePos).ToList();
                        targetNamesOrdered.Clear();
                        List<int> values = new List<int>();
                        int i = 0;
                        foreach (BlockPos pos in targetPositionsOrdered)
                        {
                            values.Add(i++);
                            targetNamesOrdered.Add(Locations[pos].SourceName);
                        }

                        return string.Join("||", values) + "\n" + string.Join("||", targetNamesOrdered) + "\n" + targetPositionsOrdered.IndexOf(forLocation.TargetPos);
                    }
            }

            return "";
        }

        private void OnConfigEventServer(string eventName, ref EnumHandling handling, IAttribute data)
        {
            ITreeAttribute tree = data as ITreeAttribute;
            TeleporterLocation forLoc = GetOrCreateLocation(new BlockPos(tree.GetInt("posX"), tree.GetInt("posY"), tree.GetInt("posZ")));

            IServerPlayer player = sapi.World.PlayerByUid(tree.GetString("playerUid")) as IServerPlayer;

            serverChannel.SendPacket(new TpLocations()
            {
                ForLocation = forLoc,
                Locations = Locations
            }, player);
        }


        public void DidTranslocateServer(IServerPlayer player)
        {
            serverChannel.SendPacket(new DidTeleport(), player);
        }

        private void OnTranslocateClient(DidTeleport networkMessage)
        {
            capi.World.PlaySoundAt(new AssetLocation("sounds/effect/translocate-breakdimension"), 0, 0, 0, null, false);
            capi.World.AddCameraShake(0.9f);
        }

    }
}
