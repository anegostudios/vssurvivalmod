using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;

namespace Vintagestory.GameContent
{
    #region Npc commands
    public interface INpcCommand
    {
        string Type { get; }

        void Start();
        void Stop();
        bool IsFinished();
        void ToAttribute(ITreeAttribute tree);
        void FromAttribute(ITreeAttribute tree);
        void Update(IServerPlayer player, ICoreAPI api, CmdArgs args);
    }

    public class NpcGotoCommand : INpcCommand
    {
        protected EntityAnimalBot entity;

        public Vec3d Target;

        public float AnimSpeed;
        public float GotoSpeed;
        public string AnimCode;


        public string Type => "goto";


        public NpcGotoCommand(EntityAnimalBot entity, Vec3d target, string animCode = "walk", float gotoSpeed = 0.02f, float animSpeed = 1)
        {
            this.entity = entity;
            this.Target = target;
            this.AnimSpeed = animSpeed;
            this.AnimCode = animCode;
            this.GotoSpeed = gotoSpeed;
        }


        public void Start()
        {
            entity.pathTraverser.NavigateTo(Target, 0.02f, OnDone, OnDone);
            if (!entity.AnimManager.StartAnimation(AnimCode))
            {
                entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = AnimCode, Code = AnimCode, AnimationSpeed = AnimSpeed });
            }

            entity.Controls.Sprint = AnimCode == "run" || AnimCode == "sprint";
        }

        public void Stop()
        {
            entity.pathTraverser.Stop();
            entity.AnimManager.StopAnimation(AnimCode);
            entity.Controls.Sprint = false;
        }

        private void OnDone()
        {
            entity.AnimManager.StopAnimation(AnimCode);
            entity.Controls.Sprint = false;
        }

        public bool IsFinished()
        {
            return !entity.pathTraverser.Active;
        }

        public override string ToString()
        {
            return string.Format("{0} to {1} (gotospeed {2}, animspeed {3})", AnimCode, Target, GotoSpeed, AnimSpeed);
        }


        public void ToAttribute(ITreeAttribute tree)
        {
            tree.SetDouble("x", Target.X);
            tree.SetDouble("y", Target.Y);
            tree.SetDouble("z", Target.Z);
            tree.SetFloat("animSpeed", AnimSpeed);
            tree.SetFloat("gotoSpeed", GotoSpeed);
            tree.SetString("animCode", AnimCode);
        }

        public void FromAttribute(ITreeAttribute tree)
        {
            Target = new Vec3d(tree.GetDouble("x"), tree.GetDouble("y"), tree.GetDouble("z"));
            AnimSpeed = tree.GetFloat("animSpeed");
            GotoSpeed = tree.GetFloat("gotoSpeed");
            AnimCode = tree.GetString("animCode");
        }

        public void Update(IServerPlayer player, ICoreAPI api, CmdArgs args)
        {
            string type = args.PopWord();

            switch (type)
            {
                case "gs":
                    GotoSpeed = (float)args.PopFloat(0);
                    player.SendMessage(GlobalConstants.CurrentChatGroup, "Ok goto speed upated to " + GotoSpeed, EnumChatType.Notification);
                    return;

                case "as":
                    AnimSpeed = (float)args.PopFloat(0);
                    player.SendMessage(GlobalConstants.CurrentChatGroup, "Ok goto speed upated to " + AnimSpeed, EnumChatType.Notification);
                    return;
            }

            player.SendMessage(GlobalConstants.CurrentChatGroup, "Excpected gs or as", EnumChatType.Notification);
        }
    }

    public class NpcTeleportCommand : INpcCommand
    {
        protected EntityAnimalBot entity;
        public Vec3d Target;

        public string Type => "tp";

        public NpcTeleportCommand(EntityAnimalBot entity, Vec3d target)
        {
            this.entity = entity;
            this.Target = target;
        }


        public void Start()
        {
            entity.TeleportToDouble(Target.X, Target.Y, Target.Z);
        }

        public void Stop()
        {

        }

        public bool IsFinished()
        {
            return true;
        }

        public override string ToString()
        {
            return "Teleport to " + Target;
        }

        public void ToAttribute(ITreeAttribute tree)
        {
            tree.SetDouble("x", Target.X);
            tree.SetDouble("y", Target.Y);
            tree.SetDouble("z", Target.Z);
        }

        public void FromAttribute(ITreeAttribute tree)
        {
            Target = new Vec3d(tree.GetDouble("x"), tree.GetDouble("y"), tree.GetDouble("z"));
        }

        public void Update(IServerPlayer player, ICoreAPI api, CmdArgs args)
        {
            player.SendMessage(GlobalConstants.CurrentChatGroup, "Not implemented", EnumChatType.Notification);
        }
    }

    public class NpcPlayAnimationCommand : INpcCommand
    {
        public string Type => "anim";

        public string AnimCode;
        public float AnimSpeed;
        EntityAnimalBot entity;

        public NpcPlayAnimationCommand(EntityAnimalBot entity, string animCode, float animSpeed)
        {
            this.entity = entity;
            this.AnimCode = animCode;
            this.AnimSpeed = animSpeed;
        }

        public void Start()
        {
            if (!entity.AnimManager.StartAnimation(AnimCode))
            {
                entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = AnimCode, Code = AnimCode, AnimationSpeed = AnimSpeed });
            }
        }

        public void Stop()
        {
            AnimationMetaData animData;
            entity.Properties.Client.AnimationsByMetaCode.TryGetValue(AnimCode, out animData);
            if (animData?.Code != null)
            {
                entity.AnimManager.StopAnimation(animData.Code);
            }
            else
            {
                entity.AnimManager.StopAnimation(AnimCode);
            }
        }

        public bool IsFinished()
        {
            AnimationMetaData animData;
            entity.Properties.Client.AnimationsByMetaCode.TryGetValue(AnimCode, out animData);

            return !entity.AnimManager.ActiveAnimationsByAnimCode.ContainsKey(AnimCode) && (animData?.Animation == null || !entity.AnimManager.ActiveAnimationsByAnimCode.ContainsKey(animData?.Animation));
        }

        public override string ToString()
        {
            return "Play animation " + AnimCode;
        }

        public void ToAttribute(ITreeAttribute tree)
        {
            tree.SetString("animCode", AnimCode);
            tree.SetFloat("animSpeed", AnimSpeed);
        }

        public void FromAttribute(ITreeAttribute tree)
        {
            AnimCode = tree.GetString("animCode");
            AnimSpeed = tree.GetFloat("animSpeed", 1);
        }


        public void Update(IServerPlayer player, ICoreAPI api, CmdArgs args)
        {
            player.SendMessage(GlobalConstants.CurrentChatGroup, "Not implemented", EnumChatType.Notification);
        }
    }

    public class NpcLookatCommand : INpcCommand
    {
        public string Type => "lookat";

        public float yaw;
        EntityAgent entity;

        public NpcLookatCommand(EntityAgent entity, float yaw)
        {
            this.entity = entity;
            this.yaw = yaw;
        }


        public bool IsFinished()
        {
            return true;
        }

        public void Start()
        {
            entity.ServerPos.Yaw = yaw;
        }

        public void Stop()
        {
            
        }


        public void FromAttribute(ITreeAttribute tree)
        {
            yaw = tree.GetFloat("yaw");
        }

        public void ToAttribute(ITreeAttribute tree)
        {
            tree.SetFloat("yaw", yaw);
        }

        public void Update(IServerPlayer player, ICoreAPI api, CmdArgs args)
        {
            yaw = (float)args.PopFloat(0);
            player.SendMessage(GlobalConstants.CurrentChatGroup, "Yaw " + yaw + " set", EnumChatType.Notification);
        }
    }

    #endregion


    public class EntityAnimalBot : EntityAgent
    {
        public string Name;

        public List<INpcCommand> Commands = new List<INpcCommand>();
        public Queue<INpcCommand> ExecutingCommands = new Queue<INpcCommand>();
        protected bool commandQueueActive;

        public bool LoopCommands;

        public PathTraverserBase pathTraverser;

        public override bool StoreWithChunk
        {
            get { return true; }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            pathTraverser = new StraightLineTraverser(this);
        }

        public void StartExecuteCommands(bool enqueue = true)
        {
            if (enqueue)
            {
                foreach (var val in Commands)
                {
                    ExecutingCommands.Enqueue(val);
                }
            }

            if (ExecutingCommands.Count > 0) ExecutingCommands.Peek().Start();
            commandQueueActive = true;
        }

        public void StopExecuteCommands()
        {
            if (ExecutingCommands.Count > 0) ExecutingCommands.Peek().Stop();
            ExecutingCommands.Clear();
            commandQueueActive = false;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (commandQueueActive)
            {
                if (ExecutingCommands.Count > 0)
                {
                    INpcCommand nowCommand = ExecutingCommands.Peek();
                    if (nowCommand.IsFinished())
                    {
                        ExecutingCommands.Dequeue();
                        if (ExecutingCommands.Count > 0) ExecutingCommands.Peek().Start();
                        else
                        {
                            if (LoopCommands) StartExecuteCommands();
                            commandQueueActive = false;
                        }
                    }
                }
                else
                {
                    if (LoopCommands) StartExecuteCommands();
                    else commandQueueActive = false;
                }
            }

        }

        
        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            if (!forClient)
            {
                ITreeAttribute ctree = new TreeAttribute();
                WatchedAttributes["commandQueue"] = ctree;

                ITreeAttribute commands = new TreeAttribute();
                ctree["commands"] = commands;

                int i = 0;
                foreach (var val in Commands)
                {
                    ITreeAttribute attr = new TreeAttribute();

                    val.ToAttribute(attr);
                    attr.SetString("type", val.Type);

                    commands["cmd" + i] = attr;
                    i++;
                }
            }

            base.ToBytes(writer, forClient);
        }


        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (!forClient)
            {
                ITreeAttribute ctree = WatchedAttributes.GetTreeAttribute("commandQueue");
                if (ctree == null) return;

                ITreeAttribute commands = ctree.GetTreeAttribute("commands");
                if (commands == null) return;

                foreach (var val in commands)
                {
                    ITreeAttribute attr = val.Value as ITreeAttribute;
                    string type = attr.GetString("type");

                    INpcCommand command = null;

                    switch (type)
                    {
                        case "tp":
                            command = new NpcTeleportCommand(this, null);
                            break;
                        case "goto":
                            command = new NpcGotoCommand(this, null, null, 0);
                            break;
                        case "anim":
                            command = new NpcPlayAnimationCommand(this, null, 1f);
                            break;
                    }

                    command.FromAttribute(attr);
                    Commands.Add(command);
                }
            }
        }
    }
}
