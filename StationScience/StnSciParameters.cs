/*
    This file is part of Station Science.

    Station Science is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Station Science is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Station Science.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using KSP.Localization;

namespace StationScience.Contracts.Parameters
{
    
    public interface PartRelated
    {
        AvailablePart GetPartType();
    }

    public interface BodyRelated
    {
        CelestialBody GetBody();
    }

    public class StnSciParameter : ContractParameter, PartRelated, BodyRelated
    {
        AvailablePart experimentType;
        CelestialBody targetBody;

        public AvailablePart GetPartType()
        {
            return experimentType;
        }

        public CelestialBody GetBody()
        {
            return targetBody;
        }

        public StnSciParameter()
        {
            //SetExperiment("StnSciExperiment1");
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        public StnSciParameter(AvailablePart type, CelestialBody body)
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
            this.experimentType = type;
            this.targetBody = body;
            //this.AddParameter(new Parameters.NewPodParameter(), null);
            this.AddParameter(new Parameters.DoExperimentParameter(), null);
            this.AddParameter(new Parameters.ReturnExperimentParameter(), null);
        }

        protected override string GetHashString()
        {
            return experimentType.name;
        }

        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_StatSciParam_Title", experimentType.title, targetBody.GetDisplayName());
        }

        protected override string GetNotes()
        {
            return Localizer.Format("#autoLOC_StatSciParam_Notes", experimentType.title, targetBody.GetDisplayName());
        }

        private bool SetExperiment(string exp)
        {
            experimentType = PartLoader.getPartInfoByName(exp);
            if (experimentType == null)
            {
                StnSciScenario.LogError("Couldn't find experiment part: " + exp);
                return false;
            }
            return true;
        }

        public void Complete()
        {
            SetComplete();
        }

        private bool SetTarget(string planet)
        {
            targetBody = FlightGlobals.Bodies.FirstOrDefault(body => body.bodyName.ToLower() == planet.ToLower());
            if (targetBody == null)
            {
                StnSciScenario.LogError("Couldn't find planet: " + planet);
                return false;
            }
            return true;
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("targetBody", targetBody.name);
            node.AddValue("experimentType", experimentType.name);
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
            string expID = node.GetValue("experimentType");
            SetExperiment(expID);
            string bodyID = node.GetValue("targetBody");
            SetTarget(bodyID);
        }

        static public AvailablePart getExperimentType(ContractParameter o)
        {
            object par = o.Parent;
            if (par == null)
                par = o.Root;
            PartRelated parent = par as PartRelated;
            if (parent != null)
                return parent.GetPartType();
            else
                return null;
        }

        static public CelestialBody getTargetBody(ContractParameter o)
        {
            BodyRelated parent = o.Parent as BodyRelated;
            if (parent != null)
                return parent.GetBody();
            else
                return null;
        }
    }

    public class NewPodParameter : ContractParameter
    {
        public NewPodParameter()
        {
            //SetExperiment("StnSciExperiment1");
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        protected override string GetHashString()
        {
            return "new pod parameter " + this.GetHashCode();
        }
        protected override string GetTitle()
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return Localizer.Format("#autoLOC_StatSciNewPod_TitleA");
            return Localizer.Format("#autoLOC_StatSciNewPod_TitleB", experimentType.title);
        }

        protected override void OnRegister()
        {
            GameEvents.onLaunch.Add(OnLaunch);
            GameEvents.onVesselSituationChange.Add(OnVesselSituationChange);
        }
        protected override void OnUnregister()
        {
            GameEvents.onLaunch.Remove(OnLaunch);
            GameEvents.onVesselSituationChange.Remove(OnVesselSituationChange);
        }

        private void OnVesselCreate(Vessel vessel)
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
            foreach (Part part in vessel.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null)
                    {
                        e.launched = (float)Planetarium.GetUniversalTime();
                    }
                }
            }
        }

        private void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel,Vessel.Situations> arg)
        {
            if(!((arg.from == Vessel.Situations.LANDED || arg.from == Vessel.Situations.PRELAUNCH) &&
                  (arg.to == Vessel.Situations.FLYING || arg.to == Vessel.Situations.SUB_ORBITAL)))
                return;
            if (arg.host.mainBody.name != "Kerbin")
                return;
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
            foreach (Part part in arg.host.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null && e.launched == 0)
                    {
                        e.launched = (float)Planetarium.GetUniversalTime();
                    }
                }
            }
        }

        private void OnLaunch(EventReport report)
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
            Vessel vessel = FlightGlobals.ActiveVessel;
            foreach (Part part in vessel.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null && e.launched == 0)
                    {
                        e.launched = (float)Planetarium.GetUniversalTime();
                    }
                }
            }
        }

        private float lastUpdate = 0;

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (lastUpdate > UnityEngine.Time.realtimeSinceStartup + .1)
                return;
            lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            Vessel vessel = FlightGlobals.ActiveVessel;
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
            if (vessel != null)
                foreach (Part part in vessel.Parts)
                {
                    if (part.name == experimentType.name)
                    {
                        StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                        if (e != null)
                        {
                            if (e.launched >= this.Root.DateAccepted)
                            {
                                SetComplete();
                                return;
                            }
                        }
                    }
                }
            SetIncomplete();
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }

    public class DoExperimentParameter : ContractParameter
    {
        public DoExperimentParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        protected override string GetHashString()
        {
            return Localizer.Format("#autoLOC_StatSciDoExp_Hash", this.GetHashCode());
        }
        protected override string GetTitle()
        {
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            if (targetBody == null)
                return Localizer.Format("#autoLOC_StatSciDoExp_TitleA");
            else
                return Localizer.Format("#autoLOC_StatSciDoExp_TitleB", targetBody.GetDisplayName());
        }

        private float lastUpdate = 0;

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (lastUpdate > UnityEngine.Time.realtimeSinceStartup + .1)
                return;
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (targetBody == null || experimentType == null)
            if (targetBody == null || experimentType == null)
            {
                return;
            }
            lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
                foreach (Part part in vessel.Parts)
                {
                    if (part.name == experimentType.name)
                    {
                        StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                        if (e != null)
                        {
                            if (e.completed >= this.Root.DateAccepted && e.completed > e.launched)
                            {
                                ScienceData[] data = e.GetData();
                                foreach (ScienceData datum in data)
                                {
                                    if (datum.subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                                    {
                                        SetComplete();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            SetIncomplete();
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }

    public class ReturnExperimentParameter : ContractParameter
    {
        public ReturnExperimentParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        public void OnAccept(Contract contract)
        {
        }

        protected override string GetHashString()
        {
            return "recover experiment " + this.GetHashCode();
        }
        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_StatSciRetParam_Title");
        }

        protected override void OnRegister()
        {
            //GameEvents.OnVesselRecoveryRequested.Add(OnRecovery);
            GameEvents.onVesselRecovered.Add(OnRecovered);
        }
        protected override void OnUnregister()
        {
            //GameEvents.OnVesselRecoveryRequested.Remove(OnRecovery);
            GameEvents.onVesselRecovered.Remove(OnRecovered);
        }

        private void OnRecovered(ProtoVessel pv, bool dummy)
        {
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (targetBody == null || experimentType == null)
            {
                return;
            }
            foreach (ProtoPartSnapshot part in pv.protoPartSnapshots)
            {
                if (part.partName == experimentType.name)
                {
                    foreach(ProtoPartModuleSnapshot module in part.modules)
                    {
                        if (module.moduleName == "StationExperiment")
                        {
                            ConfigNode cn = module.moduleValues;
                            if (!cn.HasValue("launched") || !cn.HasValue("completed"))
                                continue;
                            float launched, completed;
                            try
                            {
                                launched = float.Parse(cn.GetValue("launched"));
                                completed = float.Parse(cn.GetValue("completed"));
                            }
                            catch(Exception e)
                            {
                                StnSciScenario.LogError(e.ToString());
                                continue;
                            }
                            if (completed >= this.Root.DateAccepted)
                            {
                                foreach (ConfigNode datum in cn.GetNodes("ScienceData"))
                                {
                                    if (!datum.HasValue("subjectID"))
                                        continue;
                                    string subjectID = datum.GetValue("subjectID");
                                    if (subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                                    {
                                        StnSciParameter parent = this.Parent as StnSciParameter;
                                        SetComplete();
                                        if (parent != null)
                                            parent.Complete();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnRecovery(Vessel vessel)
        {
            StnSciScenario.Log("Recovering " + vessel.vesselName);
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (targetBody == null || experimentType == null)
            {
                return;
            }
            foreach (Part part in vessel.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null)
                    {
                        if (e.launched >= this.Root.DateAccepted && e.completed >= e.launched)
                        {
                            ScienceData[] data = e.GetData();
                            foreach (ScienceData datum in data)
                            {
                                if (datum.subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                                {
                                    StnSciParameter parent = this.Parent as StnSciParameter;
                                    SetComplete();
                                    if (parent != null)
                                        parent.Complete();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            SetIncomplete();
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }
}
