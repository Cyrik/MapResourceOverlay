using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenResourceSystem;
using UnityEngine;

namespace MapResourceOverlay
{
    public interface IOverlayProvider
    {
        Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, ResourceConfig config, bool useScansat, bool bright, double cutoff);
        OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body, ResourceConfig config);
        bool IsCoveredAt(double latitude, double longitude, CelestialBody body, ResourceConfig config);
        string GuiName { get; }
    }

    public class OverlayTooltip
    {
        public OverlayTooltip(string title, GUIContent content, Vector2 size = new Vector2())
        {
            Title = title;
            Content = content;
            Size = size;
        }

        public string Title { get; set; }
        public GUIContent Content { get; set; }
        public Vector2 Size { get; set; }
    }

    class HightmapProvider : IOverlayProvider
    {
        public Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, ResourceConfig config, bool useScansat,
            bool bright, double cutoff)
        {
            if (useScansat && ScanSatWrapper.Instance.Active() && !IsCoveredAt(latitude, longitude, body, config))
            {
                return new Color32(0, 0, 0, 0);
            }
            var scanSat = ScanSatWrapper.Instance;
            var dict = new Dictionary<string, int>();
            return scanSat.GetElevationColor32(body, longitude, latitude);
        }

        public OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            return new OverlayTooltip("",new GUIContent("Height: "+ScanSatWrapper.GetElevation(body, longitude, latitude)+"m"));
        }

        public bool IsCoveredAt(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, "AltimetryHiRes");
        }

        public string GuiName { get { return "Hight Map"; } }
    }

    class BiomeOverlayProvider : IOverlayProvider
    {
        public Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, ResourceConfig config, bool useScansat,
            bool bright, double cutoff)
        {
            if (useScansat && ScanSatWrapper.Instance.Active() && !IsCoveredAt(latitude, longitude, body, config))
            {
                return new Color32(0,0,0,0);
            }
            var scanSat = ScanSatWrapper.Instance;
            var biome = scanSat.GetBiome(longitude, latitude, body);
            if (biome != null)
            {
                var color = biome.mapColor;
                color.a = 0.30f;
                return color;
            }
            return new Color32(0, 0, 0, 0);
        }

        public OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            var scanSat = ScanSatWrapper.Instance;
            var biome = scanSat.GetBiome(longitude, latitude, body);
            return new OverlayTooltip("",new GUIContent("Biome: "+biome.name));
        }

        public bool IsCoveredAt(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, "Biome");
        }

        public string GuiName { get { return "Biome Map"; } }
    }

    public class ResourceOverlayProvider : IOverlayProvider
    {
        public Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, ResourceConfig resource, bool useScansat, bool bright, double cutoff)
        {
            var scanSat = ScanSatWrapper.Instance;
            if (useScansat && scanSat.Active() && !scanSat.IsCovered(longitude, latitude, body, resource.Resource))
            {
                return new Color32(0, 0, 0, 0);
            }
            var avail = ORSPlanetaryResourceMapData.getResourceAvailabilityByRealResourceName(body.flightGlobalsIndex, resource.Resource.ResourceName, latitude, longitude);
            var amount = avail.getAmount();
            amount = amount * 1000000;
            if (amount > cutoff)
            {
                amount = Mathf.Clamp((float)amount, 0f, 255f);
                if (!bright)
                {
                    var r = amount * (resource.HighColor.r / 255.0);
                    var g = amount * (resource.HighColor.g / 255.0);
                    var b = amount * (resource.HighColor.b / 255.0);
                    return new Color32(Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b), resource.HighColor.a);
                }
                else
                {
                    return new Color32(255, Convert.ToByte(amount), Convert.ToByte(amount), 150);
                }
            }
            return resource.LowColor;
        }

        public OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            var abundance = ORSPlanetaryResourceMapData.getResourceAvailabilityByRealResourceName(
                        body.flightGlobalsIndex, config.Resource.ResourceName, latitude, longitude)
                        .getAmount();
            return new OverlayTooltip(config.Resource.ResourceName,new GUIContent("Amount: "+(abundance * 1000000.0).ToString("0.0") + "ppm"));
        }

        public bool IsCoveredAt(double latitude, double longitude, CelestialBody body, ResourceConfig config)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, config.Resource);
        }

        public string GuiName { get { return "Resource Map"; } }

    }
}
