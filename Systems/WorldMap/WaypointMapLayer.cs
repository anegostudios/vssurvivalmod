using Cairo;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class Waypoint
    {
        public Vec3d Position;
        public string Title;
        public string Text;
        public int Color;

        public string OwningPlayerUid = null;
        public int OwningPlayerGroupId = -1;
    }


    

    public class WaypointMapLayer : MarkerMapLayer
    {
        // Server side
        public List<Waypoint> Waypoints = new List<Waypoint>();
        ICoreServerAPI sapi;

        // Client side
        List<Waypoint> ownWaypoints = new List<Waypoint>();
        List<MapComponent> wayPointComponents = new List<MapComponent>();

        LoadedTexture texture;

        public override bool RequireChunkLoaded => false;
        
        
        public WaypointMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;
                this.sapi = sapi;

                sapi.Event.GameWorldSave(OnSaveGameGettingSaved);
                sapi.RegisterCommand("waypoint", "Put a waypoint at this location which will be visible for you on the map", "[add|remove|list]", OnCmdWayPoint, Privilege.chat);
            }
        }

        private void OnCmdWayPoint(IServerPlayer player, int groupId, CmdArgs args)
        {
            string cmd = args.PopSingle();

            switch (cmd)
            {
                case "add":
                    if (args.Length == 0)
                    {
                        player.SendMessage(groupId, Lang.Get("Syntax: /waypoint add [color] [title]\nColor may be a known .net color or hex number"), EnumChatType.CommandError);
                        return;
                    }

                    string colorstring = args.PopSingle();
                    string title = args.PopAll();

                    System.Drawing.Color parsedColor;

                    if (colorstring.StartsWith("#"))
                    {
                        int argb = Int32.Parse(colorstring.Replace("#", ""), NumberStyles.HexNumber);
                        parsedColor = System.Drawing.Color.FromArgb(argb);
                    } else
                    {
                        parsedColor = System.Drawing.Color.FromName(colorstring);
                    }

                    if (title == null || title.Length == 0)
                    {
                        player.SendMessage(groupId, Lang.Get("No text supplied. Syntax: /waypoint add [color] [title]\nColor may be a known .net color or hex number"), EnumChatType.CommandError);
                        return;
                    }

                    Waypoint waypoint = new Waypoint()
                    {
                        Color = parsedColor.ToArgb() | (255 << 24),
                        OwningPlayerUid = player.PlayerUID,
                        Position = player.Entity.ServerPos.XYZ,
                        Title = title
                    };
                    

                    Waypoints.Add(waypoint);
                    player.SendMessage(groupId, Lang.Get("Ok, waypoint added"), EnumChatType.CommandSuccess);
                    ResendWaypoints(player);
                    break;


                case "remove":
                    int? id = args.PopInt();
                    Waypoint[] ownwpaypoints = Waypoints.Where((p) => p.OwningPlayerUid == player.PlayerUID).ToArray();

                    if (ownwpaypoints.Length == 0)
                    {
                        player.SendMessage(groupId, Lang.Get("You have no waypoints to delete"), EnumChatType.CommandError);
                        return;
                    }

                    if (id == null || id < 0 || id > ownwpaypoints.Length)
                    {
                        player.SendMessage(groupId, Lang.Get("Invalid waypoint number, valid ones are 0..{0}", ownwpaypoints.Length - 1), EnumChatType.CommandSuccess);
                        return;
                    }

                    Waypoints.Remove(ownwpaypoints[(int)id]);
                    RebuildMapComponents();
                    ResendWaypoints(player);
                    player.SendMessage(groupId, Lang.Get("Ok, deleted waypoint."), EnumChatType.CommandSuccess);
                    break;


                case "list":

                    StringBuilder wps = new StringBuilder();
                    int i = 0;
                    foreach (Waypoint p in Waypoints.Where((p) => p.OwningPlayerUid == player.PlayerUID).ToArray())
                    {
                        wps.AppendLine(string.Format("{0}: {1} at {2}", i, p.Title, p.Position));
                        i++;
                    }

                    if (wps.Length == 0)
                    {
                        player.SendMessage(groupId, Lang.Get("You have no waypoints"), EnumChatType.CommandSuccess);
                    } else
                    {
                        player.SendMessage(groupId, Lang.Get("Your waypoints:\n" + wps.ToString()), EnumChatType.CommandSuccess);
                    }
                    
                    break;


                default:
                    player.SendMessage(groupId, Lang.Get("Syntax: /waypoint [add|remove|list]"), EnumChatType.CommandError);
                    break;
            }
        }

        private void OnSaveGameGettingSaved()
        {
            sapi.WorldManager.StoreData("playerMapMarkers", JsonUtil.ToBytes(Waypoints));
        }
        

        public override void OnViewChangedServer(IServerPlayer fromPlayer, List<Vec2i> nowVisible, List<Vec2i> nowHidden)
        {
            ResendWaypoints(fromPlayer);
        }

        
        public override void OnMapOpenedClient()
        {
            ImageSurface surface = new ImageSurface(Format.Argb32, 16, 16);
            Context ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            ctx.Arc(8, 8, 8, 0, 2 * Math.PI);
            ctx.SetSourceRGBA(1, 1, 1, 1);
            ctx.FillPreserve();
            ctx.LineWidth = 1.5;
            ctx.SetSourceRGBA(0.5, 0.5, 0.5, 1);
            ctx.Stroke();

            texture = new LoadedTexture(api as ICoreClientAPI, (api as ICoreClientAPI).Gui.LoadCairoTexture(surface, false), 6, 6);
            ctx.Dispose();
            surface.Dispose();

            RebuildMapComponents();
        }


        public override void OnMapClosedClient()
        {
            texture?.Dispose();
        }

        public override void OnLoaded()
        {
            if (sapi != null)
            {
                try
                {
                    byte[] data = sapi.WorldManager.GetData("playerMapMarkers");
                    if (data != null) Waypoints = JsonUtil.FromBytes<List<Waypoint>>(data);
                } catch (Exception e)
                {
                    sapi.World.Logger.Error("Failed deserializing player map markers. Won't load them, sorry! Exception thrown: ", e);
                }
                
            }
            
        }

        public override void OnDataFromServer(byte[] data)
        {
            ownWaypoints.Clear();
            ownWaypoints.AddRange(SerializerUtil.Deserialize<List<Waypoint>>(data));
            RebuildMapComponents();
        }




        private void RebuildMapComponents()
        {
            if (!mapSink.IsOpened) return;

            foreach (WaypointMapComponent comp in wayPointComponents)
            {
                mapSink.RemoveMapData(comp);
                comp.Dispose();
            }

            wayPointComponents.Clear();


            foreach (Waypoint wp in ownWaypoints)
            {
                WaypointMapComponent comp = new WaypointMapComponent(wp, texture, api as ICoreClientAPI);  

                wayPointComponents.Add(comp);
                mapSink.AddMapData(comp);
            }
        }


        void ResendWaypoints(IServerPlayer toPlayer)
        {
            Dictionary<int, PlayerGroupMembership> memberOfGroups = toPlayer.ServerData.PlayerGroupMemberships;
            List<Waypoint> hisMarkers = new List<Waypoint>();

            foreach (Waypoint marker in Waypoints)
            {
                if (toPlayer.PlayerUID != marker.OwningPlayerUid && !memberOfGroups.ContainsKey(marker.OwningPlayerGroupId)) continue;
                hisMarkers.Add(marker);
            }

            if (hisMarkers.Count == 0) return;

            mapSink.SendMapDataToClient(this, toPlayer, SerializerUtil.Serialize(hisMarkers));

        }



        public override string Title => "Player Set Markers";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Server;
    }
}
