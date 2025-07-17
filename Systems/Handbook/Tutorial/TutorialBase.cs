using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface ITutorial
    {
        bool Complete { get; }
        float Progress { get; }
        string PageCode { get; }
        void Restart();
        void Skip(int count);
        void Load();
        void Save();

        bool OnStateUpdate(ActionBoolReturn<TutorialStepBase> stepCall);

        List<TutorialStepBase> GetTutorialSteps(bool skipOld);
    }

    public abstract class TutorialBase : ITutorial
    {
        protected ICoreClientAPI capi;

        protected JsonObject stepData;
        protected List<TutorialStepBase> steps = new List<TutorialStepBase>();
        public int currentStep;
        public string pageCode;

        protected TutorialBase(ICoreClientAPI capi, string pageCode)
        {
            this.capi = capi;
            this.pageCode = pageCode;
        }

        public JsonObject StepDataForSaving
        {
            get
            {
                if (stepData == null)
                {
                    stepData = new JsonObject(new JObject());
                }

                return stepData;
            }
        }

        public string PageCode => pageCode;

        public void Restart()
        {
            stepData = new JsonObject(new JObject());

            foreach (var step in steps)
            {
                step.Restart();
                step.ToJson(StepDataForSaving);
            }

            Save();
        }


        public bool OnStateUpdate(ActionBoolReturn<TutorialStepBase> stepCall)
        {
            bool anyDirty = false;
            bool anyNowCompleted = false;

            foreach (var step in steps)
            {
                if (step.Complete) continue;
                bool dirty = stepCall(step);
                anyDirty |= dirty;

                if (dirty) step.ToJson(StepDataForSaving);

                if (step.Complete) anyNowCompleted = true;
            }

            if (anyNowCompleted)
            {
                capi.Gui.PlaySound(new AssetLocation("sounds/tutorialstepsuccess.ogg"), false, 1);
                Save();
            }

            return anyDirty;
        }

        public bool Complete => steps[steps.Count - 1].Complete;

        public float Progress => steps.Count == 0 ? 0f : steps.Sum((t) => t.Complete ? 1 : 0) / (float)steps.Count;

        public void addSteps(params TutorialStepBase[] steps)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i].index = i;
            }

            this.steps.AddRange(steps);
        }


        public List<TutorialStepBase> GetTutorialSteps(bool skipOld)
        {
            if (this.steps.Count == 0) initTutorialSteps();

            List<TutorialStepBase> steps = new List<TutorialStepBase>();
            int showActive = 1;

            foreach (var step in this.steps)
            {
                if (showActive <= 0) break;
                if (stepData != null) step.FromJson(stepData);

                steps.Add(step);

                if (!step.Complete) showActive--;
            }

            if (skipOld)
            {
                while (steps.Count > 1)
                {
                    if (steps[0].Complete)
                    {
                        steps.RemoveAt(0);
                        continue;
                    }
                    break;
                }
            }

            return steps;
        }

        protected abstract void initTutorialSteps();

        public void Skip(int cnt)
        {
            while (cnt-- > 0)
            {
                var step = steps.FirstOrDefault(s => !s.Complete);
                if (step != null)
                {
                    step.Skip();
                    step.ToJson(StepDataForSaving);
                }
            }

            capi.Gui.PlaySound(new AssetLocation("sounds/tutorialstepsuccess.ogg"), false, 1);
        }


        public void Save()
        {
            JsonObject job = new JsonObject(new JObject());
            foreach (var step in steps)
            {
                step.ToJson(job);
            }

            capi.StoreModConfig(job, "tutorial-"+PageCode+".json");
        }

        public void Load()
        {
            try
            {
                stepData = capi.LoadModConfig("tutorial-" + PageCode + ".json");
            }
            catch (Exception e)
            {
                capi.Logger.Error("Failed to load tutorial-" + PageCode + ".json, the tutorial will be reset.");
                capi.Logger.Error(e);
            }
            
            if (stepData != null)
            {
                foreach (var step in steps)
                {
                    step.FromJson(stepData);
                }
            }
        }

    }
}