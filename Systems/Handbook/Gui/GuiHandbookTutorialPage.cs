using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class TutorialProgress
    {
        // 1. Find a knappable stone or flint
        // 2. Collect 5 sticks
        // 3. Craft a knife
        // 4. Harvest cattails or papyrus with knife
        // 5. Collect 5 berries, cattail roots or any other edibles
        // 6. Craft an axe
        // 7. Chop down a tree and collect 4 logs
        // 8. Collect 3 grass
        // 9. Craft 10 fire wood
        // 10. Craft a fire starter
        // 11. Create a fire pit
        // 12. Create a torch
        // 13. Craft 30 cob
        // 14. Place 30 cob
        // 15. Craft a hay bed
        // 16. Sleep
        // 17. Find 40 copper nuggets
        // 18. Craft a shovel
        // 19. Collect 64 blue clay
        // 20. Craft a crucible, 2 bowls and 1 pot
        // 21. Create and fire a pit kiln
        // 22. 

    }

    public class ModSystemTutorial : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            
        }
    }

    internal class GuiHandbookTutorialPage : GuiHandbookPage
    {
        private ICoreClientAPI capi;

        public GuiHandbookTutorialPage(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public override string PageCode => "guide-tutorial";

        public override string CategoryCode => "guide";

        public override bool IsDuplicate => false;

        public override void Dispose()
        {
            
        }

        public override RichTextComponentBase[] GetPageText(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            
        }

        public override void RenderTo(ICoreClientAPI capi, double x, double y)
        {
            
        }

        public override float TextMatchWeight(string text)
        {
            
        }
    }
}