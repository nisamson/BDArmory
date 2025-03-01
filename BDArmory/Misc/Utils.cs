using BDArmory.Core.Extension;
using BDArmory.Core;
using BDArmory.Core.Utils;
using BDArmory.FX;
using BDArmory.Modules;
using BDArmory.UI;
using KSP.IO;
using KSP.UI.Screens;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Reflection;
using System;
using UniLinq;
using UnityEngine;

namespace BDArmory.Misc
{
    public static class Utils
    {
        public static Texture2D resizeTexture = GameDatabase.Instance.GetTexture(BDArmorySetup.textureDir + "resizeSquare", false);

        public static Color ParseColor255(string color)
        {
            Color outputColor = new Color(0, 0, 0, 1);

            string[] strings = color.Split(","[0]);
            for (int i = 0; i < 4; i++)
            {
                outputColor[i] = Single.Parse(strings[i]) / 255;
            }

            return outputColor;
        }

        public static AnimationState[] SetUpAnimation(string animationName, Part part) //Thanks Majiir!
        {
            List<AnimationState> states = new List<AnimationState>();
            using (IEnumerator<UnityEngine.Animation> animation = part.FindModelAnimators(animationName).AsEnumerable().GetEnumerator())
                while (animation.MoveNext())
                {
                    if (animation.Current == null) continue;
                    AnimationState animationState = animation.Current[animationName];
                    animationState.speed = 0; // FIXME Shouldn't this be 1?
                    animationState.enabled = true;
                    animationState.wrapMode = WrapMode.ClampForever;
                    animation.Current.Blend(animationName);
                    states.Add(animationState);
                }
            return states.ToArray();
        }

        public static AnimationState SetUpSingleAnimation(string animationName, Part part)
        {
            using (IEnumerator<UnityEngine.Animation> animation = part.FindModelAnimators(animationName).AsEnumerable().GetEnumerator())
                while (animation.MoveNext())
                {
                    if (animation.Current == null) continue;
                    AnimationState animationState = animation.Current[animationName];
                    animationState.speed = 0; // FIXME Shouldn't this be 1?
                    animationState.enabled = true;
                    animationState.wrapMode = WrapMode.ClampForever;
                    animation.Current.Blend(animationName);
                    return animationState;
                }
            return null;
        }

        public static bool CheckMouseIsOnGui()
        {
            if (!BDArmorySetup.GAME_UI_ENABLED) return false;

            if (!BDInputSettingsFields.WEAP_FIRE_KEY.inputString.Contains("mouse")) return false;

            Vector3 inverseMousePos = new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0);
            Rect topGui = new Rect(0, 0, Screen.width, 65);

            if (topGui.Contains(inverseMousePos)) return true;
            if (BDArmorySetup.windowBDAToolBarEnabled && BDArmorySetup.WindowRectToolbar.Contains(inverseMousePos))
                return true;
            if (ModuleTargetingCamera.windowIsOpen && BDArmorySetup.WindowRectTargetingCam.Contains(inverseMousePos))
                return true;
            if (BDArmorySetup.Instance.ActiveWeaponManager)
            {
                MissileFire wm = BDArmorySetup.Instance.ActiveWeaponManager;

                if (wm.vesselRadarData && wm.vesselRadarData.guiEnabled)
                {
                    if (BDArmorySetup.WindowRectRadar.Contains(inverseMousePos)) return true;
                    if (wm.vesselRadarData.linkWindowOpen && wm.vesselRadarData.linkWindowRect.Contains(inverseMousePos))
                        return true;
                }
                if (wm.rwr && wm.rwr.rwrEnabled && wm.rwr.displayRWR && BDArmorySetup.WindowRectRwr.Contains(inverseMousePos))
                    return true;
                if (wm.wingCommander && wm.wingCommander.showGUI)
                {
                    if (BDArmorySetup.WindowRectWingCommander.Contains(inverseMousePos)) return true;
                    if (wm.wingCommander.showAGWindow && wm.wingCommander.agWindowRect.Contains(inverseMousePos))
                        return true;
                }

                if (extraGUIRects != null)
                {
                    for (int i = 0; i < extraGUIRects.Count; i++)
                    {
                        if (extraGUIRects[i].Contains(inverseMousePos)) return true;
                    }
                }
            }

            return false;
        }

        public static void ResizeGuiWindow(Rect windowrect, Vector2 mousePos)
        {
        }

        public static List<Rect> extraGUIRects;

        public static int RegisterGUIRect(Rect rect)
        {
            if (extraGUIRects == null)
            {
                extraGUIRects = new List<Rect>();
            }

            int index = extraGUIRects.Count;
            extraGUIRects.Add(rect);
            return index;
        }

        public static void UpdateGUIRect(Rect rect, int index)
        {
            if (extraGUIRects == null)
            {
                Debug.LogWarning("[BDArmory.Misc]: Trying to update a GUI rect for mouse position check, but Rect list is null.");
            }

            extraGUIRects[index] = rect;
        }

        public static bool MouseIsInRect(Rect rect)
        {
            Vector3 inverseMousePos = new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0);
            return rect.Contains(inverseMousePos);
        }

        //Thanks FlowerChild
        //refreshes part action window
        public static void RefreshAssociatedWindows(Part part)
        {
            IEnumerator<UIPartActionWindow> window = Object.FindObjectsOfType(typeof(UIPartActionWindow)).Cast<UIPartActionWindow>().GetEnumerator();
            while (window.MoveNext())
            {
                if (window.Current == null) continue;
                if (window.Current.part == part)
                {
                    window.Current.displayDirty = true;
                }
            }
            window.Dispose();
        }

        public static Vector3 ProjectOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            planeNormal = planeNormal.normalized;

            Plane plane = new Plane(planeNormal, planePoint);
            float distance = plane.GetDistanceToPoint(point);

            return point - (distance * planeNormal);
        }

        public static float SignedAngle(Vector3 fromDirection, Vector3 toDirection, Vector3 referenceRight)
        {
            float angle = Vector3.Angle(fromDirection, toDirection);
            float sign = Mathf.Sign(Vector3.Dot(toDirection, referenceRight));
            float finalAngle = sign * angle;
            return finalAngle;
        }

        /// <summary>
        /// Parses the string to a curve.
        /// Format: "key:pair,key:pair"
        /// </summary>
        /// <returns>The curve.</returns>
        /// <param name="curveString">Curve string.</param>
        public static FloatCurve ParseCurve(string curveString)
        {
            string[] pairs = curveString.Split(new char[] { ',' });
            Keyframe[] keys = new Keyframe[pairs.Length];
            for (int p = 0; p < pairs.Length; p++)
            {
                string[] pair = pairs[p].Split(new char[] { ':' });
                keys[p] = new Keyframe(float.Parse(pair[0]), float.Parse(pair[1]));
            }

            FloatCurve curve = new FloatCurve(keys);

            return curve;
        }

        private static int lineOfSightLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.EVA | LayerMasks.Unknown19 | LayerMasks.Unknown23);
        public static bool CheckSightLine(Vector3 origin, Vector3 target, float maxDistance, float threshold,
            float startDistance)
        {
            float dist = maxDistance;
            Ray ray = new Ray(origin, target - origin);
            ray.origin += ray.direction * startDistance;
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, dist, lineOfSightLayerMask))
            {
                if ((target - rayHit.point).sqrMagnitude < threshold * threshold)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public static bool CheckSightLineExactDistance(Vector3 origin, Vector3 target, float maxDistance,
            float threshold, float startDistance)
        {
            float dist = maxDistance;
            Ray ray = new Ray(origin, target - origin);
            ray.origin += ray.direction * startDistance;
            RaycastHit rayHit;

            if (Physics.Raycast(ray, out rayHit, dist, lineOfSightLayerMask))
            {
                if ((target - rayHit.point).sqrMagnitude < threshold * threshold)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static float[] ParseToFloatArray(string floatString)
        {
            string[] floatStrings = floatString.Split(new char[] { ',' });
            float[] floatArray = new float[floatStrings.Length];
            for (int i = 0; i < floatStrings.Length; i++)
            {
                floatArray[i] = float.Parse(floatStrings[i]);
            }

            return floatArray;
        }

        public static string FormattedGeoPos(Vector3d geoPos, bool altitude)
        {
            string finalString = string.Empty;
            //lat
            double lat = geoPos.x;
            double latSign = Math.Sign(lat);
            double latMajor = latSign * Math.Floor(Math.Abs(lat));
            double latMinor = 100 * (Math.Abs(lat) - Math.Abs(latMajor));
            string latString = latMajor.ToString("0") + " " + latMinor.ToString("0.000");
            finalString += "N:" + latString;

            //longi
            double longi = geoPos.y;
            double longiSign = Math.Sign(longi);
            double longiMajor = longiSign * Math.Floor(Math.Abs(longi));
            double longiMinor = 100 * (Math.Abs(longi) - Math.Abs(longiMajor));
            string longiString = longiMajor.ToString("0") + " " + longiMinor.ToString("0.000");
            finalString += " E:" + longiString;

            if (altitude)
            {
                finalString += " ASL:" + geoPos.z.ToString("0.000");
            }

            return finalString;
        }

        public static string FormattedGeoPosShort(Vector3d geoPos, bool altitude)
        {
            string finalString = string.Empty;
            //lat
            double lat = geoPos.x;
            double latSign = Math.Sign(lat);
            double latMajor = latSign * Math.Floor(Math.Abs(lat));
            double latMinor = 100 * (Math.Abs(lat) - Math.Abs(latMajor));
            string latString = latMajor.ToString("0") + " " + latMinor.ToString("0");
            finalString += "N:" + latString;

            //longi
            double longi = geoPos.y;
            double longiSign = Math.Sign(longi);
            double longiMajor = longiSign * Math.Floor(Math.Abs(longi));
            double longiMinor = 100 * (Math.Abs(longi) - Math.Abs(longiMajor));
            string longiString = longiMajor.ToString("0") + " " + longiMinor.ToString("0");
            finalString += " E:" + longiString;

            if (altitude)
            {
                finalString += " ASL:" + geoPos.z.ToString("0");
            }

            return finalString;
        }

        public static KeyBinding AGEnumToKeybinding(KSPActionGroup group)
        {
            string groupName = group.ToString();
            if (groupName.Contains("Custom"))
            {
                groupName = groupName.Substring(6);
                int customNumber = int.Parse(groupName);
                groupName = "CustomActionGroup" + customNumber;
            }
            else
            {
                return null;
            }

            FieldInfo field = typeof(GameSettings).GetField(groupName);
            return (KeyBinding)field.GetValue(null);
        }

        public static float GetRadarAltitudeAtPos(Vector3 position, bool clamped = true)
        {
            double latitudeAtPos = FlightGlobals.currentMainBody.GetLatitude(position);
            double longitudeAtPos = FlightGlobals.currentMainBody.GetLongitude(position);
            float altitude = (float)(FlightGlobals.currentMainBody.GetAltitude(position));
            if (clamped)
                return Mathf.Clamp(altitude - (float)FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos), 0, altitude);
            else
                return altitude - (float)FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos);
        }

        public static string JsonCompat(string json)
        {
            return json.Replace('{', '<').Replace('}', '>');
        }

        public static string JsonDecompat(string json)
        {
            return json.Replace('<', '{').Replace('>', '}');
        }

        // this stupid thing makes all the BD armory parts explode
        [KSPField]
        private static string explModelPath = "BDArmory/Models/explosion/explosion";
        [KSPField]
        public static string explSoundPath = "BDArmory/Sounds/explode1";

        public static void ForceDeadVessel(Vessel v)
        {
            Debug.Log("[BDArmory.Misc]: GM Killed Vessel " + v.GetDisplayName());
            foreach (var missileFire in VesselModuleRegistry.GetModules<MissileFire>(v))
            {
                PartExploderSystem.AddPartToExplode(missileFire.part);
                ExplosionFx.CreateExplosion(missileFire.part.transform.position, 1f, explModelPath, explSoundPath, ExplosionSourceType.Other, 0, missileFire.part);
            }
        }


        // borrowed from SmartParts - activate the next stage on a vessel
        public static void fireNextNonEmptyStage(Vessel v)
        {
            // the parts to be fired
            List<Part> resultList = new List<Part>();

            int highestNextStage = getHighestNextStage(v.rootPart, v.currentStage);
            traverseChildren(v.rootPart, highestNextStage, ref resultList);

            foreach (Part stageItem in resultList)
            {
                //Log.Info("Activate:" + stageItem);
                stageItem.activate(highestNextStage, stageItem.vessel);
                stageItem.inverseStage = v.currentStage;
            }
            v.currentStage = highestNextStage;
            //If this is the currently active vessel, activate the next, now empty, stage. This is an ugly, ugly hack but it's the only way to clear out the empty stage.
            //Switching to a vessel that has been staged this way already clears out the empty stage, so this isn't required for those.
            if (v.isActiveVessel)
            {
                StageManager.ActivateNextStage();
            }
        }

        private static int getHighestNextStage(Part p, int currentStage)
        {

            int highestChildStage = 0;

            // if this is the root part and its a decoupler: ignore it. It was probably fired before.
            // This is dirty guesswork but everything else seems not to work. KSP staging is too messy.
            if (p.vessel.rootPart == p &&
                (p.name.IndexOf("ecoupl") != -1 || p.name.IndexOf("eparat") != -1))
            {
            }
            else if (p.inverseStage < currentStage)
            {
                highestChildStage = p.inverseStage;
            }


            // Check all children. If this part has no children, inversestage or current Stage will be returned
            int childStage = 0;
            foreach (Part child in p.children)
            {
                childStage = getHighestNextStage(child, currentStage);
                if (childStage > highestChildStage && childStage < currentStage)
                {
                    highestChildStage = childStage;
                }
            }
            return highestChildStage;
        }

        private static void traverseChildren(Part p, int nextStage, ref List<Part> resultList)
        {
            if (p.inverseStage >= nextStage)
            {
                resultList.Add(p);
            }
            foreach (Part child in p.children)
            {
                traverseChildren(child, nextStage, ref resultList);
            }
        }

        public static float RoundToUnit(float value, float unit=1f)
        {
            var rounded =  Mathf.Round(value / unit) * unit;
            return (unit % 1 != 0) ? rounded : Mathf.Round(rounded); // Fix near-integer loss of precision.
        }
    }
}
