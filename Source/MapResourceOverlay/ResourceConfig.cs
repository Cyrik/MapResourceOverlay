using System;
using System.Linq;
using UnityEngine;

namespace MapResourceOverlay
{
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