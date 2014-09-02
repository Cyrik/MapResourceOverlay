using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MapResourceOverlay
{
    public interface IOverlayProvider :IConfigNode
    {
        Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat, bool bright, double cutoff);
        OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body);
        bool IsCoveredAt(double latitude, double longitude, CelestialBody body);
        string GuiName { get; }
        void Activate(CelestialBody body);
        void Deactivate();
        event EventHandler RedrawRequired;
        void DrawGui(MapOverlayGui gui);
        bool CanActivate();
        void BodyChanged(CelestialBody body);
    }

    public abstract class OverlayProviderBase : IOverlayProvider
    {
        protected CelestialBody _body;

        public virtual void Load(ConfigNode node)
        {
            
        }

        public virtual void Save(ConfigNode node)
        {
            
        }

        public virtual Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat, bool bright,
            double cutoff)
        {
            return new Color32();
        }

        public virtual OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body)
        {
            return new OverlayTooltip("",new GUIContent());
        }

        public virtual bool IsCoveredAt(double latitude, double longitude, CelestialBody body)
        {
            return true;
        }

        protected virtual void RequiresRedraw()
        {
            if (RedrawRequired != null)
            {
                RedrawRequired(this, null);
            }
        }

        public virtual string GuiName { get; private set; }
        public virtual void Activate(CelestialBody body)
        {
            _body = body;
            RequiresRedraw();
        }

        public virtual void Deactivate()
        {
            _body = null;
        }

        public event EventHandler RedrawRequired;
        public virtual void DrawGui(MapOverlayGui gui)
        {
            
        }

        public virtual bool CanActivate()
        {
            return true;
        }

        public virtual void BodyChanged(CelestialBody body)
        {
            _body = body;
        }
    }

    public class AnomalyMapProvider : OverlayProviderBase
    {
        private Dictionary<Coordinates, PQSCity> _pqsCities;

        public override void Activate(CelestialBody body)
        {
            base.Activate(body);
            CalculateAnomalies();
        }

        public override void BodyChanged(CelestialBody body)
        {
            base.BodyChanged(body);
            CalculateAnomalies();
        }

        private void CalculateAnomalies()
        {
            _pqsCities = new Dictionary<Coordinates,PQSCity>();
            PQSCity[] sites = _body.GetComponentsInChildren<PQSCity>(true);
            foreach (var pqsCity in sites)
            {
                _pqsCities[new Coordinates(_body.GetLatitude(pqsCity.transform.position),
                        _body.GetLongitude(pqsCity.transform.position))] = pqsCity;
                
            }
            this.Log(_pqsCities.Aggregate("",(x,y) => x +"    "+ y.Value.name +" lat: "+y.Key.Latitude+" long: "+y.Key.Longitude));
        }

        public override Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat, bool bright,
            double cutoff)
        {
            var anoms = _pqsCities.Keys.Where(x => x.Distance(new Coordinates(latitude, Utilities.ClampDegrees(longitude))) < 2).ToList();
            if (anoms.Count > 0)
            {
                return new Color32(0,255,255,255);
            }
            return new Color32(0,0,0,0);
        }

        public override OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body)
        {
            var anoms = _pqsCities.Keys.Where(x => x.Distance(new Coordinates(latitude, Utilities.ClampDegrees(longitude))) < 2).ToList();
            if (anoms.Count > 0)
            {
                var anom = _pqsCities[anoms.First()];
                return new OverlayTooltip(anom.name,new GUIContent(""));
            }
            return base.TooltipContent(latitude, longitude, body);
        }


        public override string GuiName
        {
            get { return "Anomaly Map"; }
        }
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

    class HeightmapProvider : OverlayProviderBase
    {
        public byte Alpha { get; set; }

        public override Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat,
            bool bright, double cutoff)
        {
            if (useScansat && ScanSatWrapper.Instance.Active() && !IsCoveredAt(latitude, longitude, body))
            {
                return new Color32(0, 0, 0, 0);
            }
            var scanSat = ScanSatWrapper.Instance;
            var color = scanSat.GetElevationColor32(body, longitude, latitude);
            color.a = Alpha;
            return color;
        }

        public override OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body)
        {
            return new OverlayTooltip("",new GUIContent("Height: "+ScanSatWrapper.GetElevation(body, longitude, latitude)+"m"));
        }

        public override bool IsCoveredAt(double latitude, double longitude, CelestialBody body)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, "AltimetryHiRes");
        }

        public override string GuiName { get { return "Height Map"; } }

        public override void Load(ConfigNode node)
        {
            try
            {
                if (!node.HasNode("Heightmap"))
                {
                    Alpha = 100;
                    return;
                }
                var myNode = node.GetNode("Heightmap");
                byte result;
                if (!Byte.TryParse(myNode.GetValue("Alpha"),out result))
                {
                    result = 100;
                }
                Alpha = result;
            }
            catch (Exception e)
            {
                this.Log("Couldnt Load " + GetType().FullName + " " + e);
            }
        }

        public override void Save(ConfigNode node)
        {
            var myNode = node.AddNode("Heightmap");
            myNode.AddValue("Alpha",Alpha);
        }
    }

    class BiomeOverlayProvider : OverlayProviderBase
    {
        public byte Alpha { get; set; }
        public override Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat,
            bool bright, double cutoff)
        {
            if (useScansat && ScanSatWrapper.Instance.Active() && !IsCoveredAt(latitude, longitude, body))
            {
                return new Color32(0,0,0,0);
            }
            var scanSat = ScanSatWrapper.Instance;
            var biome = scanSat.GetBiome(longitude, latitude, body);
            if (biome != null)
            {
                Color32 color = biome.mapColor;
                color.a = Alpha;
                return color;
            }
            return new Color32(0, 0, 0, 0);
        }

        public override OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body)
        {
            var scanSat = ScanSatWrapper.Instance;
            var biome = scanSat.GetBiome(longitude, latitude, body);
            return new OverlayTooltip("",new GUIContent("Biome: "+biome.name));
        }

        public override bool IsCoveredAt(double latitude, double longitude, CelestialBody body)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, "Biome");
        }

        public override string GuiName { get { return "Biome Map"; } }

        public override void Load(ConfigNode node)
        {
            try
            {
                if (!node.HasNode("Biomemap"))
                {
                    Alpha = 100;
                    return;
                }
                var myNode = node.GetNode("Biomemap");
                byte result;
                if (!Byte.TryParse(myNode.GetValue("Alpha"), out result))
                {
                    result = 100;
                }
                Alpha = result;
            }
            catch (Exception e)
            {
                this.Log("Couldnt Load "+GetType().FullName+ " "+e);
            }
        }

        public override void Save(ConfigNode node)
        {
            var myNode = node.AddNode("Biomemap");
            myNode.AddValue("Alpha", Alpha);
        }
    }
}
