using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SpawnPatternPacket
    {
        public CurrentPattern Pattern;
    }

    public class RiftWeatherConfig
    {
        public SpawnPattern[] Patterns;
    }

    public class SpawnPattern
    {
        public string Code;
        public float Chance;
        public float MobSpawnMul;
        public NatFloat DurationHours;
        public double StartTotalHours;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CurrentPattern
    {
        public string Code;
        public double UntilTotalHours;
    }

    /*
     * 
     *  Add a kill counter to each chunk, once that counter is reached, mobs stop spawning for a while
     *  Add "drifter weather". Sometimes more spawn, sometimes, less, sometimes none, sometimes a lot
     *  Make the temporal stabilizier prevent drifter spawn within a certain radius?
     *  Add rifts where most drifters spawn from (or even "rift weather")
     *  Make death more punishing or having drifters in your base be a bad thing
     *  Killing numbers of surface drifters in a region, tends to lead to fewer, higher level drifters spawning in that region in future (for a while)
    */
    public class ModSystemRiftWeather : ModSystem
    {
        ICoreServerAPI sapi;

        Dictionary<string, EntityProperties> drifterProps = new Dictionary<string, EntityProperties>();
        Dictionary<string, int> defaultSpawnCaps = new Dictionary<string, int>();

        CurrentPattern curPattern;
        RiftWeatherConfig config;
        Dictionary<string, SpawnPattern> patternsByCode = new Dictionary<string, SpawnPattern>();

        public SpawnPattern CurrentPattern => patternsByCode[curPattern.Code];
        public double CurrentPatternUntilHours => curPattern.UntilTotalHours;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("riftWeather").RegisterMessageType<SpawnPatternPacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("riftWeather").SetMessageHandler<SpawnPatternPacket>(onPacket);

            api.ModLoader.GetModSystem<CharacterExtraDialogs>().OnEnvText += ModSystemDrifterWeather_OnEnvText;
        }

        private void ModSystemDrifterWeather_OnEnvText(StringBuilder sb)
        {
            sb.AppendLine();
            sb.Append(Lang.Get("Rift activity: {0}", Lang.Get("rift-activity-" + curPattern.Code)));
        }

        private void onPacket(SpawnPatternPacket msg)
        {
            curPattern = msg.Pattern;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            config = api.Assets.Get("config/riftweather.json").ToObject<RiftWeatherConfig>();

            foreach (var p in config.Patterns)
            {
                patternsByCode[p.Code] = p;
            }

            sapi = api;
            base.StartServerSide(api);

            api.Event.RegisterGameTickListener(onServerTick, 2000);
            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, onRunGame);
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.SaveGameCreated += Event_SaveGameCreated;
            api.Event.PlayerJoin += Event_PlayerJoin;

            sapi.RegisterCommand("dweather", "", "", nDwCmd, Privilege.controlserver);
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            sapi.Network.GetChannel("riftWeather").SendPacket(new SpawnPatternPacket() { Pattern = curPattern }, byPlayer);
        }

        private void nDwCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            player.SendMessage(groupId, curPattern.Code, EnumChatType.Notification);
        }

        private void Event_SaveGameCreated()
        {
            choosePattern();
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("riftweather", SerializerUtil.Serialize(curPattern));
        }

        private void Event_SaveGameLoaded()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("riftweather");
                if (data == null)
                {
                    choosePattern();
                    return;
                }

                curPattern = SerializerUtil.Deserialize<CurrentPattern>(data);
            }
            catch {
                choosePattern();
            }

            if (curPattern.Code == null) choosePattern();
        }

        void choosePattern()
        {
            float weightSum = 0;
            List<SpawnPattern> patterns = new List<SpawnPattern>();

            double totalHours = sapi.World.Calendar.TotalHours;

            for (int i = 0; i < config.Patterns.Length; i++)
            {
                SpawnPattern pattern = config.Patterns[i];
                if (pattern.StartTotalHours < totalHours)
                {
                    patterns.Add(pattern);
                    weightSum += pattern.Chance;
                }
            }

            float val = (float)sapi.World.Rand.NextDouble() * weightSum;
            foreach (var pattern in patterns)
            {
                val -= pattern.Chance;
                if (val <= 0)
                {
                    double untiltotalHours = totalHours + pattern.DurationHours.nextFloat(1, sapi.World.Rand);
                    curPattern = new CurrentPattern() { Code = pattern.Code, UntilTotalHours = untiltotalHours };
                    sapi.Network.GetChannel("riftWeather").BroadcastPacket(new SpawnPatternPacket() { Pattern = curPattern });
                    return;
                }
            }

            var patt = patterns[patterns.Count - 1];
            curPattern = new CurrentPattern() { Code = patt.Code, UntilTotalHours = totalHours + patt.DurationHours.nextFloat(1, sapi.World.Rand) };

            sapi.Network.GetChannel("riftWeather").BroadcastPacket(new SpawnPatternPacket() { Pattern = curPattern });
        }

        private void onRunGame()
        {
            foreach (EntityProperties type in sapi.World.EntityTypes)
            {
                if (type.Code.Path == "drifter-normal" || type.Code.Path == "drifter-deep")
                {
                    drifterProps[type.Code.Path] = type;
                    defaultSpawnCaps[type.Code.Path] = type.Server.SpawnConditions.Runtime.MaxQuantity;
                    break;
                }
            }
        }

        private void onServerTick(float dt)
        {
            if (drifterProps == null) return;

            var pattern = patternsByCode[curPattern.Code];
            float qmul = pattern.MobSpawnMul;

            foreach (var val in drifterProps)
            {
                val.Value.Server.SpawnConditions.Runtime.MaxQuantity = Math.Max(0, (int)(defaultSpawnCaps[val.Key] * qmul));
            }

            if (curPattern.UntilTotalHours < sapi.World.Calendar.TotalHours)
            {
                choosePattern();
            }
            
        }
    }
}
