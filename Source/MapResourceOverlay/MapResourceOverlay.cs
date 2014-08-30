//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using KSP.IO;
//using UnityEngine;

//namespace MapResourceOverlay
//{
//    public class MapResourceOverlay : ScenarioModule
//    {
//        public static MapResourceOverlay Instance { get; private set; }

//        public MROGameSettings gameSettings { get; private set; }

//        private readonly string globalConfigFilename;
//        private ConfigNode globalNode = new ConfigNode();

//        private readonly List<Component> children = new List<Component>();

//        public MapResourceOverlay()
//        {
//            this.Log("Constructor");
//            Instance = this;
//            gameSettings = new MROGameSettings();

//            globalConfigFilename = IOUtils.GetFilePathFor(GetType(), "MapResourceOverlay.cfg");
//        }

        

//        public override void OnAwake()
//        {
//            this.Log("OnAwake in " + HighLogic.LoadedScene);
//            base.OnAwake();
//            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
//            {
//                this.Log("Adding MapResourceOverlay");
//                var c = gameObject.AddComponent<MapOverlay>();
//                children.Add(c);
//            }
//        }

//        public override void OnLoad(ConfigNode gameNode)
//        {
//            base.OnLoad(gameNode);
//            gameSettings.Load(gameNode);
//        }

//        public override void OnSave(ConfigNode gameNode)
//        {
//            base.OnSave(gameNode);
//            gameSettings.Save(gameNode);
//        }

//        void OnDestroy()
//        {
//            this.Log("OnDestroy");
//            foreach (Component c in children)
//            {
//                Destroy(c);
//            }
//            children.Clear();
//        }
//    }
//    public class MROGameSettings : IConfigNode
//    {
//        public bool Bright { get; set; }
//        public bool Show { get; set; }
//        public bool UseScansat { get; set; }
//        public int LowCuttoff { get; set; }
//        public void Load(ConfigNode node)
//        {
            
//        }

//        public void Save(ConfigNode node)
//        {
//            node.AddValue("Bright",Bright);
//            node.AddValue("Show",Show);
//            node.AddValue("UseScansat",UseScansat);
//            node.AddValue("LowCuttoff",LowCuttoff);
//        }
//    }
    
//}
