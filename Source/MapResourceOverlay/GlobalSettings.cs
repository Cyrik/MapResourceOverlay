//using System;
//using System.Collections.Generic;
//using UnityEngine;

//namespace MapResourceOverlay
//{
//    public class GlobalSettings : IConfigNode
//    {
//        public List<ResourceConfig> ColorConfigs { get; set; }

//        public GlobalSettings()
//        {
//            var config = new ResourceConfig
//            {
//                Resource = new Resource("Karbonite"),
//                LowColor = new Color32(0, 0, 0, 0),
//                HighColor = new Color32(255, 0, 0, 200)
//            };
//            var config2 = new ResourceConfig
//            {
//                Resource = new Resource("Ore"),
//                LowColor = new Color32(0, 0, 0, 0),
//                HighColor = new Color32(0, 255, 0, 200)
//            };
//            var config3 = new ResourceConfig
//            {
//                Resource = new Resource("Water", "Aquifer"),
//                LowColor = new Color32(0, 0, 0, 0),
//                HighColor = new Color32(0, 0, 255, 200)
//            };
//            var config4 = new ResourceConfig
//            {
//                Resource = new Resource("Minerals"),
//                LowColor = new Color32(0, 0, 0, 0),
//                HighColor = new Color32(0, 255, 255, 200)
//            };
//            var config5 = new ResourceConfig
//            {
//                Resource = new Resource("Substrate"),
//                LowColor = new Color32(0, 0, 0, 0),
//                HighColor = new Color32(255, 0, 255, 200)
//            };
//            ColorConfigs = new List<ResourceConfig>{config, config2,config3,config4,config5};
//        }

//        public void Load(ConfigNode node)
//        {
            
//            try
//            {
//                var globalSettingsNode = node.GetNode("globalSettings");
//                var colorConfigsNode = globalSettingsNode.GetNode("colorConfigs");
//                ColorConfigs = new List<ResourceConfig>();
//                foreach (ConfigNode value in colorConfigsNode.nodes)
//                {
//                    ColorConfigs.Add(ResourceConfig.Load(value));
//                }
//            }
//            catch (Exception e)
//            {
//                this.Log("Globalconfig broken"+e);
//            }
//        }

//        public void Save(ConfigNode node)
//        {
            
//            var globalSettingsNode = node.AddNode("globalSettings");
//            var colorConfigsNode = globalSettingsNode.AddNode("colorConfigs");
//            foreach (var colorConfig in ColorConfigs)
//            {
//                colorConfig.Save(colorConfigsNode);
//            }
            
//        }
        
//    }
//}