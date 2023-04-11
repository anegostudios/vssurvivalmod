using System;
using System.Collections.Generic;
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
                TutorialStepBase.Press(capi, "wasdkeys", "Use {0} {1} {2} {3} keys to move around. Use your mouse to look around. Use {4} to jump and {5} to sneak. Hold {6} and {0} to sprint.", "walkforward", "walkbackward", "walkleft", "walkright", "jump", "sneak", "sprint"),
                TutorialStepBase.Press(capi, "clicklink", "Hold {0} to release your mouse cursor. With this you can click on links.<br><a href=\"command://.tutorial skip\">Click here to continue</a>", "togglemousecontrol"),
                TutorialStepBase.Press(capi, "keymods", "Use {0} to open your inventory and {1} to your character inventory. You can used the Esc key to close inventories", "inventorydialog", "characterdialog"),
                TutorialStepBase.Collect(capi,
                    "getknappablestones", "Find {0} <itemstack type=\"item\">flint|stone-chert|stone-granite|stone-peridotite|stone-andesite|stone-basalt|stone-obsidian</itemstack> knappable stones found loosely on the ground. Collect them using <icon>rightmousebutton</icon> or <icon>leftmousebutton</icon>",
                    (stack) => stack.ItemAttributes?.IsTrue("knappable") == true, 3
                ),
                TutorialStepBase.Collect(capi,
                    "getsticks", "Nice! Now find {0} <itemstack type=\"item\">stick</itemstack> sticks, found on the ground in forests or when breaking leaves with branches inside.",
                    (stack) => stack.Collectible.Code.Path == "stick", 5
                ),
                TutorialStepBase.Knap(capi,
                    "knapknife", "You're a natural. Use 2 stones to create a <itemstack type=\"item\">knifeblade-flint|knifeblade-chert|knifeblade-granite|knifeblade-peridotite|knifeblade-andesite|knifeblade-basalt|knifeblade-obsidian</itemstack> knife blade with the <a href=\"handbook://craftinginfo-knapping\">knapping system</a>. <hk>sneak</hk> + <icon>rightmousebutton</icon> with a knappable stone in hands on the ground to begin.",
                    (stack) => stack.Collectible.Code.Path.Contains("knifeblade"), 1
                ),
                TutorialStepBase.Craft(capi,
                    "craftknife", "Open your inventory (with <hk>inventorydialog</hk>), to craft a <itemstack type=\"item\">knife-generic-flint|knife-generic-chert|knife-generic-granite|knife-generic-peridotite|knife-generic-andesite|knife-generic-basalt|knife-generic-obsidian</itemstack> <a href=\"handbooksearch://knife\">knife</a> in your 3x3 crafting grid. Use the handbook (open with <hk>handbook</hk>) to look up the recipe.",
                    (stack) => stack.Collectible.Tool == EnumTool.Knife, 1
                ),
                TutorialStepBase.Collect(capi,
                    "getcattails", "Welcome to the stone age! With your knife in hands, hold <icon>leftmousebutton</icon> to harvest {0} <itemstack type=\"item\">papyrustops|cattailtops</itemstack> from <itemstack type=\"block\">tallplant-coopersreed-land-normal-free|tallplant-papyrus-land-normal-free</itemstack> cattails or papyrus", (stack) => stack.Collectible.Code.Path == "papyrustops" || stack.Collectible.Code.Path == "cattailtops", 10
                ),
                TutorialStepBase.Craft(capi,
                    "craftbasket", "Storage time! Using 10 reeds or papyrus, craft a <itemstack type=\"item\">basket</itemstack> <a href=\"handbook://block-basket\">basket</a> in your inventory (<hk>inventorydialog</hk>). Ultimately you'd want to have 4 of them.",
                    (stack) => stack.Collectible.StorageFlags == EnumItemStorageFlags.Backpack, 1
                ),
                TutorialStepBase.Collect(capi,
                    "getfood", "Don't forget about food! Collect {0} berries, mushrooms, wild crops or other edibles. Harvest berries with <icon>rightmousebutton</icon>",
                    (stack) => stack.Collectible.NutritionProps != null, 10
                ),
                TutorialStepBase.Craft(capi,
                    "knapaxe", "Time to invent fire. Craft an axe. Use 2 knappable stones to make an axe head, then combine it with a stick in yor inventory",
                    (stack) => stack.Collectible.Tool == EnumTool.Axe, 1
                ),
                TutorialStepBase.Collect(capi,
                    "getlogs", "With your axe in hands, chop down a small tree by holding <icon>leftmousebutton</icon>. Collect {0} logs.",
                    (stack) => stack.Collectible is BlockLog, 4
                ),
                TutorialStepBase.Craft(capi,
                    "craftfirewood", "Way to go, you lumberjack! Let's turn that into firewood. Use the logs and axe to craft {0} <itemstack type=\"item\">firewood</itemstack> <a href=\"handbook://item-firewood\">firewood</a> in your inventory.",
                    (stack) => stack.Collectible is ItemFirewood, 4
                ),
                TutorialStepBase.Collect(capi,
                    "getdrygrass", "We also need tinder. With your knife in hands, break tall grass to collect {0} dry grass.",
                    (stack) => stack.Collectible.Code.Path == "drygrass", 3
                ),
                TutorialStepBase.LookAt(capi,
                    "makefirepit", "With the dry grass in your hands, hold <hk>sneak</hk> + <icon>rightmousebutton</icon> while aiming at the ground to place the tinder for your firepit.",
                    (blocksel) => blocksel.Block is BlockFirepit
                ),
                TutorialStepBase.LookAt(capi,
                    "finishfirepit", "Add 4 firewood to your firepit with <icon>rightmousebutton</icon>.",
                    (blocksel) => (blocksel.Block as BlockFirepit)?.Stage == 5
                ),
                TutorialStepBase.Craft(capi,
                    "createfirestarter", "Craft a <itemstack type=\"item\">firestarter</itemstack> <a href=\"handbook://item-firestarter\">fire starter</a>.",
                    (stack) => stack.Collectible is ItemFirestarter, 1
                ),
                TutorialStepBase.LookAt(capi,
                    "ignitefirepit", "With the fire starter in hands, use <icon>rightmousebutton</icon> on the firepit to ignite it. It might take a couple of tries.",
                    (blocksel) =>
                    {
                        if (!(blocksel.Block is BlockFirepit)) return false;
                        var befirepit = capi.World.BlockAccessor.GetBlockEntity(blocksel.Position) as BlockEntityFirepit;
                        return befirepit.IsBurning;
                    }
                ),
                TutorialStepBase.Craft(capi, 
                    "maketorch", "You have discovered fire! \\o/<br>Use it to create torches and to boil meat, cattail roots or to make meals. Craft a <itemstack type=\"block\">torch-basic-extinct-up</itemstack> <a href=\"handbook://block-torch-basic-extinct-up\">torch</a> next.",
                    (stack) => stack.Collectible is BlockTorch,
                    1
                ),
                TutorialStepBase.Grab(capi,
                    "ignitetorch", "Place the torch in the cooking slot of your firepit and give it a couple of seconds. Pick up a <itemstack type=\"block\">torch-basic-lit-up</itemstack> lit torch from your firepit.",
                    (stack) => (stack.Collectible as BlockTorch)?.IsExtinct == false,
                    1
                ),
                TutorialStepBase.Collect(capi,
                    "finished", "Congratulations! Now you know the basics of Vintage Story. This starter tutorial is now finished. More tutorials to follow soon. Until then, check out the <a href=\"handbook://craftinginfo-starterguide\">starter guide</a>",
                    (stack) => false,
                    1
                )
            );
        }

    }
}