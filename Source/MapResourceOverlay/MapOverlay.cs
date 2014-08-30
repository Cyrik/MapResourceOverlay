using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using KSP.IO;
using Toolbar;
using UnityEngine;
using OpenResourceSystem;

namespace MapResourceOverlay
{
    public class MapOverlay : ScenarioModule
    {
        private CelestialBody _body;
        private Mesh _mesh;

        private List<ResourceConfig> _resources;

        private IButton _mapOverlayButton;
        private MapOverlayGui _gui;
        private bool _changed;
        private ResourceConfig _selectedResourceName;
        private Coordinates _mouseCoords;
        private CelestialBody _targetBody;
        private delegate bool IsCoveredDelegate(double lon, double lat, CelestialBody body, int mask);

        private IsCoveredDelegate _scansatIsCoveredDelegate;
        private Type _scansatEnum;
        private Transform _origTransform;

        private Vector2 _mouse;
        private int _toolTipId;
        private int _currentLat;
        [KSPField(isPersistant = true)]
        public int cutoff = 20;
        [KSPField(isPersistant = true)]
        public bool bright;
        [KSPField(isPersistant = true)]
        public bool useScansat;
        [KSPField(isPersistant = true)]
        public bool show = true;
        [KSPField(isPersistant = true)] public bool showTooltip = true;
        private GlobalSettings _globalSettings;


        public int Cutoff
        {
            get { return cutoff; }
            set
            {
                if (cutoff != value)
                {
                    cutoff = value;
                    _changed = true;
                }
            }
        }
        public bool Bright
        {
            get { return bright; }
            set {
                if (bright != value)
                {
                    bright = value; _changed = true;
                }
                }
        }
        public bool UseScansat
        {
            get { return useScansat; }
            set
            {
                if (useScansat != value)
                {
                    useScansat = value;
                    _changed = true;
                }
            }
        }
        public bool Show
        {
            get { return show; }
            set
            {
                if (show != value)
                {
                    show = value;
                    _changed = true;
                }
            }
        }

        public override void OnAwake()
        {
            this.Log("Awaking");
            _origTransform = gameObject.transform.parent;
            var filter = gameObject.AddComponent<MeshFilter>();
            if (filter != null)
            {
                _mesh = filter.mesh;
            }
            else
            {
                _mesh = gameObject.GetComponent<MeshFilter>().mesh;
            }
            _globalSettings = new GlobalSettings();
            gameObject.AddComponent<MeshRenderer>();
            
            base.OnAwake();
            _mapOverlayButton = ToolbarManager.Instance.add("MapResourceOverlay", "ResourceOverlay");
            _mapOverlayButton.TexturePath = "MapResourceOverlay/Assets/MapOverlayIcon";
            _mapOverlayButton.ToolTip = "Map Resource Overlay";
            _mapOverlayButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
            _mapOverlayButton.OnClick += e => ToggleGui();
            _toolTipId = new System.Random().Next(65536) + Assembly.GetExecutingAssembly().GetName().Name.GetHashCode() + "tooltip".GetHashCode();
            GameEvents.onHideUI.Add(MakeInvisible);
            GameEvents.onShowUI.Add(MakeVisible);
        }

        public void ToggleGui()
        {
            if (_gui != null)
            {
                _gui.SetVisible(false);
            }
            _gui = new MapOverlayGui(this);
            _gui.SetVisible(true);
        }


        public void OnDisable()
        {
            this.Log("disabling MapOverlay");
            
        }
        public void OnDestroy()
        {
            
            this.Log("destroying MapResourceOverlay");
            gameObject.transform.parent = _origTransform;
            _mapOverlayButton.Destroy();
            if (_gui != null)
            {
                _gui.Model = null;
            }
            Show = false;
            gameObject.renderer.enabled = false;
            
            GameEvents.onHideUI.Remove(MakeInvisible);
            GameEvents.onShowUI.Remove(MakeVisible);
            
        }

        public void MakeVisible()
        {
            Show = true;
        }

        public void MakeInvisible()
        {
            Show = false;
        }

        public void Start()
        {
            this.Log("MapResourceOverlay starting");
            gameObject.layer = 10;
            

            SelectedResourceName = Resources[0];
            InitializeScansatIntegration();
        }



        private static CelestialBody GetTargetBody(MapObject target)
        {
            if (target.type == MapObject.MapObjectType.CELESTIALBODY)
                return target.celestialBody;
            if (target.type == MapObject.MapObjectType.MANEUVERNODE)
                return target.maneuverNode.patch.referenceBody;
            if (target.type == MapObject.MapObjectType.VESSEL)
                return target.vessel.mainBody;
            return null;
        }

        public List<ResourceConfig> Resources
        {
            get { return _resources; }
        }

        public ResourceConfig SelectedResourceName
        {
            get { return _selectedResourceName; }
            set
            {
                _selectedResourceName = value;
                _changed = true;
            }
        }

        public bool ShowTooltip
        {
            get { return showTooltip; }
            set { showTooltip = value; }
        }

        private int GetScansatId(string resourceName)
        {
            if (_scansatEnum != null)
            {
                return (int)_scansatEnum.GetField(resourceName).GetValue(null);
            }
            return 0;
        }

        private void InitializeScansatIntegration()
        {
            var scanutil = AssemblyLoader.loadedAssemblies.SelectMany(x => x.assembly.GetExportedTypes())
                        .FirstOrDefault(x => x.FullName == "SCANsat.SCANUtil");
            var scandata =
                AssemblyLoader.loadedAssemblies.SelectMany(x => x.assembly.GetExportedTypes())
                    .FirstOrDefault(x => x.FullName == "SCANsat.SCANdata");
            if (scanutil != null && scandata != null)
            {
                var method = scanutil.GetMethod("isCovered",
                    new[] { typeof(double), typeof(double), typeof(CelestialBody), typeof(int) });
                if (method != null)
                {
                    _scansatIsCoveredDelegate = (IsCoveredDelegate)Delegate.CreateDelegate(typeof(IsCoveredDelegate), method);
                }
                var tester = scandata.GetNestedType("SCANtype");
                _scansatEnum = tester;

            }
        }

        public void Update()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT)
            {
                gameObject.renderer.enabled = (false);
            }
            else UpdateMapView();
        }
        private void UpdateMapView()
        {
            if (!show || MapView.MapCamera == null)
            {
                gameObject.renderer.enabled = false;
            }
            else
            {
                gameObject.renderer.enabled = true;
                _targetBody = GetTargetBody(MapView.MapCamera.target);

                if (_targetBody != null && (_targetBody != _body || _changed))
                {
                    this.Log("Drawing at " + _targetBody.name + " because " + (_targetBody != _body ? "body changed." : "something else changed."));
                    _changed = false;
                    var dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var radii = System.IO.File.ReadAllLines(dir + "/Assets/Radii.cfg");
                    var radius = float.Parse(radii.First(x => x.StartsWith(_targetBody.GetName())).Split('=')[1]);
                    _body = _targetBody;
                    evilmesh(_targetBody, SelectedResourceName);
                    gameObject.renderer.material = new Material(System.IO.File.ReadAllText(dir + "/Assets/MapOverlayShader.txt"));
                    gameObject.renderer.enabled = true;
                    gameObject.renderer.castShadows = false;
                    gameObject.transform.parent = ScaledSpace.Instance.scaledSpaceTransforms.FirstOrDefault(t => t.name == _body.bodyName);
                    gameObject.layer = 10;
                    gameObject.transform.localScale = Vector3.one * 1000f * radius;
                    gameObject.transform.localPosition = (Vector3.zero);
                    gameObject.transform.localRotation = (Quaternion.identity);
                }
                if (_targetBody != null && useScansat && _scansatIsCoveredDelegate != null)
                {
                    RecalculateColors(_targetBody, SelectedResourceName);
                }
            }
        }

        private void RecalculateColors(CelestialBody targetBody, ResourceConfig resource)
        {
            const int nbLong = 360;
            #region Vertices
            var colors = _mesh.colors32;

            colors[0] = CalculateColor32At(targetBody, resource, 90, 0);
            for (int lat = _currentLat; lat < _currentLat + 2; lat++)
            {
                for (int lon = 0; lon <= nbLong; lon++)
                {
                    colors[lon + lat * (nbLong + 1) + 1] = CalculateColor32At(targetBody, resource, 90 - lat, lon);
                }
            }
            colors[colors.Length - 1] = CalculateColor32At(targetBody, resource, -90, 0);
            #endregion
            _currentLat += 2;
            if (_currentLat >= 180)
            {
                _currentLat = 0;
            }
            _mesh.colors32 = colors;
        }

        public void OnGUI()
        {
            bool paused = false;
            if (HighLogic.LoadedSceneIsFlight)
            {
                try
                {
                    paused = PauseMenu.isOpen || FlightResultsDialog.isDisplaying;
                }
                catch (Exception)
                {
                    // ignore the error and assume the pause menu is not open
                }
            }
            if (_targetBody != FlightGlobals.ActiveVessel.mainBody || paused || !showTooltip) //dont show tooltips on different bodys or ORS lags
            {
                return;
            }
            if (show && _targetBody != null)
            {
                if (Event.current.type == EventType.Layout)
                {
                    try
                    {
                        _mouseCoords = GetMouseCoordinates(_targetBody);
                        _mouse = Event.current.mousePosition;
                        if (useScansat && _scansatIsCoveredDelegate != null && _mouseCoords != null &&
                            !_scansatIsCoveredDelegate(_mouseCoords.Longitude, _mouseCoords.Latitude, _targetBody, GetScansatId(_selectedResourceName.Resource.ScansatName)))
                        {
                            _mouseCoords = null;
                        }
                    }
                    catch (NullReferenceException e)
                    {
                        this.Log("layout nullref" + e);
                    }
                }
                if (_mouseCoords != null)
                {

                    _toolTipId = 0;
                    var abundance = ORSPlanetaryResourceMapData.getResourceAvailabilityByRealResourceName(
                        _targetBody.flightGlobalsIndex, _selectedResourceName.Resource.ResourceName, _mouseCoords.Latitude, _mouseCoords.Longitude)
                        .getAmount();
                    string abundanceString = (abundance * 1000000.0).ToString("0.0") + "ppm";
                    
                    GUI.Window(_toolTipId, new Rect(_mouse.x + 10, _mouse.y + 10, 200f, 55f), i =>
                    {
                        GUI.Label(new Rect(5, 10, 190, 20), "Long: " + _mouseCoords.Longitude.ToString("###.##") + " Lat: " + _mouseCoords.Latitude.ToString("####.##"));
                        GUI.Label(new Rect(5, 30, 190, 20), "Amount: " + abundanceString);

                    },
                    _selectedResourceName.Resource.ResourceName);
                }
            }
        }
        public static Coordinates GetMouseCoordinates(CelestialBody targetBody)
        {
            Ray mouseRay = PlanetariumCamera.Camera.ScreenPointToRay(Input.mousePosition);
            mouseRay.origin = ScaledSpace.ScaledToLocalSpace(mouseRay.origin);
            var bodyToOrigin = mouseRay.origin - targetBody.position;
            double curRadius = targetBody.pqsController.radiusMax;
            double lastRadius = 0;
            int loops = 0;
            while (loops < 50)
            {
                Vector3d relSurfacePosition;
                if (PQS.LineSphereIntersection(bodyToOrigin, mouseRay.direction, curRadius, out relSurfacePosition))
                {
                    var surfacePoint = targetBody.position + relSurfacePosition;
                    double alt = targetBody.pqsController.GetSurfaceHeight(
                        QuaternionD.AngleAxis(targetBody.GetLongitude(surfacePoint), Vector3d.down) * QuaternionD.AngleAxis(targetBody.GetLatitude(surfacePoint), Vector3d.forward) * Vector3d.right);
                    double error = Math.Abs(curRadius - alt);
                    if (error < (targetBody.pqsController.radiusMax - targetBody.pqsController.radiusMin) / 100)
                    {
                        return new Coordinates(targetBody.GetLatitude(surfacePoint), Utilities.ClampDegrees(targetBody.GetLongitude(surfacePoint)));
                    }
                    else
                    {
                        lastRadius = curRadius;
                        curRadius = alt;
                        loops++;
                    }
                }
                else
                {
                    if (loops == 0)
                    {
                        break;
                    }
                    else
                    { // Went too low, needs to try higher
                        curRadius = (lastRadius * 9 + curRadius) / 10;
                        loops++;
                    }
                }
            }
            return null;
        }


        private void evilmesh(CelestialBody targetBody, ResourceConfig resource)
        {

            _mesh.Clear();

            const float radius = 1f;
            // Longitude |||
            const int nbLong = 360;
            // Latitude ---
            const int nbLat = 180;

            #region Vertices
            Vector3[] vertices = new Vector3[(nbLong + 1) * nbLat + 2];
            var colors = new Color32[(nbLong + 1) * nbLat + 2];
            float _pi = Mathf.PI;
            float _2pi = _pi * 2f;

            vertices[0] = Vector3.up * radius;
            colors[0] = CalculateColor32At(targetBody, resource, 90, 0);
            for (int lat = 0; lat < nbLat; lat++)
            {
                float a1 = _pi * (lat + 1) / (nbLat + 1);
                float sin1 = Mathf.Sin(a1);
                float cos1 = Mathf.Cos(a1);

                for (int lon = 0; lon <= nbLong; lon++)
                {
                    float a2 = _2pi * (lon == nbLong ? 0 : lon) / nbLong;
                    float sin2 = Mathf.Sin(a2);
                    float cos2 = Mathf.Cos(a2);

                    vertices[lon + lat * (nbLong + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
                    colors[lon + lat * (nbLong + 1) + 1] = CalculateColor32At(targetBody, resource, 90 - lat, lon);
                }
            }
            vertices[vertices.Length - 1] = Vector3.up * -radius;
            colors[vertices.Length - 1] = CalculateColor32At(targetBody, resource, -90, 0);
            #endregion

            #region Normales
            Vector3[] normales = new Vector3[vertices.Length];
            for (int n = 0; n < vertices.Length; n++)
                normales[n] = vertices[n].normalized;
            #endregion

            #region UVs
            Vector2[] uvs = new Vector2[vertices.Length];
            uvs[0] = Vector2.up;
            uvs[uvs.Length - 1] = Vector2.zero;
            for (int lat = 0; lat < nbLat; lat++)
                for (int lon = 0; lon <= nbLong; lon++)
                    uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
            #endregion

            #region Triangles
            int nbFaces = vertices.Length;
            int nbTriangles = nbFaces * 2;
            int nbIndexes = nbTriangles * 3;
            int[] triangles = new int[nbIndexes];

            //Top Cap
            int i = 0;
            for (int lon = 0; lon < nbLong; lon++)
            {
                triangles[i++] = lon + 2;
                triangles[i++] = lon + 1;
                triangles[i++] = 0;
            }

            //Middle
            for (int lat = 0; lat < nbLat - 1; lat++)
            {
                for (int lon = 0; lon < nbLong; lon++)
                {
                    int current = lon + lat * (nbLong + 1) + 1;
                    int next = current + nbLong + 1;

                    triangles[i++] = next + 1;
                    triangles[i++] = current + 1;
                    triangles[i++] = current;

                    triangles[i++] = next;
                    triangles[i++] = next + 1;
                    triangles[i++] = current;
                }
            }

            //Bottom Cap
            for (int lon = 0; lon < nbLong; lon++)
            {
                triangles[i++] = vertices.Length - 1;
                triangles[i++] = vertices.Length - (lon + 2) - 1;
                triangles[i++] = vertices.Length - (lon + 1) - 1;
            }
            #endregion

            _mesh.vertices = vertices;
            _mesh.normals = normales;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.colors32 = colors;
            _mesh.RecalculateBounds();
            _mesh.Optimize();
        }

        private Color32 CalculateColor32At(CelestialBody body, ResourceConfig resource, double latitude,
            double longitude)
        {
            if (useScansat && _scansatIsCoveredDelegate != null &&
                !_scansatIsCoveredDelegate(longitude, latitude, body, GetScansatId(resource.Resource.ScansatName)))
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

        public override void OnLoad(ConfigNode node)
        {
            this.Log("loading");
            base.OnLoad(node);
            var globalConfigFilename = IOUtils.GetFilePathFor(GetType(), "MapResourceOverlay.cfg");
            if (File.Exists<MapOverlay>(globalConfigFilename))
            {
                var globalNode = ConfigNode.Load(globalConfigFilename);
                _globalSettings.Load(globalNode);
            }
            _resources = _globalSettings.ColorConfigs;
        }

        public override void OnSave(ConfigNode node)
        {
            this.Log("saving");
            base.OnSave(node);
            var gloablNode = new ConfigNode();
            _globalSettings.Save(gloablNode);
            gloablNode.Save(IOUtils.GetFilePathFor(GetType(), "MapResourceOverlay.cfg"));

        }
    }

    public class GlobalSettings : IConfigNode
    {
        public List<ResourceConfig> ColorConfigs { get; set; }

        public GlobalSettings()
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
            ColorConfigs = new List<ResourceConfig>{config, config2,config3,config4,config5};
        }

        public void Load(ConfigNode node)
        {
            
            try
            {
                var globalSettingsNode = node.GetNode("globalSettings");
                var colorConfigsNode = globalSettingsNode.GetNode("colorConfigs");
                ColorConfigs = new List<ResourceConfig>();
                foreach (ConfigNode value in colorConfigsNode.nodes)
                {
                    ColorConfigs.Add(ResourceConfig.Load(value));
                }
            }
            catch (Exception e)
            {
                this.Log("Globalconfig broken"+e);
            }
        }

        public void Save(ConfigNode node)
        {
            
            var globalSettingsNode = node.AddNode("globalSettings");
            var colorConfigsNode = globalSettingsNode.AddNode("colorConfigs");
            foreach (var colorConfig in ColorConfigs)
            {
                colorConfig.Save(colorConfigsNode);
            }
            
        }
        
    }
    public class ResourceConfig
    {
        public Resource Resource { get; set; }
        public Color32 LowColor { get; set; }
        public Color32 HighColor { get; set; }
        public static ResourceConfig Load(ConfigNode configNode)
        {
            var res = new ResourceConfig
            {
                Resource = Resource.DeserializeResource(configNode.GetValue("Resource")),
                LowColor = StringToColor(configNode.GetValue("LowColor")),
                HighColor = StringToColor(configNode.GetValue("HighColor"))
            };
            return res;
        }

        private static Color StringToColor(string str)
        {
            var strArr = str.TrimStart('(').TrimEnd(')').Split(',');
            byte r, g, b, a;
            if (strArr.Count() == 4 && Byte.TryParse(strArr[0], out r) && Byte.TryParse(strArr[1], out g) && Byte.TryParse(strArr[2], out b) && Byte.TryParse(strArr[3], out a))
            {
                return new Color32(r, g, b, a);
            }
            return new Color32();
        }

        public void Save(ConfigNode node)
        {
            var colorConfigNode = node.AddNode(Resource.ResourceName);
            colorConfigNode.AddValue("Resource", Resource.Serialize());
            colorConfigNode.AddValue("LowColor", ColorToString(LowColor));
            colorConfigNode.AddValue("HighColor", ColorToString(HighColor));
        }

        private string ColorToString(Color32 color)
        {
            return "(" + color.r + "," + color.g + "," + color.b + "," + color.a + ")";
        }
    }
}
