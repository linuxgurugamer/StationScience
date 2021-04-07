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

using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP_Log;

namespace StationScience
{
    public class StationExperiment : ModuleScienceExperiment
    {

        public const string EUREKAS = "Eurekas";
        public const string KUARQS = "Kuarqs";
        public const string BIOPRODUCTS = "Bioproducts";


        internal class Requirement
        {
            internal string name;
            internal float amount;
            internal Requirement(string name, float amount)
            {
                this.name = name;
                this.amount = amount;
            }
        }
        public enum Status
        {
            Idle,
            Running,
            Completed,
            BadLocation,
            Storage,
            Inoperable,
            Starved
        }

        static Log Log;

        internal Dictionary<string, Requirement> requirements = new Dictionary<string, Requirement>();

      [KSPField(isPersistant = false)]
        public int eurekasRequired;

        [KSPField(isPersistant = false)]
        public int kuarqsRequired;

        [KSPField(isPersistant = false)]
        public int bioproductsRequired;


        [KSPField(isPersistant = false)]
        public float kuarqHalflife;

        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")]
        public float kuarqDecay;

        [KSPField(isPersistant = false, guiName = "Status", guiActive = true)]
        public Status currentStatus = Status.Idle;

        [KSPField(isPersistant = true)]
        public float launched = 0;

        [KSPField(isPersistant = true)]
        public float completed = 0;

        [KSPField(isPersistant = true)]
        public string last_subjectId = "";

        public static bool CheckBoring(Vessel vessel, bool msg = false)
        {
            Log.Info(vessel.Landed + ", " + vessel.landedAt + ", " + vessel.launchTime + ", " + vessel.situation + ", " + vessel.orbit.referenceBody.name);
            if ((vessel.orbit.referenceBody == FlightGlobals.GetHomeBody()) && (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.SPLASHED || vessel.altitude <= vessel.orbit.referenceBody.atmosphereDepth))
            {
                if (msg)
                    ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_boring"), 6, ScreenMessageStyle.UPPER_CENTER);
                return true;
            }
            return false;
        }

        public PartResource GetResource(string name)
        {
            return ResourceHelper.getResource(part, name);
        }

        public double GetResourceAmount(string name)
        {
            return ResourceHelper.getResourceAmount(part, name);
        }

        public double GetResourceMaxAmount(string name)
        {
            return ResourceHelper.getResourceMaxAmount(part, name);
        }

        public PartResource SetResourceMaxAmount(string name, double max)
        {
            return ResourceHelper.setResourceMaxAmount(part, name, max);
        }

        public bool Finished()
        {
            bool finished = true;
            double numEurekas = GetResourceAmount(EUREKAS);
            double numKuarqs = GetResourceAmount(KUARQS);
            double numBioproducts = GetResourceAmount(BIOPRODUCTS);
            foreach (var r in requirements)
            {
                double num = GetResourceAmount(r.Value.name);
                Log.Info(part.partInfo.title + " "+r.Value.name +": " + num + "/" + r.Value.amount.ToString("F1"));

                if (Math.Round(num, 2) < r.Value.amount)
                        finished = false;
            }
            //Log.Info(part.partInfo.title + " Eurekas: " + numEurekas + "/" + eurekasRequired);
            //Log.Info(part.partInfo.title + " Kuarqs: " + numKuarqs + "/" + kuarqsRequired);
            //Log.Info(part.partInfo.title + " Bioproducts: " + numBioproducts + "/" + bioproductsRequired);
            //return Math.Round(numEurekas, 2) >= eurekasRequired && Math.Round(numKuarqs,2) >= kuarqsRequired && //Math.Round(numBioproducts, 2) >= bioproductsRequired - 0.001;
            return finished;
        }


        // this is an unbelievable hack, but it's the only thing i've found that works
        /// <summary>
        /// Loads requirements from given config node
        /// </summary>
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (part.partInfo != null)
            {
                node =
                    GameDatabase.Instance.GetConfigs("PART")
                                .Single(c => part.partInfo.name == c.name.Replace('_', '.'))
                                .config.GetNodes("MODULE")
                                .Single(n => n.GetValue("name") == moduleName);
            }

            var pList = PartResourceLibrary.Instance.resourceDefinitions;

            foreach (ConfigNode resNode in node.GetNodes("REQUIREMENT"))
            {
                try
                {
                    string name = resNode.GetValue("name");
                    float amt = float.Parse(resNode.GetValue("maxAmount"));
                    requirements.Add(name, new Requirement(name, amt));

                    // check if this resource can be pumped
                    var def = pList[name];
                    if (def.resourceTransferMode != ResourceTransferMode.NONE)
                    {
                        // add the resource so it can be pre-filled in the VAB
                        PartResource resource = part.AddResource(resNode);

                        // but remove it so it doesn't show up in the info box in the VAB
                        part.Resources.Remove(resource);
                    }
                }
                catch { }
            }
            // Following needed to maintain compatibility with older parts
            if (eurekasRequired > 0 && !requirements.ContainsKey(EUREKAS))
                requirements.Add(EUREKAS, new Requirement(EUREKAS, eurekasRequired));
            if (kuarqsRequired > 0 && !requirements.ContainsKey(KUARQS))
                requirements.Add(KUARQS, new Requirement(KUARQS, kuarqsRequired));
            if (bioproductsRequired > 0 && !requirements.ContainsKey(BIOPRODUCTS))
                requirements.Add(BIOPRODUCTS, new Requirement(BIOPRODUCTS, bioproductsRequired));
        }

        public override void OnStart(StartState state)
        {
#if DEBUG
            Log = new Log("StationScience", Log.LEVEL.INFO);
#else
      Log = new Log("StationScience", Log.LEVEL.ERROR);
#endif
            base.OnStart(state);
            if (state == StartState.Editor) { return; }
            if (requirements.ContainsKey(KUARQS) && kuarqHalflife > 0)
            //if (kuarqsRequired > 0 && kuarqHalflife > 0)
            {
                Fields["kuarqDecay"].guiActive = true; // (kuarqsRequired > 0 && kuarqHalflife > 0);
                Events["DeployExperiment"].active = Finished();
                Events["StartExperiment"].active = false;

                if (ResearchAndDevelopment.GetExperiment(experimentID).IsAvailableWhile(
                     GetScienceSituation(vessel), vessel.mainBody))
                    currentStatus = Status.Completed;
                else
                    currentStatus = Status.BadLocation;
            } else
            {
                if (Inoperable)
                    currentStatus = Status.Inoperable;
                else if (Deployed)
                    currentStatus = Status.Storage;
                else if (GetResource(EUREKAS) != null)
                    currentStatus = Status.Running;
                else
                    currentStatus = Status.Idle;

                Events["DeployExperiment"].active = !Deployed;
                Events["StartExperiment"].active = (!Inoperable && GetScienceCount() == 0);
            }


            this.part.force_activate();
            StartCoroutine(UpdateStatus());
            //Actions["DeployAction"].active = false;
        }

        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_startExp", active = true)]
        public void StartExperiment()
        {
            if (GetScienceCount() > 0)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_finalized"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (CheckBoring(vessel, true)) return;
            PartResource eurekas = null;
            PartResource bioproducts = null;
            foreach (var r in requirements)
            {
                PartResource pr = SetResourceMaxAmount(r.Value.name, r.Value.amount);
                if (r.Value.name == EUREKAS) eurekas = pr;
                if (r.Value.name == BIOPRODUCTS) bioproducts = pr;
            }
            //PartResource eurekas = SetResourceMaxAmount(EUREKAS, eurekasRequired);
            //PartResource kuarqs = SetResourceMaxAmount(KUARQS, kuarqsRequired);
            //PartResource bioproducts = SetResourceMaxAmount(BIOPRODUCTS, bioproductsRequired);
            if (eurekas != null && eurekas.amount == 0 && bioproducts != null) bioproducts.amount = 0;
            Events["StartExperiment"].active = false;
            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_started"), 6, ScreenMessageStyle.UPPER_CENTER);

            currentStatus = Status.Running;
        }

        [KSPAction("#autoLOC_StatSci_startExp")]
        public void StartExpAction(KSPActionParam p)
        {
            StartExperiment();
        }


        public bool DeployChecks()
        {
            if (CheckBoring(vessel, true)) return false;
            if (Finished())
            {
                Events["DeployExperiment"].active = false;
                Events["StartExperiment"].active = false;
                return true;
            }
            else
            {
                ScreenMessages.PostScreenMessage("#autoLOC_StatSci_screen_notfinished", 6, ScreenMessageStyle.UPPER_CENTER);
            }
            return false;
        }

        new public void DeployExperiment()
        {
            if (DeployChecks())
            {
                base.DeployExperiment();
                currentStatus = Status.Storage;
            }
        }

        new public void DeployAction(KSPActionParam p)
        {
            if (DeployChecks())
            {
                base.DeployAction(p);
                currentStatus = Status.Storage;
            }
        }
         
        new public void ResetExperiment()
        {
            base.ResetExperiment();
            StopResearch(BIOPRODUCTS);
            Events["StartExperiment"].active = true;
            currentStatus = Status.Idle;
        }

        new public void ResetExperimentExternal()
        {
            base.ResetExperimentExternal();
            StopResearch(BIOPRODUCTS);
            Events["StartExperiment"].active = true;
            currentStatus = Status.Idle;
        }

        new public void ResetAction(KSPActionParam p)
        {
            base.ResetAction(p);
            StopResearch(BIOPRODUCTS);
            Events["StartExperiment"].active = true;
            currentStatus = Status.Idle;
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (requirements.ContainsKey(KUARQS) && kuarqHalflife > 0)
            //if (kuarqHalflife > 0 && kuarqsRequired > 0)
            {
                var kuarqs = GetResource(KUARQS);
                float kuarqsRequired = requirements[KUARQS].amount;
                if (kuarqs != null && kuarqs.amount < (.99 * kuarqsRequired))
                {
                    double decay = Math.Pow(.5, TimeWarp.fixedDeltaTime / kuarqHalflife);
                    kuarqDecay = (float)((kuarqs.amount * (1 - decay)) / TimeWarp.fixedDeltaTime);
                    kuarqs.amount = kuarqs.amount * decay;
                }
                else
                    kuarqDecay = 0;
            }
        }

        public void StopResearch(string resName)
        {
            SetResourceMaxAmount(resName, 0);
        }

        public void StopResearch()
        {
            StopResearch(EUREKAS);
            StopResearch(KUARQS);
        }

        public System.Collections.IEnumerator UpdateStatus()
        {
            while (true)
            {
                Log.Info(part.partInfo.title + "updateStatus");
                double numEurekas = GetResourceAmount(EUREKAS);
                double numEurekasMax = GetResourceMaxAmount(EUREKAS);
                double numKuarqs = GetResourceAmount(KUARQS);
                double numKuarqsMax = GetResourceMaxAmount(KUARQS);
                double numBioproducts = GetResourceAmount(BIOPRODUCTS);
                int sciCount = GetScienceCount();
                Log.Info(part.partInfo.title + " finished: " + Finished());
                if (!Finished())
                {
                    Events["DeployExperiment"].active = false;
                    Events["StartExperiment"].active = (!Inoperable && sciCount == 0 && numEurekasMax == 0 && numKuarqsMax == 0);
                }
                else
                {
                    Events["DeployExperiment"].active = true;
                    Events["StartExperiment"].active = false;
                }
                var subject = ScienceHelper.getScienceSubject(experimentID, vessel);
                string subjectId = ((subject == null) ? "" : subject.id);
                if(subjectId != "" && last_subjectId != "" && last_subjectId != subjectId &&
                    (numEurekas > 0 || numKuarqs > 0 || (numBioproducts > 0 && sciCount == 0))) {
                    ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_locchange", part.partInfo.title), 6, ScreenMessageStyle.UPPER_CENTER);
                    StopResearch();
                    StopResearch(BIOPRODUCTS);
                }
                last_subjectId = subjectId;
                if (sciCount > 0)
                {
                    StopResearch();
                    if (completed == 0)
                        completed = (float) Planetarium.GetUniversalTime();
                }
                if (numEurekas > 0)
                {
                    var eurekasModules = vessel.FindPartModulesImplementing<StationScienceModule>();
                    if (eurekasModules == null || eurekasModules.Count() < 1)
                    {
                        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_detatch", part.partInfo.title), 2, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
                if (numEurekas <= 0 || numKuarqs <=0)
                {
                    currentStatus = Status.Starved;
                }
                else
                {
                    currentStatus = Status.Running;
                }
                /*
                if (numKuarqs > 0)
                {
                    var kuarqModules = vessel.FindPartModulesImplementing<KuarqGenerator>();
                    if (kuarqModules == null || kuarqModules.Count() < 1)
                    {
                        stopResearch(KUARQS);
                    }
                }
                */
                if (numBioproducts > 0 && Inoperable)
                {
                    StopResearch(BIOPRODUCTS);
                }
                if (requirements.ContainsKey(BIOPRODUCTS) && GetScienceCount() > 0 && numBioproducts < requirements[BIOPRODUCTS].amount)
                //if (bioproductsRequired > 0 && GetScienceCount() > 0 && numBioproducts < bioproductsRequired)
                {
                    ResetExperiment();
                }
#if false
                // make sure we keep updating while changes are possible
                if (currentStatus == Status.Running
                        || currentStatus == Status.Completed
                        || currentStatus == Status.BadLocation)
                    ReadyToDeploy(false);
#endif
                ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(experimentID);
                if (!experiment.IsAvailableWhile(GetScienceSituation(vessel), vessel.mainBody))
                {
                    ScreenMessages.PostScreenMessage(Localizer.Format("Can't perform experiment here."), 6, ScreenMessageStyle.UPPER_CENTER);
                    currentStatus = Status.BadLocation;
                }


                yield return new UnityEngine.WaitForSeconds(1f);
            }
        }



        public override string GetInfo()
        {
            string ret = "";
            string reqLab = "", reqCyclo = "", reqZoo = "";
            foreach (var r in requirements)
            {
                if (ret != "") ret += "\n";
                ret += r.Value.name +" "+Localizer.Format("#autoLOC_StatSci_Req", r.Value.amount);
                if (r.Value.name == EUREKAS)
                    reqLab = Localizer.Format("#autoLOC_StatSci_LabReq");
                if (r.Value.name == KUARQS)
                {
                    double productionRequired = 0.01;
                    if (kuarqHalflife > 0)
                    {
                        if (ret != "") ret += "\n";
                        ret += Localizer.Format("#autoLOC_StatSci_KuarkHalf", kuarqHalflife);
                        productionRequired = requirements[KUARQS].amount /* kuarqsRequired */ * (1 - Math.Pow(.5, 1.0 / kuarqHalflife));
                        ret += "\n";
                        ret += Localizer.Format("#autoLOC_StatSci_KuarkProd", productionRequired.ToString("F3"));
                    }
                    if (productionRequired > 1)
                        reqCyclo = Localizer.Format("#autoLOC_StatSci_CycReqM", Math.Ceiling(productionRequired));
                    else
                        reqCyclo = Localizer.Format("#autoLOC_StatSci_CycReq");
                }
                if (r.Value.name == BIOPRODUCTS)
                {
                    double bioproductDensity = ResourceHelper.getResourceDensity(BIOPRODUCTS);
                    if (bioproductDensity > 0)
                        ret += Localizer.Format("#autoLOC_StatSci_BioMass", Math.Round( requirements[BIOPRODUCTS].amount /* bioproductsRequired */ * bioproductDensity + part.mass, 2));
                    reqZoo = Localizer.Format("#autoLOC_StatSci_ZooReq");
                }
            }
#if false
            //if (eurekasRequired > 0)
            //{
            //    ret += Localizer.Format("#autoLOC_StatSci_EuReq", eurekasRequired);
            //    reqLab = Localizer.Format("#autoLOC_StatSci_LabReq");
            //}
            if (kuarqsRequired > 0)
            {
                if (ret != "") ret += "\n";
                ret += Localizer.Format("#autoLOC_StatSci_KuarkReq", kuarqsRequired);
                double productionRequired = 0.01;
                if (kuarqHalflife > 0)
                {
                    if (ret != "") ret += "\n";
                    ret += Localizer.Format("#autoLOC_StatSci_KuarkHalf", kuarqHalflife);
                    productionRequired = kuarqsRequired * (1 - Math.Pow(.5, 1.0 / kuarqHalflife));
                    ret += "\n";
                    ret += Localizer.Format("#autoLOC_StatSci_KuarkProd", productionRequired);
                }
                if (productionRequired > 1)
                    reqCyclo = Localizer.Format("#autoLOC_StatSci_CycReqM", Math.Ceiling(productionRequired));
                else
                    reqCyclo = Localizer.Format("#autoLOC_StatSci_CycReq");
            }
            if (bioproductsRequired > 0)
            {
                if (ret != "") ret += "\n";
                ret += Localizer.Format("#autoLOC_StatSci_BioReq", bioproductsRequired);
                double bioproductDensity = ResourceHelper.getResourceDensity(BIOPRODUCTS);
                if (bioproductDensity > 0)
                    ret += Localizer.Format("#autoLOC_StatSci_BioMass", Math.Round(bioproductsRequired * bioproductDensity + part.mass,2));
                reqZoo = Localizer.Format("#autoLOC_StatSci_ZooReq");
            }
#endif
            return ret + reqLab + reqCyclo + reqZoo + "\n\n" + base.GetInfo();
        }

#region Helper Functions

        protected static ExperimentSituations GetScienceSituation(Vessel vessel)
        {
            switch (vessel.situation)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.PRELAUNCH:
                    return ExperimentSituations.SrfLanded;

                case Vessel.Situations.SPLASHED:
                    return ExperimentSituations.SrfSplashed;

                case Vessel.Situations.FLYING:
                    if (vessel.altitude < (double)vessel.mainBody.scienceValues.flyingAltitudeThreshold)
                        return ExperimentSituations.FlyingLow;
                    return ExperimentSituations.FlyingHigh;

                default:
                    if (vessel.altitude < (double)vessel.mainBody.scienceValues.spaceAltitudeThreshold)
                        return ExperimentSituations.InSpaceLow;
                    return ExperimentSituations.InSpaceHigh;
            }
        }
#endregion
    }
}