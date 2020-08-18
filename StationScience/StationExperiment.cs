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
namespace StationScience
{
    public class StationExperiment : ModuleScienceExperiment
    {
        [KSPField(isPersistant = false)]
        public int eurekasRequired;

        [KSPField(isPersistant = false)]
        public int kuarqsRequired;

        [KSPField(isPersistant = false)]
        public float kuarqHalflife;

        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")]
        public float kuarqDecay;

        [KSPField(isPersistant = false)]
        public int bioproductsRequired;

        [KSPField(isPersistant = true)]
        public float launched = 0;

        [KSPField(isPersistant = true)]
        public float completed = 0;

        [KSPField(isPersistant = true)]
        public string last_subjectId = "";

        public static bool CheckBoring(Vessel vessel, bool msg = false)
        {
            //print(vessel.Landed + ", " + vessel.landedAt + ", " + vessel.launchTime + ", " + vessel.situation + ", " + vessel.orbit.referenceBody.name);
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
            double numEurekas = GetResourceAmount("Eurekas");
            double numKuarqs = GetResourceAmount("Kuarqs");
            double numBioproducts = GetResourceAmount("Bioproducts");
            //print(part.partInfo.title + " Eurekas: " + numEurekas + "/" + eurekasRequired);
            //print(part.partInfo.title + " Kuarqs: " + numKuarqs + "/" + kuarqsRequired);
            //print(part.partInfo.title + " Bioproducts: " + numBioproducts + "/" + bioproductsRequired);
            return Math.Round(numEurekas, 2) >= eurekasRequired && Math.Round(numKuarqs,2) >= kuarqsRequired && Math.Round(numBioproducts, 2) >= bioproductsRequired - 0.001;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor) { return; }
            Fields["kuarqDecay"].guiActive = (kuarqsRequired > 0 && kuarqHalflife > 0);
            Events["DeployExperiment"].active = Finished();
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
            PartResource eurekas = SetResourceMaxAmount("Eurekas", eurekasRequired);
            PartResource kuarqs = SetResourceMaxAmount("Kuarqs", kuarqsRequired);
            PartResource bioproducts = SetResourceMaxAmount("Bioproducts", bioproductsRequired);
            if (eurekas.amount == 0 && bioproducts != null) bioproducts.amount = 0;
            Events["StartExperiment"].active = false;
            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_started"), 6, ScreenMessageStyle.UPPER_CENTER);
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
                base.DeployExperiment();
        }

        new public void DeployAction(KSPActionParam p)
        {
            if (DeployChecks())
                base.DeployAction(p);
        }

        new public void ResetExperiment()
        {
            base.ResetExperiment();
            StopResearch("Bioproducts");
            Events["StartExperiment"].active = true;
        }

        new public void ResetExperimentExternal()
        {
            base.ResetExperimentExternal();
            StopResearch("Bioproducts");
            Events["StartExperiment"].active = true;
        }

        new public void ResetAction(KSPActionParam p)
        {
            base.ResetAction(p);
            StopResearch("Bioproducts");
            Events["StartExperiment"].active = true;
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (kuarqHalflife > 0 && kuarqsRequired > 0)
            {
                var kuarqs = GetResource("Kuarqs");
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
            StopResearch("Eurekas");
            StopResearch("Kuarqs");
        }

        public System.Collections.IEnumerator UpdateStatus()
        {
            while (true)
            {
                //print(part.partInfo.title + "updateStatus");
                double numEurekas = GetResourceAmount("Eurekas");
                double numEurekasMax = GetResourceMaxAmount("Eurekas");
                double numKuarqs = GetResourceAmount("Kuarqs");
                double numKuarqsMax = GetResourceMaxAmount("Kuarqs");
                double numBioproducts = GetResourceAmount("Bioproducts");
                int sciCount = GetScienceCount();
                //print(part.partInfo.title + " finished: " + finished());
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
                    StopResearch("Bioproducts");
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
                /*
                if (numKuarqs > 0)
                {
                    var kuarqModules = vessel.FindPartModulesImplementing<KuarqGenerator>();
                    if (kuarqModules == null || kuarqModules.Count() < 1)
                    {
                        stopResearch("Kuarqs");
                    }
                }
                */
                if (numBioproducts > 0 && Inoperable)
                {
                    StopResearch("Bioproducts");
                }
                if (bioproductsRequired > 0 && GetScienceCount() > 0 && numBioproducts < bioproductsRequired)
                {
                    ResetExperiment();
                }
                yield return new UnityEngine.WaitForSeconds(1f);
            }
        }

        public override string GetInfo()
        {
            string ret = "";
            string reqLab = "", reqCyclo = "", reqZoo = "";
            if (eurekasRequired > 0)
            {
                ret += Localizer.Format("#autoLOC_StatSci_EuReq", eurekasRequired);
                reqLab = Localizer.Format("#autoLOC_StatSci_LabReq");
            }
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
                double bioproductDensity = ResourceHelper.getResourceDensity("Bioproducts");
                if (bioproductDensity > 0)
                    ret += Localizer.Format("#autoLOC_StatSci_BioMass", Math.Round(bioproductsRequired * bioproductDensity + part.mass,2));
                reqZoo = Localizer.Format("#autoLOC_StatSci_ZooReq");
            }
            return ret + reqLab + reqCyclo + reqZoo + "\n\n" + base.GetInfo();
        }
    }
}