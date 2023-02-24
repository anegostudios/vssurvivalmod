using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class LocatorProps
    {
        public string SchematicCode;
        public string WaypointText;
        public string WaypointIcon;
        public double[] WaypointColor;
    }

    public class ItemLocatorMap : Item, ITradeableCollectible
    {
        ModSystemStructureLocator strucLocSys;
        GenStoryStructures storyStructures;
        LocatorProps props;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            strucLocSys = api.ModLoader.GetModSystem<ModSystemStructureLocator>();
            storyStructures = api.ModLoader.GetModSystem<GenStoryStructures>();
            props = Attributes["locatorProps"].AsObject<LocatorProps>();
        }

        public bool OnDidTrade(EntityTrader trader, ItemStack stack, EnumTradeDirection tradeDir)
        {
            var loc = strucLocSys.FindFreshStructureLocation(props.SchematicCode, trader.SidedPos.AsBlockPos, 350);
            stack.Attributes.SetInt("structureIndex", loc.StructureIndex);
            stack.Attributes.SetInt("regionX", loc.RegionX);
            stack.Attributes.SetInt("regionZ", loc.RegionZ);

            strucLocSys.ConsumeStructureLocation(loc);

            return true;
        }

        public EnumTransactionResult OnTryTrade(EntityTrader eTrader, ItemSlot tradeSlot, EnumTradeDirection tradeDir)
        {
            if (tradeSlot is ItemSlotTrade slottrade)
            {
                if (strucLocSys.FindFreshStructureLocation(props.SchematicCode, eTrader.SidedPos.AsBlockPos, 350) == null)
                {
                    slottrade.TradeItem.Stock = 0;
                    return EnumTransactionResult.TraderNotEnoughSupplyOrDemand;
                }
            }

            return EnumTransactionResult.Success;
        }

        public bool ShouldTrade(EntityTrader trader, TradeItem tradeIdem, EnumTradeDirection tradeDir)
        {
            return strucLocSys.FindFreshStructureLocation(props.SchematicCode, trader.SidedPos.AsBlockPos, 350) != null;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            handling = EnumHandHandling.Handled;
            var player = (byEntity as EntityPlayer).Player as IServerPlayer;
            if (player == null)
            {   
                return;
            }

            var wml = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
            if (wml != null)
            {
                var attr = slot.Itemstack.Attributes;
                Vec3d pos = null;
                if (attr.HasAttribute("structureIndex"))
                {
                    pos = getStructureCenter(attr);
                } else
                {
                    foreach (var val in storyStructures.storyStructureInstances)
                    {
                        if (val.Key == props.SchematicCode)
                        {
                            pos = val.Value.CenterPos.ToVec3d().Add(0.5, 0.5, 0.5);
                        }
                    }
                }
                
                if (pos == null)
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("No location found on this map"), EnumChatType.Notification);
                    return;
                }

                wml.AddWaypoint(new Waypoint()
                {
                    Color = ColorUtil.ColorFromRgba((int)(props.WaypointColor[0] * 255), (int)(props.WaypointColor[1] * 255), (int)(props.WaypointColor[2] * 255), (int)(props.WaypointColor[3] * 255)),
                    Icon = props.WaypointIcon,
                    Pinned = true,
                    Position = pos,
                    OwningPlayerUid = (byEntity as EntityPlayer).PlayerUID,
                    Title = Lang.Get(props.WaypointText),
                }, player);

                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("{0} location added to your world map", Lang.Get(props.WaypointText)), EnumChatType.Notification);
            }
        }

        private Vec3d getStructureCenter(API.Datastructures.ITreeAttribute attr)
        {
            var struc = strucLocSys.GetStructure(new StructureLocation()
            {
                StructureIndex = attr.GetInt("structureIndex"),
                RegionX = attr.GetInt("regionX"),
                RegionZ = attr.GetInt("regionZ")
            });

            var c = struc.Location.Center;
            Vec3d pos = new Vec3d(c.X + 0.5, c.Y + 0.5, c.Z + 0.5);
            return pos;
        }
    }
}
