using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class Auction : IComparable<Auction>
    {
        public ItemStack ItemStack;
        [ProtoMember(1)]
        public long AuctionId;
        [ProtoMember(2)]
        public byte[] ItemStackSerialized;
        [ProtoMember(3)]
        public int Price;
        [ProtoMember(4)]
        public int TraderCut;
        [ProtoMember(5)]
        public double PostedTotalHours;
        [ProtoMember(6)]
        public double ExpireTotalHours;
        [ProtoMember(7)]
        public Vec3d SrcAuctioneerEntityPos;
        [ProtoMember(8)]
        public long SrcAuctioneerEntityId;

        [ProtoMember(9)]
        public string SellerUid;
        [ProtoMember(10)]
        public string SellerName;
        [ProtoMember(11)]
        public long SellerEntityId;

        [ProtoMember(12)]
        public string BuyerUid;
        [ProtoMember(13)]
        public string BuyerName;
        [ProtoMember(14)]
        public double RetrievableTotalHours;

        [ProtoMember(15)]
        public Vec3d DstAuctioneerEntityPos;
        [ProtoMember(16)]
        public long DstAuctioneerEntityId;
        [ProtoMember(17)]
        public EnumAuctionState State;
        [ProtoMember(18)]
        public bool MoneyCollected;
        [ProtoMember(19)]
        public bool WithDelivery;
        

        [OnDeserialized]
        protected void OnDeserializedMethod(StreamingContext context)
        {
            using (MemoryStream ms = new MemoryStream(ItemStackSerialized))
            {
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    ItemStack = new ItemStack(reader);
                }
            }
        }

        [ProtoBeforeSerialization]
        protected void BeforeSerialization()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    ItemStack.ToBytes(writer);
                }

                ItemStackSerialized = ms.ToArray();
            }
        }


        public string GetExpireText(ICoreAPI api)
        {
            switch (State)
            {
                case EnumAuctionState.Active:
                    {
                        double activeHours = (ExpireTotalHours - api.World.Calendar.TotalHours);
                        return prettyHours(activeHours, api);
                    }
                case EnumAuctionState.Expired:
                    {
                        double waithours = (RetrievableTotalHours - api.World.Calendar.TotalHours);
                        if (waithours > 0)
                        {
                            return Lang.Get("Expired, returning to owner. {0}", prettyHours(waithours, api));
                        } else
                        {
                            return Lang.Get("Expired, returned to owner.");
                        }
                    }
                case EnumAuctionState.Sold:
                    {
                        double waithours = (RetrievableTotalHours - api.World.Calendar.TotalHours);
                        

                        if (api.World.Config.GetBool("allowMap", true))
                        {
                            if (WithDelivery)
                            {
                                string traderMapLink = string.Format("worldmap://={0}={1}={2}=" + Lang.Get("Delivery of {0}x{1}", ItemStack.StackSize, ItemStack.GetName()), DstAuctioneerEntityPos.XInt, DstAuctioneerEntityPos.YInt, DstAuctioneerEntityPos.ZInt);

                                if (waithours > 0)
                                {
                                    return Lang.Get("Sold to {0}, en route to <a href=\"{2}\">trader</a>. {1}", BuyerName, prettyHours(waithours, api), traderMapLink);
                                }
                                else
                                {
                                    return Lang.Get("Sold to {0}, delievered to <a href=\"{1}\">trader</a>.", BuyerName, traderMapLink);
                                }
                            } else
                            {
                                string traderMapLink = string.Format("worldmap://={0}={1}={2}=" + Lang.Get("Pickup of {0}x{1}", ItemStack.StackSize, ItemStack.GetName()), DstAuctioneerEntityPos.XInt, DstAuctioneerEntityPos.YInt, DstAuctioneerEntityPos.ZInt);

                                if (waithours > 0)
                                {
                                    return Lang.Get("Sold to {0}, <a href=\"{2}\">preparing pickup</a>. {1}", BuyerName, prettyHours(waithours, api), traderMapLink);
                                }
                                else
                                {
                                    return Lang.Get("Sold to {0}, pick up at <a href=\"{1}\">trader</a>.", BuyerName, traderMapLink);
                                }
                            }
                        } else
                        {
                            var pos = DstAuctioneerEntityPos.AsBlockPos.Sub(api.World.DefaultSpawnPosition.AsBlockPos);

                            if (waithours > 0)
                            {
                                return Lang.Get("Sold to {0}, en route to trader at {1},{2},{3}. {4}", BuyerName, pos.X, pos.Y, pos.Z, prettyHours(waithours, api));
                            }
                            else
                            {
                                return Lang.Get("Sold to {0}, delievered to trader at {1},{2},{3}.", BuyerName, pos.X, pos.Y, pos.Z);
                            }
                        }
                    }
                case EnumAuctionState.SoldRetrieved:
                    {
                        return Lang.Get("Sold and retrieved.");
                    }
            }

            return "unknown";
        }

        public string prettyHours(double rlHours, ICoreAPI api)
        {
            string durationText = Lang.Get("{0:0} hrs left", rlHours);
            if (rlHours / api.World.Calendar.HoursPerDay > 1.5f)
            {
                durationText = Lang.Get("{0:0.#} days left", rlHours / api.World.Calendar.HoursPerDay);
            }
            if (rlHours < 1)
            {
                durationText = Lang.Get("{0:0} min left", rlHours * 60);
            }

            return durationText;
        }

        /*Value 	Meaning
        Less than zero 	This instance precedes obj in the sort order.
        Zero 	This instance occurs in the same position in the sort order as obj.
        Greater than zero 	This instance follows obj in the sort order. 
        */
        public int CompareTo(Auction other)
        {
            if (State == EnumAuctionState.Active && other.State == EnumAuctionState.Active)
            {
                return (int)(1000*(ExpireTotalHours - other.ExpireTotalHours));
            }
            if (State == other.State)
            {
                return (int)(1000 * (RetrievableTotalHours - other.RetrievableTotalHours));
            }

            return (int)State - (int)other.State;
        }
    }

    [ProtoContract]
    public class AuctionsData
    {
        [ProtoMember(1)]
        public OrderedDictionary<long, Auction> auctions = new OrderedDictionary<long, Auction>();
        [ProtoMember(2)]
        public long nextAuctionId;
        [ProtoMember(3)]
        public Dictionary<string, float> DebtToTraderByPlayer = new Dictionary<string, float>();
    }



    public enum EnumAuctionAction
    {
        EnterAuctionHouse = 0,
        LeaveAuctionHouse = 1,
        PurchaseAuction = 2,
        RetrieveAuction = 3,
        PlaceAuction = 4
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AuctionActionPacket
    {
        public EnumAuctionAction Action;
        public long AuctionId;
        public long AtAuctioneerEntityId;
        public int DurationWeeks;
        public int Price;
        public bool WithDelivery;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AuctionActionResponsePacket
    {
        public string ErrorCode;
        public EnumAuctionAction Action;
        public long AuctionId;
        public long AtAuctioneerEntityId;
        public bool MoneyReceived;

        public int Price;
    }


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AuctionlistPacket
    {
        public bool IsFullUpdate;
        public Auction[] NewAuctions;
        public long[] RemovedAuctions;
        public float TraderDebt;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class DebtPacket
    {
        public float TraderDebt;
    }
}
