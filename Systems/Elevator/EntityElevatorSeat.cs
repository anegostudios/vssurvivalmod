using System.Collections.Generic;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent;

public class EntityElevatorSeat : EntityRideableSeat
{
    public override EnumMountAngleMode AngleMode => EnumMountAngleMode.Unaffected;
    Dictionary<string, string> animations => (Entity as EntityElevator).MountAnimations;
    public string actionAnim;

    public override AnimationMetaData SuggestedAnimation
    {
        get
        {
            if (actionAnim == null) return null;

            if (Passenger?.Properties?.Client.AnimationsByMetaCode?.TryGetValue(actionAnim, out var ameta) == true)
            {
                return ameta;
            }

            return null;
        }
    }

    public EntityElevatorSeat(IMountable mountablesupplier, string seatId, SeatConfig config) : base(mountablesupplier, seatId, config)
    {
        RideableClassName = "elevator";
    }

    public override bool CanUnmount(EntityAgent entityAgent)
    {
        if (Entity is EntityElevator elevator && elevator.IsMoving)
        {
            return false;
        }
        return true;
    }

    public override bool CanMount(EntityAgent entityAgent)
    {
        if (Entity is EntityElevator elevator && elevator.IsMoving)
        {
            return false;
        }
        return base.CanMount(entityAgent);
    }

    public override void DidMount(EntityAgent entityAgent)
    {
        base.DidMount(entityAgent);

        entityAgent.AnimManager.StartAnimation(animations["idle"]);
    }

    public override void DidUnmount(EntityAgent entityAgent)
    {
        if (Passenger != null)
        {
            Passenger.AnimManager?.StopAnimation(animations["idle"]);
        }

        base.DidUnmount(entityAgent);
    }

    protected override void tryTeleportToFreeLocation()
    {
        Passenger?.TeleportTo(Passenger.ServerPos.Add(0, 0.2, 0));
    }
}
