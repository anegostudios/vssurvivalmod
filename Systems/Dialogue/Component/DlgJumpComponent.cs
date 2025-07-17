
#nullable disable
namespace Vintagestory.GameContent
{
    public class DlgJumpComponent : DialogueComponent
    {
        public string Target;


        public override string Execute()
        {
            setVars();

            return Target;
        }
    }

}
