using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public delegate bool ItemStackMatcherDelegate(ItemStack stack);

    public delegate bool BlockMatcherDelegate(BlockPos pos, Block placedblock, ItemStack withStackInHands);
    public delegate bool BlockLookatMatcherDelegate(BlockSelection blockSel);

    public abstract class TutorialStepGeneric : TutorialStepBase
    {
        protected ICoreClientAPI capi;

        protected TutorialStepGeneric(ICoreClientAPI capi, string text)
        {
            this.capi = capi;
            this.text = text;
        }

        abstract protected PlayerMilestoneWatcherGeneric watcher { get; }
        public override bool Complete => watcher.MilestoneReached();

        public override RichTextComponentBase[] GetText(CairoFont font)
        {
            string vtmlCode = Lang.Get(text, watcher.QuantityAchieved >= watcher.QuantityGoal ? watcher.QuantityGoal : watcher.QuantityGoal - watcher.QuantityAchieved);

            return VtmlUtil.Richtextify(capi, vtmlCode, font);
        }

        public override void Restart()
        {
            watcher.Restart();
        }

        public override void Skip()
        {
            watcher.Skip();
        }

        public override void FromJson(JsonObject job)
        {
            watcher.FromJson(job[code]);
        }

        public override void ToJson(JsonObject job)
        {
            var childJobj = new JObject();
            watcher.ToJson(new JsonObject(childJobj));

            if (childJobj.Count > 0)
            {
                job.Token[code] = childJobj;
            }
        }
    }

    public class TutorialStepPressHotkeys : TutorialStepBase
    {
        List<string> hotkeysToPress = new List<string>();
        HashSet<string> hotkeysPressed = new HashSet<string>();

        
        ICoreClientAPI capi;

        public TutorialStepPressHotkeys(ICoreClientAPI capi, string text, params string[] hotkeys)
        {
            this.capi = capi;
            this.text = text;
            hotkeysToPress.AddRange(hotkeys);
        }

        public override bool Complete => hotkeysPressed.Count == hotkeysToPress.Count;

        public override RichTextComponentBase[] GetText(CairoFont font)
        {
            var hks = capi.Input.HotKeys;

            List<string> hotkeyvtml = new List<string>();
            foreach (var hkcode in hotkeysToPress)
            {
                if (hotkeysPressed.Contains(hkcode))
                {
                    hotkeyvtml.Add("<font color=\"#99ff99\">" + "<hk>" + hkcode + "</hk></font>");
                } else
                {
                    hotkeyvtml.Add("<hk>" + hkcode + "</hk>");
                }
            }

            string vtmlCode = Lang.Get("tutorialstep-numbered", index+1, Lang.Get(text, hotkeyvtml.ToArray()));

            return VtmlUtil.Richtextify(capi, vtmlCode, font);
        }

        public override void Restart()
        {
            hotkeysPressed.Clear();
            deferredActionTrigger = EnumEntityAction.None;
            deferredActionPreReq = EnumEntityAction.None;
        }

        public override void Skip()
        {
            foreach (var hk in hotkeysToPress)
            {
                hotkeysPressed.Add(hk);
            }
        }

        public override bool OnHotkeyPressed(string hotkeycode, KeyCombination keyComb)
        {
            if (hotkeysToPress.Contains(hotkeycode) && !hotkeysPressed.Contains(hotkeycode))
            {
                hotkeysPressed.Add(hotkeycode);
                return true;
            }

            return false;
        }


        HashSet<EnumEntityAction> activeActions = new HashSet<EnumEntityAction>();
        EnumEntityAction deferredActionTrigger = EnumEntityAction.None;
        EnumEntityAction deferredActionPreReq = EnumEntityAction.None;

        public override bool OnAction(EnumEntityAction action, bool on)
        {
            if (on) activeActions.Add(action);
            else activeActions.Remove(action);

            EnumEntityAction preCondition = action == EnumEntityAction.Sprint ? EnumEntityAction.Forward : EnumEntityAction.None;

            if (on && actionToHotkeyMapping.TryGetValue(action, out var keycode))
            {
                if (preCondition != EnumEntityAction.None && !activeActions.Contains(preCondition))
                {
                    deferredActionTrigger = preCondition;
                    deferredActionPreReq = action;
                    return false;
                }

                if (action == deferredActionTrigger && activeActions.Contains(deferredActionPreReq) && actionToHotkeyMapping.TryGetValue(deferredActionPreReq, out var keycode2))
                {
                    deferredActionTrigger = EnumEntityAction.None;
                    deferredActionPreReq = EnumEntityAction.None;
                    bool preReqResult = OnHotkeyPressed(keycode2, null);
                    return OnHotkeyPressed(keycode, null) || preReqResult;
                }

                return OnHotkeyPressed(keycode, null);
            }

            if (action == deferredActionPreReq && !on)
            {
                deferredActionTrigger = EnumEntityAction.None;
                deferredActionPreReq = EnumEntityAction.None;
            }

            return false;
        }

        public override void ToJson(JsonObject job)
        {
            if (hotkeysPressed.Count > 0)
            {
                var childJobj = job.Token[code] = new JObject();
                new JsonObject(childJobj).Token["pressed"] = new JArray(hotkeysPressed.ToArray());
            }
        }

        public override void FromJson(JsonObject job)
        {
            var arr = job[code]["pressed"].Token as JArray;
            if (arr == null) return;

            hotkeysPressed.Clear();
            foreach (string val in arr)
            {
                hotkeysPressed.Add(val);
            }
        }

        public static Dictionary<EnumEntityAction, string> actionToHotkeyMapping = new Dictionary<EnumEntityAction, string>()
        {
            { EnumEntityAction.Forward, "walkforward" },
            { EnumEntityAction.Backward, "walkbackward" },
            { EnumEntityAction.Left, "walkleft" },
            { EnumEntityAction.Right, "walkright" },
            { EnumEntityAction.Sneak, "sneak" },
            { EnumEntityAction.Sprint, "sprint" },
            { EnumEntityAction.Jump, "jump" },
            { EnumEntityAction.FloorSit, "sitdown" },
            { EnumEntityAction.CtrlKey, "ctrl" },
            { EnumEntityAction.ShiftKey, "shift" }
        };
    }

    public abstract class TutorialStepBase
    {
        protected string code;
        public string text;
        public int index;

        public abstract RichTextComponentBase[] GetText(CairoFont font);

        public virtual bool OnItemStackReceived(ItemStack stack, string eventName) { return false; }

        public virtual bool OnBlockPlaced(BlockPos pos, Block block, ItemStack withStackInHands) { return false; }

        public virtual bool OnBlockLookedAt(BlockSelection currentBlockSelection)
        {
            return false;
        }

        public virtual bool OnHotkeyPressed(string hotkeycode, KeyCombination keyComb)
        {
            return false;
        }


        public virtual bool OnAction(EnumEntityAction action, bool on)
        {
            return false;
        }

        public virtual void FromJson(JsonObject job)
        {

        }

        public virtual void ToJson(JsonObject job)
        {

        }

        public static TutorialStepReceive Grab(ICoreClientAPI capi, string code, string text, ItemStackMatcherDelegate matcher, int goal)
        {
            return new TutorialStepReceive(capi, text, matcher, EnumReceiveType.Grab, goal) { code = code };
        }
        public static TutorialStepReceive Collect(ICoreClientAPI capi, string code, string text, ItemStackMatcherDelegate matcher, int goal)
        {
            return new TutorialStepReceive(capi, text, matcher, EnumReceiveType.Collect, goal) { code = code };
        }
        public static TutorialStepReceive Craft(ICoreClientAPI capi, string code, string text, ItemStackMatcherDelegate matcher, int goal)
        {
            return new TutorialStepReceive(capi, text, matcher, EnumReceiveType.Craft, goal) { code = code };
        }
        public static TutorialStepReceive Knap(ICoreClientAPI capi, string code, string text, ItemStackMatcherDelegate matcher, int goal)
        {
            return new TutorialStepReceive(capi, text, matcher, EnumReceiveType.Knap, goal) { code = code };
        }
        public static TutorialStepReceive Clayform(ICoreClientAPI capi, string code, string text, ItemStackMatcherDelegate matcher, int goal)
        {
            return new TutorialStepReceive(capi, text, matcher, EnumReceiveType.Clayform, goal) { code = code };
        }

        public static TutorialStepPlaceBlock Place(ICoreClientAPI capi, string code, string text, BlockMatcherDelegate matcher, int goal)
        {
            return new TutorialStepPlaceBlock(capi, text, matcher, goal) { code = code };
        }

        public static TutorialStepLookatBlock LookAt(ICoreClientAPI capi, string code, string text, BlockLookatMatcherDelegate matcher)
        {
            return new TutorialStepLookatBlock(capi, text, matcher, 1) { code = code };
        }

        public static TutorialStepPressHotkeys Press(ICoreClientAPI capi, string code, string text, params string[] hotkeycodes)
        {
            return new TutorialStepPressHotkeys(capi, text, hotkeycodes) { code = code };
        }

        public abstract bool Complete { get; }

        public abstract void Skip();
        public abstract void Restart();
        
    }
}