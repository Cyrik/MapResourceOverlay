using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = System.Object;

namespace MapResourceOverlay
{
    public static class Extensions
    {
        public static void Log(this Object obj, string msg)
        {
            Debug.Log("[MRO]["+obj.GetType()+"][" + (new StackTrace()).GetFrame(1).GetMethod().Name + "] " + msg);
        }

        public static bool IsAvailableWhileFixed(this ScienceExperiment experiment, ExperimentSituations situations, CelestialBody body)
        {
            return experiment.IsAvailableWhile(situations, body) &&
                   !(experiment.experimentTitle == "Sample" &&
                    !(situations == ExperimentSituations.SrfLanded || situations == ExperimentSituations.SrfSplashed));
        }

        public static CelestialBody GetTargetBody(this MapObject target)
        {
            if (target.type == MapObject.MapObjectType.CELESTIALBODY)
                return target.celestialBody;
            if (target.type == MapObject.MapObjectType.MANEUVERNODE)
                return target.maneuverNode.patch.referenceBody;
            if (target.type == MapObject.MapObjectType.VESSEL)
                return target.vessel.mainBody;
            return null;
        }
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        public static Coordinates GetMouseCoordinates(this CelestialBody targetBody)
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
    }
}
