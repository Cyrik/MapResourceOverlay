using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MapResourceOverlay
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class ScenarioAdder : MonoBehaviour
    {
        void Start()
        {
            var game = HighLogic.CurrentGame;

            ProtoScenarioModule psm = game.scenarios.Find(s => s.moduleName == typeof(MapOverlay).Name);
            if (psm == null)
            {
                this.Log("Adding the scenario module.");
                game.AddProtoScenarioModule(typeof(MapOverlay),GameScenes.FLIGHT);
            }
            else
            {
                if (psm.targetScenes.All(s => s != GameScenes.FLIGHT))
                {
                    psm.targetScenes.Add(GameScenes.FLIGHT);
                }
            }
        }
    }
}
