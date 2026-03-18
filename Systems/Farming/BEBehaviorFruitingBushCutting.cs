using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BEBehaviorFruitingBushCutting : BlockEntityBehavior
{
    public double matureTotalDays;
    protected string? traits;

    public BEBehaviorFruitingBushCutting(BlockEntity blockentity) : base(blockentity) { }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        Blockentity.RegisterGameTickListener(OnMatureTick, 10000, api.World.Rand.Next(5000));
    }

    private void OnMatureTick(float dt)
    {
        if (Api.World.Calendar.TotalDays >= matureTotalDays)
        {
            var block = Api.World.GetBlock(AssetLocation.Create(Block.Attributes["maturedBlockCode"].AsString(), Block.Code.Domain));
            if (block == null) return;

            Api.World.BlockAccessor.SetBlock(block.BlockId, Pos);
            Api.World.BlockAccessor.GetBlockEntity(Pos).GetBehavior<BEBehaviorFruitingBush>()?.OnGrownFromCutting(traits);
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine(Lang.Get("Matures within {0} months", (int)Math.Ceiling(matureTotalDays - Api.World.Calendar.TotalDays) / Api.World.Calendar.DaysPerMonth));
        base.GetBlockInfo(forPlayer, dsc);
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        var min = Block.Attributes["matureTotalMonthsMin"].AsDouble(2);
        var max = Block.Attributes["matureTotalMonthsMin"].AsDouble(4);
        matureTotalDays = Api.World.Calendar.TotalDays + (min + Api.World.Rand.NextDouble() * (max - min)) * Api.World.Calendar.DaysPerMonth;
        traits = byItemStack?.Attributes.GetString("traits");
        base.OnBlockPlaced(byItemStack);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        traits = tree.GetString("traits");
        matureTotalDays = tree.GetDouble("matureTotalDays");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("matureTotalDays", matureTotalDays);
        tree.SetString("traits", traits);
    }
}
