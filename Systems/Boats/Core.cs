using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class Core : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterMountable("boat", EntityBoatSeat.GetMountable);
            api.RegisterMountable("rideableanimal", EntityRideableSeat.GetMountable);
            api.RegisterItemClass("ItemBoat", typeof(ItemBoat));
            api.RegisterEntity("EntityBoat", typeof(EntityBoat));
            api.RegisterItemClass("ItemRoller", typeof(ItemRoller));
            api.RegisterEntity("EntityBoatConstruction", typeof(EntityBoatConstruction));
        }
    }
}
