using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.Client.NoObf;

public class StoryGenFailedSystem : ModSystem
{
    private IClientNetworkChannel clientChannel;
    private ICoreClientAPI capi;
    private GuiDialogStoryGenFailed storyGenGui;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientChannel = api.Network.RegisterChannel("StoryGenFailed");
        clientChannel.RegisterMessageType<StoryGenFailed>();
        clientChannel.SetMessageHandler<StoryGenFailed>(OnReceived);
        storyGenGui = new GuiDialogStoryGenFailed(capi);
        capi.Gui.RegisterDialog(storyGenGui);
    }

    private void OnReceived(StoryGenFailed packet)
    {
        storyGenGui.storyGenFailed = packet;
        if (storyGenGui.isInitilized)
        {
            storyGenGui.TryOpen();
        }
    }
}

public class GuiDialogStoryGenFailed : GuiDialog
{
    public StoryGenFailed storyGenFailed;
    public bool isInitilized;

    public GuiDialogStoryGenFailed(ICoreClientAPI capi) : base(capi)
    {

    }

    public override string ToggleKeyCombinationCode => null;

    private void Compose()
    {
        var font = CairoFont.WhiteSmallText();
        var bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);

        var textBounds = ElementBounds.Fixed(0, 0, 600, 500);

        var titleBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, 0, 690, 30);
        var insetBounds = textBounds.ForkBoundingParent(5, 5, 5, 5).FixedUnder(titleBounds);
        var clippingBounds = textBounds.CopyOffsetedSibling();
        var scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds);

        var text = Lang.Get("storygenfailed-text");
        var message = storyGenFailed?.MissingStructures != null ? string.Join(",", storyGenFailed.MissingStructures) : "";
        text += $"\n{message}<br><br>";

        SingleComposer = capi.Gui
            .CreateCompo("storygenfailed", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("Automatic Story Location Generation Failed"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddInset(insetBounds)
                .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                .BeginClip(clippingBounds)
                    .AddRichtext(text, font, textBounds, null, "storygenfailed")
                .EndClip()
            .EndChildElements()
            .Compose()
            ;
        clippingBounds.CalcWorldBounds();

        SingleComposer.GetScrollbar("scrollbar").SetHeights(
            (float)(clippingBounds.fixedHeight),
            (float)(textBounds.fixedHeight)
        );
    }
    private void OnNewScrollbarvalue(float value)
    {
        ElementBounds bounds = SingleComposer.GetRichtext("storygenfailed").Bounds;
        bounds.fixedY = 10 - value;

        bounds.CalcWorldBounds();
    }

    private bool OnOk()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    public override void OnGuiOpened()
    {
        Compose();
        base.OnGuiOpened();
    }

    public override void OnLevelFinalize()
    {
        isInitilized = true;
        if (storyGenFailed != null)
        {
            Compose();
            TryOpen();
        }
    }
}
