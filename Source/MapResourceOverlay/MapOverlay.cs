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
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MapOverlay : MonoBehaviour
    {
        private CelestialBody _body;
        private Mesh _mesh;
        private readonly List<string> _resources = new List<string> { "Karbonite", "Minerals", "Ore", "Substrate", "Water" };
        private readonly IButton _mapOverlayButton;
        private MapOverlayGui _gui;
        private bool _changed;
        private string _selectedResourceName;


        public MapOverlay()
        {
            _mapOverlayButton = ToolbarManager.Instance.add("MapResourceOverlay", "ResourceOverlay");
            _mapOverlayButton.TexturePath = "MapResourceOverlay/Assets/MapOverlayIcon";
            _mapOverlayButton.ToolTip = "Map Resource Overlay";
            _mapOverlayButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
            _mapOverlayButton.OnClick += e => ToggleGui();
        }

        public void ToggleGui()
        {
            _gui = new MapOverlayGui(this);
            _gui.SetVisible(true);
        }
        public void Awake()
        {
        }
        public void Start()
        {
            Show = false;
            gameObject.layer = 10;
            gameObject.AddComponent<MeshRenderer>();
            _mesh = gameObject.AddComponent<MeshFilter>().mesh;
            SelectedResourceName = Resources[0];
        }

        public void Update()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT && HighLogic.LoadedScene != GameScenes.TRACKSTATION)
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
                var targetBody = GetTargetBody(MapView.MapCamera.target);

                if (targetBody != null && targetBody != _body || _changed)
                {
                    _changed = false;
                    var dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var radii = System.IO.File.ReadAllLines(dir+"/Assets/Radii.cfg");
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
                }
            }
        }

        private void evilmesh(CelestialBody targetBody, string resourceName)
        {
            int bodyIndex = FlightGlobals.Bodies.IndexOf(targetBody);

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
            colors[0] = new Color32(0, 0, 0, 0);
            for (int lat = 0; lat < nbLat; lat++)
            {
                float a1 = _pi * (float)(lat + 1) / (nbLat + 1);
                float sin1 = Mathf.Sin(a1);
                float cos1 = Mathf.Cos(a1);

                for (int lon = 0; lon <= nbLong; lon++)
                {
                    float a2 = _2pi * (float)(lon == nbLong ? 0 : lon) / nbLong;
                    float sin2 = Mathf.Sin(a2);
                    float cos2 = Mathf.Cos(a2);

                    vertices[lon + lat * (nbLong + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
                    var avail = ORSPlanetaryResourceMapData.getResourceAvailabilityByRealResourceName(bodyIndex, resourceName, 90 - lat, lon);
                    var amount = avail.getAmount();
                    amount = Mathf.Clamp((float)amount * 500000f, 0f, 255f);

                    colors[lon + lat * (nbLong + 1) + 1] = new Color32(Convert.ToByte(amount), byte.MinValue, byte.MinValue, 100);
                }
            }
            vertices[vertices.Length - 1] = Vector3.up * -radius;
            colors[vertices.Length - 1] = new Color32(0, 0, 0, 0);
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

                    triangles[i++] = current;
                    triangles[i++] = current + 1;
                    triangles[i++] = next + 1;

                    triangles[i++] = current;
                    triangles[i++] = next + 1;
                    triangles[i++] = next;
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
        public bool Show { get; set; }

        public List<string> Resources
        {
            get { return _resources; }
        }

        public string SelectedResourceName
        {
            get { return _selectedResourceName; }
            set 
            { 
                _selectedResourceName = value;
                _changed = true;
            }
        }
    }

    public class MapOverlayGui : Window<MapOverlayGui>
    {
        private readonly MapOverlay _model;

        public MapOverlayGui(MapOverlay model) : base("Map Overlay", 300, 200)
        {
            _model = model;
        }

        protected override void DrawWindowContents(int windowId)
        {
            if (GUILayout.Button("Toggle Overlay"))
            {
                _model.Show = !_model.Show;
            }
            GUILayout.Box("");
            GUILayout.BeginVertical();
            foreach (var res in _model.Resources)
            {                
                if (GUILayout.Button(res))
                {
                    _model.SelectedResourceName = res;
                }
            }
            GUILayout.EndVertical();
        }
    }

}
