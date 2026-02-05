using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumTradeDirection
    {
        Buy, Sell
    }

    public interface ITalkUtil
    {
        EntityTalkUtil TalkUtil { get; }
    }

    public class EntityTrader : EntityTradingHumanoid, ITalkUtil
    {
        public static API.Datastructures.OrderedDictionary<string, TraderPersonality> Personalities = new ()
        {
            { "formal", new TraderPersonality(1 * 1.5f, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f * 1.5f, 0.9f, 1.1f) },
            { "lazy", new TraderPersonality(1.65f * 1.5f, 0.7f, 0.9f) },
            { "rowdy", new TraderPersonality(0.75f * 1.5f, 1f, 1.8f) },
        };

        public EntityTalkUtil talkUtil;


        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "formal"); }
            set {
                WatchedAttributes.SetString("personality", value);
                talkUtil?.SetModifiers(Personalities[value].ChordDelayMul, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);
            }
        }


        public override EntityTalkUtil TalkUtil => talkUtil;

        public EntityTrader()
        {
            AnimManager = new PersonalizedAnimationManager();
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (api.Side == EnumAppSide.Client)
            {
                talkUtil = new EntityTalkUtil(api as ICoreClientAPI, this, false);
            }

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            this.Personality = this.Personality; // to update the talkutil
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


        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EntityServerPacketId.Hurt)
            {
                if (!Alive) return;
                talkUtil.Talk(EnumTalkType.Hurt);
            }
            if (packetid == (int)EntityServerPacketId.Death)
            {
                talkUtil.Talk(EnumTalkType.Death);
            }
        }



        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);


            if (Alive)
            {
                bool almostDone = true;
                if (AnimManager.Animator != null)
                {
                    foreach (var anim in AnimManager.Animator.Animations)
                    {
                        if (anim.Active) almostDone &= anim.AnimProgress >= 0.95f;
                    }
                }

                if (almostDone)
                {
                    AnimManager.StartAnimation(new AnimationMetaData() { Code = "idle", Animation = "idle", EaseOutSpeed = 10, EaseInSpeed = 10 });
                }


            }

            if (World.Side == EnumAppSide.Client) {
                talkUtil.OnGameTick(dt);
            }
        }


        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);
            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
        }


        public override void Revive()
        {
            base.Revive();

            if (Attributes.HasAttribute("spawnX"))
            {
                Pos.X = Attributes.GetDouble("spawnX");
                Pos.Y = Attributes.GetDouble("spawnY");
                Pos.Z = Attributes.GetDouble("spawnZ");
            }
        }

        public override void PlayEntitySound(string type, IPlayer dualCallByPlayer = null)
        {
            if (type == "hurt" && World.Side == EnumAppSide.Server)
            {
                (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, (int)EntityServerPacketId.Hurt);
                return;
            }
            if (type == "death" && World.Side == EnumAppSide.Server)
            {
                (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, (int)EntityServerPacketId.Death);
                return;
            }

            base.PlayEntitySound(type, dualCallByPlayer);
        }
    }
}
