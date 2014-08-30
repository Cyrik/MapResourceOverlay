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
    }
}