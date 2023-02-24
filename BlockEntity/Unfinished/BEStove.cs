namespace Vintagestory.GameContent
{
    public class BlockEntityStove : BlockEntityFirepit
    {
        public override bool BurnsAllFuell
        {
            get { return false; }
        }

        public override float HeatModifier
        {
            get { return 1.1f; }
        }

        public override float BurnDurationModifier
        {
            get { return 1.2f; }
        }

        public override string DialogTitle
        {
            get { return "Stove"; }
        }
    }
}
