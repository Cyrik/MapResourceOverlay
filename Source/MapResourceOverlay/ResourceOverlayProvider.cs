using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using LibNoise.Unity.Operator;
using UnityEngine;
using Object = System.Object;

namespace MapResourceOverlay
{
    public class ResourceOverlayProvider : OverlayProviderBase
    {
        private ResourceConfig _activeResource;
        private double _displayMax;

        private delegate Object GetResourceAvailabilityByRealResourceNameDelegate(
            int bodyIndex, string resourceName, double lon, double lat);
        
        private Func<object,object> _getAmount;

        private GetResourceAvailabilityByRealResourceNameDelegate _getResourceAvailabilityByRealResourceName;
        private bool _logaritmic;
        private bool _coloredScale;

        public ResourceConfig ActiveResource
        {
            get { return _activeResource; }
            set
            {
                _activeResource = value;
                CalculateBase();
                RequiresRedraw();
            }
        }

        public override void Activate(CelestialBody body)
        {
            base.Activate(body);
            CalculateBase();
            RequiresRedraw();
        }

        private void CalculateBase()
        {
            _displayMax = 0;
            double avg = 0;
            for (int lat = 0; lat < 180; lat++)
            {
                for (int lon = 0; lon < 360; lon++)
                {
                    var amount = (double)_getAmount(_getResourceAvailabilityByRealResourceName(_body.flightGlobalsIndex,
                        ActiveResource.Resource.ResourceName, lon, lat));
                    if (amount > _displayMax)
                    {
                        _displayMax = amount;
                    }
                    avg += amount;
                }
            }
            _displayMax *= 1000000;
            avg = avg/(180*360);
        }

        public override Color32 CalculateColor32(double latitude, double longitude, CelestialBody body, bool useScansat,
            bool bright, double cutoff)
        {
            var scanSat = ScanSatWrapper.Instance;
            if (useScansat && scanSat.Active() && !scanSat.IsCovered(longitude, latitude, body, ActiveResource.Resource))
            {
                return new Color32(0, 0, 0, 0);
            }
            var avail = _getResourceAvailabilityByRealResourceName(body.flightGlobalsIndex,
                ActiveResource.Resource.ResourceName, latitude, longitude);
            var amount = (double)_getAmount(avail) * 1000000;
            if (amount > cutoff)
            {
                if (Logaritmic)
                {
                    amount = ((Math.Log(amount, 2) - (Math.Log(cutoff, 2))) * 255) / ((Math.Log(_displayMax, 2) - (Math.Log(cutoff, 2))));
                }
                else if (_coloredScale)
                {
                    var color = ScanSatWrapper.heightToColor((float) amount, cutoff, (float) _displayMax);
                    color.a = ActiveResource.HighColor.a;
                    return color;
                }
                else
                {
                    amount =((amount - cutoff) * 255) / (_displayMax - cutoff);
                }
                amount = Mathf.Clamp((float) amount, 0f, 255f);
                if (!bright)
                {
                    var r = amount*(ActiveResource.HighColor.r/255.0);
                    var g = amount*(ActiveResource.HighColor.g/255.0);
                    var b = amount*(ActiveResource.HighColor.b/255.0);
                    return new Color32(Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b),
                        ActiveResource.HighColor.a);
                }
                else
                {
                    return new Color32(155, Convert.ToByte(amount), Convert.ToByte(amount), 150);
                }
            }
            return ActiveResource.LowColor;
        }

        public bool Logaritmic
        {
            get { return _logaritmic; }
            set
            {
                if (_logaritmic!= value)
                {
                    _logaritmic = value;
                    RequiresRedraw();
                }
            }
        }

        public override OverlayTooltip TooltipContent(double latitude, double longitude, CelestialBody body)
        {
            var abundance = (double)_getAmount(_getResourceAvailabilityByRealResourceName(body.flightGlobalsIndex,
                ActiveResource.Resource.ResourceName, latitude, longitude));
            return new OverlayTooltip(ActiveResource.Resource.ResourceName,
                new GUIContent("Amount: " + (abundance*1000000.0).ToString("0.0") + "ppm"));
        }

        public override bool IsCoveredAt(double latitude, double longitude, CelestialBody body)
        {
            return ScanSatWrapper.Instance.IsCovered(longitude, latitude, body, ActiveResource.Resource);
        }

        public override string GuiName
        {
            get { return "Resource Map"; }
        }

        public List<ResourceConfig> ColorConfigs { get; set; }

        private void LoadFailsafe()
        {
            var config = new ResourceConfig
            {
                Resource = new Resource("Karbonite"),
                LowColor = new Color32(0, 0, 0, 0),
                HighColor = new Color32(255, 0, 0, 200)
            };
            var config2 = new ResourceConfig
            {
                Resource = new Resource("Ore"),
                LowColor = new Color32(0, 0, 0, 0),
                HighColor = new Color32(0, 255, 0, 200)
            };
            var config3 = new ResourceConfig
            {
                Resource = new Resource("Water", "Aquifer"),
                LowColor = new Color32(0, 0, 0, 0),
                HighColor = new Color32(0, 0, 255, 200)
            };
            var config4 = new ResourceConfig
            {
                Resource = new Resource("Minerals"),
                LowColor = new Color32(0, 0, 0, 0),
                HighColor = new Color32(0, 255, 255, 200)
            };
            var config5 = new ResourceConfig
            {
                Resource = new Resource("Substrate"),
                LowColor = new Color32(0, 0, 0, 0),
                HighColor = new Color32(255, 0, 255, 200)
            };
            ColorConfigs = new List<ResourceConfig> { config, config2, config3, config4, config5 };
            ActiveResource = config;
        }

        public override void Load(ConfigNode node)
        {
            try
            {
                InitiateOrs();
            }
            catch (Exception e)
            {
                this.Log("Couldnt find ORS" + e);
            }
            try
            {
                if (node.HasNode("ResourceOverlay"))
                {
                    var globalSettingsNode = node.GetNode("ResourceOverlay");
                    ActiveResource = ResourceConfig.Load(globalSettingsNode.GetNode("ActiveResource").nodes[0]);
                    var colorConfigsNode = globalSettingsNode.GetNode("colorConfigs");
                    ColorConfigs = new List<ResourceConfig>();
                    foreach (ConfigNode value in colorConfigsNode.nodes)
                    {
                        ColorConfigs.Add(ResourceConfig.Load(value));
                    }
                }
            }
            catch (Exception e)
            {
                this.Log("Could not load config, using default, because " + e);
                LoadFailsafe();
            }
        }

        public override void Save(ConfigNode node)
        {
            var globalSettingsNode = node.AddNode("ResourceOverlay");
            ActiveResource.Save(globalSettingsNode.AddNode("ActiveResource"));
            var colorConfigsNode = globalSettingsNode.AddNode("colorConfigs");
            foreach (var colorConfig in ColorConfigs)
            {
                colorConfig.Save(colorConfigsNode);
            }
        }

        public override bool CanActivate()
        {
            this.Log("?"+ (_getAmount != null));
            return _getAmount != null && _getResourceAvailabilityByRealResourceName != null;
        }

        private void InitiateOrs()
        {
            var orsPlanetaryResoureMapDataType = AssemblyLoader.loadedAssemblies.SelectMany(
                x => x.assembly.GetExportedTypes())
                .FirstOrDefault(x => x.FullName == "OpenResourceSystem.ORSPlanetaryResourceMapData");
            if (orsPlanetaryResoureMapDataType != null)
            {
                var method = orsPlanetaryResoureMapDataType.GetMethod("getResourceAvailabilityByRealResourceName",
                    new[] {typeof (int), typeof (string), typeof (double), typeof (double)});
                if (method != null)
                {
                    var getAmountMethod = method.ReturnType.GetMethod("getAmount");
                    
                    _getResourceAvailabilityByRealResourceName =
                        (GetResourceAvailabilityByRealResourceNameDelegate)
                            Delegate.CreateDelegate(typeof (GetResourceAvailabilityByRealResourceNameDelegate), method);

                    _getAmount = GenerateFunc(getAmountMethod);
                }
            }
        }

        static Func<object, object> GenerateFunc(MethodInfo method)
        {
            var instance = Expression.Parameter(typeof(object), "instance");

            var methodCall = Expression.Call(
                Expression.Convert(instance, method.ReflectedType),
                method
                );

            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(methodCall, typeof(object)),
                instance
                ).Compile();
        }

        public override void DrawGui(MapOverlayGui gui)
        {
            base.DrawGui(gui);
            GUILayout.BeginVertical();
            Logaritmic = GUILayout.Toggle(Logaritmic, "Logarithmic Scale");
            ColoredScale = GUILayout.Toggle(ColoredScale, "Colored Scale");
            GUILayout.Space(15);
            foreach (var res in ColorConfigs)
            {
                if (GUILayout.Button(res.Resource.ResourceName))
                {
                    ActiveResource = res;
                }
            }
            GUILayout.EndVertical();
        }

        public bool ColoredScale
        {
            get { return _coloredScale; }
            set
            {
                if (_coloredScale != value)
                {
                    _coloredScale = value;
                    RequiresRedraw();
                }
                
                
            }
        }
    }
}