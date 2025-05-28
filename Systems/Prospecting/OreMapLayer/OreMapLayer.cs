using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class OreMapLayer : MarkerMapLayer
    {
        // Server side
        public Dictionary<string, List<PropickReading>> PropickReadingsByPlayer = new Dictionary<string, List<PropickReading>>();
        ICoreServerAPI sapi;

        // Client side
        public List<PropickReading> ownPropickReadings = new List<PropickReading>();
        List<MapComponent> wayPointComponents = new List<MapComponent>();
        List<MapComponent> tmpWayPointComponents = new List<MapComponent>();
        public override bool RequireChunkLoaded => false;
        public MeshRef quadModel;
        ICoreClientAPI capi;

        /// <summary>
        /// List 
        /// </summary>
        CreateIconTextureDelegate oremapIconDele;
        public LoadedTexture oremapTexture;

        string filterByOreCode;

        public OreMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                var iconAsset = api.Assets.Get("textures/icons/worldmap/0-circle.svg");

                oremapIconDele = () =>
                {
                    var size = (int)Math.Ceiling(20 * RuntimeEnv.GUIScale);
                    return capi.Gui.LoadSvg(iconAsset.Location, size, size, size, size, ColorUtil.WhiteArgb);
                };

                capi.Gui.Icons.CustomIcons["wpOreMapIcon"] = (ctx, x, y, w, h, rgba) =>
                {
                    var col = ColorUtil.ColorFromRgba(rgba);
                    capi.Gui.DrawSvg(iconAsset, ctx.GetTarget() as ImageSurface, ctx.Matrix, x, y, (int)w, (int)h, col);
                };
            }

            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;
                this.sapi = sapi;
                sapi.Event.GameWorldSave += OnSaveGameGettingSaved;
                sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
            } else
            {
                quadModel = (api as ICoreClientAPI).Render.UploadMesh(QuadMeshUtil.GetQuad());
            }
        }

        public override void ComposeDialogExtras(GuiDialogWorldMap guiDialogWorldMap, GuiComposer compo)
        {
            string key = "worldmap-layer-" + LayerGroupCode;

            HashSet<string> orecodes = new HashSet<string>();
            foreach (var val in ownPropickReadings)
            {
                foreach (var reading in val.OreReadings)
                {
                    orecodes.Add(reading.Key);
                }
            }

            string[] values = new string[] { null }.Append(orecodes.ToArray());
            string[] names = new string[] { Lang.Get("worldmap-ores-everything") }.Append(orecodes.Select(code => Lang.Get("ore-"+code)).ToArray());

            ElementBounds dlgBounds =
                ElementStdBounds.AutosizedMainDialog
                .WithFixedPosition(
                    (compo.Bounds.renderX + compo.Bounds.OuterWidth) / RuntimeEnv.GUIScale + 10,
                    compo.Bounds.renderY / RuntimeEnv.GUIScale
                )
                .WithAlignment(EnumDialogArea.None)
            ;

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            guiDialogWorldMap.Composers[key] = 
                capi.Gui
                    .CreateCompo(key, dlgBounds)
                    .AddShadedDialogBG(bgBounds, false)
                    .AddDialogTitleBar(Lang.Get("maplayer-prospecting"), () => { guiDialogWorldMap.Composers[key].Enabled = false; })
                    .BeginChildElements(bgBounds)
                        .AddDropDown(values, names, 0, onSelectionChanged, ElementBounds.Fixed(0, 30, 160, 35))
                    .EndChildElements()
                    .Compose()
            ;

            guiDialogWorldMap.Composers[key].Enabled = false;
        }


        private void onSelectionChanged(string code, bool selected)
        {
            filterByOreCode = code;
            RebuildMapComponents();
        }

        public int AddWaypoint(PropickReading waypoint, IServerPlayer player)
        {
            // radfast 11.3.25: This method is not currently used in 1.20.5, nothing calls it. Left here for mod backwards compatibility

            var plrReadings = getOrLoadReadings(player);
            plrReadings.Add(waypoint);
            ResendWaypoints(player);
            return plrReadings.Count - 1;
        }

        public List<PropickReading> getOrLoadReadings(IPlayer player)
        {
            if (PropickReadingsByPlayer.TryGetValue(player.PlayerUID, out var readings))
            {
                return readings;
            }

            byte[] data = sapi.WorldManager.SaveGame.GetData("oreMapMarkers-" + player.PlayerUID);
            if (data != null)
            {
                return PropickReadingsByPlayer[player.PlayerUID] = SerializerUtil.Deserialize<List<PropickReading>>(data);                
            }

            return PropickReadingsByPlayer[player.PlayerUID] = new List<PropickReading>();
        }

        private void OnSaveGameGettingSaved()
        {
            ISaveGame savegame = sapi.WorldManager.SaveGame;
            using FastMemoryStream ms = new ();
            foreach (var val in PropickReadingsByPlayer)
            {
                savegame.StoreData("oreMapMarkers-" + val.Key, SerializerUtil.Serialize(val.Value, ms));
            }
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            try
            {
                if (PropickReadingsByPlayer.TryGetValue(player.PlayerUID, out var readings))
                {
                    ISaveGame savegame = sapi.WorldManager.SaveGame;
                    savegame.StoreData("oreMapMarkers-" + player.PlayerUID, SerializerUtil.Serialize(readings));
                }
                PropickReadingsByPlayer.Remove(player.PlayerUID);
            }
            catch { }
        }

        [Obsolete("Receiving the OnViewChangedPacket now calls: OnViewChangedServer(fromPlayer, int x1, int z1, int x2, int z2) but retained in 1.20.10 for backwards compatibility")]
        public override void OnViewChangedServer(IServerPlayer fromPlayer, List<FastVec2i> nowVisible, List<FastVec2i> nowHidden)
        {
            ResendWaypoints(fromPlayer);
        }

        public override void OnViewChangedServer(IServerPlayer fromPlayer, int x1, int z1, int x2, int z2)
        {
            OnViewChangedServer(fromPlayer, null, null);
        }
        
        public override void OnMapOpenedClient()
        {
            ensureIconTexturesLoaded();

            RebuildMapComponents();
        }


        protected void ensureIconTexturesLoaded()
        {
            oremapTexture?.Dispose();
            oremapTexture = oremapIconDele();
        }

        public override void OnMapClosedClient()
        {
            foreach (var val in tmpWayPointComponents)
            {
                wayPointComponents.Remove(val);
            }

            tmpWayPointComponents.Clear();
        }

        public override void Dispose()
        {
            oremapTexture?.Dispose();
            oremapTexture = null;
            quadModel?.Dispose();

            base.Dispose();
        }



        public override void OnDataFromServer(byte[] data)
        {
            ownPropickReadings.Clear();
            ownPropickReadings.AddRange(SerializerUtil.Deserialize<List<PropickReading>>(data));
            RebuildMapComponents();
        }


        public void RebuildMapComponents()
        {
            if (!mapSink.IsOpened) return;

            foreach (var val in tmpWayPointComponents)
            {
                wayPointComponents.Remove(val);
            }

            foreach (OreMapComponent comp in wayPointComponents)
            {
                comp.Dispose();
            }

            wayPointComponents.Clear();

            for (int i = 0; i < ownPropickReadings.Count; i++)
            {
                var reading = ownPropickReadings[i];

                if (filterByOreCode == null || (reading.GetTotalFactor(filterByOreCode) > PropickReading.MentionThreshold))
                {
                    OreMapComponent comp = new OreMapComponent(i, reading, this, api as ICoreClientAPI, filterByOreCode);
                    wayPointComponents.Add(comp);
                }
            }

            wayPointComponents.AddRange(tmpWayPointComponents);
        }


        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!Active) return;

            foreach (var val in wayPointComponents)
            {
                val.Render(mapElem, dt);
            }
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            if (!Active) return;

            foreach (var val in wayPointComponents)
            {
                val.OnMouseMove(args, mapElem, hoverText);
            }
        }

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            if (!Active) return;

            foreach (var val in wayPointComponents)
            {
                val.OnMouseUpOnElement(args, mapElem);
                if (args.Handled) break;
            }
        }


        void ResendWaypoints(IServerPlayer toPlayer)
        {
            var plrReadings = getOrLoadReadings(toPlayer);
            mapSink.SendMapDataToClient(this, toPlayer, SerializerUtil.Serialize(plrReadings));
        }

        public void Delete(IPlayer player, int waypointIndex)
        {
            if (api.Side == EnumAppSide.Client)
            {
                ownPropickReadings.RemoveAt(waypointIndex);
                (api as ICoreClientAPI).Network.GetChannel("oremap").SendPacket(new DeleteReadingPacket() { Index=waypointIndex });
                RebuildMapComponents();
                return;
            }
            var plrReadings = getOrLoadReadings(player);
            if (plrReadings.Count > waypointIndex) plrReadings.RemoveAt(waypointIndex);    // index might be outside the list due to server-client desync on laggy connection (eg. client version of waypoints list represents the server version from 250ms ago or more by the time roundtrip communication has occurred to bring us back to here)
        }

        public override string Title => "Player Ore map readings";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Server;
        public override string LayerGroupCode => "prospecting";
    }
}
