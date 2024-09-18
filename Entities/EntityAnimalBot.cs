using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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
    }

    public class NpcGotoCommand : INpcCommand
    {
        protected EntityAnimalBot entity;

        public Vec3d Target;

        public float AnimSpeed;
        public float GotoSpeed;
        public string AnimCode;
        public bool astar;

        public string Type => "goto";


        public NpcGotoCommand(EntityAnimalBot entity, Vec3d target, bool astar, string animCode = "walk", float gotoSpeed = 0.02f, float animSpeed = 1)
        {
            this.entity = entity;
            this.astar = astar;
            this.Target = target;
            this.AnimSpeed = animSpeed;
            this.AnimCode = animCode;
            this.GotoSpeed = gotoSpeed;
        }


        public void Start()
        {
            if (astar)
            {
                entity.wppathTraverser.WalkTowards(Target, GotoSpeed, /*size + */0.2f, OnDone, OnDone);
            } else
            {
                entity.linepathTraverser.NavigateTo(Target, GotoSpeed, OnDone, OnDone);
            }
            

            if (AnimSpeed != 0.02f)
            {
                entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = AnimCode, Code = AnimCode, AnimationSpeed = AnimSpeed }.Init());
            }
            else
            {

                if (!entity.AnimManager.StartAnimation(AnimCode))
                {
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = AnimCode, Code = AnimCode, AnimationSpeed = AnimSpeed }.Init());
                }

            }
            entity.Controls.Sprint = AnimCode == "run" || AnimCode == "sprint";
        }

        public void Stop()
        {
            entity.linepathTraverser.Stop();
            entity.wppathTraverser.Stop();
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
            return !entity.linepathTraverser.Active;
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
            tree.SetBool("astar", astar);
        }

        public void FromAttribute(ITreeAttribute tree)
        {
            Target = new Vec3d(tree.GetDouble("x"), tree.GetDouble("y"), tree.GetDouble("z"));
            AnimSpeed = tree.GetFloat("animSpeed");
            GotoSpeed = tree.GetFloat("gotoSpeed");
            AnimCode = tree.GetString("animCode");
            astar = tree.GetBool("astar");
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
            if (AnimSpeed != 1 || !entity.AnimManager.StartAnimation(AnimCode))
            {
                entity.AnimManager.StartAnimation(new AnimationMetaData() { 
                    Animation = AnimCode, 
                    Code = AnimCode, 
                    AnimationSpeed = AnimSpeed 
                }.Init());
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

        public override string ToString()
        {
            return "Look at " + yaw;
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

        public PathTraverserBase linepathTraverser;
        public PathTraverserBase wppathTraverser;

        public override bool StoreWithChunk
        {
            get { return true; }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            linepathTraverser = new StraightLineTraverser(this);
            wppathTraverser = new WaypointsTraverser(this);
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

            if (ExecutingCommands.Count > 0)
            {
                var cmd = ExecutingCommands.Peek();
                cmd.Start();
                WatchedAttributes.SetString("currentCommand", cmd.Type);
            }

            commandQueueActive = true;
        }

        public void StopExecuteCommands()
        {
            if (ExecutingCommands.Count > 0) ExecutingCommands.Peek().Stop();
            ExecutingCommands.Clear();
            commandQueueActive = false;
            WatchedAttributes.SetString("currentCommand", "");
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            linepathTraverser.OnGameTick(dt);
            wppathTraverser.OnGameTick(dt);

            if (commandQueueActive)
            {
                if (ExecutingCommands.Count > 0)
                {
                    INpcCommand nowCommand = ExecutingCommands.Peek();
                    if (nowCommand.IsFinished())
                    {
                        WatchedAttributes.SetString("currentCommand", "");

                        ExecutingCommands.Dequeue();
                        if (ExecutingCommands.Count > 0) ExecutingCommands.Peek().Start();
                        else
                        {
                            if (LoopCommands) StartExecuteCommands();
                            else commandQueueActive = false;
                        }
                    }
                }
                else
                {
                    if (LoopCommands) StartExecuteCommands();
                    else commandQueueActive = false;
                }
            }

            World.FrameProfiler.Mark("entityAnimalBot-pathfinder-and-commands");
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

                WatchedAttributes.SetBool("loop", LoopCommands);
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
                            command = new NpcGotoCommand(this, null, false, null, 0);
                            break;
                        case "anim":
                            command = new NpcPlayAnimationCommand(this, null, 1f);
                            break;
                        case "lookat":
                            command = new NpcLookatCommand(this, 0);
                            break;
                    }

                    command.FromAttribute(attr);
                    Commands.Add(command);
                }

                LoopCommands = WatchedAttributes.GetBool("loop");
            }
        }
    }
}
