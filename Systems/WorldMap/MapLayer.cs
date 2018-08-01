using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public enum EnumMinMagFilter
    {
        Linear,
        Nearest
    }

    public enum EnumMapAppSide
    {
        Client,
        Server
    }

    public class MapLegendItem
    {
        public int Color;
        public string Name;
    }

    public class MapMarker
    {
        Vec2i Position;
        string Icon;
        string Name;
    }

    public abstract class MapLayer
    {
        public abstract string Title { get; }

        /// <summary>
        /// Where the data is at. If server side, the server will call the method ToBytes() send that data to the client and decode it there with the FromBytes() method
        /// If client side, no client<->server sync is done
        /// </summary>
        public abstract EnumMapAppSide DataSide { get; }

        public virtual bool RequireChunkLoaded => true;

        public string RequirePrivilege;
        public string RequireCode;
        public EnumGameMode? RequiredGameMode;

        public int ZIndex = 1;
        protected ICoreAPI api;
        protected IWorldMapManager mapSink;

        public HashSet<Vec2i> LoadedChunks = new HashSet<Vec2i>();

        public MapLayer(ICoreAPI api, IWorldMapManager mapSink)
        {
            this.api = api;
            this.mapSink = mapSink;
        }
        
        public virtual void OnOffThreadTick()
        {

        }

        public virtual void OnViewChangedClient(List<Vec2i> nowVisible, List<Vec2i> nowHidden)
        {

        }

        public virtual void OnViewChangedServer(IServerPlayer fromPlayer, List<Vec2i> nowVisible, List<Vec2i> nowHidden)
        {
        }

        /// <summary>
        /// Called on the client
        /// </summary>
        public virtual void OnMapOpenedClient()
        {

        }

        /// <summary>
        /// Called on the client
        /// </summary>
        public virtual void OnMapClosedClient()
        {

        }

        /// <summary>
        /// Called on the server when dataside == server
        /// </summary>
        /// <param name="fromPlayer"></param>
        public virtual void OnMapOpenedServer(IServerPlayer fromPlayer)
        {
            
        }

        /// <summary>
        /// Called on the server when dataside == server
        /// </summary>
        /// <param name="fromPlayer"></param>
        public virtual void OnMapClosedServer(IServerPlayer fromPlayer)
        {

        }

        

        public virtual void OnDataFromServer(byte[] data) { }
        public virtual void OnDataFromClient(byte[] data) { }

        /// <summary>
        /// Server: Called when server entered runphase rungame
        /// Client: Called when the item/block textures have been loaded
        /// </summary>
        public virtual void OnLoaded()
        {            

        }

        
    }


    public abstract class RGBMapLayer : MapLayer
    {
        public Dictionary<Vec2i, int> ChunkTextures = new Dictionary<Vec2i, int>();
        public bool Visible;

        public RGBMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
        }

        
        public abstract MapLegendItem[] LegendItems { get; }
        public abstract EnumMinMagFilter MinFilter { get; }
        public abstract EnumMinMagFilter MagFilter { get; }
    }

    public abstract class MarkerMapLayer : MapLayer
    {
        public Dictionary<string, int> IconTextures = new Dictionary<string, int>();

        public MarkerMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
        }

        
    }


    public class WorldMapsPacket
    {
        public MapLayer[] Maps;
    }

}
