using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockGasifier : Block , IIgnitable
{
    EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
    {
        var beb = api.World.BlockAccessor.GetBlockEntity(pos).GetBehavior<BEBehaviorJonasGasifier>();
        if (beb.Lit)
        {
            return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }
        
        return EnumIgniteState.NotIgnitable;
    }


    public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
    {
        var beb = api.World.BlockAccessor.GetBlockEntity(pos).GetBehavior<BEBehaviorJonasGasifier>();
        if (beb.HasFuel && !beb.Lit)
        {
            return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }
            
        return EnumIgniteState.NotIgnitablePreventDefault;
    }

    public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
    {
        var beb = api.World.BlockAccessor.GetBlockEntity(pos).GetBehavior<BEBehaviorJonasGasifier>();
        handling = EnumHandling.PreventDefault;
        beb.Lit = true;
        beb.burnStartTotalHours = api.World.Calendar.TotalHours;
        beb.UpdateState();
    }
}