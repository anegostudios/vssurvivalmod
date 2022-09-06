using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RiftList
    {
        public List<Rift> rifts = new List<Rift>();
    }

    public class ModSystemRifts : ModSystem
    {
        ICoreAPI api;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        RiftRenderer renderer;

        public Dictionary<int, Rift> riftsById = new Dictionary<int, Rift>();
        public ILoadedSound[] riftSounds = new ILoadedSound[4];
        public Rift[] nearestRifts;

        public IServerNetworkChannel schannel;

        public int despawnDistance = 240;
        public int spawnMinDistance = 8;
        public int spawnAddDistance = 230;

        bool riftsEnabled = true;

        Dictionary<string, long> chunkIndexbyPlayer = new Dictionary<string, long>();

        ModSystemRiftWeather modRiftWeather;


        string riftMode;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            api.Network.RegisterChannel("rifts").RegisterMessageType<RiftList>();

            modRiftWeather = api.ModLoader.GetModSystem<ModSystemRiftWeather>();
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            renderer = new RiftRenderer(api, riftsById);

            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.RegisterGameTickListener(onClientTick, 100);

            api.Network.GetChannel("rifts").SetMessageHandler<RiftList>(onRifts);
        }

        private void onRifts(RiftList riftlist)
        {
            HashSet<int> toRemove = new HashSet<int>();
            toRemove.AddRange(this.riftsById.Keys);

            foreach (var rift in riftlist.rifts)
            {
                toRemove.Remove(rift.RiftId);

                if (riftsById.TryGetValue(rift.RiftId, out var existingRift))
                {
                    existingRift.SetFrom(rift);
                } else
                {
                    riftsById[rift.RiftId] = rift;
                }
            }

            foreach (var id in toRemove)
            {
                riftsById.Remove(id);
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.PlayerJoin += Event_PlayerJoin;
            api.Event.RegisterGameTickListener(OnServerTick100ms, 101);
            api.Event.RegisterGameTickListener(OnServerTick3s, 2999);
            api.RegisterCommand("rifttest", "", "", onCmdRiftTest);

            schannel = sapi.Network.GetChannel("rifts");
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            if (!riftsEnabled) return;

            BroadCastRifts(byPlayer);
        }

        private void OnServerTick100ms(float dt)
        {
            if (riftMode != "visible") return;

            foreach (IServerPlayer plr in sapi.World.AllOnlinePlayers)
            {
                if (plr.ConnectionState != EnumClientState.Playing) continue;

                var bh = plr.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
                if (bh == null) continue;
                bh.stabilityOffset = 0;

                var plrPos = plr.Entity.Pos.XYZ;
                Rift rift = riftsById.Values.Nearest((r) => r.Position.SquareDistanceTo(plrPos));
                if (rift == null) continue;

                float dist = Math.Max(0, GameMath.Sqrt(plrPos.SquareDistanceTo(rift.Position)) - 2 - rift.Size / 2f);
                
                if (bh != null)
                {
                    bh.stabilityOffset = -Math.Pow(Math.Max(0, 1 - dist / 3), 2) * 20;
                }
            }
        }

        private void OnServerTick3s(float dt)
        {
            if (!riftsEnabled) return;

            var players = sapi.World.AllOnlinePlayers;
            Dictionary<string, List<Rift>> nearbyRiftsByPlayerUid = new Dictionary<string, List<Rift>>();

            foreach (var player in players)
            {
                nearbyRiftsByPlayerUid[player.PlayerUID] = new List<Rift>();
            }

            bool modified = KillOldRifts(nearbyRiftsByPlayerUid);
            modified |= SpawnNewRifts(nearbyRiftsByPlayerUid);


            if (modified)
            {
                BroadCastRifts();
            } else
            {
                foreach (var player in players)
                {
                    long index3d = getPlayerChunkIndex(player);
                    if (!chunkIndexbyPlayer.ContainsKey(player.PlayerUID) || chunkIndexbyPlayer[player.PlayerUID] != index3d)
                    {
                        BroadCastRifts(player);
                    }

                    chunkIndexbyPlayer[player.PlayerUID] = index3d;
                }
            }
        }

        private bool SpawnNewRifts(Dictionary<string, List<Rift>> nearbyRiftsByPlayerUid)
        {
            var uids = nearbyRiftsByPlayerUid.Keys;
            int riftsSpawned = 0;

            foreach (var uid in uids)
            {
                float cap = GetRiftCap(uid);
                var plr = api.World.PlayerByUid(uid);
                if (plr.WorldData.CurrentGameMode == EnumGameMode.Creative || plr.WorldData.CurrentGameMode == EnumGameMode.Spectator) continue;

                var nearbyRifts = nearbyRiftsByPlayerUid[uid].Count;
                int canSpawnCount = (int)(cap - nearbyRifts);

                float fract = (cap - nearbyRifts) - canSpawnCount;
                if (api.World.Rand.NextDouble() < fract / 50.0) canSpawnCount++;

                if (canSpawnCount <= 0) continue;
                
                if (api.World.Calendar.TotalDays < 2 && api.World.Calendar.GetDayLightStrength(plr.Entity.Pos.AsBlockPos) > 0.9f) continue;

                for (int i = 0; i < canSpawnCount; i++)
                {
                    double distance = spawnMinDistance + api.World.Rand.NextDouble() * spawnAddDistance;
                    double angle = api.World.Rand.NextDouble() * GameMath.TWOPI;

                    double dz = distance * Math.Sin(angle);
                    double dx = distance * Math.Cos(angle);

                    Vec3d riftPos = plr.Entity.Pos.XYZ.Add(dx, 0, dz);
                    BlockPos pos = new BlockPos((int)riftPos.X, 0, (int)riftPos.Z);
                    pos.Y = api.World.BlockAccessor.GetTerrainMapheightAt(pos);

                    var block = api.World.BlockAccessor.GetBlock(pos);
                    if (block.Replaceable < 6000)
                    {
                        pos.Up();
                    }
                    block = api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
                    if (block.IsLiquid() && api.World.Rand.NextDouble() > 0.1) continue;

                    // Don't spawn near bases
                    int blocklight = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlyBlockLight);
                    int blocklightup = api.World.BlockAccessor.GetLightLevel(pos.UpCopy(), EnumLightLevelType.OnlyBlockLight);
                    int blocklightup2 = api.World.BlockAccessor.GetLightLevel(pos.UpCopy(2), EnumLightLevelType.OnlyBlockLight);
                    if (blocklight >= 3 || blocklightup >= 3 || blocklightup2 >= 3) continue;

                    float size = 2 + (float)api.World.Rand.NextDouble() * 4f;

                    riftPos.Y = pos.Y + size / 2f + 1;
                    var rift = new Rift()
                    {
                        RiftId = NextRiftId,
                        Position = riftPos,
                        Size = size,
                        SpawnedTotalHours = api.World.Calendar.TotalHours,
                        DieAtTotalHours = api.World.Calendar.TotalHours + 8 + api.World.Rand.NextDouble() * 48
                    };

                    riftsById[rift.RiftId] = rift;
                    riftsSpawned++;

                    // Update the list as we go, so we don't spawn overlapping amounts of rifts around players
                    foreach (var nuid in uids)
                    {
                        if (plr.Entity.Pos.HorDistanceTo(riftPos) <= despawnDistance)
                        {
                            nearbyRiftsByPlayerUid[plr.PlayerUID].Add(rift);
                        }
                    }
                }
            }

            return riftsSpawned > 0;
        }

        private float GetRiftCap(string playerUid)
        {
            var plr = api.World.PlayerByUid(playerUid);
            var pos = plr.Entity.Pos;
            float daylight = api.World.Calendar.GetDayLightStrength(pos.X, pos.Z);

            return 5 * modRiftWeather.CurrentPattern.MobSpawnMul * GameMath.Clamp(1.1f - daylight, 0.35f, 1);
        }

        private bool KillOldRifts(Dictionary<string, List<Rift>> nearbyRiftsByPlayerUid)
        {
            bool riftModified = false;

            double totalHours = api.World.Calendar.TotalHours;
            var players = sapi.World.AllOnlinePlayers;

            HashSet<int> toRemove = new HashSet<int>();

            foreach (var rift in riftsById.Values)
            {
                if (rift.DieAtTotalHours <= totalHours)
                {
                    toRemove.Add(rift.RiftId);
                    riftModified = true;
                    continue;
                }

                var nearbyPlrs = players.InRange((player) => player.Entity.Pos.HorDistanceTo(rift.Position), despawnDistance);

                if (nearbyPlrs.Count == 0)
                {
                    rift.DieAtTotalHours = Math.Min(rift.DieAtTotalHours, api.World.Calendar.TotalHours + 0.2);
                    riftModified = true;
                    continue;
                }

                foreach (var plr in nearbyPlrs)
                {
                    nearbyRiftsByPlayerUid[plr.PlayerUID].Add(rift);
                }
            }

            foreach (var id in toRemove) riftsById.Remove(id);

            foreach (var val in nearbyRiftsByPlayerUid)
            {
                float cap = GetRiftCap(val.Key);
                float overSpawn = val.Value.Count - cap;
                if (overSpawn <= 0) continue;

                var plrPos = api.World.PlayerByUid(val.Key).Entity.Pos.XYZ;
                var rifts = val.Value.OrderBy(rift => rift.DieAtTotalHours).ToList();

                for (int i = 0; i < Math.Min(rifts.Count, (int)overSpawn); i++)
                {
                    rifts[i].DieAtTotalHours = Math.Min(rifts[i].DieAtTotalHours, api.World.Calendar.TotalHours + 0.2);
                    riftModified = true;
                }
            }

            return riftModified;
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("rifts", riftsById);
        }

        private void Event_SaveGameLoaded()
        {
            riftMode = api.World.Config.GetString("temporalRifts", "visible");
            riftsEnabled = riftMode != "off";
            if (!riftsEnabled) return;

            try
            {
                var rifts = sapi.WorldManager.SaveGame.GetData<List<Rift>>("rifts");
                if (rifts != null)
                {
                    foreach (var rift in rifts)
                    {
                        riftsById[rift.RiftId] = rift;
                    }
                }
            }
            catch (Exception) {
                
            }

            if (riftsById == null)
            {
                riftsById = new Dictionary<int, Rift>();
            }
        }

        public void BroadCastRifts(IPlayer onlyToPlayer = null)
        {
            if (riftMode != "visible") return;

            List<Rift> plrLists = new List<Rift>();
            float minDistSq = (float)Math.Pow(despawnDistance + 10, 2);

            foreach (var plr in sapi.World.AllOnlinePlayers)
            {
                if (onlyToPlayer != null && onlyToPlayer.PlayerUID != plr.PlayerUID) continue;

                chunkIndexbyPlayer[plr.PlayerUID] = getPlayerChunkIndex(plr);

                var splr = plr as IServerPlayer;
                var plrPos = splr.Entity.Pos.XYZ;

                foreach (var rift in riftsById.Values)
                {
                    if (rift.Position.SquareDistanceTo(plrPos) < minDistSq)
                    {
                        plrLists.Add(rift);
                    }
                }

                schannel.SendPacket(new RiftList() { rifts = plrLists }, splr);
                plrLists.Clear();
            }
        }

        private long getPlayerChunkIndex(IPlayer plr)
        {
            var pos = plr.Entity.Pos;
            var cs = api.World.BlockAccessor.ChunkSize;
            return (api as ICoreServerAPI).WorldManager.ChunkIndex3D((int)pos.X / cs, (int)pos.Y / cs, (int)pos.Z / cs);
        }

        private void onClientTick(float dt)
        {
            if (!riftsEnabled) return;

            Vec3d plrPos = capi.World.Player.Entity.Pos.XYZ.Add(capi.World.Player.Entity.LocalEyePos);

            nearestRifts = riftsById.Values.OrderBy(rift => rift.Position.SquareDistanceTo(plrPos) + (rift.HasLineOfSight ? 0 : 20*20)).ToArray();

            for (int i = 0; i < Math.Min(4, nearestRifts.Length); i++)
            {
                Rift rift = nearestRifts[i];

                rift.OnNearTick(capi, dt);

                ILoadedSound sound = riftSounds[i];

                if (!sound.IsPlaying)
                {
                    sound.Start();
                    sound.PlaybackPosition = sound.SoundLengthSeconds * (float)capi.World.Rand.NextDouble();
                }

                float vol = GameMath.Clamp(rift.GetNowSize(capi) / 3f, 0.1f, 1f);
                
                sound.SetVolume(vol * rift.VolumeMul);
                sound.SetPosition((float)rift.Position.X, (float)rift.Position.Y, (float)rift.Position.Z);
            }

            for (int i = nearestRifts.Length; i < 4; i++)
            {
                if (riftSounds[i].IsPlaying)
                {
                    riftSounds[i].Stop();
                }
            }
        }

        private void Event_LeaveWorld()
        {
            for (int i = 0; i < 4; i++)
            {
                riftSounds[i]?.Stop();
                riftSounds[i]?.Dispose();
            }
        }

        private void Event_BlockTexturesLoaded()
        {
            for (int i = 0; i < 4; i++)
            {
                riftSounds[i] = capi.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/effect/rift.ogg"),
                    ShouldLoop = true,
                    Position = null,
                    DisposeOnFinish = false,
                    Volume = 1,
                    Range = 24,
                    SoundType = EnumSoundType.AmbientGlitchunaffected
                });
            }
        }

        int riftId;
        public int NextRiftId => riftId++;

        private void onCmdRiftTest(IServerPlayer player, int groupId, CmdArgs args)
        {
            Vec3d pos = player.Entity.Pos.XYZ;

            string cmd = args.PopWord();

            if (cmd == null)
            {
                player.SendMessage(groupId, riftsById.Count + " rifts loaded", EnumChatType.Notification);
                return;
            }

            if (cmd == "clear")
            {
                riftsById.Clear();
            }

            if (cmd == "fade")
            {
                foreach (var rift in riftsById.Values)
                {
                    rift.DieAtTotalHours = Math.Min(rift.DieAtTotalHours, api.World.Calendar.TotalHours + 0.2);
                }
            }

            if (cmd == "spawn")
            {
                int cnt = (int)args.PopInt(200);
                for (int i = 0; i < cnt; i++)
                {
                    double distance = spawnMinDistance + api.World.Rand.NextDouble() * spawnAddDistance;
                    double angle = api.World.Rand.NextDouble() * GameMath.TWOPI;

                    double dz = distance * Math.Sin(angle);
                    double dx = distance * Math.Cos(angle);

                    Vec3d riftPos = pos.AddCopy(dx, 0, dz);

                    BlockPos bpos = new BlockPos((int)riftPos.X, 0, (int)riftPos.Z);
                    bpos.Y = api.World.BlockAccessor.GetTerrainMapheightAt(bpos);

                    var block = api.World.BlockAccessor.GetBlock(bpos, BlockLayersAccess.Fluid);
                    if (block.IsLiquid() && api.World.Rand.NextDouble() > 0.1) continue;

                    float size = 2 + (float)api.World.Rand.NextDouble() * 4f;

                    riftPos.Y = bpos.Y + size / 2f + 1;

                    var rift = new Rift()
                    {
                        RiftId = NextRiftId,
                        Position = riftPos,
                        Size = size,
                        SpawnedTotalHours = api.World.Calendar.TotalHours,
                        DieAtTotalHours = api.World.Calendar.TotalHours + 8 + api.World.Rand.NextDouble() * 48
                    };

                    riftsById[rift.RiftId] = rift;
                }
            }

            if (cmd == "spawnhere")
            {
                var riftPos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos);
                float size = 3;

                var rift = new Rift()
                {
                    RiftId = NextRiftId,
                    Position = riftPos,
                    Size = size,
                    SpawnedTotalHours = api.World.Calendar.TotalHours,
                    DieAtTotalHours = api.World.Calendar.TotalHours + 8 + api.World.Rand.NextDouble() * 48
                };

                riftsById[rift.RiftId] = rift;
            }

            BroadCastRifts();
        }
    }
}
