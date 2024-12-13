using ProperVersion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class DialogueConfig
    {
        public DialogueComponent[] components;

        int uniqueIdCounter;
        public void Init()
        {
            foreach (var component in components)
            {
                component.Init(ref uniqueIdCounter);
            }
        }
    }

    public class ItemRepairConfig
    {
        public int Amount;
    }

    public delegate bool CanConverseDelegate(out string errorMessage);

    public class EntityBehaviorConversable : EntityBehavior
    {
        public static int BeginConvoPacketId = 1213;
        public static int SelectAnswerPacketId = 1214;
        public static int CloseConvoPacketId = 1215;

        public Dictionary<string, DialogueController> ControllerByPlayer = new Dictionary<string, DialogueController>();
        public GuiDialogueDialog Dialog;
        public EntityTalkUtil TalkUtil => (entity is ITalkUtil tu) ? tu.TalkUtil : talkUtilInst;

        EntityTalkUtil talkUtilInst;
        IWorldAccessor world;
        EntityAgent eagent;
        DialogueConfig dialogue;
        AssetLocation dialogueLoc;

        bool approachPlayer;


        public Action<DialogueController> OnControllerCreated;
        public event CanConverseDelegate CanConverse;

        EntityBehaviorActivityDriven bhActivityDriven;

        public DialogueController GetOrCreateController(EntityPlayer player)
        {
            if (player == null) return null;

            DialogueController controller;

            if (ControllerByPlayer.TryGetValue(player.PlayerUID, out controller))
            {
                foreach (var cmp in dialogue.components)
                {
                    cmp.SetReferences(controller, Dialog);
                }

                return controller;
            } else
            {
                dialogue = loadDialogue(dialogueLoc, player);
                if (dialogue == null) return null;

                controller = ControllerByPlayer[player.PlayerUID] = new DialogueController(world.Api, player, entity as EntityAgent, dialogue);
                controller.DialogTriggers += Controller_DialogTriggers;
                OnControllerCreated?.Invoke(controller);

                foreach (var cmp in dialogue.components)
                {
                    cmp.SetReferences(controller, Dialog);
                }

                return controller;
            }

        }

        private int Controller_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            if (value == "closedialogue")
            {
                Dialog?.TryClose();
            }

            if (value == "playanimation")
            {
                entity.AnimManager.StartAnimation(data.AsObject<AnimationMetaData>());
            }

            if (value == "giveitemstack")
            {
                if (entity.World.Side == EnumAppSide.Server)
                {
                    var jstack = data.AsObject<JsonItemStack>();
                    jstack.Resolve(entity.World, "conversable giveitem trigger");
                    ItemStack itemstack = jstack.ResolvedItemstack;
                    if (triggeringEntity.TryGiveItemStack(itemstack))
                    {
                        entity.World.SpawnItemEntity(itemstack, triggeringEntity.Pos.XYZ);
                    }
                }
            }

            if (value == "spawnentity")
            {
                if (entity.World.Side == EnumAppSide.Server)
                {
                    var cfg = data.AsObject<DlgSpawnEntityConfig>();

                    float weightsum = 0;
                    for (int i =0; i < cfg.Codes.Length; i++) weightsum += cfg.Codes[i].Weight;
                    var rnd = entity.World.Rand.NextDouble() * weightsum;

                    for (int i = 0; i < cfg.Codes.Length; i++) {
                        if ((rnd -= cfg.Codes[i].Weight) <= 0)
                        {
                            TrySpawnEntity((triggeringEntity as EntityPlayer)?.Player, cfg.Codes[i].Code, cfg.Range, cfg);
                            break;
                        }
                    }
                }
            }


            if (value == "takefrominventory")
            {
                if (entity.World.Side == EnumAppSide.Server)
                {
                    var jstack = data.AsObject<JsonItemStack>();
                    jstack.Resolve(entity.World, "conversable giveitem trigger");
                    ItemStack wantStack = jstack.ResolvedItemstack;
                    var slot = DlgTalkComponent.FindDesiredItem(triggeringEntity, wantStack);
                    if (slot != null)
                    {
                        slot.TakeOut(jstack.Quantity);
                        slot.MarkDirty();
                    }
                }
            }

            if (value == "repairheldtool" || value == "repairheldarmor")
            {
                if (entity.World.Side == EnumAppSide.Server)
                {
                    var slot = triggeringEntity.RightHandItemSlot;
                    if (!slot.Empty)
                    {
                        var rpcfg = data.AsObject<ItemRepairConfig>();
                        var d = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
                        var max = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);

                        bool repairable = value == "repairheldtool" ? (slot.Itemstack.Collectible.Tool != null) : (slot.Itemstack.Collectible.FirstCodePart() == "armor");

                        if (repairable && d < max) {
                            slot.Itemstack.Collectible.SetDurability(slot.Itemstack, Math.Min(max, d + rpcfg.Amount));
                            slot.MarkDirty();
                        }
                    }
                }
            }

            if (value == "attack")
            {
                var damagetype = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), data["type"].AsString("BluntAttack"));

                triggeringEntity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = damagetype,
                }, data["damage"].AsInt(0));
            }

            if (value == "revealname")
            {
                var plr = (triggeringEntity as EntityPlayer)?.Player;
                if (plr != null)
                {
                    string arg = data["selector"].ToString();
                    if (arg != null && arg.StartsWith("e["))
                    {
                        EntitiesArgParser test = new EntitiesArgParser("test", world.Api, true);
                        TextCommandCallingArgs packedArgs = new TextCommandCallingArgs()
                        {
                            Caller = new Caller()
                            {
                                Type = EnumCallerType.Console,
                                CallerRole = "admin",
                                CallerPrivileges = new string[] { "*" },
                                FromChatGroupId = GlobalConstants.ConsoleGroup,
                                Pos = new Vec3d(0.5, 0.5, 0.5)
                            },
                            RawArgs = new CmdArgs(arg)
                        };
                        EnumParseResult result = test.TryProcess(packedArgs);
                        if (result == EnumParseResult.Good) {
                            foreach (var e in (Entity[])test.GetValue())
                            {
                                e.GetBehavior<EntityBehaviorNameTag>().SetNameRevealedFor(plr.PlayerUID);
                            }
                        } else
                        {
                            world.Logger.Warning("Conversable trigger: Unable to reveal name, invalid selector - " + arg);
                        }
                    } else
                    {
                        entity.GetBehavior<EntityBehaviorNameTag>().SetNameRevealedFor(plr.PlayerUID);
                    }

                }
            }

            return -1;
        }

        private void TrySpawnEntity(IPlayer forplayer, string entityCode, float range, DlgSpawnEntityConfig cfg)
        {
            var etype = entity.World.GetEntityType(AssetLocation.Create(entityCode, entity.Code.Domain));
            if (etype == null)
            {
                entity.World.Logger.Warning("Dialogue system, unable to spawn {0}, no such entity exists", entityCode);
                return;
            }

            var centerpos = entity.ServerPos;
            var minpos = centerpos.Copy().Add(-range, 0, -range).AsBlockPos;
            var maxpos = centerpos.Copy().Add(range, 0, range).AsBlockPos;


            var spawnpos = findSpawnPos(forplayer, etype, minpos, maxpos, false, 4);

            if (spawnpos == null)
            {
                spawnpos = findSpawnPos(forplayer, etype, minpos, maxpos, true, 1);
            }

            if (spawnpos == null)
            {
                spawnpos = findSpawnPos(forplayer, etype, minpos, maxpos, true, 1);
            }

            if (spawnpos != null)
            {
                var spawnentity = entity.Api.ClassRegistry.CreateEntity(etype);
                spawnentity.ServerPos.SetPos(spawnpos);
                entity.World.SpawnEntity(spawnentity);

                if (cfg.GiveStacks != null)
                {
                    foreach (var stack in cfg.GiveStacks)
                    {
                        if (stack.Resolve(entity.World, "spawn entity give stack"))
                        {
                            entity.Api.Event.EnqueueMainThreadTask(() =>
                            {
                                spawnentity.TryGiveItemStack(stack.ResolvedItemstack.Clone());
                            }, "tradedlggivestack");
                        }
                    }
                }
            }
        }

        private Vec3d findSpawnPos(IPlayer forplayer, EntityProperties etype, BlockPos minpos, BlockPos maxpos, bool rainheightmap, int mindistance)
        {
            bool spawned = false;
            BlockPos tmp = new BlockPos();
            var ba = entity.World.BlockAccessor;
            const int chunksize = GlobalConstants.ChunkSize;
            var collisionTester = entity.World.CollisionTester;
            var sapi = entity.Api as ICoreServerAPI;
            Vec3d okspawnpos = null;

            var epos = entity.ServerPos.XYZ;

            ba.WalkBlocks(minpos, maxpos, (block, x, y, z) =>
            {
                if (spawned) return;
                if (epos.DistanceTo(x, y, z) < mindistance) return;

                int lz = z % chunksize;
                int lx = x % chunksize;
                var mc = ba.GetMapChunkAtBlockPos(tmp.Set(x, y, z));
                int ty = rainheightmap ? mc.RainHeightMap[lz * chunksize + lx] : (mc.WorldGenTerrainHeightMap[lz * chunksize + lx]+1);

                Vec3d spawnpos = new Vec3d(x + 0.5, ty + 0.1, z + 0.5);
                Cuboidf collisionBox = etype.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);
                if (!collisionTester.IsColliding(ba, collisionBox, spawnpos, false))
                {
                    var resp = sapi.World.Claims.TestAccess(forplayer, spawnpos.AsBlockPos, EnumBlockAccessFlags.BuildOrBreak);
                    if (resp == EnumWorldAccessResponse.Granted)
                    {
                        spawned = true;
                        okspawnpos = spawnpos;
                    }
                }

            }, true);

            return okspawnpos;
        }

        public EntityBehaviorConversable(Entity entity) : base(entity)
        {
            world = entity.World;
            eagent = entity as EntityAgent;

            if (world.Side == EnumAppSide.Client && !(entity is ITalkUtil))
            {
                talkUtilInst = new EntityTalkUtil(world.Api as ICoreClientAPI, entity, false);
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            approachPlayer = attributes["approachPlayer"].AsBool(true);

            var dlgStr = attributes["dialogue"].AsString();
            dialogueLoc = AssetLocation.Create(dlgStr, entity.Code.Domain);

            if (entity.World.Side == EnumAppSide.Server)
            {
                foreach (var val in properties.Client.BehaviorsAsJsonObj)
                {
                    if (val["code"].ToString() == attributes["code"].ToString())
                    {
                        if (dlgStr != val["dialogue"].AsString())
                        {
                            throw new InvalidOperationException(string.Format("Conversable behavior for entity {0}: You must define the same dialogue path on the client as well as the server side, currently they are set to {1} and {2}.",
                                entity.Code,
                                dlgStr,
                                val["dialogue"].AsString()
                            ));
                        }
                    }
                }
            }

            if (dialogueLoc == null)
            {
                world.Logger.Error("entity behavior conversable for entity " + entity.Code + ", dialogue path not set. Won't load dialogue.");
                return;
            }


        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            bhActivityDriven = entity.GetBehavior<EntityBehaviorActivityDriven>();
        }

        public override void OnEntitySpawn()
        {
            setupTaskBlocker();
        }

        public override void OnEntityLoaded()
        {
            setupTaskBlocker();
        }


        void setupTaskBlocker()
        {
            if (entity.Api.Side != EnumAppSide.Server) return;
            var bhtaskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (bhtaskAi != null)
            {
                bhtaskAi.TaskManager.OnShouldExecuteTask += (task) => ControllerByPlayer.Count == 0 || task is AiTaskIdle || task is AiTaskSeekEntity || task is AiTaskGotoEntity;
            }

            var bhActivityDriven = entity.GetBehavior<EntityBehaviorActivityDriven>();
            if (bhActivityDriven != null)
            {
                bhActivityDriven.OnShouldRunActivitySystem += () => ControllerByPlayer.Count == 0 && gototask == null;
            }
        }


        private DialogueConfig loadDialogue(AssetLocation loc, EntityPlayer forPlayer)
        {
            string charclass = forPlayer.WatchedAttributes.GetString("characterClass");
            string ownPersonality = entity.WatchedAttributes.GetString("personality");

            var asset = world.AssetManager.TryGet(loc.Clone().WithPathAppendixOnce($"-{ownPersonality}-{charclass}.json"));

            if (asset == null)
            {
                asset = world.AssetManager.TryGet(loc.Clone().WithPathAppendixOnce($"-{ownPersonality}.json"));
            }

            if (asset == null)
            {
                asset = world.AssetManager.TryGet(loc.WithPathAppendixOnce(".json"));
            }

            if (asset == null)
            {
                world.Logger.Error("Entitybehavior conversable for entity " + entity.Code + ", dialogue asset "+loc+" not found. Won't load dialogue.");
                return null;
            }

            try
            {
                var cfg = asset.ToObject<DialogueConfig>();
                cfg.Init();
                return cfg;

            } catch (Exception e)
            {
                world.Logger.Error("Entitybehavior conversable for entity {0}, dialogue asset is invalid:", entity.Code);
                world.Logger.Error(e);
                return null;
            }
        }

        public override string PropertyName()
        {
            return "conversable";
        }

        AiTaskGotoEntity gototask;
        float gotoaccum = 0;

        public const float BeginTalkRangeSq = 3 * 3;
        public const float ApproachRangeSq = 4 * 4;
        public const float StopTalkRangeSq = 5 * 5;

        public override void OnGameTick(float deltaTime)
        {
            if (gototask != null)
            {
                gotoaccum += deltaTime;

                if (gototask.TargetReached())
                {
                    var splr = (gototask.targetEntity as EntityPlayer)?.Player as IServerPlayer;
                    var sapi = entity.World.Api as ICoreServerAPI;
                    if (splr != null && splr.ConnectionState == EnumClientState.Playing)
                    {
                        var tasklook = new AiTaskLookAtEntity(eagent);
                        tasklook.manualExecute = true;
                        tasklook.targetEntity = gototask.targetEntity;
                        AiTaskManager tmgr = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                        tmgr.ExecuteTask(tasklook, 1);

                        sapi.Network.SendEntityPacket(splr, entity.EntityId, BeginConvoPacketId);
                        beginConvoServer(splr);
                    }
                    gototask = null;
                }

                if (gototask?.Finished == true || gotoaccum > 3)
                {
                    gototask = null;
                }
            }

            foreach (var val in ControllerByPlayer)
            {
                var player = world.PlayerByUid(val.Key);
                var entityplayer = player.Entity;
                if (!entityplayer.Alive || entityplayer.Pos.SquareDistanceTo(entity.Pos) > StopTalkRangeSq)
                {
                    ControllerByPlayer.Remove(val.Key);

                    if (world.Api is ICoreServerAPI sapi)
                    {
                        sapi.Network.SendEntityPacket(player as IServerPlayer, entity.EntityId, CloseConvoPacketId);
                    } else
                    {
                        Dialog?.TryClose();
                    }
                    break;
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode != EnumInteractMode.Interact || !(byEntity is EntityPlayer))
            {
                handled = EnumHandling.PassThrough;
                return;
            }

            if (!entity.Alive) return;

            if (CanConverse != null)
            {
                foreach (CanConverseDelegate act in CanConverse.GetInvocationList())
                {
                    if (!act.Invoke(out string errorMsg))
                    {
                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendIngameError("cantconverse", Lang.Get(errorMsg));
                        return;
                    }
                }
            }

            GetOrCreateController(byEntity as EntityPlayer);

            handled = EnumHandling.PreventDefault;

            EntityPlayer entityplr = byEntity as EntityPlayer;
            IPlayer player = world.PlayerByUid(entityplr.PlayerUID);

            if (world.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)world.Api;

                if (entityplr.Pos.SquareDistanceTo(entity.Pos) <= BeginTalkRangeSq && Dialog?.IsOpened() != true)
                {
                    beginConvoClient();
                }

                TalkUtil.Talk(EnumTalkType.Meet);
            }

            if (world.Side == EnumAppSide.Server && gototask == null && byEntity.Pos.SquareDistanceTo(entity.Pos) <= ApproachRangeSq && !remainStationaryOnCall())
            {
                // Make the entity walk towards the player
                AiTaskManager tmgr = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                if (tmgr != null)
                {
                    tmgr.StopTask(typeof(AiTaskWander));

                    gototask = new AiTaskGotoEntity(eagent, entityplr);
                    gototask.allowedExtraDistance = 1.0f;
                    if (gototask.TargetReached() || !approachPlayer)
                    {
                        gotoaccum = 0;
                        gototask = null;
                        var tasklook = new AiTaskLookAtEntity(eagent);
                        tasklook.manualExecute = true;
                        tasklook.targetEntity = entityplr;
                        tmgr.ExecuteTask(tasklook, 1);
                    }
                    else
                    {
                        tmgr.ExecuteTask(gototask, 1);
                        bhActivityDriven?.ActivitySystem.Pause();
                    }



                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = "welcome", Code = "welcome", Weight = 10, EaseOutSpeed = 10000, EaseInSpeed = 10000 });
                    entity.AnimManager.StopAnimation("idle");
                }
            }
        }


        public string[] remainStationaryAnimations = new string[] { "sit-idle", "sit-write", "sit-tinker", "sitfloor", "sitedge", "sitchair", "sitchairtable", "eatsittable", "bowl-eatsittable" };
        private bool remainStationaryOnCall()
        {
            var eagent = entity as EntityAgent;
            return
                (eagent != null && eagent.MountedOn != null && eagent.MountedOn is BlockEntityBed)
                || eagent.AnimManager.IsAnimationActive(remainStationaryAnimations)
            ;
        }

        private bool beginConvoClient()
        {
            ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;
            EntityPlayer entityplr = capi.World.Player.Entity;

            // Will break all kinds of things if we allow multiple concurrent of these dialogs
            if (capi.Gui.OpenedGuis.FirstOrDefault(dlg => dlg is GuiDialogueDialog && dlg.IsOpened()) == null)
            {
                Dialog = new GuiDialogueDialog(capi, eagent);
                Dialog.OnClosed += Dialog_OnClosed;

                var controller = GetOrCreateController(entityplr);
                if (controller == null)
                {
                    capi.TriggerIngameError(this, "errord", Lang.Get("Error when loading dialogue. Check log files."));
                    return false;
                }

                Dialog.InitAndOpen();
                controller.ContinueExecute();
                capi.Network.SendEntityPacket(entity.EntityId, BeginConvoPacketId);
            }
            else
            {
                capi.TriggerIngameError(this, "onlyonedialog", Lang.Get("Can only trade with one trader at a time"));
                return false;
            }


            return true;
        }

        private void Dialog_OnClosed()
        {
            ControllerByPlayer.Clear();
            Dialog = null;
            (world.Api as ICoreClientAPI).Network.SendEntityPacket(entity.EntityId, CloseConvoPacketId);
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);

            if (packetid == BeginConvoPacketId)
            {
                beginConvoServer(player);
            }

            if (packetid == SelectAnswerPacketId)
            {
                int id = SerializerUtil.Deserialize<int>(data);
                var controller = GetOrCreateController(player.Entity);

                controller.PlayerSelectAnswerById(id);
            }

            if (packetid == CloseConvoPacketId)
            {
                ControllerByPlayer.Remove(player.PlayerUID);
            }
        }

        private void beginConvoServer(IServerPlayer player)
        {
            var controller = GetOrCreateController(player.Entity);
            controller.ContinueExecute();
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedServerPacket(packetid, data, ref handled);

            if (packetid == BeginConvoPacketId)
            {
                ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;
                EntityPlayer entityplr = capi.World.Player.Entity;

                if (entityplr.Pos.SquareDistanceTo(entity.Pos) > StopTalkRangeSq || Dialog?.IsOpened() == true || !beginConvoClient())
                {
                    capi.Network.SendEntityPacket(this.entity.EntityId, CloseConvoPacketId);
                }
            }

            if (packetid == CloseConvoPacketId)
            {
                ControllerByPlayer.Clear();
                Dialog?.TryClose();
                Dialog = null;
            }
        }
    }

    internal class DlgSpawnEntityConfig
    {
        public WeightedCode[] Codes;
        public float Range;
        public JsonItemStack[] GiveStacks;
    }
}
