using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public enum EnumMapResolution
    {
        Chunk,
        MapRegion
    }

    public enum EnumMapType
    {
        DensityMap,
        LerpedTypeMap,
        TypeMap
    }

    public class MapLegendItem
    {
        public int Color;
        public string Name;
    }

    public class WorldMap
    {
        public string Title;
        public IntMap MapData;
        public EnumMapType MapType;
        public EnumMapResolution Resolution;

        public MapLegendItem[] LegendItems;
    }

    

    public class WorldMapsPacket
    {
        public WorldMap[] Maps;
    }

    public class WorldMapManager : ModSystem
    {
        ICoreClientAPI capi;
        IGuiDialog worldMapDlg;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            capi.RegisterHotKey("worldmap", "World Map", GlKeys.M, HotkeyType.CreativeTool);
            capi.SetHotKeyHandler("worldmap", OnHotKeyWorldMap);
        }

        private bool OnHotKeyWorldMap(KeyCombination comb)
        {
            worldMapDlg = capi.World.OpenDialog("WorldMap");

            return true;
        }









        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }
    }
}
