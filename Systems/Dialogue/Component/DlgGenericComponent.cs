
#nullable disable
namespace Vintagestory.GameContent
{
    public class DlgGenericComponent : DialogueComponent
    {
        public override string Execute()
        {
            setVars();

            return JumpTo != null ? JumpTo : "next";
        }
    }

}
