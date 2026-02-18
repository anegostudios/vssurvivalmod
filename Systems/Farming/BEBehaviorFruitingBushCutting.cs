using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BEBehaviorFruitingBushCutting : BlockEntityBehavior
{
    public double matureTotalDays;

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
            var blockcode = AssetLocation.Create(Block.Attributes["maturedBlockCode"].AsString(), Block.Code.Domain);
            var block = Api.World.GetBlock(blockcode);
            if (block != null)
            {
                Api.World.BlockAccessor.SetBlock(block.BlockId, Blockentity.Pos);

                StandardWorldProperty fertilities = Api.Assets.TryGet("worldproperties/abstract/fertility.json").ToObject<StandardWorldProperty>();

                var belowBlock = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
                int fi = fertilities.Variants.IndexOf(elem => elem.Code.Path == belowBlock.Variant["fertility"]);
                if (fi > 0)
                {
                    var code = belowBlock.CodeWithVariant("fertility", fertilities.Variants[fi - 1].Code.Path);
                    var lessfertileblock = Api.World.GetBlock(code);
                    if (lessfertileblock != null)
                    {
                        Api.World.BlockAccessor.SetBlock(lessfertileblock.Id, Pos.DownCopy());
                    }
                }
            }
        }
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        var min = Block.Attributes["matureTotalMonthsMin"].AsDouble(6);
        var max = Block.Attributes["matureTotalMonthsMin"].AsDouble(12);
        matureTotalDays = (min + Api.World.Rand.NextDouble() * (max - min)) * Api.World.Calendar.DaysPerMonth;

        base.OnBlockPlaced(byItemStack);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        matureTotalDays = tree.GetDouble("matureTotalDays");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("matureTotalDays", matureTotalDays);
    }
}
