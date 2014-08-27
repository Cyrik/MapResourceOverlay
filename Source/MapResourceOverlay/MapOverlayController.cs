using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapResourceOverlay
{
    public class MapOverlayController : ScenarioModule
    {
        private static MapOverlayController _current;
        public static MapOverlayController Current
        {
            get
            {
                if (_current)
                {
                    return _current;
                }
                var game = HighLogic.CurrentGame;
                if (game == null) { return null; }
                var scenario = game.scenarios.Select(s => s.moduleRef).OfType<MapOverlayController>().SingleOrDefault();
                if (scenario != null)
                {
                    return scenario;
                }
                var proto = game.AddProtoScenarioModule(typeof(MapOverlayController), GameScenes.FLIGHT, GameScenes.TRACKSTATION);
                if (proto.targetScenes.Contains(HighLogic.LoadedScene))
                {
                    proto.Load(ScenarioRunner.fetch);
                }

                return game.scenarios.Select(s => s.moduleRef).OfType<MapOverlayController>().SingleOrDefault();
            }
        }
        [KSPField(isPersistant = true)] public int Cutoff;
        [KSPField(isPersistant = true)] public bool Bright = true;
        [KSPField(isPersistant = true)] public bool UseScansat = true;
        [KSPField(isPersistant = true)] public bool Show;

        public void OnDestroy()
        {
            _current = null;
        }
    }
}
