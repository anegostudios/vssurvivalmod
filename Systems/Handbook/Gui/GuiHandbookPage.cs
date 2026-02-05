using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface IHandBookPageCodeProvider
    {
        string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack);
    }

    public struct WeightedHandbookPage
    {
        public int TitleMatches;
        public int TextMatches;
        public int TitleLength;
        public GuiHandbookPage Page;
    }

    public struct PageText
    {
        public string Title;
        public string Text;
    }

    public abstract class GuiHandbookPage : IFlatListItem
    {
        public int PageNumber;
        public abstract string PageCode { get; }

        public abstract string CategoryCode { get; }

        /// <summary>
        /// Render the list item
        /// </summary>
        /// <param name="capi"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public abstract void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWdith, double cellHeight);
        public abstract void Dispose();
        public bool Visible { get; set; } = true;
        public abstract PageText GetPageText();
        public abstract bool IsDuplicate { get; }

        public abstract void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor);

        /// <summary>
        /// For Command Handbook pages, returns the relative position of the searchtext in the whole page text, range 0-1.
        /// <br/>(Currently not implemented in the regular handbook, only in the Command Handbook)
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        public virtual float GetSearchTextPosRel(string searchText)
        {
            return 0;
        }
    }
}
