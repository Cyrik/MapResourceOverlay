using System.Runtime.Serialization;

namespace MapResourceOverlay
{
    public class Resource
    {
        public string ResourceName { get; set; }
        public string ScansatName { get; set; }

        public Resource(string resourceName, string scansatName = null)
        {
            ResourceName = resourceName;
            ScansatName = scansatName ?? resourceName;
        }

        public string Serialize()
        {
            return "ResourceName=" + ResourceName + ",ScansatName=" + ScansatName;
        }

        public static Resource DeserializeResource(string str)
        {
            var arr = str.Split(',');
            var resourceName = arr[0].Split('=')[1];
            var scansatName = resourceName;
            if (arr.Length == 2)
            {
                scansatName = arr[1].Split('=')[1];
            }
            return new Resource(resourceName,scansatName);
        }
    }
}