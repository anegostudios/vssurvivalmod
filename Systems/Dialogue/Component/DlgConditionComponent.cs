
#nullable disable
namespace Vintagestory.GameContent
{
    public class DlgConditionComponent : DialogueComponent
    {
        public string Variable;
        public string IsValue;
        public bool InvertCondition;

        public string ThenJumpTo;
        public string ElseJumpTo;


        public override string Execute()
        {
            setVars();

            if (IsConditionMet(Variable, IsValue, InvertCondition))
            {
                return ThenJumpTo;
            } else
            {
                return ElseJumpTo;
            }
        }
    }

}
