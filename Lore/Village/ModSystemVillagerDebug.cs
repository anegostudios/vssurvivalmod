using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.Essentials;
using Vintagestory.API.Common.Entities;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.WorldEdit;
using System.Text;
using System;
using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.GameContent
{
    public delegate TextCommandResult DressedEntityEachDelegate(EntityDressedHumanoid entity, Dictionary<string, WeightedCode[]> pro);

    public class ModSystemVillagerDebug : ModSystem
    {
        ICoreServerAPI sapi;
        ICoreClientAPI capi;
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("dev")

                .BeginSub("talk")
                    .WithArgs(parsers.Entities("entity"), parsers.OptionalWord("talk type"))
                    .HandleWith((args) => CmdUtil.EntityEach(args, (e) =>
                    {
                        if (e == args.Caller.Entity) return TextCommandResult.Success("Ignoring removal of caller");
                        var ebh = e.GetBehavior<EntityBehaviorConversable>();
                        if (ebh != null)
                        {
                            if (args.Parsers[1].IsMissing)
                            {
                                StringBuilder sbt = new StringBuilder();
                                foreach (var talktype in Enum.GetValues(typeof(EnumTalkType)))
                                {
                                    if (sbt.Length > 0) sbt.Append(", ");
                                    sbt.Append(talktype);
                                }

                                return TextCommandResult.Success(sbt.ToString());
                            }

                            if (Enum.TryParse<EnumTalkType>(args[1] as string, true, out var tt))
                            {
                                ebh.TalkUtil.Talk(tt);
                            }
                        }
                        return TextCommandResult.Success("Ok, executed");
                    }))
                .EndSub()
                .BeginSub("pro")
                    .WithArgs(parsers.Entities("entity"))
                    .BeginSub("reload")
                        .HandleWith(proReload)
                    .EndSub()
                .EndSub();
            ;
            base.StartClientSide(api);
        }

        private TextCommandResult proReload(TextCommandCallingArgs args)
        {
            capi.Assets.Reload(AssetCategory.config);
            capi.ModLoader.GetModSystem<HumanoidOutfits>().Reload();
            return OnEach(args, (edh, pro) =>
            {
                edh.MarkShapeModified();
                return TextCommandResult.Success("Ok reloaded.");
            });
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi= api;
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .GetOrCreate("dev")
                .BeginSub("astardebug")
                    .WithArgs(api.ChatCommands.Parsers.Entities("entity"))
                    .HandleWith((args) => CmdUtil.EntityEach(args, (e) =>
                    {
                        if (e == args.Caller.Entity) return TextCommandResult.Success("Ignoring removal of caller");
                        var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
                        if (ebh != null)
                        {
                            var on = !(ebh.ActivitySystem.wppathTraverser as WaypointsTraverser).PathFindDebug;
                            (ebh.ActivitySystem.wppathTraverser as WaypointsTraverser).PathFindDebug = on;
                            return TextCommandResult.Success("Astar debug now " + (on ? "on" : "off"));
                        }
                        return TextCommandResult.Success("Entity is lacking EntityBehaviorActivityDriven");
                    }))
                .EndSub()
                .BeginSub("pro")
                    .WithArgs(parsers.Entities("entity"))

                    .BeginSub("freeze")
                        .WithArgs(parsers.OptionalBool("on"))
                        .HandleWith(cmdFreeze)
                    .EndSub()
                    .BeginSub("unfreeze")
                        .WithArgs(parsers.OptionalBool("on"))
                        .HandleWith(cmdUnFreeze)
                    .EndSub()

                    .BeginSub("set")
                        .WithArgs(parsers.Word("slot"), parsers.All("codes"))
                        .HandleWith(proSet)
                    .EndSub()

                    .BeginSub("naked")
                        .HandleWith(proNaked)
                    .EndSub()

                    .BeginSub("clear")
                        .WithArgs(parsers.Word("slot"))
                        .HandleWith(proClear)
                    .EndSub()

                    .BeginSub("add")
                        .WithArgs(parsers.Word("slot"), parsers.Word("code"))
                        .HandleWith(proAdd)
                    .EndSub()

                    .BeginSub("remove")
                        .HandleWith(proRemove)
                    .EndSub()

                    .BeginSub("export")
                        .HandleWith(proExport)
                    .EndSub()

                    .BeginSub("test")
                        .HandleWith(proTest)
                    .EndSub()
                .EndSub()

                .BeginSub("dress")
                    .WithArgs(api.ChatCommands.Parsers.Entities("entity"))
                    .HandleWith((args) => CmdUtil.EntityEach(args, (e) =>
                    {
                        if (e == args.Caller.Entity) return TextCommandResult.Success("Ignoring caller");
                        if (e is EntityDressedHumanoid edh)
                        {
                            StringBuilder sb = new StringBuilder();
                            for (int i = 0; i < edh.OutfitCodes.Length; i++)
                            {
                                sb.AppendLine(edh.OutfitSlots[i] + ": " + edh.OutfitCodes[i]);
                            }

                            return TextCommandResult.Success("Current outfit:\n" + sb.ToString());
                        }
                        return TextCommandResult.Success("Ok, entity removed");
                    }))
                .EndSub()

              
            ;
        }


        private TextCommandResult cmdUnFreeze(TextCommandCallingArgs args)
        {
            return this.freeze(args, false);
        }

        private TextCommandResult cmdFreeze(TextCommandCallingArgs args)
        {
            bool freeze = args.Parsers[1].IsMissing ? true : (bool)args[1];
            return this.freeze(args, freeze);
        }

        private TextCommandResult freeze(TextCommandCallingArgs args, bool freeze)
        {
            return CmdUtil.EntityEach(args, (e) =>
            {
                if (e == args.Caller.Entity) return TextCommandResult.Success("Ignoring caller");

                EntityBehaviorTaskAI taskAi = e.GetBehavior<EntityBehaviorTaskAI>();
                var ebh = e.GetBehavior<EntityBehaviorActivityDriven>();
                if (ebh != null) ebh.ActivitySystem.PauseAutoSelection(freeze);

                if (freeze)
                {
                    if (taskAi != null) taskAi.TaskManager.OnShouldExecuteTask += TaskManager_OnShouldExecuteTask;
                }
                else
                {
                    if (taskAi != null) taskAi.TaskManager.OnShouldExecuteTask -= TaskManager_OnShouldExecuteTask;
                }
                return TextCommandResult.Success("Ok. Freeze set");
            });
        }

        private bool TaskManager_OnShouldExecuteTask(IAiTask t)
        {
            return false;
        }

        private TextCommandResult proSet(TextCommandCallingArgs args)
        {
            string slot = (string)args[1];
            string values = args.Parsers[2].GetValue() as string;
            var wcodes = values.Split(" ").Select(v => new WeightedCode() { Code = v, Weight = 1 }).ToArray();

            return OnEach(args, (edh, pro) =>
            {
                pro[slot] = wcodes;
                edh.LoadOutfitCodes();

                return TextCommandResult.Success("ok, slot " + slot + " set to " + values);
            });            
        }

        private TextCommandResult proNaked(TextCommandCallingArgs args)
        {
            return OnEach(args, (edh, pro) =>
            {
                var cfg = sapi.ModLoader.GetModSystem<HumanoidOutfits>().GetConfig(edh.OutfitConfigFileName);
                foreach (var val in cfg.BySlot)
                {
                    pro[val.Code] = Array.Empty<WeightedCode>();
                }

                edh.LoadOutfitCodes();

                return TextCommandResult.Success("ok, all slots cleared");
            });
        }

        private TextCommandResult proClear(TextCommandCallingArgs args)
        {
            string slot = (string)args[1];

            return OnEach(args, (edh, pro) =>
            {
                pro[slot] = Array.Empty<WeightedCode>();
                edh.LoadOutfitCodes();

                return TextCommandResult.Success("ok, slot " + slot + " cleared");
            });
        }


        private TextCommandResult proAdd(TextCommandCallingArgs args)
        {
            string slot = (string)args[1];
            string value = (string)args[2];

            return OnEach(args, (edh, pro) =>
            {
                if (pro[slot].FirstOrDefault(wc => wc.Code == value) != null)
                {
                    return TextCommandResult.Error("Value " + value + " already exists in slot " + slot);
                }
                pro[slot] = pro[slot].Append(new WeightedCode() { Code = value });

                edh.LoadOutfitCodes();

                return TextCommandResult.Success("ok, " + value + " added to slot " + slot);
            });
        }

        private TextCommandResult proRemove(TextCommandCallingArgs args)
        {
            string slot = (string)args[1];
            string value = (string)args[2];

            return OnEach(args, (edh, pro) =>
            {
                var filtered = pro[slot].Where(v => v.Code != value).ToArray();

                if (pro[slot].Length != filtered.Length) {
                    edh.LoadOutfitCodes();

                    return TextCommandResult.Success("ok, " + value + " removed from slot " + slot);
                } else
                {
                    return TextCommandResult.Error("Value " + value + " not present in " + slot);
                }
                
            });
        }

        private TextCommandResult proExport(TextCommandCallingArgs args)
        {
            StringBuilder sb = new StringBuilder();

            var result = OnEach(args, (edh, pro) =>
            {
                sb.AppendLine(JsonUtil.ToString(edh.partialRandomOutfitsOverride));
                return TextCommandResult.Success();
            });

            var serverChannel = sapi.Network.GetChannel("worldedit");
            serverChannel.SendPacket(new CopyToClipboardPacket() { Text = sb.ToString() }, args.Caller.Player as IServerPlayer);
            return result;
        }

        private TextCommandResult proTest(TextCommandCallingArgs args)
        {
            return OnEach(args, (edh, pro) =>
            {
                edh.LoadOutfitCodes();
                return TextCommandResult.Success("ok, reloaded");
            });
        }



        private TextCommandResult OnEach(TextCommandCallingArgs args, DressedEntityEachDelegate dele)
        {
            return CmdUtil.EntityEach(args, (e) =>
            {
                if (e == args.Caller.Entity) return TextCommandResult.Success("Ignoring caller");
                if (e is EntityDressedHumanoid edh)
                {
                    return dele(edh, getOrCreatePro(e));

                }
                return TextCommandResult.Success("Ok, entity removed");
            });
        }

        private Dictionary<string, WeightedCode[]> getOrCreatePro(Entity entity)
        {
            var edh = entity as EntityDressedHumanoid;
            if (edh.partialRandomOutfitsOverride == null)
            {
                edh.partialRandomOutfitsOverride = entity.Properties.Attributes["partialRandomOutfits"].AsObject<Dictionary<string, WeightedCode[]>>();
                if (edh.partialRandomOutfitsOverride == null) {
                    edh.partialRandomOutfitsOverride = new Dictionary<string, WeightedCode[]>();
                }
            }

            return edh.partialRandomOutfitsOverride;
        }
    }

}
