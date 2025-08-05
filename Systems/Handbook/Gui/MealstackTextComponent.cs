using Cairo;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Vintagestory.API.Client
{


    public delegate ItemStack StackDisplayDelegate();

    /// <summary>
    /// Draws a randomized meal itemstack
    /// </summary>
    public class MealstackTextComponent : ItemstackComponentBase
    {
        public bool ShowTooltip = true;

        DummySlot dummySlot;

        protected Action<CookingRecipe>? onMealClicked;

        protected float secondsVisible = 1;
        protected int curItemIndex;

        public bool ShowStackSize { get; set; }
        public bool Background { get; set; } = false;

        public Vec3f renderOffset = new Vec3f();

        public float renderSize = 0.58f;

        double unscaledSize;

        CookingRecipe recipe;

        ItemStack? ingredient;

        ItemStack[] allstacks;

        Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? cachedValidStacks;

        int slots;
        bool isPie;
        public bool RandomBowlBlock { get; set; } = true;

        /// <summary>
        /// Flips through given array of item stacks every second
        /// </summary>
        /// <param name="capi"></param>
        /// <param name="itemstacks"></param>
        /// <param name="unscaledSize"></param>
        /// <param name="floatType"></param>
        /// <param name="onMealClicked"></param>
        public MealstackTextComponent(ICoreClientAPI capi, ref Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? cachedValidStacks, ItemStack mealBlock, CookingRecipe recipe, double unscaledSize, EnumFloat floatType, ItemStack[] allstacks, Action<CookingRecipe>? onMealClicked = null, int slots = 4, bool isPie = false, ItemStack? ingredient = null) : base(capi)
        {
            dummySlot = new DummySlot(mealBlock);
            this.cachedValidStacks = cachedValidStacks;

            if (dummySlot.Itemstack?.Collectible is IBlockMealContainer meal)
            {
                if (isPie) dummySlot.Itemstack.Attributes.SetString("topCrustType", BlockPie.TopCrustTypes[capi.World.Rand.Next(BlockPie.TopCrustTypes.Length)].Code);
                meal.SetContents(recipe.Code!, dummySlot.Itemstack, isPie ? BlockPie.GenerateRandomPie(capi, ref cachedValidStacks, recipe, ingredient) : recipe.GenerateRandomMeal(capi, ref cachedValidStacks, allstacks, slots, ingredient), 1);
            }

            this.ingredient = ingredient;
            this.allstacks = allstacks;
            this.slots = slots;
            this.isPie = isPie;
            this.recipe = recipe;
            this.unscaledSize = unscaledSize;
            this.Float = floatType;
            this.BoundsPerLine = new LineRectangled[] { new LineRectangled(0, 0, GuiElement.scaled(unscaledSize), GuiElement.scaled(unscaledSize)) };
            this.onMealClicked = onMealClicked;
        }

        public override EnumCalcBoundsResult CalcBounds(TextFlowPath[] flowPath, double currentLineHeight, double offsetX, double lineY, out double nextOffsetX)
        {
            TextFlowPath curfp = GetCurrentFlowPathSection(flowPath, lineY);
            offsetX += GuiElement.scaled(PaddingLeft);

            bool requireLinebreak = offsetX + BoundsPerLine[0].Width > curfp.X2;

            this.BoundsPerLine[0].X = requireLinebreak ? 0 : offsetX;
            this.BoundsPerLine[0].Y = lineY + (requireLinebreak ? currentLineHeight : 0);

            BoundsPerLine[0].Width = GuiElement.scaled(unscaledSize) + GuiElement.scaled(PaddingRight);

            nextOffsetX = (requireLinebreak ? 0 : offsetX) + BoundsPerLine[0].Width;

            return requireLinebreak ? EnumCalcBoundsResult.Nextline : EnumCalcBoundsResult.Continue;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            if (Background)
            {
                ctx.SetSourceRGBA(1, 1, 1, 0.2);
                ctx.Rectangle(
                    BoundsPerLine[0].X, 
                    BoundsPerLine[0].Y,  /* - BoundsPerLine[0].Ascent / 2 */ /* why /2??? */ /* why this ascent at all???? wtf? */
                    BoundsPerLine[0].Width, 
                    BoundsPerLine[0].Height
                );
                ctx.Fill();
            }
        }

        public override void RenderInteractiveElements(float deltaTime, double renderX, double renderY, double renderZ)
        {
            int relx = (int)(api.Input.MouseX - renderX + renderOffset.X);
            int rely = (int)(api.Input.MouseY - renderY + renderOffset.Y);
            LineRectangled bounds = BoundsPerLine[0];
            bool mouseover = bounds.PointInside(relx, rely);

            IBlockMealContainer? mealBlock = dummySlot.Itemstack?.Collectible as IBlockMealContainer;
            if (mealBlock == null) return;

            if (!mouseover && (secondsVisible -= deltaTime) <= 0)
            {
                secondsVisible = 1;
                if (RandomBowlBlock)
                {
                    if (isPie) dummySlot.Itemstack?.Attributes.SetString("topCrustType", BlockPie.TopCrustTypes[capi.World.Rand.Next(BlockPie.TopCrustTypes.Length)].Code);
                    else dummySlot.Itemstack = new(BlockMeal.AllMealBowls![capi.World.Rand.Next(BlockMeal.AllMealBowls.Length)]);
                }
                mealBlock.SetContents(recipe.Code!, dummySlot.Itemstack!, isPie ? BlockPie.GenerateRandomPie(capi, ref cachedValidStacks, recipe, ingredient) : recipe.GenerateRandomMeal(capi, ref cachedValidStacks, allstacks, slots, ingredient), 1);
            }

            ElementBounds scibounds = ElementBounds.FixedSize((int)(bounds.Width / RuntimeEnv.GUIScale), (int)(bounds.Height / RuntimeEnv.GUIScale));
            scibounds.ParentBounds = capi.Gui.WindowBounds;
            
            scibounds.CalcWorldBounds();
            scibounds.absFixedX = renderX + bounds.X + renderOffset.X;
            scibounds.absFixedY = renderY + bounds.Y + renderOffset.Y /*- BoundsPerLine[0].Ascent / 2 - why???? */;
            scibounds.absInnerWidth *= renderSize / 0.58f;
            scibounds.absInnerHeight *= renderSize / 0.58f;

            api.Render.PushScissor(scibounds, true);

            api.Render.RenderItemstackToGui(dummySlot, 
                renderX + bounds.X + bounds.Width * 0.5f + renderOffset.X + offX, 
                renderY + bounds.Y + bounds.Height * 0.5f + renderOffset.Y + offY /*- BoundsPerLine[0].Ascent / 2 - why?????*/, 
                100 + renderOffset.Z, (float)GuiElement.scaled(unscaledSize) * renderSize, 
                ColorUtil.WhiteArgb, true, false, ShowStackSize
            );

            api.Render.PopScissor();


            if (mouseover && ShowTooltip)
            {
                RenderItemstackTooltip(dummySlot, renderX + relx, renderY + rely, deltaTime);
            }
        }

        public override void OnMouseDown(MouseEvent args)
        {
            foreach (var val in BoundsPerLine)
            {
                if (val.PointInside(args.X, args.Y))
                {
                    onMealClicked?.Invoke(recipe);
                }
            }
        }


    }
}
