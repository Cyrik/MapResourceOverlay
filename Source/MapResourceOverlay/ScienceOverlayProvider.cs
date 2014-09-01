using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapResourceOverlay
{
    class ScienceOverlayProvider : OverlayProviderBase
    {
        private ScienceOverlayView _scienceOverlayView;
        private ExperimentSituations _situation;
        private List<ScienceExperiment> _experiments; 
        public ScienceOverlayProvider()
        {
            _experiments = new List<ScienceExperiment>();
            _situation = ExperimentSituations.FlyingHigh;
        }

        public override Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat, bool bright, double cutoff)
        {
            if (useScansat && ScanSatWrapper.Instance.Active() && !IsCoveredAt(latitude,longitude,body))
            {
                return new Color32(0,0,0,0);
            }
            var biome = ScanSatWrapper.Instance.GetBiome(longitude, latitude, body);
            var experiments = _experiments
                        .GroupBy(x => x.BiomeIsRelevantWhile(_situation))
                        .SelectMany(x => x.Select(y => new{exp = y, subj = ResearchAndDevelopment.GetExperimentSubject(y,_situation,body,x.Key ? biome.name:"")}))
                        .Select(x => ResearchAndDevelopment.GetScienceValue(x.exp.dataScale * x.exp.baseValue,x.subj))
                        .Sum();
            return new Color32(Convert.ToByte(Mathf.Clamp(experiments,0,255)),0,0,150);
        }

        public override OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body)
        {
            var biome = ScanSatWrapper.Instance.GetBiome(longitude, latitude, body);
            var all = Enum.GetValues(typeof(ExperimentSituations)).Cast<ExperimentSituations>()
                .Select(situation => ResearchAndDevelopment.GetExperimentIDs().Select(ResearchAndDevelopment.GetExperiment)
                    .Where(x => x.biomeMask != 0 && x.situationMask != 0)
                    .Where(x => x.IsAvailableWhile(situation, body))
                    .GroupBy(x => x.BiomeIsRelevantWhile(situation))
                    .SelectMany(x => x.Select(y => new { exp = y, subj = ResearchAndDevelopment.GetExperimentSubject(y, situation, body, x.Key ? biome.name : "") }))
                    .Select(x => new { value = ResearchAndDevelopment.GetScienceValue(x.exp.dataScale * x.exp.baseValue, x.subj), exp = x })
                    .Aggregate(new { str = "", total = 0f },
                        (a, x) => new { str = a.str + " name: " + x.exp.exp.experimentTitle + " value: " + x.value, total = a.total + x.value },
                        result => "Situation " + Enum.GetName(typeof(ExperimentSituations), situation) + " total: " + result.total + result.str))
                .Aggregate("", (str, x) => str + x + "\n");
            var main = ResearchAndDevelopment.GetExperimentIDs().Select(ResearchAndDevelopment.GetExperiment)
                    .Where(x => x.biomeMask != 0 && x.situationMask != 0)
                    .Where(x => x.IsAvailableWhile(_situation, body))
                    .GroupBy(x => x.BiomeIsRelevantWhile(_situation))
                    .SelectMany(x => x.Select(y => new { exp = y, subj = ResearchAndDevelopment.GetExperimentSubject(y, _situation, body, x.Key ? biome.name : "") }))
                    .Select(x => new { value = ResearchAndDevelopment.GetScienceValue(x.exp.dataScale * x.exp.baseValue, x.subj), exp = x })
                    .Aggregate(new { str = "", total = 0f },
                        (a, x) => new { str = a.str + " name: " + x.exp.exp.experimentTitle + " value: " + x.value, total = a.total + x.value },
                        result => "Situation " + Enum.GetName(typeof(ExperimentSituations), _situation) + " total: " + result.total + result.str);
            return new OverlayTooltip(biome.name,new GUIContent(main+"\n\n"+all), new Vector2(500,500));
        }

        public override bool IsCoveredAt(double latitude, double longitude, CelestialBody body)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, "AltimetryHiRes");
        }

        public override void Activate(CelestialBody body)
        {
            base.Activate(body);
            if (_scienceOverlayView != null)
            {
                _scienceOverlayView.SetVisible(false);
            }
        }

        public override string GuiName { get { return "Science Map"; }  }

        public ExperimentSituations Situation
        {
            get { return _situation; }
            set
            {
                _situation = value; 
                SetExperiments(_situation);
                RequiresRedraw();
            }
        }
        
        public override void DrawGui(MapOverlayGui window)
        {
            if (GUILayout.Button("Science Map Options"))
            {
                _scienceOverlayView = new ScienceOverlayView(this);
            }
        }

        public void SetExperiments(ExperimentSituations situations)
        {
            _experiments = ResearchAndDevelopment.GetExperimentIDs().Select(ResearchAndDevelopment.GetExperiment)
                .Where(x => x.biomeMask != 0 && x.situationMask != 0)
                .Where(x => x.IsAvailableWhile(situations, _body)).ToList();
        }

    }

    internal class ScienceOverlayView : Window<ScienceOverlayView>
    {
        private readonly ScienceOverlayProvider _model;

        public ScienceOverlayView(ScienceOverlayProvider model)
            : base("Science Map Options", 250, 400)
        {
            _model = model;
            SetVisible(true);
        }

        protected override void DrawWindowContents(int windowId)
        {
            GUILayout.BeginVertical();
            foreach (var situation in Enum.GetValues(typeof(ExperimentSituations)).Cast<ExperimentSituations>())
            {
                if (GUILayout.Button(Enum.GetName(typeof(ExperimentSituations),situation)))
                {
                    _model.Situation = situation;
                }
            }
            GUILayout.EndVertical();
        }
    }
}