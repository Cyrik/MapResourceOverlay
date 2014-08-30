using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Debug = UnityEngine.Debug;
using Object = System.Object;

namespace MapResourceOverlay
{
    public static class Extensions
    {
        public static void Log(this Object obj, string msg)
        {
            Debug.Log("[MRO][" + (new StackTrace()).GetFrame(1).GetMethod().Name + "] " + msg);
        }

    }
}
