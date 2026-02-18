using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;

public interface ICrop
{
    bool Ripe { get; }
    EnumSoilNutrient RequiredNutrient { get; }
}

public enum EnumFruitingBushHealthState
{
    Barren,
    Struggling,
    Healthy,
    Bountiful
}

public enum EnumFruitingBushGrowthState
{
    Young = 0,
    Mature = 1,
    Flowering = 2,
    Ripening = 3,
    Ripe = 4,
    Dormant = 5
}

public class FruitingBushState
{
    /// <summary>
    /// When the bush was planted
    /// </summary>
    public double PlantedTotalDays;
    /// <summary>
    /// When it matured or when it will mature
    /// </summary>
    public double MatureTotalDays;
    /// <summary>
    /// What growth state the bush is in
    /// </summary>
    EnumFruitingBushGrowthState growthstate;
    public EnumFruitingBushGrowthState Growthstate
    {
        get { return growthstate; }
        set
        {
            if (value != growthstate) { MeshDirty = true; }
            growthstate = value;
        }
    }
    /// <summary>
    /// For wild bushes: What (always static) health state it is in
    /// </summary>
    EnumFruitingBushHealthState? wildBushState;
    public EnumFruitingBushHealthState? WildBushState {
        get { return wildBushState; }
        set
        {
            if (value != wildBushState) { MeshDirty = true; }
            wildBushState = value;
        }
    }

    public bool MeshDirty = false;

    public double LastCuttingTakenTotalDays = -99999;

    public void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        PlantedTotalDays = tree.GetDouble("plantedTotalDays");
        MatureTotalDays = tree.GetDouble("matureTotalDays");
        LastCuttingTakenTotalDays = tree.GetDouble("lastCuttingTakenTotalDays", -99999);
        Growthstate = (EnumFruitingBushGrowthState)tree.GetInt("growthState");


        WildBushState = null;
        if (tree.HasAttribute("wildBushState")) WildBushState = (EnumFruitingBushHealthState)tree.GetInt("wildBushState");
    }

    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetDouble("plantedTotalDays", PlantedTotalDays);
        tree.SetDouble("matureTotalDays", MatureTotalDays);
        tree.SetDouble("lastCuttingTakenTotalDays", LastCuttingTakenTotalDays);
        tree.SetInt("growthState", (int)Growthstate);
        if (WildBushState != null) tree.SetInt("wildBushState", (int)WildBushState);
    }
}
