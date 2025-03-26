using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ModSystemOreMap : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;
        public override double ExecuteOrder() => 1;

        ICoreClientAPI capi;
        ICoreAPI api;
        ICoreServerAPI sapi;
        public ProspectingMetaData prospectingMetaData;

        public override void Start(ICoreAPI api)
        {
            api.ModLoader.GetModSystem<WorldMapManager>().RegisterMapLayer<OreMapLayer>("ores", 0.75);
            api.Network
                .RegisterChannel("oremap")
                .RegisterMessageType<PropickReading>()              // radfast 11.3.25:  such messages are not sent in the vanilla game in 1.20.5, left here in case of mod backward compatibility
                .RegisterMessageType<ProspectingMetaData>()
                .RegisterMessageType<DeleteReadingPacket>()
            ;
            this.api = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network
                .GetChannel("oremap")
                .SetMessageHandler<PropickReading>(onPropickReadingPacket)              // radfast 11.3.25:  such messages are not sent in the vanilla game in 1.20.5, left here in case of mod backward compatibility
                .SetMessageHandler<ProspectingMetaData>(onPropickMetaData)
            ;

            capi = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.PlayerJoin += Event_PlayerJoin;
            api.Network
                .GetChannel("oremap")
                .SetMessageHandler<DeleteReadingPacket>(onDeleteReading)
            ;
        }

        private void onDeleteReading(IServerPlayer fromPlayer, DeleteReadingPacket packet)
        {
            var layers = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers;
            var oml = layers.FirstOrDefault(ml => ml is OreMapLayer) as OreMapLayer;
            if (oml == null) return;

            oml.Delete(fromPlayer, packet.Index);
            //oml.RebuildMapComponents();    // radfast 11.3.25: It makes no sense to call this on the server side
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            var ppws = ObjectCacheUtil.TryGet<ProPickWorkSpace>(api, "propickworkspace");
            if (ppws != null)
            {
                sapi.Network.GetChannel("oremap").SendPacket(new ProspectingMetaData() { PageCodes = ppws.pageCodes }, byPlayer);
            }
        }

        private void onPropickMetaData(ProspectingMetaData packet)
        {
            this.prospectingMetaData = packet;
        }

        private void onPropickReadingPacket(PropickReading reading)
        {
            // radfast 11.3.25: This method is not currently used in 1.20.5, such packets are not sent in vanilla. Left here in case of mod backward compatibility

            var oml = capi.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is OreMapLayer) as OreMapLayer;
            if (oml == null) return;

            oml.ownPropickReadings.Add(reading);
            oml.RebuildMapComponents();
        }

        public void DidProbe(PropickReading results, IServerPlayer splr)
        {
            var oml = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is OreMapLayer) as OreMapLayer;
            if (oml == null) return;

            var readings = oml.getOrLoadReadings(splr);
            readings.Add(results);
        }
    }


    [ProtoContract]
    public class OreReading
    {
        [ProtoMember(1)]
        public string DepositCode;
        [ProtoMember(2)]
        public double TotalFactor;
        [ProtoMember(3)]
        public double PartsPerThousand;
    }

    [ProtoContract]
    public class ProspectingMetaData
    {
        [ProtoMember(1)]
        public Dictionary<string, string> PageCodes = new Dictionary<string, string>();
    }
    [ProtoContract]
    public class DeleteReadingPacket
    {
        [ProtoMember(1)]
        public int Index;
    }

}
