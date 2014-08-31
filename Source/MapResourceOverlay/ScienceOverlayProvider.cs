using System;
using System.Linq;
using UnityEngine;

namespace MapResourceOverlay
{
    class ScienceOverlayProvider : IOverlayProvider
    {
        public Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, ResourceConfig config, bool useScansat,
            bool bright, double cutoff)
        {
            var situation = ExperimentSituations.InSpaceLow;
            var biome = ScanSatWrapper.Instance.GetBiome(longitude, latitude, body);
            var experiments = ResearchAndDevelopment.GetExperimentIDs().Select(ResearchAndDevelopment.GetExperiment)
                .Where(x => x.biomeMask != 0 && x.situationMask != 0)
                .Where(x => x.IsAvailableWhile(situation,body))
                .GroupBy(x => x.BiomeIsRelevantWhile(situation))
                .SelectMany(x => x.Select(y => new{exp = y, subj = ResearchAndDevelopment.GetExperimentSubject(y,situation,body,x.Key ? biome.name:"")}))
                .Select(x => ResearchAndDevelopment.GetScienceValue(x.exp.dataScale * x.exp.baseValue,x.subj))
                .Sum();
            return new Color32(Convert.ToByte(Mathf.Clamp(experiments,0,255)),0,0,150);

        }

        public OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            var biome = ScanSatWrapper.Instance.GetBiome(longitude, latitude, body);
            var test = Enum.GetValues(typeof(ExperimentSituations)).Cast<ExperimentSituations>()
                .Select(situation => ResearchAndDevelopment.GetExperimentIDs().Select(ResearchAndDevelopment.GetExperiment)
                    .Where(x => x.biomeMask != 0 && x.situationMask != 0)
                    .Where(x => x.IsAvailableWhile(situation, body))
                    .GroupBy(x => x.BiomeIsRelevantWhile(situation))
                    .SelectMany(x => x.Select(y => new { exp = y, subj = ResearchAndDevelopment.GetExperimentSubject(y, situation, body, x.Key ? biome.name : "") }))
                    .Select(x => new {value =ResearchAndDevelopment.GetScienceValue(x.exp.dataScale * x.exp.baseValue, x.subj), exp = x})
                    .Aggregate(new {str = "", total = 0f}, 
                        (a,x) => new {str = a.str+" n: "+x.exp.exp.experimentTitle+" v: "+x.value, total = a.total + x.value},
                        result => "Situation "+Enum.GetName(typeof(ExperimentSituations),situation)+"total: "+result.total+result.str))
                .Aggregate("", (str,x) => str+x+"\n");

            return new OverlayTooltip(biome.name,new GUIContent(test), new Vector2(500,500));
        }

        public bool IsCoveredAt(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, "AltimetryHiRes");
        }

        public string GuiName { get { return "Science Map"; }  }
        private float GetBodyScienceValueMultipler(ExperimentSituations situations,CelestialBody body)
        {
            switch (situations)
            {
                case ExperimentSituations.FlyingHigh:
                    return body.scienceValues.FlyingHighDataValue;
                case ExperimentSituations.FlyingLow:
                    return body.scienceValues.FlyingLowDataValue;
                case ExperimentSituations.InSpaceHigh:
                    return body.scienceValues.InSpaceHighDataValue;
                case ExperimentSituations.InSpaceLow:
                    return body.scienceValues.InSpaceLowDataValue;
                case ExperimentSituations.SrfLanded:
                    return body.scienceValues.LandedDataValue;
                case ExperimentSituations.SrfSplashed:
                    return body.scienceValues.SplashedDataValue;
                default:
                    return 0f;
            }

        }
    }
}