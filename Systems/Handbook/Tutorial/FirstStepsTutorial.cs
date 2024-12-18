using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class FirstStepsTutorial : TutorialBase
    {
        public FirstStepsTutorial(ICoreClientAPI capi) : base(capi, "tutorial-firststeps")
        {
        }

        protected override void initTutorialSteps()
        {
            // 1. Find 2 knappable stone or flint
            // 2. Collect 5 sticks
            // 3. Craft a knife
            // 4. Harvest 20 cattails or papyrus with knife
            // 5. Craft a basket
            // 5. Collect 5 berries, cattail roots or any other edibles
            // 6. Craft an axe
            // 7. Chop down a tree and collect 4 logs
            // 8. Collect 3 grass
            // 9. Craft 10 fire wood
            // 10. Craft a fire starter
            // 11. Create a fire pit
            // 12. Create a torch

            //   Not yet implemented...
            // 13. Craft 30 cob
            // 14. Place 30 cob
            // 15. Craft a hay bed
            // 16. Sleep
            // 17. Find 40 copper nuggets
            // 18. Craft a shovel
            // 19. Collect 64 blue clay
            // 20. Craft a crucible, 2 bowls and 1 pot
            // 21. Craft a hammer and pickaxe tool mold
            // 22. Create and fire a 2 pit kilns
            // 23. Smelt 40 copper nuggest
            // 24. Cast a hammer and pickaxe

            this.steps.Clear();
            addSteps(
                TutorialStepBase.Press(capi, "wasdkeys", "tutorial-firststeps-1", "walkforward", "walkbackward", "walkleft", "walkright", "jump", "sneak", "sprint"),
                TutorialStepBase.Press(capi, "clicklink", "tutorial-firststeps-2", "togglemousecontrol"),
                TutorialStepBase.Press(capi, "keymods", "tutorial-firststeps-3", "inventorydialog", "characterdialog"),
                TutorialStepBase.Collect(capi, "getknappablestones", "tutorial-firststeps-4", (stack) => stack.ItemAttributes?.IsTrue("knappable") == true, 3),
                TutorialStepBase.Collect(capi, "getsticks", "tutorial-firststeps-5", (stack) => stack.Collectible.Code.Path == "stick", 5),
                TutorialStepBase.Knap(capi, "knapknife", "tutorial-firststeps-6", (stack) => stack.Collectible.Code.Path.Contains("knifeblade"), 1),
                TutorialStepBase.Craft(capi, "craftknife", "tutorial-firststeps-7", (stack) => stack.Collectible.Tool == EnumTool.Knife, 1),
                TutorialStepBase.Collect(capi, "getcattails", "tutorial-firststeps-8", (stack) => stack.Collectible.Code.Path == "papyrustops" || stack.Collectible.Code.Path == "cattailtops", 10),
                TutorialStepBase.Craft(capi, "craftbasket", "tutorial-firststeps-9", (stack) => stack.Collectible.GetCollectibleInterface<IHeldBag>() != null, 1),
                TutorialStepBase.Collect(capi, "getfood", "tutorial-firststeps-10", (stack) => stack.Collectible.NutritionProps != null, 10),
                TutorialStepBase.Craft(capi, "knapaxe", "tutorial-firststeps-11", (stack) => stack.Collectible.Tool == EnumTool.Axe, 1),
                TutorialStepBase.Collect(capi, "getlogs", "tutorial-firststeps-12", (stack) => stack.Collectible is BlockLog, 4),
                TutorialStepBase.Craft(capi, "craftfirewood", "tutorial-firststeps-13", (stack) => stack.Collectible is ItemFirewood, 4),
                TutorialStepBase.Collect(capi, "getdrygrass", "tutorial-firststeps-14", (stack) => stack.Collectible.Code.Path == "drygrass", 3),
                TutorialStepBase.LookAt(capi, "makefirepit", "tutorial-firststeps-15", (blocksel) => blocksel.Block is BlockFirepit),
                TutorialStepBase.LookAt(capi, "finishfirepit", "tutorial-firststeps-16", (blocksel) => (blocksel.Block as BlockFirepit)?.Stage == 5),
                TutorialStepBase.Craft(capi, "createfirestarter", "tutorial-firststeps-17", (stack) => stack.Collectible is ItemFirestarter, 1),
                TutorialStepBase.LookAt(capi,
                    "ignitefirepit", "tutorial-firststeps-18",
                    (blocksel) =>
                    {
                        if (!(blocksel.Block is BlockFirepit)) return false;
                        var befirepit = capi.World.BlockAccessor.GetBlockEntity(blocksel.Position) as BlockEntityFirepit;
                        return befirepit.IsBurning;
                    }
                ),
                TutorialStepBase.Craft(capi, "maketorch", "tutorial-firststeps-19", (stack) => stack.Collectible.Code.Path.Contains("torch-basic-extinct"), 1),
                TutorialStepBase.Grab(capi, "ignitetorch", "tutorial-firststeps-20", (stack) => (stack.Collectible as BlockTorch)?.IsExtinct == false, 1),
                TutorialStepBase.Place(capi, "finished", "tutorial-firststeps-21", (pos, block, stack) => block is BlockTorch bt && !bt.IsExtinct, 1)
            );
        }

    }
}