using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public delegate bool CanSitDelegate(EntityAgent eagent, out string errorMessage);

    public class EntityBehaviorCreatureCarrier : EntityBehaviorSeatable, IRopeTiedCreatureCarrier
    {
        public EntityBehaviorCreatureCarrier(Entity entity) : base(entity)
        {
        }
    }

    public class EntityBehaviorSeatable : EntityBehavior, IVariableSeatsMountable
    {
        public IMountableSeat[] Seats { get; set; }
        public SeatConfig[] SeatConfigs;
        public EntityPos Position => entity.SidedPos;
        public double StepPitch => (entity.Properties.Client.Renderer as EntityShapeRenderer).stepPitch;
        ICoreAPI Api => entity.Api;

        bool interactMountAnySeat;

        public event CanSitDelegate CanSit;

        public Entity Controller { get; set; }

        public Entity OnEntity => entity;

        public EntityControls ControllingControls
        {
            get
            {
                foreach (var seat in Seats)
                {
                    if (seat.CanControl) return seat.Controls;
                }
                return null;
            }
        }

        public EntityBehaviorSeatable(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            SeatConfigs = attributes["seats"].AsObject<SeatConfig[]>();

            interactMountAnySeat = attributes["interactMountAnySeat"].AsBool(false);

            int i = 0;
            foreach (var seatConfig in SeatConfigs)
            {
                if (seatConfig.SeatId == null)
                {
                    seatConfig.SeatId = "baseseat-" + i++;
                }
                RegisterSeat(seatConfig);
            }

            if (Api is ICoreClientAPI)
            {
                entity.WatchedAttributes.RegisterModifiedListener("seatdata", UpdatePassenger);
            }

            base.Initialize(properties, attributes);
        }



        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            // The mounted entity will try to mount as well, but at that time, the boat might not have been loaded, so we'll try mounting on both ends.
            for (int i = 0; i < Seats.Length; i++)
            {
                var seat = Seats[i];

                if (seat.Config == null)
                {
                    Seats = Seats.RemoveAt(i);
                    Api.Logger.Warning("Entity {0}, Seat #{1}, id {2} was loaded but not registered, will remove.", entity.Code, i, seat.SeatId);
                    i--;
                    continue;
                }

                if (seat.PassengerEntityIdForInit != 0 && seat.Passenger == null)
                {
                    var byEntity = Api.World.GetEntityById(seat.PassengerEntityIdForInit) as EntityAgent;
                    if (byEntity != null)
                    {
                        byEntity.TryMount(seat);
                    }
                }
            }
        }


        public bool TryMount(EntityAgent carriedCreature)
        {
            if (carriedCreature != null)
            {
                foreach (var seat in Seats)
                {
                    if (seat.Passenger != null) continue;
                    if (carriedCreature.TryMount(seat))
                    {
                        carriedCreature.Controls.StopAllMovement();
                        return true;
                    }
                }
            }

            return false;
        }

        

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode != EnumInteractMode.Interact || !entity.Alive || !byEntity.Alive) return;
            if (!allowSit(byEntity)) return;
            if (itemslot.Itemstack?.Collectible is ItemRope) return;

            int seleBox = (byEntity as EntityPlayer).EntitySelection?.SelectionBoxIndex ?? -1;
            var bhs = entity.GetBehavior<EntityBehaviorSelectionBoxes>();

            if (bhs != null && byEntity.MountedOn == null && seleBox > 0)
            {
                var apap = bhs.selectionBoxes[seleBox - 1];
                string apname = apap.AttachPoint.Code;
                var seat = Seats.FirstOrDefault((seat) => seat.Config.APName == apname || seat.Config.SelectionBox == apname);
                if (seat != null && seat.Passenger != null && seat.Passenger.HasBehavior<EntityBehaviorRopeTieable>())
                {
                    (seat.Passenger as EntityAgent).TryUnmount();
                    handled = EnumHandling.PreventSubsequent;
                    return;
                }
            }

            if (byEntity.Controls.CtrlKey)
            {
                return;
            }

            // If we have the selection boxes behavior, use that
            if (seleBox > 0 && bhs != null)
            {
                var apap = bhs.selectionBoxes[seleBox - 1];
                string apname = apap.AttachPoint.Code;

                var seat = Seats.FirstOrDefault((seat) => seat.Config.APName == apname || seat.Config.SelectionBox == apname);
                if (seat != null && CanSitOn(seat, seleBox - 1) && byEntity.TryMount(seat))
                {
                    handled = EnumHandling.PreventSubsequent;
                    if (Api.Side == EnumAppSide.Server)
                    {
                        Api.World.Logger.Audit("{0} mounts/embarks a {1} at {2}.", byEntity?.GetName(), entity.Code.ToShortString(), entity.ServerPos.AsBlockPos);
                    }
                    return;
                }

                if (!interactMountAnySeat || !itemslot.Empty)
                {
                    return;
                }
            }

            mountAnySeat(byEntity, out handled);
        }

        private bool allowSit(EntityAgent byEntity)
        {
            if (CanSit == null) return true;
            
            ICoreClientAPI capi = Api as ICoreClientAPI;
            foreach (CanSitDelegate dele in CanSit.GetInvocationList())
            {
                if (!dele(byEntity, out string errMsg))
                {
                    if (errMsg != null)
                    {
                        capi?.TriggerIngameError(this, "cantride", Lang.Get("cantride-" + errMsg));
                    }
                    return false;
                }
            }

            return true;            
        }

        private void mountAnySeat(EntityAgent byEntity, out EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;

            // Get on the first available controllable seat
            foreach (var seat in Seats)
            {
                if (!CanSitOn(seat)) continue;
                if (!seat.CanControl) continue;
                if (byEntity.TryMount(seat))
                {
                    if (Api.Side == EnumAppSide.Server)
                    {
                        Api.World.Logger.Audit("{0} mounts/embarks a {1} at {2}.", byEntity?.GetName(), entity.Code.ToShortString(), entity.ServerPos.AsBlockPos);
                    }
                    return;
                }
            }

            // Otherwise just any seat
            foreach (var seat in Seats)
            {
                if (!CanSitOn(seat)) continue;
                if (byEntity.TryMount(seat))
                {
                    if (Api.Side == EnumAppSide.Server)
                    {
                        Api.World.Logger.Audit("{0} mounts/embarks a {1} at {2}.", byEntity?.GetName(), entity.Code.ToShortString(), entity.ServerPos.AsBlockPos);
                    }
                    return;
                }
            }
        }

        public bool CanSitOn(IMountableSeat seat, int selectionBoxIndex = -1)
        {
            if (seat.Passenger != null) return false;
            var bha = entity.GetBehavior<EntityBehaviorAttachable>();
            if (bha != null)
            {
                if (selectionBoxIndex == -1)
                {
                    var bhas = entity.GetBehavior<EntityBehaviorSelectionBoxes>();
                    selectionBoxIndex = bhas.selectionBoxes.IndexOf(x => x.AttachPoint.Code == seat.Config.SelectionBox);
                }
                var targetSlot = bha.GetSlotFromSelectionBoxIndex(selectionBoxIndex);
                var category = targetSlot?.Itemstack?.Item?.Attributes?["attachableToEntity"]["categoryCode"].AsString();
                if (targetSlot?.Empty == false && category != "seat" &&  category != "saddle" && category != "pillion")
                {
                    return false;
                }

                var slot = bha.GetSlotConfigFromAPName(seat.Config.APName);
                if (slot?.Empty == false)
                {
                    return 
                        slot.Itemstack.ItemAttributes?["isSaddle"].AsBool(false) == true ||  // Can only sit if in this slot there is a saddle
                        slot.Itemstack.ItemAttributes?["attachableToEntity"]["seatConfig"].Exists == true ||
                        slot.Itemstack.ItemAttributes?["attachableToEntity"]["seatConfigBySlotCode"].Exists == true
                    ;
                }
            }

            return true;
        }

        public void RegisterSeat(SeatConfig seatconfig)
        {
            if (seatconfig?.SeatId == null) throw new ArgumentNullException("seatConfig.SeatId must be set");

            if (Seats == null) Seats = Array.Empty<IMountableSeat>();

            int index = Seats.IndexOf(s => s.SeatId == seatconfig.SeatId);
            if (index < 0)
            {
                Seats = Seats.Append(CreateSeat(seatconfig.SeatId, seatconfig));
            }
            else
            {
                Seats[index].Config = seatconfig;
            }

            entity.WatchedAttributes.MarkAllDirty();
        }

        public void RemoveSeat(string seatId)
        {
            int index = Seats.IndexOf(s => s.SeatId == seatId);
            if (index < 0) return;
            Seats = Seats.RemoveAt(index);
            entity.WatchedAttributes.MarkAllDirty();
        }

        private ITreeAttribute seatsToAttr()
        {
            TreeAttribute tree = new TreeAttribute();

            for (int i = 0; i < Seats.Length; i++)
            {
                var seat = Seats[i];
                tree["s" + i] =
                    new TreeAttribute()
                    .Set("passenger", new LongAttribute(seat.Passenger?.EntityId ?? 0))
                    .Set("seatid", new StringAttribute(seat.SeatId))
                ;
            }

            return tree;
        }

        private void seatsFromAttr()
        {
            var tree = entity.WatchedAttributes["seatdata"] as TreeAttribute;
            if (tree == null) return;
            if (Seats == null || Seats.Length != tree.Count)
            {
                Seats = new IMountableSeat[tree.Count];
            }


            for (int i = 0; i < tree.Count; i++)
            {
                var stree = tree["s" + i] as TreeAttribute;

                Seats[i] = CreateSeat((stree["seatid"] as StringAttribute).value, null);
                Seats[i].PassengerEntityIdForInit = (stree["passenger"] as LongAttribute).value;
            }
        } 

        private void UpdatePassenger()
        {
            var tree = entity.WatchedAttributes["seatdata"] as TreeAttribute;
            if (tree == null) return;

            for (int i = 0; i < tree.Count; i++)
            {
                var stree = tree["s" + i] as TreeAttribute;

                if (Api.World.GetEntityById((stree["passenger"] as LongAttribute).value) is EntityAgent passanger)
                {
                    ((EntitySeat)Seats[i]).Passenger = passanger;
                }else if (Seats[i].Passenger != null)
                {
                    ((EntitySeat)Seats[i]).Passenger = null;
                }
            }
        }

        protected virtual IMountableSeat CreateSeat(string seatId, SeatConfig config)
        {
            return (entity as ISeatInstSupplier).CreateSeat(this, seatId, config);
        }

        public override void FromBytes(bool isSync)
        {
            seatsFromAttr();
        }

        public override void ToBytes(bool forClient)
        {
            entity.WatchedAttributes["seatdata"] = seatsToAttr();
        }

        public virtual bool AnyMounted()
        {
            var Seats = this.Seats;
            for (int i = 0; i < Seats.Length; i++)
            {
                if (Seats[i].Passenger != null) return true;
            }
            return false;
        }

        public override string PropertyName() => "seatable";



        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (es.SelectionBoxIndex > 0)
            {
                return SeatableInteractionHelp.GetOrCreateInteractionHelp(world.Api, this, Seats, es.SelectionBoxIndex - 1);
            }

            return base.GetInteractionHelp(world, es, player, ref handled);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);

            foreach (var seat in Seats) {
                (seat?.Passenger as EntityAgent)?.TryUnmount();
            }
        }
    }



    public class SeatableInteractionHelp
    {
        public static WorldInteraction[] GetOrCreateInteractionHelp(ICoreAPI api, EntityBehaviorSeatable eba, IMountableSeat[] seats, int slotIndex)
        {
            IMountableSeat seat = getSeat(eba, seats, slotIndex);
            if (seat == null) return null;

            if (seat.Config.Attributes?["ropeTieablesOnly"].AsBool(false) == true)
            {
                List<ItemStack> ropetiableStacks = ObjectCacheUtil.GetOrCreate(api, "interactionhelp-ropetiablestacks", () =>
                {
                    List<ItemStack> ropetiableStacks = new List<ItemStack>();

                    foreach (var etype in api.World.EntityTypes)
                    {
                        foreach (var val in etype.Client.BehaviorsAsJsonObj)
                        {
                            if (val["code"].AsString() == "ropetieable")
                            {
                                var invitem = api.World.GetItem(AssetLocation.Create("creature-" + etype.Code.Path, etype.Code.Domain));
                                if (invitem != null)
                                {
                                    ropetiableStacks.Add(new ItemStack(invitem));
                                }
                            }
                        }
                    }

                    return ropetiableStacks;
                });

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = seat.Passenger != null ? "seatableentity-dismountcreature" : "seatableentity-mountcreature",
                        Itemstacks = ropetiableStacks.ToArray(),
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            }

            if (eba.CanSitOn(seat, slotIndex))
            {
                return new WorldInteraction[]
                {
                new WorldInteraction()
                {
                    ActionLangCode = "seatableentity-mount",
                    MouseButton = EnumMouseButton.Right,
                }
                };
            }

            return null;
        }

        private static IMountableSeat getSeat(EntityBehaviorSeatable eba, IMountableSeat[] seats, int slotIndex)
        {
            var bhs = eba.entity.GetBehavior<EntityBehaviorSelectionBoxes>();
            if (bhs == null) return null;

            var apap = bhs.selectionBoxes[slotIndex];
            string apname = apap.AttachPoint.Code;

            return seats.FirstOrDefault((seat) => seat.Config.APName == apname || seat.Config.SelectionBox == apname);
        }
    }
}
