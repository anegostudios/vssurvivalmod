using System;
using System.Collections.Generic;
using System.Linq;
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
    }

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


        public Action<DialogueController> onControllerCreated;


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

                controller = ControllerByPlayer[player.PlayerUID] = new DialogueController(world.Api, player, entity as EntityAgent, dialogue);
                controller.DialogTriggers += Controller_DialogTriggers;
                onControllerCreated?.Invoke(controller);

                foreach (var cmp in dialogue.components)
                {
                    cmp.SetReferences(controller, Dialog);
                }

                return controller;
            }
                
        }

        private int Controller_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            if (value == "playanimation")
            {
                entity.AnimManager.StartAnimation(data.AsObject<AnimationMetaData>());
                return -1;
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

                return -1;
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

                return -1;
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

            return -1;
        }

        public EntityBehaviorConversable(Entity entity) : base(entity)
        {
            world = entity.World;
            eagent = entity as EntityAgent;

            if (world.Side == EnumAppSide.Client && !(entity is ITalkUtil))
            {
                talkUtilInst = new EntityTalkUtil(world.Api as ICoreClientAPI, entity);
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            dialogueLoc = AssetLocation.Create(attributes["dialogue"].AsString(), entity.Code.Domain);
            if (dialogueLoc == null)
            {
                world.Logger.Error("entity behavior conversable for entity " + entity.Code + ", dialogue path not set. Won't load dialogue.");
                return;
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
                return asset.ToObject<DialogueConfig>();
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

        public override void OnGameTick(float deltaTime)
        {
            foreach (var val in ControllerByPlayer)
            {
                var player = world.PlayerByUid(val.Key);
                var entityplayer = player.Entity;
                if (!entityplayer.Alive || entityplayer.Pos.SquareDistanceTo(entity.Pos) > 5)
                {
                    ControllerByPlayer.Remove(val.Key);

                    if (world.Api is ICoreServerAPI sapi)
                    {
                        sapi.Network.SendEntityPacket(player as IServerPlayer, entity.EntityId, CloseConvoPacketId);
                    } else
                    {
                        (world.Api as ICoreClientAPI).Network.SendEntityPacket(entity.EntityId, CloseConvoPacketId);
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

            handled = EnumHandling.PreventDefault;

            EntityPlayer entityplr = byEntity as EntityPlayer;
            IPlayer player = world.PlayerByUid(entityplr.PlayerUID);

            if (world.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)world.Api;

                if (entityplr.Pos.SquareDistanceTo(entity.Pos) <= 5 && Dialog?.IsOpened() != true)
                {
                    // Will break all kinds of things if we allow multiple concurrent of these dialogs
                    if (capi.Gui.OpenedGuis.FirstOrDefault(dlg => dlg is GuiDialogueDialog && dlg.IsOpened()) == null)
                    {
                        Dialog = new GuiDialogueDialog(capi, eagent);
                        Dialog.OnClosed += Dialog_OnClosed;
                        var controller = GetOrCreateController(entityplr);

                        Dialog.InitAndOpen();
                        controller.ContinueExecute();
                        capi.Network.SendEntityPacket(entity.EntityId, BeginConvoPacketId);
                    }
                    else
                    {
                        capi.TriggerIngameError(this, "onlyonedialog", Lang.Get("Can only trade with one trader at a time"));
                    }
                }

                TalkUtil.Talk(EnumTalkType.Meet);
            }

            if (world.Side == EnumAppSide.Server)
            {
                // Make the entity walk towards the player
                AiTaskManager tmgr = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                if (tmgr != null)
                {
                    tmgr.StopTask(typeof(AiTaskWander));

                    AiTaskGotoEntity task = new AiTaskGotoEntity(eagent, entityplr);
                    if (task.TargetReached())
                    {
                        var tasklook = new AiTaskLookAtEntity(eagent);
                        tasklook.manualExecute = true;
                        tasklook.targetEntity = entityplr;
                        tmgr.ExecuteTask(tasklook, 1);
                    }
                    else
                    {
                        tmgr.ExecuteTask(task, 1);
                    }

                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = "welcome", Code = "welcome", Weight = 10, EaseOutSpeed = 10000, EaseInSpeed = 10000 });
                    entity.AnimManager.StopAnimation("idle");
                }
            }
        }

        private void Dialog_OnClosed()
        {
            ControllerByPlayer.Clear();
            (world.Api as ICoreClientAPI).Network.SendEntityPacket(entity.EntityId, CloseConvoPacketId);
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);

            if (packetid == BeginConvoPacketId)
            {
                var controller = GetOrCreateController(player.Entity);
                controller.ContinueExecute();
            }

            if (packetid == SelectAnswerPacketId)
            {
                int index = SerializerUtil.Deserialize<int>(data);
                var controller = GetOrCreateController(player.Entity);

                controller.PlayerSelectAnswer(index);
            }

            if (packetid == CloseConvoPacketId)
            {
                ControllerByPlayer.Remove(player.PlayerUID);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedServerPacket(packetid, data, ref handled);

            if (packetid == CloseConvoPacketId)
            {
                ControllerByPlayer.Clear();
                Dialog?.TryClose();
                Dialog = null;
            }
        }

        

    }
}
