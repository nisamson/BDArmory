using System;
using System.Collections.Generic;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.Radar;
using UnityEngine;

namespace BDArmory.Targeting
{
    public struct TargetSignatureData : IEquatable<TargetSignatureData>
    {
        public Vector3 velocity;
        public Vector3 geoPos;
        public Vector3 acceleration;
        public bool exists;
        public float timeAcquired;
        public float signalStrength;
        public TargetInfo targetInfo;
        public BDTeam Team;
        public Vector2 pingPosition;
        public VesselECMJInfo vesselJammer;
        public ModuleRadar lockedByRadar;
        public Vessel vessel;
        bool orbital;
        Orbit orbit;

        public bool Equals(TargetSignatureData other)
        {
            return
                exists == other.exists &&
                geoPos == other.geoPos &&
                timeAcquired == other.timeAcquired;
        }

        public TargetSignatureData(Vessel v, float _signalStrength)
        {
            orbital = v.InOrbit();
            orbit = v.orbit;

            timeAcquired = Time.time;
            vessel = v;
            velocity = v.Velocity();

            geoPos = VectorUtils.WorldPositionToGeoCoords(v.CoM, v.mainBody);
            acceleration = v.acceleration_immediate;
            exists = true;

            signalStrength = _signalStrength;

            targetInfo = v.gameObject.GetComponent<TargetInfo>();

            // vessel never been picked up on radar before: create new targetinfo record
            if (targetInfo == null)
            {
                targetInfo = v.gameObject.AddComponent<TargetInfo>();
            }

            Team = null;

            if (targetInfo)  // Always true, as we just set it?
            {
                Team = targetInfo.Team;
            }
            else
            {
                var mf = VesselModuleRegistry.GetMissileFire(v, true);
                if (mf != null) Team = mf.Team;
            }

            vesselJammer = v.gameObject.GetComponent<VesselECMJInfo>();

            pingPosition = Vector2.zero;
            lockedByRadar = null;
        }

        public TargetSignatureData(CMFlare flare, float _signalStrength)
        {
            velocity = flare.velocity;
            geoPos = VectorUtils.WorldPositionToGeoCoords(flare.transform.position, FlightGlobals.currentMainBody);
            exists = true;
            acceleration = Vector3.zero;
            timeAcquired = Time.time;
            signalStrength = _signalStrength;
            targetInfo = null;
            vesselJammer = null;
            Team = null;
            pingPosition = Vector2.zero;
            orbital = false;
            orbit = null;
            lockedByRadar = null;
            vessel = null;
        }

        public TargetSignatureData(Vector3 _velocity, Vector3 _position, Vector3 _acceleration, bool _exists, float _signalStrength)
        {
            velocity = _velocity;
            geoPos = VectorUtils.WorldPositionToGeoCoords(_position, FlightGlobals.currentMainBody);
            acceleration = _acceleration;
            exists = _exists;
            timeAcquired = Time.time;
            signalStrength = _signalStrength;
            targetInfo = null;
            vesselJammer = null;
            Team = null;
            pingPosition = Vector2.zero;
            orbital = false;
            orbit = null;
            lockedByRadar = null;
            vessel = null;
        }

        public Vector3 position
        {
            get
            {
                return VectorUtils.GetWorldSurfacePostion(geoPos, FlightGlobals.currentMainBody);
            }
            set
            {
                geoPos = VectorUtils.WorldPositionToGeoCoords(value, FlightGlobals.currentMainBody);
            }
        }

        public Vector3 predictedPosition
        {
            get
            {
                return position + (velocity * age);
            }
        }

        public Vector3 predictedPositionWithChaffFactor
        {
            get
            {
                // get chaff factor of vessel and calculate decoy distortion caused by chaff echos
                float decoyFactor = 0f;
                Vector3 posDistortion = Vector3.zero;

                if (vessel != null)
                {
                    // chaff check
                    decoyFactor = (1f - RadarUtils.GetVesselChaffFactor(vessel));

                    if (decoyFactor > 0f)
                    {
                        // with ecm on better chaff effectiveness due to higher modifiedSignature
                        // higher speed -> missile decoyed further "behind" where the chaff drops (also means that for head-on engagements chaff is most like less effective!)
                        posDistortion = (vessel.GetSrfVelocity() * -1f * Mathf.Clamp(decoyFactor * decoyFactor, 0f, 0.5f)) + (UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(targetInfo.radarModifiedSignature, targetInfo.radarModifiedSignature * targetInfo.radarModifiedSignature) * decoyFactor);
                        posDistortion *= Mathf.Max(BDArmorySettings.CHAFF_FACTOR, 0f);
                    }
                }

                return position + (velocity * age) + posDistortion;
            }
        }

        public float altitude
        {
            get
            {
                return geoPos.z;
            }
        }

        public float age
        {
            get
            {
                return (Time.time - timeAcquired);
            }
        }

        public static TargetSignatureData noTarget
        {
            get
            {
                return new TargetSignatureData(Vector3.zero, Vector3.zero, Vector3.zero, false, 0);
            }
        }

        public static void ResetTSDArray(ref TargetSignatureData[] tsdArray)
        {
            for (int i = 0; i < tsdArray.Length; i++)
            {
                tsdArray[i] = noTarget;
            }
        }
    }
}
