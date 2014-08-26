﻿using System;
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
    [KSPAddon(KSPAddon.Startup.Flight, true)]
    public class MapOverlay : MonoBehaviour
    {
        private CelestialBody _body;
        private Mesh _mesh;

        private readonly List<Resource> _resources = new List<Resource>
        {
            new Resource("Karbonite"),
            new Resource("Minerals"),
            new Resource("Ore"),
            new Resource("Substrate"),
            new Resource("Water","Aquifer")
        };

        private readonly IButton _mapOverlayButton;
        private MapOverlayGui _gui;
        private bool _changed;
        private Resource _selectedResourceName;
        private Coordinates _mouseCoords;
        private CelestialBody targetBody;
        private double _lastRedraw;
        private delegate bool IsCoveredDelegate(double lon, double lat, CelestialBody body, int mask);

        private IsCoveredDelegate _scansatIsCoveredDelegate;
        private Type _scansatEnum;
        [KSPField(isPersistant = true)] public bool _show;

        private Vector2 mouse;
        [KSPField(isPersistant = true)] public bool _useScansat;
        private int _toolTipId;
        private int _currentLat;


        public MapOverlay()
        {
            _mapOverlayButton = ToolbarManager.Instance.add("MapResourceOverlay", "ResourceOverlay");
            _mapOverlayButton.TexturePath = "MapResourceOverlay/Assets/MapOverlayIcon";
            _mapOverlayButton.ToolTip = "Map Resource Overlay";
            _mapOverlayButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
            _mapOverlayButton.OnClick += e => ToggleGui();
            DontDestroyOnLoad(this);
            _toolTipId = new System.Random().Next(65536) + Assembly.GetExecutingAssembly().GetName().Name.GetHashCode() + "tooltip".GetHashCode();
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

        public void OnDestroy()
        {
            Debug.Log("destroying MapResourceOverlay");
            _mapOverlayButton.Destroy();
            if (_gui != null)
            {
                _gui.Model = null;
                _gui.SetVisible(false);
            }
            gameObject.renderer.enabled = false;
        }

        public void Awake()
        {
            
        }
        public void Start()
        {
            Debug.Log("MapResourceOverlay starting");
            gameObject.layer = 10;
            gameObject.AddComponent<MeshRenderer>();
            _mesh = gameObject.AddComponent<MeshFilter>().mesh;
            SelectedResourceName = Resources[0];
            InitializeScansatIntegration();
        }

        public void Update()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT)
            {
                gameObject.renderer.enabled = (false);
            }
            else
                updateMapView();
        }

        private void updateMapView()
        {
            if (!Show || MapView.MapCamera == null)
            {
                gameObject.renderer.enabled = false;
            }
            else
            {
                gameObject.renderer.enabled = true;
                targetBody = GetTargetBody(MapView.MapCamera.target);

                if (targetBody != null && (targetBody != _body || _changed))
                {
                    _changed = false;
                    Debug.Log("new target: " + targetBody.GetName());
                    var dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var radii = System.IO.File.ReadAllLines(dir + "/Assets/Radii.cfg");
                    var radius = float.Parse(radii.First(x => x.StartsWith(targetBody.GetName())).Split('=')[1]);
                    _body = targetBody;
                    evilmesh(targetBody, SelectedResourceName);
                    gameObject.renderer.material = new Material(System.IO.File.ReadAllText(dir + "/Assets/MapOverlayShader.txt"));
                    gameObject.renderer.enabled = true;
                    gameObject.renderer.castShadows = false;
                    gameObject.transform.parent = ScaledSpace.Instance.scaledSpaceTransforms.FirstOrDefault(t => t.name == _body.name); ;
                    gameObject.layer = 10;
                    gameObject.transform.localScale = Vector3.one * 1000f * radius;
                    gameObject.transform.localPosition = (Vector3.zero);
                    gameObject.transform.localRotation = (Quaternion.identity);
                    _lastRedraw = Planetarium.GetUniversalTime();
                }
                if (targetBody != null && UseScansat && _scansatIsCoveredDelegate != null &&
                    _lastRedraw < Planetarium.GetUniversalTime())
                {
                    RecalculateColors(targetBody, SelectedResourceName);
                    _lastRedraw = Planetarium.GetUniversalTime();
                }
            }
        }

        private void RecalculateColors(CelestialBody targetBody, Resource resource)
        {
            // Longitude |||
            const int nbLong = 360;
            #region Vertices
            var colors = _mesh.colors32;

            colors[0] = CalculateColor32At(targetBody, resource, 90, 0);
            for (int lat = _currentLat; lat < _currentLat+2; lat++)
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
            if (Show && targetBody != null)
            {
                if (Event.current.type == EventType.Layout)
                {
                    _mouseCoords = GetMouseCoordinates(targetBody);
                    mouse = Event.current.mousePosition;
                    if (UseScansat && _scansatIsCoveredDelegate != null &&_mouseCoords != null &&
                        !_scansatIsCoveredDelegate(_mouseCoords.Longitude, _mouseCoords.Latitude, targetBody, GetScansatId(_selectedResourceName.ScansatName)))
                    {
                        _mouseCoords = null;
                    }
                }
                if (_mouseCoords != null)
                {

                    _toolTipId = 0;
                    var abundance = ORSPlanetaryResourceMapData.getResourceAvailabilityByRealResourceName(
                        targetBody.flightGlobalsIndex, _selectedResourceName.ResourceName, _mouseCoords.Latitude, _mouseCoords.Longitude)
                        .getAmount();
                    string abundanceString;
                    if (abundance > 0.001)
                    {
                        abundanceString = (abundance * 100.0).ToString("0.00") + "%";
                    }
                    else
                    {
                        abundanceString = (abundance * 1000000.0).ToString("0.0") + "ppm";
                    }
                    GUI.Window(_toolTipId, new Rect(mouse.x + 10, mouse.y + 10, 200f, 55f), i =>
                    {
                        GUI.Label(new Rect(5, 10, 190, 20), "Long: " + _mouseCoords.Longitude.ToString("###.##") + " Lat: " + _mouseCoords.Latitude.ToString("####.##"));
                        GUI.Label(new Rect(5, 30, 190, 20), "Amount: " + abundanceString);

                    },
                    _selectedResourceName.ResourceName);
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


        private void evilmesh(CelestialBody targetBody, Resource resource)
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
                    colors[lon + lat*(nbLong + 1) + 1] = CalculateColor32At(targetBody, resource, 90 - lat, lon);
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

        private Color32 CalculateColor32At(CelestialBody body, Resource resource, double latitude,
            double longitude)
        {
            if (UseScansat && _scansatIsCoveredDelegate != null &&
                !_scansatIsCoveredDelegate(longitude,latitude,body,GetScansatId(resource.ScansatName)))
            {
                return new Color32(0,0,0,0);
            }


            var avail = ORSPlanetaryResourceMapData.getResourceAvailabilityByRealResourceName(body.flightGlobalsIndex, resource.ResourceName, latitude, longitude);
            var amount = avail.getAmount();
            amount = Mathf.Clamp((float)amount * 500000f, 0f, 255f);
            if (amount > 0.0)
            {
                return new Color32(Convert.ToByte(amount), byte.MinValue, byte.MinValue, 70);
            }
            else
            {
                return new Color32(byte.MinValue, byte.MinValue, byte.MinValue, 0);
            }
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

        public bool Show
        {
            get { return _show; }
            set { _show = value; _changed = true; }
        }

        public List<Resource> Resources
        {
            get { return _resources; }
        }

        public Resource SelectedResourceName
        {
            get { return _selectedResourceName; }
            set
            {
                _selectedResourceName = value;
                _changed = true;
            }
        }

        public bool UseScansat
        {
            get { return _useScansat; }
            set { _useScansat = value; _changed = true; }
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
    }

    public class MapOverlayGui : Window<MapOverlayGui>
    {
        public MapOverlay Model;

        public MapOverlayGui(MapOverlay model)
            : base("Map Overlay", 300, 200)
        {
            Model = model;

        }

        protected override void DrawWindowContents(int windowId)
        {
            GUILayout.BeginVertical();
            if (Model.Show)
            {
                if (GUILayout.Button("Disable Overlay"))
                {
                    Model.Show = false;
                }
            }
            else
            {
                if (GUILayout.Button("Enable Overlay"))
                {
                    Model.Show = true;
                }
            }
            if (Model.UseScansat)
            {
                if (GUILayout.Button("Disable Scansat"))
                {
                    Model.UseScansat = false;
                }
            }
            else
            {
                if (GUILayout.Button("Enable Scansat"))
                {
                    Model.UseScansat = true;
                }
            }

            
            
            GUILayout.Box("");
            
            foreach (var res in Model.Resources)
            {
                if (GUILayout.Button(res.ResourceName))
                {
                    Model.SelectedResourceName = res;
                }
            }
            GUILayout.EndVertical();
        }
    }

    public class Coordinates
    {
        private readonly double _latitude;
        private readonly double _longitude;

        public Coordinates(double latitude, double longitude)
        {
            _latitude = latitude;
            _longitude = longitude;
        }

        public double Latitude
        {
            get { return _latitude; }
        }

        public double Longitude
        {
            get { return _longitude; }
        }
    }

    public class Resource
    {
        public string ResourceName { get; set; }
        public string ScansatName { get; set; }

        public Resource(string resourceName, string scansatName = null)
        {
            ResourceName = resourceName;
            ScansatName = scansatName ?? resourceName;
        }
    }
}