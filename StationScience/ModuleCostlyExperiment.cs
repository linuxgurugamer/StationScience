/*
    This file is part of Costly Science.

    Costly Science is free software: you can redistribute it and/or
	modify it under the terms of the GNU General Public	License as 
	published by the Free Software Foundation, either version 3 of
	the License, or (at your option) any later version.

    Costly Science is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
	General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Costly Science. If not, see <http://www.gnu.org/licenses/>.
*/
#if false
//#define DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StationScience
{

	public class ModuleCostlyExperiment : StationExperiment // ModuleScienceExperiment
	{
		public enum Status
		{
			Idle,
			Running,
			Completed,
			BadLocation,
			Storage,
			Inoperable,
		}

		[KSPField(isPersistant = false)]
		new public float kuarqHalflife;

		[KSPField(isPersistant = false, guiName = "Decay rate", guiUnits = " kuarqs/s",
			guiActive = false, guiFormat = "F2")]
		new public float kuarqDecay;

		[KSPField(isPersistant = false, guiName = "Status", guiActive = true)]
		public Status currentStatus = Status.Idle;

		// this is an unbelievable hack, but it's the only thing i've found that works
		public List<string> requirementNames = new List<string>();
		public List<float> requirementAmounts = new List<float>();

		/// <summary>
		/// Loads requirements from given config node
		/// </summary>
		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			var pList = PartResourceLibrary.Instance.resourceDefinitions;

			foreach (ConfigNode resNode in node.GetNodes("REQUIREMENT"))
			{
				requirementNames.Add(resNode.GetValue("name"));
				requirementAmounts.Add(float.Parse(resNode.GetValue("maxAmount")));

				// check if this resource can be pumped
				var def = pList[resNode.GetValue("name")];
				if (def.resourceTransferMode != ResourceTransferMode.NONE)
				{
					// add the resource so it can be pre-filled in the VAB
					PartResource resource = part.AddResource(resNode);

					// but remove it so it doesn't show up in the info box in the VAB
					part.Resources.Remove(resource);
				}
			}
		}

		/// <summary>
		/// Makes kuarqDecay field visible in non-editor scenes,
		/// and that relevant events are visible
		/// </summary>
		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if (state == StartState.Editor || state == StartState.None) return;

			Fields["kuarqDecay"].guiActive = requirementNames.Contains(KUARQS);

			if (ReadyToDeploy(false) && !Deployed)
			{
				if (ResearchAndDevelopment.GetExperiment(experimentID).IsAvailableWhile(
								 GetScienceSituation(vessel), vessel.mainBody))
					currentStatus = Status.Completed;
				else
					currentStatus = Status.BadLocation;

				Events["DeployExperiment"].active = true;
				Events["StartExperiment"].active = false;
			}
			else
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

			StartCoroutine(UpdateStatus());
		}

		[KSPEvent(guiActive = true, guiName = "Start Experiment", active = true)]
		new public void StartExperiment()
		{
			if (Deployed || Inoperable) return;

			AddAllResources();
			Events["StartExperiment"].active = false;
			ShoutToScreen("Started experiment!");
			currentStatus = Status.Running;
		}

		[KSPAction("Start Experiment")]
		new public void StartExpAction(KSPActionParam p)
		{
			StartExperiment();
		}

		/// <summary>
		/// True if all required resources are present
		/// </summary>
		public bool ReadyToDeploy(bool displayMessage = true)
		{
			for (int i = 0; i < requirementNames.Count; i++)
			{
				//float result = part.RequestResource(requirementNames[i], requirementAmounts[i]);
				double result = GetResourceAmount(requirementNames[i]);

				if (result < requirementAmounts[i] - 0.001)
				{
					if (displayMessage)
						ShoutToScreen("Experiment not finished yet!");

					currentStatus = Status.Running;

					return false;
				}
			}

			ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(experimentID);
			if (!experiment.IsAvailableWhile(GetScienceSituation(vessel), vessel.mainBody))
			{
				if (displayMessage)
					ShoutToScreen("Can't perform experiment here.");

				currentStatus = Status.BadLocation;

				return false;
			}

			currentStatus = Status.Completed;

			return true;
		}

		new public void DeployExperiment()
		{
			if (ReadyToDeploy())
			{
				base.DeployExperiment();
#if false
				RemoveAllResources();
#endif
				currentStatus = Status.Storage;
			}
		}

		new public void DeployAction(KSPActionParam p)
		{
			if (ReadyToDeploy())
			{
				base.DeployAction(p);
#if false
				RemoveAllResources();
#endif
				currentStatus = Status.Storage;
			}
		}

		new public void ResetExperiment()
		{
			base.ResetExperiment();
			Events["StartExperiment"].active = true;
			currentStatus = Status.Idle;
		}

		new public void ResetExperimentExternal()
		{
			base.ResetExperimentExternal();
			Events["StartExperiment"].active = true;
			currentStatus = Status.Idle;
		}

		new public void ResetAction(KSPActionParam p)
		{
			base.ResetAction(p);
			Events["StartExperiment"].active = true;
			currentStatus = Status.Idle;
		}

		/// <summary>
		/// Decays kuarqs each physics update, dependent on halflife and amount present
		/// </summary>
		public override void OnFixedUpdate()
		{
			base.OnFixedUpdate();

			var kuarqs = GetResource(KUARQS);
			if (kuarqs != null)
			{
				if (kuarqHalflife > 0 && kuarqs.maxAmount > 0)
				{
					if (kuarqs.amount < (.99 * kuarqs.maxAmount))
					{
						double decay = Math.Pow(.5, TimeWarp.fixedDeltaTime / kuarqHalflife);
						kuarqDecay = (float)((kuarqs.amount * (1 - decay)) / TimeWarp.fixedDeltaTime);
						kuarqs.amount = kuarqs.amount * decay;
					}
					else
						kuarqDecay = 0;
				}
			}
		}

		new public System.Collections.IEnumerator UpdateStatus()
		{
			while (true)
			{
				// make sure we keep updating while changes are possible
				if (currentStatus == Status.Running
						|| currentStatus == Status.Completed
						|| currentStatus == Status.BadLocation)
					ReadyToDeploy(false);

				yield return new UnityEngine.WaitForSeconds(2f);
			}
		}

		/// <summary>
		/// Displays part info in VAB and SPH thumbnails
		/// </summary>
		public override string GetInfo()
		{
			return base.GetInfo();
#if false
			string result = "Consumed for science:";

			for (int i = 0; i < requirementNames.Count; i++)
			{
				if (requirementAmounts[i] > 0)
				{
					if (result != "") result += "\n  ";
					result += requirementNames[i] + ": " + requirementAmounts[i];

					if (requirementNames[i] == KUARQS)
					{
						result += "\n  - Decay halflife: " + kuarqHalflife + " seconds";

						if (kuarqHalflife > 0)
							result += String.Format("\n  - Production required: {0:F2} k/sec",
								requirementAmounts[i] * (1 - Math.Pow(.5, 1.0 / kuarqHalflife)));
					}
				}
			}
			return result;
#endif
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

		protected virtual float GetMaxAmount(string name)
		{
			for (int i = 0; i < requirementNames.Count; i++)
			{
				if (requirementNames[i].Equals(name))
					return requirementAmounts[i];
			}

			return 0.0f;
		}

		new protected virtual PartResource GetResource(string name)
		{
			return part.Resources.Get(name);
		}

		new protected virtual double GetResourceAmount(string name)
		{
			PartResource res = GetResource(name);
			return (res == null) ? 0 : res.amount;
		}

		protected virtual void AddAllResources()
		{
			foreach (string name in requirementNames)
				AddResource(name);
		}

		protected virtual PartResource AddResource(string name)
		{
			PartResource resource = GetResource(name);
			float max = GetMaxAmount(name);

			if (resource == null)
			{
				ConfigNode node = new ConfigNode("RESOURCE");
				node.AddValue("name", name);
				node.AddValue("amount", 0);
				node.AddValue("maxAmount", max);
				resource = part.AddResource(node);
			}
			else
			{
				resource.maxAmount = max;
			}
			return resource;
		}

#if false
		protected virtual void RemoveAllResources()
		{
			foreach (string name in requirementNames)
				RemoveResource(name);
		}

		protected virtual PartResource RemoveResource(string name)
		{
			PartResource resource = GetResource(name);
			if (resource != null)
				part.Resources.Remove(resource);
			//resource.amount = 0;

			return resource;
		}
#endif

		protected static void ShoutToScreen(string message)
		{
			ScreenMessages.PostScreenMessage(message, 6, ScreenMessageStyle.UPPER_CENTER);
		}

#endregion
	}
}

#endif