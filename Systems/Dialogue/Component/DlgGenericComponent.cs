
#nullable disable
namespace Vintagestory.GameContent
{
    public class DlgGenericComponent : DialogueComponent
    {
        public override string Execute()
        {
            setVars();

            if (Code == "opentrade") return "welcomeback";

            return JumpTo != null ? JumpTo : "next";
        }
    }

}
