using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MapLayerData
    {
        public string ForMapLayer;
        public byte[] Data;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class OnMapToggle
    {
        public bool OpenOrClose;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class OnViewChangedPacket
    {
        public List<Vec2i> NowVisible = new List<Vec2i>();
        public List<Vec2i> NowHidden = new List<Vec2i>();
    }


    public class WorldMapManager : ModSystem, IWorldMapManager
    {
        public Dictionary<string, Type> MapLayerRegistry = new Dictionary<string, Type>();

        ICoreAPI api;

        // Client side stuff
        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;

        GuiDialogWorldMap worldMapDlg;
        public List<MapLayer> MapLayers = new List<MapLayer>();
        public bool IsOpened => worldMapDlg?.IsOpened() == true;


        // Client and Server side stuff
        Thread mapLayerGenThread;
        bool isShuttingDown = false;

        // Server side stuff
        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;


        




        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            RegisterDefaultMapLayers();
            this.api = api;
        }

        public void RegisterDefaultMapLayers()
        {
            RegisterMapLayer<ChunkMapLayer>("chunks");
            RegisterMapLayer<PlayerMapLayer>("players");
            RegisterMapLayer<WaypointMapLayer>("waypoints");
        }

        public void RegisterMapLayer<T>(string code) where T : MapLayer
        {
            MapLayerRegistry[code] = typeof(T);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            capi.Input.RegisterHotKey("worldmaphud", "World Map HUD (Small overlay)", GlKeys.F6, HotkeyType.GeneralControls);
            capi.Input.RegisterHotKey("worldmapdialog", "World Map Dialog", GlKeys.M, HotkeyType.GeneralControls);
            capi.Input.SetHotKeyHandler("worldmaphud", OnHotKeyWorldMapHud);
            capi.Input.SetHotKeyHandler("worldmapdialog", OnHotKeyWorldMapDlg);
            capi.Event.BlockTexturesLoaded(OnLoaded);
            capi.Event.OnLevelFinalize += OnLvlFinalize;

            capi.Event.LeaveWorld += () => isShuttingDown = true;

            clientChannel =
                api.Network.RegisterChannel("worldmap")
               .RegisterMessageType(typeof(MapLayerData[]))
               .RegisterMessageType(typeof(OnViewChangedPacket))
               .RegisterMessageType(typeof(OnMapToggle))
               .SetMessageHandler<MapLayerData[]>(OnMapLayerDataReceivedClient)
            ;
        }

        private void OnLvlFinalize()
        {
            if (capi != null && (capi.Settings.Bool["hudOpened"] || !capi.Settings.Bool.Exists("hudOpened")) && (worldMapDlg == null || !worldMapDlg.IsOpened()))
            {
                ToggleMap(EnumDialogType.HUD);
            }

        }

        private void OnMapLayerDataReceivedClient(MapLayerData[] msg)
        {
            for (int i = 0; i < msg.Length; i++)
            {
                Type type = MapLayerRegistry[msg[i].ForMapLayer];
                MapLayers.FirstOrDefault(x => x.GetType() == type)?.OnDataFromServer(msg[i].Data);
            }
        }

        private void OnLoaded()
        {
            foreach (var val in MapLayerRegistry)
            {
                MapLayers.Add((MapLayer)Activator.CreateInstance(val.Value, api, this));
            }
            

            foreach (MapLayer layer in MapLayers)
            {
                layer.OnLoaded();
            }

            mapLayerGenThread = new Thread(new ThreadStart(() =>
            {
                while (!isShuttingDown)
                {
                    foreach (MapLayer layer in MapLayers)
                    {
                        layer.OnOffThreadTick();
                    }

                    Thread.Sleep(20);
                }
            }));

            mapLayerGenThread.IsBackground = true;
            mapLayerGenThread.Start();

        }

        private bool OnHotKeyWorldMapHud(KeyCombination comb)
        {
            ToggleMap(EnumDialogType.HUD);
            return true;
        }

        private bool OnHotKeyWorldMapDlg(KeyCombination comb)
        {
            ToggleMap(EnumDialogType.Dialog);
            return true;
        }


        void ToggleMap(EnumDialogType asType)
        {
            bool isDlgOpened = worldMapDlg != null && worldMapDlg.IsOpened();

            if (worldMapDlg != null)
            {
                if (!isDlgOpened)
                {
                    if (asType == EnumDialogType.HUD) capi.Settings.Bool["hudOpened"] = true;

                    worldMapDlg.Open(asType);
                }
                else
                {
                    worldMapDlg.TryClose();

                    if (asType == EnumDialogType.HUD)
                    {
                        capi.Settings.Bool["hudOpened"] = false;
                    }
                    else
                    {
                        if (worldMapDlg.DialogType != asType)
                        {
                            worldMapDlg.Open(asType);
                            return;
                        }

                        if (capi.Settings.Bool["hudOpened"])
                        {
                            worldMapDlg.Open(EnumDialogType.HUD);
                        }
                    }                
                }
                return;
            }

            worldMapDlg = new GuiDialogWorldMap(onViewChangedClient, capi);

            worldMapDlg.OnOpened += () =>
            {
                foreach (MapLayer layer in MapLayers) layer.OnMapOpenedClient();
                clientChannel.SendPacket(new OnMapToggle() { OpenOrClose = true });
            };

            worldMapDlg.OnClosed += () => {
                foreach (MapLayer layer in MapLayers) layer.OnMapClosedClient();
                clientChannel.SendPacket(new OnMapToggle() { OpenOrClose = false });
            };

            worldMapDlg.Open(asType);
            if (asType == EnumDialogType.HUD) capi.Settings.Bool["hudOpened"] = true;
        }





        private void onViewChangedClient(List<Vec2i> nowVisible, List<Vec2i> nowHidden)
        {
            foreach (MapLayer layer in MapLayers)
            {
                layer.OnViewChangedClient(nowVisible, nowHidden);
            }

            clientChannel.SendPacket(new OnViewChangedPacket() { NowVisible = nowVisible, NowHidden = nowHidden });
        }

        public void AddMapData(MapComponent cmp)
        {
            worldMapDlg.mapElem.AddMapComponent(cmp);
        }

        public void RemoveMapData(MapComponent cmp)
        {
            worldMapDlg.mapElem.RemoveMapComponent(cmp);
        }
        
        
        public void TranslateWorldPosToViewPos(Vec3d worldPos, ref Vec2f viewPos)
        {
            worldMapDlg.mapElem.TranslateWorldPosToViewPos(worldPos, ref viewPos);
        }

        public void SendMapDataToServer(MapLayer forMapLayer, byte[] data)
        {
            if (api.Side == EnumAppSide.Server) return;

            List<MapLayerData> maplayerdatas = new List<MapLayerData>();

            maplayerdatas.Add(new MapLayerData()
            {
                Data = data,
                ForMapLayer = MapLayerRegistry.FirstOrDefault(x => x.Value == forMapLayer.GetType()).Key
            });

            clientChannel.SendPacket(maplayerdatas.ToArray());
        }


        #region Server Side


        public override void StartServerSide(ICoreServerAPI sapi)
        {
            this.sapi = sapi;

            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnLoaded);
            sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, () => isShuttingDown = true);

            serverChannel =
               sapi.Network.RegisterChannel("worldmap")
               .RegisterMessageType(typeof(MapLayerData[]))
               .RegisterMessageType(typeof(OnViewChangedPacket))
               .RegisterMessageType(typeof(OnMapToggle))
               .SetMessageHandler<OnMapToggle>(OnMapToggledServer)
               .SetMessageHandler<OnViewChangedPacket>(OnViewChangedServer)
               .SetMessageHandler<MapLayerData[]>(OnMapLayerDataReceivedServer)
            ;
            
        }

        private void OnMapLayerDataReceivedServer(IServerPlayer fromPlayer, MapLayerData[] msg)
        {
            for (int i = 0; i < msg.Length; i++)
            {
                Type type = MapLayerRegistry[msg[i].ForMapLayer];
                MapLayers.FirstOrDefault(x => x.GetType() == type)?.OnDataFromClient(msg[i].Data);
            }
        }

        private void OnMapToggledServer(IServerPlayer fromPlayer, OnMapToggle msg)
        {
            foreach (MapLayer layer in MapLayers)
            {
                if (layer.DataSide == EnumMapAppSide.Client) continue;

                if (msg.OpenOrClose)
                {
                    layer.OnMapOpenedServer(fromPlayer);
                }
                else
                {
                    layer.OnMapClosedServer(fromPlayer);
                }
            }
        }

        private void OnViewChangedServer(IServerPlayer fromPlayer, OnViewChangedPacket networkMessage)
        {
            List<Vec2i> empty = new List<Vec2i>();

            foreach (MapLayer layer in MapLayers)
            {
                if (layer.DataSide == EnumMapAppSide.Client) continue;

                layer.OnViewChangedServer(fromPlayer, networkMessage.NowVisible, empty);
            }
        }

        public void SendMapDataToClient(MapLayer forMapLayer, IServerPlayer forPlayer, byte[] data)
        {
            if (api.Side == EnumAppSide.Client) return;

            List<MapLayerData> maplayerdatas = new List<MapLayerData>();

            maplayerdatas.Add(new MapLayerData()
            {
                Data = data,
                ForMapLayer = MapLayerRegistry.FirstOrDefault(x => x.Value == forMapLayer.GetType()).Key
            });

            serverChannel.SendPacket(maplayerdatas.ToArray(), forPlayer);
        }

        #endregion

    }
}
