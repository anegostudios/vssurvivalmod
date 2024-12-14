using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using System.IO;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Newtonsoft.Json.Linq;

namespace Vintagestory.GameContent
{
    public class EntityVillager : EntityDressedHumanoid, ITalkUtil
    {
        public OrderedDictionary<string, TraderPersonality> Personalities => Properties.Attributes["personalities"].AsObject<OrderedDictionary<string, TraderPersonality>>();

        public EntityVillager()
        {
            AnimManager = new PersonalizedAnimationManager();
        }

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "balanced"); }
            set
            {
                var ps = Personalities;
                WatchedAttributes.SetString("personality", value);
                TalkUtil?.SetModifiers(ps[value].ChordDelayMul, ps[value].PitchModifier, ps[value].VolumneModifier);
            }
        }

        public string VoiceSound
        {
            get { 
                if (!WatchedAttributes.HasAttribute("voiceSound"))
                {
                    var sounds = Properties.Attributes["voiceSounds"].AsStringArray();
                    var index = Api.World.Rand.Next(sounds.Length);
                    var sound = sounds[index];
                    WatchedAttributes.SetString("voiceSound", sound);
                    TalkUtil.soundName = AssetLocation.Create(sound, Code.Domain);
                    return sound;
                }

                return WatchedAttributes.GetString("voiceSound"); 
            }
            set { 
                WatchedAttributes.SetString("voiceSound", value);
                TalkUtil.soundName = AssetLocation.Create(value, Code.Domain);
            }
        }

        public EntityTalkUtil TalkUtil { get; set; }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (!WatchedAttributes.HasAttribute("personality"))
            {
                var p = Personalities;
                int index = api.World.Rand.Next(p.Count);
                this.Personality = p.GetKeyAtIndex(index);
            }

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            (AnimManager as PersonalizedAnimationManager).All = true;

            if (api.Side == EnumAppSide.Client)
            {
                bool isMultiSoundVoice = true;
                TalkUtil = new EntityTalkUtil(api as ICoreClientAPI, this, isMultiSoundVoice);
                TalkUtil.soundName = AssetLocation.Create(VoiceSound, Code.Domain);
            }

            this.Personality = this.Personality; // to update the talkutil
        }


        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EntityServerPacketId.Hurt)
            {
                if (!Alive) return;
                TalkUtil.Talk(EnumTalkType.Hurt);
            }
            if (packetid == (int)EntityServerPacketId.Death)
            {
                TalkUtil.Talk(EnumTalkType.Death);
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Client)
            {
                TalkUtil.OnGameTick(dt);
            }
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Api.Side == EnumAppSide.Server)
            {
                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(Personalities.Count));
                (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
        }

        public override string GetInfoText()
        {
            var text = base.GetInfoText();

            var capi = Api as ICoreClientAPI;
            if (capi != null && capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                text += "\nPersonality: " + Personality;
                text += "\nVoice: " + VoiceSound;
            }

            return text;
        }
    }

}
