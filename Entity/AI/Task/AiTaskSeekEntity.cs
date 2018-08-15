using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskSeekEntity : AiTaskBase
    {
        EntityAgent targetEntity;
        Vec3d targetPos;
        float moveSpeed = 0.02f;
        float seekingRange = 25f;
        float maxFollowTime = 60;
        
        bool stuck = false;
        string[] seekEntityCodesExact = new string[] { "player" };
        string[] seekEntityCodesBeginsWith = new string[0];

        float currentFollowTime = 0;

        bool alarmHerd = false;

        public AiTaskSeekEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["seekingRange"] != null)
            {
                seekingRange = taskConfig["seekingRange"].AsFloat(25);
            }

            if (taskConfig["maxFollowTime"] != null)
            {
                maxFollowTime = taskConfig["maxFollowTime"].AsFloat(60);
            }

            if (taskConfig["alarmHerd"] != null)
            {
                alarmHerd = taskConfig["alarmHerd"].AsBool(false);
            }

            if (taskConfig["entityCodes"] != null)
            {
                string[] codes = taskConfig["entityCodes"].AsStringArray(new string[] { "player" });

                List<string> exact = new List<string>();
                List<string> beginswith = new List<string>();

                for (int i = 0; i < codes.Length; i++)
                {
                    string code = codes[i];
                    if (code.EndsWith("*")) beginswith.Add(code.Substring(0, code.Length - 1));
                    else exact.Add(code);
                }

                seekEntityCodesExact = exact.ToArray();
                seekEntityCodesBeginsWith = beginswith.ToArray();
            }
        }


        public override bool ShouldExecute()
        {
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (rand.NextDouble() > 0.03f) return false;
            if (whenInEmotionState != null && !entity.HasEmotionState(whenInEmotionState)) return false;
            if (whenNotInEmotionState != null && entity.HasEmotionState(whenNotInEmotionState)) return false;

            targetEntity = (EntityAgent)entity.World.GetNearestEntity(entity.ServerPos.XYZ, seekingRange, seekingRange, (e) => {
                if (!e.Alive || !e.IsInteractable || e.Type == null || e.Entityid == this.entity.Entityid) return false;

                for (int i = 0; i < seekEntityCodesExact.Length; i++)
                {
                    if (e.Type.Code.Path == seekEntityCodesExact[i])
                    {
                        if (e.Type.Code.Path == "player")
                        {
                            IPlayer player = entity.World.PlayerByUid(((EntityPlayer)e).PlayerUID);
                            return player == null || (player.WorldData.CurrentGameMode != EnumGameMode.Creative && player.WorldData.CurrentGameMode != EnumGameMode.Spectator);
                        }
                        return true;
                    }
                }


                for (int i = 0; i < seekEntityCodesBeginsWith.Length; i++)
                {
                    if (e.Type.Code.Path.StartsWith(seekEntityCodesBeginsWith[i])) return true;
                }

                return false;
            });

            if (targetEntity != null)
            {
                if (alarmHerd && entity.HerdId > 0)
                {
                    entity.World.GetNearestEntity(entity.ServerPos.XYZ, seekingRange, seekingRange, (e) =>
                    {
                        EntityAgent agent = e as EntityAgent;
                        if (e.Entityid != entity.Entityid && agent != null && agent.Alive && agent.HerdId == entity.HerdId)
                        {
                            agent.Notify("seekEntity", targetEntity);
                        }

                        return false;
                    });
                }

                targetPos = targetEntity.ServerPos.XYZ;

                if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) <= MinDistanceToTarget())
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public float MinDistanceToTarget()
        {
            return System.Math.Max(0.1f, (targetEntity.CollisionBox.X2 - targetEntity.CollisionBox.X1) / 2 + (entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2);
        }

        public override void StartExecute()
        {
            base.StartExecute();
            stuck = false;
            entity.PathTraverser.GoTo(targetPos, moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck);
            currentFollowTime = 0;
        }

        public override bool ContinueExecute(float dt)
        {
            currentFollowTime += dt;

            entity.PathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
            entity.PathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
            entity.PathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;

            Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);
            

            bool inCreativeMode = (targetEntity is EntityPlayer) && entity.World.PlayerByUid(((EntityPlayer)targetEntity).PlayerUID)?.WorldData?.CurrentGameMode == EnumGameMode.Creative;

            float minDist = MinDistanceToTarget();

            return
                currentFollowTime < maxFollowTime &&
                distance < seekingRange * seekingRange &&
                distance > minDist &&
                targetEntity.Alive &&
                !inCreativeMode &&
                !stuck
            ;
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            entity.PathTraverser.Stop();
        }


        public override bool Notify(string key, object data)
        {
            if (key == "seekEntity")
            {
                targetEntity = (EntityAgent)data;
                targetPos = targetEntity.ServerPos.XYZ;
                return true;
            }

            return false;
        }


        private void OnStuck()
        {
            stuck = true;   
        }

        private void OnGoalReached()
        {
            entity.PathTraverser.Active = true;
        }
    }
}
