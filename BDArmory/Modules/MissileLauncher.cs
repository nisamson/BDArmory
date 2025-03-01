using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.FX;
using BDArmory.Guidances;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.Radar;
using BDArmory.Targeting;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.Modules
{
    public class MissileLauncher : MissileBase
    {
        #region Variable Declarations

        [KSPField]
        public string homingType = "AAM";

        [KSPField]
        public string targetingType = "none";

        public MissileTurret missileTurret = null;
        public BDRotaryRail rotaryRail = null;
        public BDDeployableRail deployableRail = null;

        [KSPField]
        public string exhaustPrefabPath;

        [KSPField]
        public string boostExhaustPrefabPath;

        [KSPField]
        public string boostExhaustTransformName;

        #region Aero

        [KSPField]
        public bool aero = false;

        [KSPField]
        public float liftArea = 0.015f;

        [KSPField]
        public float steerMult = 0.5f;

        [KSPField]
        public float torqueRampUp = 30f;
        Vector3 aeroTorque = Vector3.zero;
        float controlAuthority;
        float finalMaxTorque;

        [KSPField]
        public float aeroSteerDamping = 0;

        #endregion Aero

        [KSPField]
        public float maxTorque = 90;

        [KSPField]
        public float thrust = 30;

        [KSPField]
        public float cruiseThrust = 3;

        [KSPField]
        public float boostTime = 2.2f;

        [KSPField]
        public float cruiseTime = 45;

        [KSPField]
        public float cruiseDelay = 0;

        [KSPField]
        public float maxAoA = 35;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_Direction"),//Direction: 
            UI_Toggle(disabledText = "#LOC_BDArmory_Direction_disabledText", enabledText = "#LOC_BDArmory_Direction_enabledText")]//Lateral--Forward
        public bool decoupleForward = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_DecoupleSpeed"),//Decouple Speed
                  UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
        public float decoupleSpeed = 0;

        [KSPField]
        public float clearanceRadius = 0.14f;

        public override float ClearanceRadius => clearanceRadius;

        [KSPField]
        public float clearanceLength = 0.14f;

        public override float ClearanceLength => clearanceLength;

        [KSPField]
        public float optimumAirspeed = 220;

        [KSPField]
        public float blastRadius = 150;

        [KSPField]
        public float blastPower = 25;

        [KSPField]
        public float blastHeat = -1;

        [KSPField]
        public float maxTurnRateDPS = 20;

        [KSPField]
        public bool proxyDetonate = true;

        [KSPField]
        public string audioClipPath = string.Empty;

        AudioClip thrustAudio;

        [KSPField]
        public string boostClipPath = string.Empty;

        AudioClip boostAudio;

        [KSPField]
        public bool isSeismicCharge = false;

        [KSPField]
        public float rndAngVel = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxAltitude"),//Max Altitude
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 10f, scene = UI_Scene.All)]
        public float maxAltitude = 0f;

        [KSPField]
        public string rotationTransformName = string.Empty;
        Transform rotationTransform;

        [KSPField]
        public bool terminalManeuvering = false;

        [KSPField]
        public string terminalGuidanceType = "";

        [KSPField]
        public float terminalGuidanceDistance = 0.0f;

        private bool terminalGuidanceActive;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_TerminalGuidance"), UI_Toggle(disabledText = "#LOC_BDArmory_false", enabledText = "#LOC_BDArmory_true")]//Terminal Guidance: false true
        public bool terminalGuidanceShouldActivate = true;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosion";

        public string explSoundPath = "BDArmory/Sounds/explode1";

        //weapon specifications
        [KSPField(advancedTweakable = true, isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_FiringPriority"),
            UI_FloatRange(minValue = 0, maxValue = 10, stepIncrement = 1, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float priority = 0; //per-weapon priority selection override

        [KSPField]
        public bool spoolEngine = false;

        [KSPField]
        public bool hasRCS = false;

        [KSPField]
        public float rcsThrust = 1;
        float rcsRVelThreshold = 0.13f;
        KSPParticleEmitter upRCS;
        KSPParticleEmitter downRCS;
        KSPParticleEmitter leftRCS;
        KSPParticleEmitter rightRCS;
        KSPParticleEmitter forwardRCS;
        float rcsAudioMinInterval = 0.2f;

        private AudioSource audioSource;
        public AudioSource sfAudioSource;
        List<KSPParticleEmitter> pEmitters;
        List<BDAGaplessParticleEmitter> gaplessEmitters;

        float cmTimer;

        //deploy animation
        [KSPField]
        public string deployAnimationName = "";

        [KSPField]
        public float deployedDrag = 0.02f;

        [KSPField]
        public float deployTime = 0.2f;

        [KSPField]
        public string flightAnimationName = "";

        [KSPField]
        public bool OneShotAnim = true;

        [KSPField]
        public bool useSimpleDrag = false;

        [KSPField]
        public float simpleDrag = 0.02f;

        [KSPField]
        public float simpleStableTorque = 5;

        [KSPField]
        public Vector3 simpleCoD = new Vector3(0, 0, -1);

        [KSPField]
        public float agmDescentRatio = 1.45f;

        float currentThrust;

        public bool deployed;
        //public float deployedTime;

        AnimationState[] deployStates;

        AnimationState[] animStates;

        bool hasPlayedFlyby;

        float debugTurnRate;

        List<GameObject> boosters;

        [KSPField]
        public bool decoupleBoosters = false;

        [KSPField]
        public float boosterDecoupleSpeed = 5;

        [KSPField]
        public float boosterMass = 0;

        Transform vesselReferenceTransform;

        [KSPField]
        public string boostTransformName = string.Empty;
        List<KSPParticleEmitter> boostEmitters;
        List<BDAGaplessParticleEmitter> boostGaplessEmitters;

        [KSPField]
        public bool torpedo = false;

        [KSPField]
        public float waterImpactTolerance = 25;

        //ballistic options
        [KSPField]
        public bool indirect = false;

        [KSPField]
        public bool vacuumSteerable = true;

        public GPSTargetInfo designatedGPSInfo;

        float[] rcsFiredTimes;
        KSPParticleEmitter[] rcsTransforms;

        #endregion Variable Declarations

        [KSPAction("Fire Missile")]
        public void AGFire(KSPActionParam param)
        {
            if (BDArmorySetup.Instance.ActiveWeaponManager != null && BDArmorySetup.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySetup.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
            if (missileTurret)
            {
                missileTurret.FireMissile(this);
            }
            else if (rotaryRail)
            {
                rotaryRail.FireMissile(this);
            }
            else if (deployableRail)
            {
                deployableRail.FireMissile(this);
            }
            else
            {
                FireMissile();
            }
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiName = "#LOC_BDArmory_FireMissile", active = true)]//Fire Missile
        public void GuiFire()
        {
            if (BDArmorySetup.Instance.ActiveWeaponManager != null && BDArmorySetup.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySetup.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
            if (missileTurret)
            {
                missileTurret.FireMissile(this);
            }
            else if (rotaryRail)
            {
                rotaryRail.FireMissile(this);
            }
            else if (deployableRail)
            {
                deployableRail.FireMissile(this);
            }
            else
            {
                FireMissile();
            }
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "#LOC_BDArmory_Jettison")]//Jettison
        public override void Jettison()
        {
            if (missileTurret) return;

            part.decouple(0);
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPAction("Jettison")]
        public void AGJettsion(KSPActionParam param)
        {
            Jettison();
        }

        void ParseWeaponClass()
        {
            missileType = missileType.ToLower();
            if (missileType == "bomb")
            {
                weaponClass = WeaponClasses.Bomb;
            }
            else if (missileType == "torpedo" || missileType == "depthcharge")
            {
                weaponClass = WeaponClasses.SLW;
            }
            else
            {
                weaponClass = WeaponClasses.Missile;
            }
        }

        public override void OnStart(StartState state)
        {
            //base.OnStart(state);
            ParseWeaponClass();

            if (shortName == string.Empty)
            {
                shortName = part.partInfo.title;
            }

            gaplessEmitters = new List<BDAGaplessParticleEmitter>();
            pEmitters = new List<KSPParticleEmitter>();
            boostEmitters = new List<KSPParticleEmitter>();
            boostGaplessEmitters = new List<BDAGaplessParticleEmitter>();

            Fields["maxOffBoresight"].guiActive = false;
            Fields["maxOffBoresight"].guiActiveEditor = false;
            Fields["maxStaticLaunchRange"].guiActive = false;
            Fields["maxStaticLaunchRange"].guiActiveEditor = false;
            Fields["minStaticLaunchRange"].guiActive = false;
            Fields["minStaticLaunchRange"].guiActiveEditor = false;

            if (isTimed)
            {
                Fields["detonationTime"].guiActive = true;
                Fields["detonationTime"].guiActiveEditor = true;
            }
            else
            {
                Fields["detonationTime"].guiActive = false;
                Fields["detonationTime"].guiActiveEditor = false;
            }

            ParseModes();
            // extension for feature_engagementenvelope
            InitializeEngagementRange(minStaticLaunchRange, maxStaticLaunchRange);

            using (var pEemitter = part.FindModelComponents<KSPParticleEmitter>().GetEnumerator())
                while (pEemitter.MoveNext())
                {
                    if (pEemitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(pEemitter.Current);
                    pEemitter.Current.emit = false;
                }

            if (HighLogic.LoadedSceneIsFlight)
            {
                //TODO: Backward compatibility wordaround
                if (part.FindModuleImplementing<BDExplosivePart>() == null)
                {
                    FromBlastPowerToTNTMass();
                }
                else
                {
                    //New Explosive module
                    DisablingExplosives(part);
                }

                MissileReferenceTransform = part.FindModelTransform("missileTransform");
                if (!MissileReferenceTransform)
                {
                    MissileReferenceTransform = part.partTransform;
                }

                if (!string.IsNullOrEmpty(exhaustPrefabPath))
                {
                    using (var t = part.FindModelTransforms("exhaustTransform").AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            AttachExhaustPrefab(exhaustPrefabPath, this, t.Current);
                        }
                }

                if (!string.IsNullOrEmpty(boostExhaustPrefabPath) && !string.IsNullOrEmpty(boostExhaustTransformName))
                {
                    using (var t = part.FindModelTransforms(boostExhaustTransformName).AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            AttachExhaustPrefab(boostExhaustPrefabPath, this, t.Current);
                        }
                }

                boosters = new List<GameObject>();
                if (!string.IsNullOrEmpty(boostTransformName))
                {
                    using (var t = part.FindModelTransforms(boostTransformName).AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            boosters.Add(t.Current.gameObject);
                            using (var be = t.Current.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator())
                                while (be.MoveNext())
                                {
                                    if (be.Current == null) continue;
                                    if (be.Current.useWorldSpace)
                                    {
                                        if (be.Current.GetComponent<BDAGaplessParticleEmitter>()) continue;
                                        BDAGaplessParticleEmitter ge = be.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                                        ge.part = part;
                                        boostGaplessEmitters.Add(ge);
                                    }
                                    else
                                    {
                                        if (!boostEmitters.Contains(be.Current))
                                        {
                                            boostEmitters.Add(be.Current);
                                        }
                                        EffectBehaviour.AddParticleEmitter(be.Current);
                                    }
                                }
                        }
                }

                using (var pEmitter = part.partTransform.Find("model").GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator())
                    while (pEmitter.MoveNext())
                    {
                        if (pEmitter.Current == null) continue;
                        if (pEmitter.Current.GetComponent<BDAGaplessParticleEmitter>() || boostEmitters.Contains(pEmitter.Current))
                        {
                            continue;
                        }

                        if (pEmitter.Current.useWorldSpace)
                        {
                            BDAGaplessParticleEmitter gaplessEmitter = pEmitter.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                            gaplessEmitter.part = part;
                            gaplessEmitters.Add(gaplessEmitter);
                        }
                        else
                        {
                            if (pEmitter.Current.transform.name != boostTransformName)
                            {
                                pEmitters.Add(pEmitter.Current);
                            }
                            else
                            {
                                boostEmitters.Add(pEmitter.Current);
                            }
                            EffectBehaviour.AddParticleEmitter(pEmitter.Current);
                        }
                    }

                cmTimer = Time.time;

                part.force_activate();

                using (var pe = pEmitters.GetEnumerator())
                    while (pe.MoveNext())
                    {
                        if (pe.Current == null) continue;
                        if (hasRCS)
                        {
                            if (pe.Current.gameObject.name == "rcsUp") upRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsDown") downRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsLeft") leftRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsRight") rightRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsForward") forwardRCS = pe.Current;
                        }

                        if (!pe.Current.gameObject.name.Contains("rcs") && !pe.Current.useWorldSpace)
                        {
                            pe.Current.sizeGrow = 99999;
                        }
                    }

                if (rotationTransformName != string.Empty)
                {
                    rotationTransform = part.FindModelTransform(rotationTransformName);
                }

                if (hasRCS)
                {
                    SetupRCS();
                    KillRCS();
                }
                SetupAudio();
            }

            if (GuidanceMode != GuidanceModes.Cruise)
            {
                CruiseAltitudeRange();
                Fields["CruiseAltitude"].guiActive = false;
                Fields["CruiseAltitude"].guiActiveEditor = false;
                Fields["CruiseSpeed"].guiActive = false;
                Fields["CruiseSpeed"].guiActiveEditor = false;
                Events["CruiseAltitudeRange"].guiActive = false;
                Events["CruiseAltitudeRange"].guiActiveEditor = false;
                Fields["CruisePredictionTime"].guiActiveEditor = false;
            }

            if (GuidanceMode != GuidanceModes.AGM)
            {
                Fields["maxAltitude"].guiActive = false;
                Fields["maxAltitude"].guiActiveEditor = false;
            }
            if (GuidanceMode != GuidanceModes.AGMBallistic)
            {
                Fields["BallisticOverShootFactor"].guiActive = false;
                Fields["BallisticOverShootFactor"].guiActiveEditor = false;
                Fields["BallisticAngle"].guiActive = false;
                Fields["BallisticAngle"].guiActiveEditor = false;
            }

            if (part.partInfo.title.Contains("Bomb"))
            {
                Fields["dropTime"].guiActive = false;
                Fields["dropTime"].guiActiveEditor = false;
            }

            if (TargetingModeTerminal != TargetingModes.None)
            {
                Fields["terminalGuidanceShouldActivate"].guiName += terminalGuidanceType;
            }
            else
            {
                Fields["terminalGuidanceShouldActivate"].guiActive = false;
                Fields["terminalGuidanceShouldActivate"].guiActiveEditor = false;
            }

            if (deployAnimationName != "")
            {
                deployStates = Utils.SetUpAnimation(deployAnimationName, part);
            }
            else
            {
                deployedDrag = simpleDrag;
            }

            if (flightAnimationName != "")
            {
                animStates = Utils.SetUpAnimation(flightAnimationName, part);
            }

            SetInitialDetonationDistance();

            // fill lockedSensorFOVBias with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Heat || TargetingModeTerminal == TargetingModes.Heat) && heatThreshold > 0 && lockedSensorFOVBias.minTime == float.MaxValue)
            {
                float a = lockedSensorFOV / 2f;
                float b = -1f * ((1f - 1f / 1.2f));
                float[] x = new float[6] { 0f * a, 0.2f * a, 0.4f * a, 0.6f * a, 0.8f * a, 1f * a };
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: OnStart missile " + shortName + ": setting default lockedSensorFOVBias curve to:");
                for (int i = 0; i < 6; i++)
                {
                    lockedSensorFOVBias.Add(x[i], b / (a * a) * x[i] * x[i] + 1f, -1f / 3f * x[i] / (a * a), -1f / 3f * x[i] / (a * a));
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("key = " + x[i] + " " + (b / (a * a) * x[i] * x[i] + 1f) + " " + (-1f / 3f * x[i] / (a * a)) + " " + (-1f / 3f * x[i] / (a * a)));
                }
            }

            // fill lockedSensorVelocityBias with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Heat || TargetingModeTerminal == TargetingModes.Heat) && heatThreshold > 0 && lockedSensorVelocityBias.minTime == float.MaxValue)
            {
                lockedSensorVelocityBias.Add(0f, 1f);
                lockedSensorVelocityBias.Add(180f, 1f);
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.MissileLauncher]: OnStart missile " + shortName + ": setting default lockedSensorVelocityBias curve to:");
                    Debug.Log("key = 0 1");
                    Debug.Log("key = 180 1");
                }
            }

            // fill activeRadarLockTrackCurve with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Radar || TargetingModeTerminal == TargetingModes.Radar) && activeRadarRange > 0 && activeRadarLockTrackCurve.minTime == float.MaxValue)
            {
                activeRadarLockTrackCurve.Add(0f, 0f);
                activeRadarLockTrackCurve.Add(activeRadarRange, RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS);           // TODO: tune & balance constants!
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: OnStart missile " + shortName + ": setting default locktrackcurve with maxrange/minrcs: " + activeRadarLockTrackCurve.maxTime + "/" + RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS);
            }
            List<ClusterBomb>.Enumerator cluster = part.FindModulesImplementing<ClusterBomb>().GetEnumerator();
            while (cluster.MoveNext())
            {
                if (cluster.Current == null) continue;
                clusterbomb = cluster.Current.submunitions.Count;
                break;
            }
            cluster.Dispose();
            List<ModuleEMP>.Enumerator emp = part.FindModulesImplementing<ModuleEMP>().GetEnumerator();
            while (emp.MoveNext())
            {
                if (emp.Current == null) continue;
                EMP = emp.Current;
                break;
            }
            emp.Dispose();
        }

        /// <summary>
        /// This method will convert the blastPower to a tnt mass equivalent
        /// </summary>
        private void FromBlastPowerToTNTMass()
        {
            blastPower = BlastPhysicsUtils.CalculateExplosiveMass(blastRadius);
        }

        void OnCollisionEnter(Collision col)
        {
            base.CollisionEnter(col);
        }

        void SetupAudio()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 1;
                audioSource.maxDistance = 1000;
                audioSource.loop = true;
                audioSource.pitch = 1f;
                audioSource.priority = 255;
                audioSource.spatialBlend = 1;
            }

            if (audioClipPath != string.Empty)
            {
                audioSource.clip = GameDatabase.Instance.GetAudioClip(audioClipPath);
            }

            if (sfAudioSource == null)
            {
                sfAudioSource = gameObject.AddComponent<AudioSource>();
                sfAudioSource.minDistance = 1;
                sfAudioSource.maxDistance = 2000;
                sfAudioSource.dopplerLevel = 0;
                sfAudioSource.priority = 230;
                sfAudioSource.spatialBlend = 1;
            }

            if (audioClipPath != string.Empty)
            {
                thrustAudio = GameDatabase.Instance.GetAudioClip(audioClipPath);
            }

            if (boostClipPath != string.Empty)
            {
                boostAudio = GameDatabase.Instance.GetAudioClip(boostClipPath);
            }

            UpdateVolume();
            BDArmorySetup.OnVolumeChange -= UpdateVolume; // Remove it if it's already there. (Doesn't matter if it isn't.)
            BDArmorySetup.OnVolumeChange += UpdateVolume;
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
            if (sfAudioSource)
            {
                sfAudioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }

        void Update()
        {
            if (!HasFired)
                CheckDetonationState();
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(part.transform.position) > 0) //#710
                {
                    float a = (float)FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude;
                    float d = FlightGlobals.getAltitudeAtPos(part.transform.position);
                    dropTime = ((float)Math.Sqrt(a * (a + (8 * d))) - a) / (2 * a) - (Time.fixedDeltaTime * 1.5f); //quadratic equation for accel to find time from known force and vel
                }// adjusts droptime to delay the MissileRoutine IEnum so torps won't start boosting until splashdown 
            }
        }

        void OnDestroy()
        {
            DetachExhaustPrefabs();
            KillRCS();
            if (upRCS) EffectBehaviour.RemoveParticleEmitter(upRCS);
            if (downRCS) EffectBehaviour.RemoveParticleEmitter(downRCS);
            if (leftRCS) EffectBehaviour.RemoveParticleEmitter(leftRCS);
            if (rightRCS) EffectBehaviour.RemoveParticleEmitter(rightRCS);
            if (pEmitters != null)
                foreach (var pe in pEmitters)
                    if (pe) EffectBehaviour.RemoveParticleEmitter(pe);
            if (boostEmitters != null)
                foreach (var pe in boostEmitters)
                    if (pe) EffectBehaviour.RemoveParticleEmitter(pe);
            BDArmorySetup.OnVolumeChange -= UpdateVolume;
            GameEvents.onPartDie.Remove(PartDie);
            if (vesselReferenceTransform != null && vesselReferenceTransform.gameObject != null)
            {
                Destroy(vesselReferenceTransform.gameObject);
            }
        }

        public override float GetBlastRadius()
        {
            if (part.FindModuleImplementing<BDExplosivePart>() != null)
            {
                return part.FindModuleImplementing<BDExplosivePart>().GetBlastRadius();
            }
            else
            {
                return blastRadius;
            }
        }

        public override void FireMissile()
        {
            if (HasFired) return;

            try // FIXME Remove this once the fix is sufficiently tested.
            {
                SetupExplosive(this.part);
                HasFired = true;

                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: Missile Fired! " + vessel.vesselName);

                GameEvents.onPartDie.Add(PartDie);
                BDATargetManager.FiredMissiles.Add(this);

                if (GetComponentInChildren<KSPParticleEmitter>())
                {
                    BDArmorySetup.numberOfParticleEmitters++;
                }

                var wpm = VesselModuleRegistry.GetMissileFire(vessel, true);
                if (wpm != null) Team = wpm.Team;

                if (sfAudioSource == null) SetupAudio();
                sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/deployClick"));
                SourceVessel = vessel;

                //TARGETING
                TargetPosition = transform.position + (transform.forward * 5000); //set initial target position so if no target update, missileBase will count a miss if it nears this point or is flying post-thrust
                startDirection = transform.forward;

                SetLaserTargeting();
                SetAntiRadTargeting();

                part.decouple(0);
                part.force_activate();
                part.Unpack();
                vessel.situation = Vessel.Situations.FLYING;
                part.rb.isKinematic = false;
                part.bodyLiftMultiplier = 0;
                part.dragModel = Part.DragModel.NONE;

                //add target info to vessel
                AddTargetInfoToVessel();
                StartCoroutine(DecoupleRoutine());

                vessel.vesselName = GetShortName();
                vessel.vesselType = VesselType.Probe;

                TimeFired = Time.time;

                //setting ref transform for navball
                GameObject refObject = new GameObject();
                refObject.transform.rotation = Quaternion.LookRotation(-transform.up, transform.forward);
                refObject.transform.parent = transform;
                part.SetReferenceTransform(refObject.transform);
                vessel.SetReferenceTransform(part);
                vesselReferenceTransform = refObject.transform;
                DetonationDistanceState = DetonationDistanceStates.NotSafe;
                MissileState = MissileStates.Drop;
                part.crashTolerance = 9999; //to combat stresses of launch, missle generate a lot of G Force

                StartCoroutine(MissileRoutine());
            }
            catch (Exception e)
            {
                Debug.LogError("[BDArmory.MissileLauncher]: DEBUG " + e.Message);
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null part?: " + (part == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG part: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null part.rb?: " + (part.rb == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG part.rb: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null BDATargetManager.FiredMissiles?: " + (BDATargetManager.FiredMissiles == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG BDATargetManager.FiredMissiles: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null vessel?: " + (vessel == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG vessel: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null sfAudioSource?: " + (sfAudioSource == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG sfAudioSource: " + e2.Message); }
                throw; // Re-throw the exception so behaviour is unchanged so we see it.
            }
        }

        IEnumerator DecoupleRoutine()
        {
            yield return new WaitForFixedUpdate();

            if (rndAngVel > 0)
            {
                part.rb.angularVelocity += UnityEngine.Random.insideUnitSphere.normalized * rndAngVel;
            }

            if (decoupleForward)
            {
                part.rb.velocity += decoupleSpeed * part.transform.forward;
            }
            else
            {
                part.rb.velocity += decoupleSpeed * -part.transform.up;
            }
        }

        /// <summary>
        /// Fires the missileBase on target vessel.  Used by AI currently.
        /// </summary>
        /// <param name="v">V.</param>
        public void FireMissileOnTarget(Vessel v)
        {
            if (!HasFired)
            {
                legacyTargetVessel = v;
                FireMissile();
            }
        }

        void OnDisable()
        {
            if (TargetingMode == TargetingModes.AntiRad)
            {
                RadarWarningReceiver.OnRadarPing -= ReceiveRadarPing;
            }
        }

        public override void OnFixedUpdate()
        {
            try // FIXME Remove this once the fix is sufficiently tested.
            {
                debugString.Length = 0;

                if (HasFired && !HasExploded && part != null)
                {
                    CheckDetonationState();
                    CheckDetonationDistance();

                    part.rb.isKinematic = false;
                    AntiSpin();

                    //simpleDrag
                    if (useSimpleDrag)
                    {
                        SimpleDrag();
                    }

                    //flybyaudio
                    float mCamDistanceSqr = (FlightCamera.fetch.mainCamera.transform.position - transform.position).sqrMagnitude;
                    float mCamRelVSqr = (float)(FlightGlobals.ActiveVessel.Velocity() - vessel.Velocity()).sqrMagnitude;
                    if (!hasPlayedFlyby
                       && FlightGlobals.ActiveVessel != vessel
                       && FlightGlobals.ActiveVessel != SourceVessel
                       && mCamDistanceSqr < 400 * 400 && mCamRelVSqr > 300 * 300
                       && mCamRelVSqr < 800 * 800
                       && Vector3.Angle(vessel.Velocity(), FlightGlobals.ActiveVessel.transform.position - transform.position) < 60)
                    {
                        if (sfAudioSource == null) SetupAudio();
                        sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/missileFlyby"));
                        hasPlayedFlyby = true;
                    }

                    if (vessel.isActiveVessel)
                    {
                        audioSource.dopplerLevel = 0;
                    }
                    else
                    {
                        audioSource.dopplerLevel = 1f;
                    }

                    if (TimeIndex > 0.5f)
                    {
                        if (torpedo)
                        {
                            if (vessel.altitude > 0)
                            {
                                part.crashTolerance = waterImpactTolerance;
                            }
                            else
                            {
                                part.crashTolerance = 1;
                            }
                        }
                        else
                        {
                            part.crashTolerance = 1;
                        }
                    }

                    UpdateThrustForces();
                    UpdateGuidance();
                    //RaycastCollisions();

                    //Timed detonation
                    if (isTimed && TimeIndex > detonationTime)
                    {
                        Detonate();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BDArmory.MissileLauncher]: DEBUG " + e.Message);
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null part?: " + (part == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG part: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null part.rb?: " + (part.rb == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG part.rb: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null vessel?: " + (vessel == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG vessel: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null audioSource?: " + (audioSource == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG audioSource: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null sfAudioSource?: " + (sfAudioSource == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: sfAudioSource: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null FlightGlobals.ActiveVessel?: " + (FlightGlobals.ActiveVessel == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG FlightGlobals.ActiveVessel: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null FlightCamera.fetch?: " + (FlightCamera.fetch == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG FlightCamera.fetch: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null FlightCamera.fetch.mainCamera?: " + (FlightCamera.fetch.mainCamera == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG FlightCamera.fetch.mainCamera: " + e2.Message); }
                throw; // Re-throw the exception so behaviour is unchanged so we see it.
            }
        }

        private void CheckMiss()
        {
            float sqrDist = (float)((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (vessel.CoM + (vessel.Velocity() * Time.fixedDeltaTime))).sqrMagnitude;
            if (sqrDist < 160000 || MissileState == MissileStates.PostThrust)
            {
                checkMiss = true;
            }
            if (maxAltitude != 0f)
            {
                if (vessel.altitude >= maxAltitude) checkMiss = true;
            }

            //kill guidance if missileBase has missed
            if (!HasMissed && checkMiss)
            {
                bool noProgress = MissileState == MissileStates.PostThrust && (Vector3.Dot(vessel.Velocity() - TargetVelocity, TargetPosition - vessel.transform.position) < 0);
                bool pastGracePeriod = TimeIndex > ((vessel.LandedOrSplashed ? 0f : dropTime) + 180f / maxTurnRateDPS);
                bool targetBehindMissile = Vector3.Dot(TargetPosition - transform.position, transform.forward) < 0f;
                if ((pastGracePeriod && targetBehindMissile) || noProgress) // Check that we're not moving away from the target after a grace period
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: Missile has missed!");

                    if (vessel.altitude >= maxAltitude && maxAltitude != 0f)
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: CheckMiss trigged by MaxAltitude");

                    HasMissed = true;
                    guidanceActive = false;

                    TargetMf = null;

                    MissileLauncher launcher = this as MissileLauncher;
                    if (launcher != null)
                    {
                        if (launcher.hasRCS) launcher.KillRCS();
                    }

                    if (sqrDist < Mathf.Pow(GetBlastRadius() * 0.5f, 2)) part.Destroy();

                    isTimed = true;
                    detonationTime = TimeIndex + 1.5f;
                    return;
                }
            }
        }

        void UpdateGuidance()
        {
            string debugTarget = "none";
            if (guidanceActive)
            {
                if (TargetingMode == TargetingModes.Heat)
                {
                    UpdateHeatTarget();
                    if (heatTarget.vessel)
                        debugTarget = heatTarget.vessel.GetDisplayName() + " " + heatTarget.signalStrength.ToString();
                    else if (heatTarget.signalStrength > 0)
                        debugTarget = "Flare " + heatTarget.signalStrength.ToString();
                }
                else if (TargetingMode == TargetingModes.Radar)
                {
                    UpdateRadarTarget();
                    if (radarTarget.vessel)
                        debugTarget = radarTarget.vessel.GetDisplayName() + " " + radarTarget.signalStrength.ToString();
                    else if (radarTarget.signalStrength > 0)
                        debugTarget = "Chaff " + radarTarget.signalStrength.ToString();
                }
                else if (TargetingMode == TargetingModes.Laser)
                {
                    UpdateLaserTarget();
                    debugTarget = TargetPosition.ToString();
                }
                else if (TargetingMode == TargetingModes.Gps)
                {
                    UpdateGPSTarget();
                    debugTarget = UpdateGPSTarget().ToString();
                }
                else if (TargetingMode == TargetingModes.AntiRad)
                {
                    UpdateAntiRadiationTarget();
                    debugTarget = TargetPosition.ToString();
                }

                UpdateTerminalGuidance();
            }

            if (MissileState != MissileStates.Idle && MissileState != MissileStates.Drop) //guidance
            {
                //guidance and attitude stabilisation scales to atmospheric density. //use part.atmDensity
                float atmosMultiplier = Mathf.Clamp01(2.5f * (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position), FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody));

                if (vessel.srfSpeed < optimumAirspeed)
                {
                    float optimumSpeedFactor = (float)vessel.srfSpeed / (2 * optimumAirspeed);
                    controlAuthority = Mathf.Clamp01(atmosMultiplier * (-Mathf.Abs(2 * optimumSpeedFactor - 1) + 1));
                }
                else
                {
                    controlAuthority = Mathf.Clamp01(atmosMultiplier);
                }

                if (vacuumSteerable)
                {
                    controlAuthority = 1;
                }

                debugString.Append($"controlAuthority: {controlAuthority}");
                debugString.Append(Environment.NewLine);

                if (guidanceActive)// && timeIndex - dropTime > 0.5f)
                {
                    WarnTarget();

                    if (legacyTargetVessel && legacyTargetVessel.loaded)
                    {
                        Vector3 targetCoMPos = legacyTargetVessel.CoM;
                        TargetPosition = targetCoMPos + legacyTargetVessel.Velocity() * Time.fixedDeltaTime;
                    }

                    //increaseTurnRate after launch
                    float turnRateDPS = Mathf.Clamp(((TimeIndex - dropTime) / boostTime) * maxTurnRateDPS * 25f, 0, maxTurnRateDPS);
                    if (!hasRCS)
                    {
                        turnRateDPS *= controlAuthority;
                    }

                    //decrease turn rate after thrust cuts out
                    if (TimeIndex > dropTime + boostTime + cruiseTime)
                    {
                        var clampedTurnRate = Mathf.Clamp(maxTurnRateDPS - ((TimeIndex - dropTime - boostTime - cruiseTime) * 0.45f),
                            1, maxTurnRateDPS);
                        turnRateDPS = clampedTurnRate;

                        if (!vacuumSteerable)
                        {
                            turnRateDPS *= atmosMultiplier;
                        }

                        if (hasRCS)
                        {
                            turnRateDPS = 0;
                        }
                    }

                    if (hasRCS)
                    {
                        if (turnRateDPS > 0)
                        {
                            DoRCS();
                        }
                        else
                        {
                            KillRCS();
                        }
                    }
                    debugTurnRate = turnRateDPS;

                    finalMaxTorque = Mathf.Clamp((TimeIndex - dropTime) * torqueRampUp, 0, maxTorque); //ramp up torque

                    if (GuidanceMode == GuidanceModes.AAMLead)
                    {
                        AAMGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.AGM)
                    {
                        AGMGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.AGMBallistic)
                    {
                        AGMBallisticGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.BeamRiding)
                    {
                        BeamRideGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.RCS)
                    {
                        part.transform.rotation = Quaternion.RotateTowards(part.transform.rotation, Quaternion.LookRotation(TargetPosition - part.transform.position, part.transform.up), turnRateDPS * Time.fixedDeltaTime);
                    }
                    else if (GuidanceMode == GuidanceModes.Cruise)
                    {
                        CruiseGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.SLW)
                    {
                        SLWGuidance();
                    }

                }
                else
                {
                    CheckMiss();
                    TargetMf = null;
                    if (aero)
                    {
                        aeroTorque = MissileGuidance.DoAeroForces(this, transform.position + (20 * vessel.Velocity()), liftArea, .25f, aeroTorque, maxTorque, maxAoA);
                    }
                }

                if (aero && aeroSteerDamping > 0)
                {
                    part.rb.AddRelativeTorque(-aeroSteerDamping * part.transform.InverseTransformVector(part.rb.angularVelocity));
                }

                if (hasRCS && !guidanceActive)
                {
                    KillRCS();
                }
            }

            debugString.Append("Missile target=" + debugTarget);
            debugString.Append(Environment.NewLine);
        }

        // feature_engagementenvelope: terminal guidance mode for cruise missiles
        private void UpdateTerminalGuidance()
        {
            // check if guidance mode should be changed for terminal phase
            float distanceSqr = (TargetPosition - transform.position).sqrMagnitude;

            if (terminalGuidanceShouldActivate && !terminalGuidanceActive && (TargetingModeTerminal != TargetingModes.None) && (distanceSqr < terminalGuidanceDistance * terminalGuidanceDistance))
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: missile " + this.name + " updating targeting mode: " + terminalGuidanceType);

                TargetingMode = TargetingModeTerminal;
                terminalGuidanceActive = true;
                TargetAcquired = false;

                switch (TargetingModeTerminal)
                {
                    case TargetingModes.Heat:
                        // gets ground heat targets and after locking one, disallows the lock to break to another target
                        heatTarget = BDATargetManager.GetHeatTarget(SourceVessel, vessel, new Ray(transform.position + (50 * GetForwardTransform()), GetForwardTransform()), heatTarget, terminalGuidanceDistance, heatThreshold, true, lockedSensorFOVBias, lockedSensorVelocityBias, SourceVessel ? VesselModuleRegistry.GetModule<MissileFire>(SourceVessel) : null, true);
                        if (heatTarget.exists)
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Heat target acquired! Position: " + heatTarget.position + ", heatscore: " + heatTarget.signalStrength);
                            }
                            TargetAcquired = true;
                            TargetPosition = heatTarget.position + (heatTarget.velocity * Time.fixedDeltaTime);
                            TargetVelocity = heatTarget.velocity;
                            TargetAcceleration = heatTarget.acceleration;
                            lockFailTimer = 0;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);

                            // Adjust heat score based on distance missile will travel in the next update
                            if (heatTarget.signalStrength > 0)
                            {
                                float currentFactor = (1400 * 1400) / Mathf.Clamp((heatTarget.position - transform.position).sqrMagnitude, 90000, 36000000);
                                Vector3 currVel = (float)vessel.srfSpeed * vessel.Velocity().normalized;
                                float futureFactor = (1400 * 1400) / Mathf.Clamp((TargetPosition - (transform.position + (currVel * Time.fixedDeltaTime))).sqrMagnitude, 90000, 36000000);
                                heatTarget.signalStrength *= futureFactor / currentFactor;
                            }
                        }
                        else
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Missile heatseeker could not acquire a target lock.");
                            }
                        }
                        break;

                    case TargetingModes.Radar:

                        // pretend we have an active radar seeker for ground targets:
                        TargetSignatureData[] scannedTargets = new TargetSignatureData[5];
                        TargetSignatureData.ResetTSDArray(ref scannedTargets);
                        Ray ray = new Ray(transform.position, GetForwardTransform());

                        //RadarUtils.UpdateRadarLock(ray, maxOffBoresight, activeRadarMinThresh, ref scannedTargets, 0.4f, true, RadarWarningReceiver.RWRThreatTypes.MissileLock, true);
                        RadarUtils.RadarUpdateMissileLock(ray, maxOffBoresight, ref scannedTargets, 0.4f, this);
                        float sqrThresh = Mathf.Pow(terminalGuidanceDistance * 1.5f, 2);

                        //float smallestAngle = maxOffBoresight;
                        TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                        for (int i = 0; i < scannedTargets.Length; i++)
                        {
                            if (scannedTargets[i].exists && (scannedTargets[i].predictedPosition - TargetPosition).sqrMagnitude < sqrThresh)
                            {
                                //re-check engagement envelope, only lock appropriate targets
                                if (CheckTargetEngagementEnvelope(scannedTargets[i].targetInfo))
                                {
                                    lockedTarget = scannedTargets[i];
                                    ActiveRadar = true;
                                }
                            }
                        }

                        if (lockedTarget.exists)
                        {
                            radarTarget = lockedTarget;
                            TargetAcquired = true;
                            if (weaponClass == WeaponClasses.SLW)
                            {
                                TargetPosition = radarTarget.predictedPosition;
                            }
                            else
                                TargetPosition = radarTarget.predictedPositionWithChaffFactor;
                            TargetVelocity = radarTarget.velocity;
                            TargetAcceleration = radarTarget.acceleration;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);

                            if (weaponClass == WeaponClasses.SLW)
                                RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), 45, RadarWarningReceiver.RWRThreatTypes.Torpedo, 2f);
                            else
                                RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), 45, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);

                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Pitbull! Radar missileBase has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0") + " - target: " + radarTarget.vessel.name);
                        }
                        else
                        {
                            TargetAcquired = true;
                            TargetPosition = VectorUtils.GetWorldSurfacePostion(UpdateGPSTarget(), vessel.mainBody); //putting back the GPS target if no radar target found
                            TargetVelocity = Vector3.zero;
                            TargetAcceleration = Vector3.zero;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);
                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Missile radar could not acquire a target lock - Defaulting to GPS Target");
                        }
                        break;

                    case TargetingModes.Laser:
                        // not very useful, currently unsupported!
                        break;

                    case TargetingModes.Gps:
                        // from gps to gps -> no actions need to be done!
                        break;

                    case TargetingModes.AntiRad:
                        TargetAcquired = true;
                        targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody); // Set the GPS coordinates from the current target position.
                        SetAntiRadTargeting(); //should then already work automatically via OnReceiveRadarPing
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Antiradiation mode set! Waiting for radar signals...");
                        break;
                }
            }
        }

        void UpdateThrustForces()
        {
            if (MissileState == MissileStates.PostThrust) return;
            if (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(part.transform.position) > 0) return; //#710, no torp thrust out of water
            if (currentThrust * Throttle > 0)
            {
                debugString.Append("Missile thrust=" + currentThrust * Throttle);
                debugString.Append(Environment.NewLine);

                part.rb.AddRelativeForce(currentThrust * Throttle * Vector3.forward);
            }
        }

        IEnumerator MissileRoutine()
        {
            MissileState = MissileStates.Drop;
            StartCoroutine(DeployAnimRoutine());
            yield return new WaitForSeconds(dropTime);
            yield return StartCoroutine(BoostRoutine());
            StartCoroutine(FlightAnimRoutine());
            yield return new WaitForSeconds(cruiseDelay);
            yield return StartCoroutine(CruiseRoutine());
        }

        IEnumerator DeployAnimRoutine()
        {
            yield return new WaitForSeconds(deployTime);
            if (deployStates == null)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.LogWarning("[BDArmory.MissileLauncher]: deployStates was null, aborting AnimRoutine.");
                yield break;
            }

            if (!string.IsNullOrEmpty(deployAnimationName))
            {
                deployed = true;
                using (var anim = deployStates.AsEnumerable().GetEnumerator())
                    while (anim.MoveNext())
                    {
                        if (anim.Current == null) continue;
                        anim.Current.speed = 1;
                    }
            }
        }
        IEnumerator FlightAnimRoutine()
        {
            if (animStates == null)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.LogWarning("[BDArmory.MissileLauncher]: animStates was null, aborting AnimRoutine.");
                yield break;
            }

            if (!string.IsNullOrEmpty(flightAnimationName))
            {
                using (var anim = animStates.AsEnumerable().GetEnumerator())
                    while (anim.MoveNext())
                    {
                        if (anim.Current == null) continue;
                        if (!OneShotAnim)
                        {
                            anim.Current.wrapMode = WrapMode.Loop;
                        }
                        anim.Current.speed = 1;
                    }
            }
        }
        IEnumerator BoostRoutine()
        {
            StartBoost();
            float boostStartTime = Time.time;
            while (Time.time - boostStartTime < boostTime)
            {
                //light, sound & particle fx
                //sound
                if (!BDArmorySetup.GameIsPaused)
                {
                    if (!audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                }
                else if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                //particleFx
                using (var emitter = boostEmitters.GetEnumerator())
                    while (emitter.MoveNext())
                    {
                        if (emitter.Current == null) continue;
                        if (!hasRCS)
                        {
                            emitter.Current.sizeGrow = Mathf.Lerp(emitter.Current.sizeGrow, 0, 20 * Time.deltaTime);
                        }
                    }

                using (var gpe = boostGaplessEmitters.GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        if ((!vessel.InVacuum() && Throttle > 0) && weaponClass != WeaponClasses.SLW || (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(part.transform.position) < 0)) //#710
                        {
                            gpe.Current.emit = true;
                            gpe.Current.pEmitter.worldVelocity = 2 * ParticleTurbulence.flareTurbulence;
                        }
                        else
                        {
                            gpe.Current.emit = false;
                        }
                    }

                //thrust
                if (spoolEngine)
                {
                    currentThrust = Mathf.MoveTowards(currentThrust, thrust, thrust / 10);
                }

                yield return null;
            }
            EndBoost();
        }

        void StartBoost()
        {
            MissileState = MissileStates.Boost;

            if (audioSource == null || sfAudioSource == null) SetupAudio();
            if (boostAudio)
            {
                audioSource.clip = boostAudio;
            }
            else if (thrustAudio)
            {
                audioSource.clip = thrustAudio;
            }

            using (var light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator())
                while (light.MoveNext())
                {
                    if (light.Current == null) continue;
                    light.Current.intensity = 1.5f;
                }

            if (!spoolEngine)
            {
                currentThrust = thrust;
            }

            if (string.IsNullOrEmpty(boostTransformName))
            {
                boostEmitters = pEmitters;
                boostGaplessEmitters = gaplessEmitters;
            }

            using (var emitter = boostEmitters.GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    emitter.Current.emit = true;
                }

            if (hasRCS)
            {
                forwardRCS.emit = true;
            }

            if (!(thrust > 0)) return;
            sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/launch"));
            RadarWarningReceiver.WarnMissileLaunch(transform.position, transform.forward);
        }

        void EndBoost()
        {
            using (var emitter = boostEmitters.GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    emitter.Current.emit = false;
                }

            using (var gEmitter = boostGaplessEmitters.GetEnumerator())
                while (gEmitter.MoveNext())
                {
                    if (gEmitter.Current == null) continue;
                    gEmitter.Current.emit = false;
                }

            if (decoupleBoosters)
            {
                part.mass -= boosterMass;
                using (var booster = boosters.GetEnumerator())
                    while (booster.MoveNext())
                    {
                        if (booster.Current == null) continue;
                        booster.Current.AddComponent<DecoupledBooster>().DecoupleBooster(part.rb.velocity, boosterDecoupleSpeed);
                    }
            }

            if (cruiseDelay > 0)
            {
                currentThrust = 0;
            }
        }

        IEnumerator CruiseRoutine()
        {
            StartCruise();
            float cruiseStartTime = Time.time;
            while (Time.time - cruiseStartTime < cruiseTime)
            {
                if (!BDArmorySetup.GameIsPaused)
                {
                    if (!audioSource.isPlaying || audioSource.clip != thrustAudio)
                    {
                        audioSource.clip = thrustAudio;
                        audioSource.Play();
                    }
                }
                else if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                audioSource.volume = Throttle;

                //particleFx
                using (var emitter = pEmitters.GetEnumerator())
                    while (emitter.MoveNext())
                    {
                        if (emitter.Current == null) continue;
                        if (!hasRCS)
                        {
                            emitter.Current.sizeGrow = Mathf.Lerp(emitter.Current.sizeGrow, 0, 20 * Time.deltaTime);
                        }

                        emitter.Current.maxSize = Mathf.Clamp01(Throttle / Mathf.Clamp((float)vessel.atmDensity, 0.2f, 1f));
                        if (weaponClass != WeaponClasses.SLW || (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(part.transform.position) < 0)) //#710
                        {
                            emitter.Current.emit = true;
                        }
                        else
                        {
                            emitter.Current.emit = false; // #710, shut down thrust FX for torps out of water
                        }
                    }

                using (var gpe = gaplessEmitters.GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        if (weaponClass != WeaponClasses.SLW || (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(part.transform.position) < 0)) //#710
                        {
                            gpe.Current.pEmitter.maxSize = Mathf.Clamp01(Throttle / Mathf.Clamp((float)vessel.atmDensity, 0.2f, 1f));
                            gpe.Current.emit = true;
                            gpe.Current.pEmitter.worldVelocity = 2 * ParticleTurbulence.flareTurbulence;
                        }
                        else
                        {
                            gpe.Current.emit = false;
                        }
                    }

                if (spoolEngine)
                {
                    currentThrust = Mathf.MoveTowards(currentThrust, cruiseThrust, cruiseThrust / 10);
                }
                yield return null;
            }
            EndCruise();
        }

        void StartCruise()
        {
            MissileState = MissileStates.Cruise;

            if (audioSource == null) SetupAudio();
            if (thrustAudio)
            {
                audioSource.clip = thrustAudio;
            }

            currentThrust = spoolEngine ? 0 : cruiseThrust;

            using (var pEmitter = pEmitters.GetEnumerator())
                while (pEmitter.MoveNext())
                {
                    if (pEmitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(pEmitter.Current);
                    pEmitter.Current.emit = true;
                }

            using (var gEmitter = gaplessEmitters.GetEnumerator())
                while (gEmitter.MoveNext())
                {
                    if (gEmitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(gEmitter.Current.pEmitter);
                    gEmitter.Current.emit = true;
                }

            if (!hasRCS) return;
            forwardRCS.emit = false;
            audioSource.Stop();
        }

        void EndCruise()
        {
            MissileState = MissileStates.PostThrust;

            IEnumerator<Light> light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator();
            while (light.MoveNext())
            {
                if (light.Current == null) continue;
                light.Current.intensity = 0;
            }
            light.Dispose();

            StartCoroutine(FadeOutAudio());
            StartCoroutine(FadeOutEmitters());
        }

        IEnumerator FadeOutAudio()
        {
            if (thrustAudio && audioSource.isPlaying)
            {
                while (audioSource.volume > 0 || audioSource.pitch > 0)
                {
                    audioSource.volume = Mathf.Lerp(audioSource.volume, 0, 5 * Time.deltaTime);
                    audioSource.pitch = Mathf.Lerp(audioSource.pitch, 0, 5 * Time.deltaTime);
                    yield return null;
                }
            }
        }

        IEnumerator FadeOutEmitters()
        {
            float fadeoutStartTime = Time.time;
            while (Time.time - fadeoutStartTime < 5)
            {
                using (var pe = pEmitters.GetEnumerator())
                    while (pe.MoveNext())
                    {
                        if (pe.Current == null) continue;
                        pe.Current.maxEmission = Mathf.FloorToInt(pe.Current.maxEmission * 0.8f);
                        pe.Current.minEmission = Mathf.FloorToInt(pe.Current.minEmission * 0.8f);
                    }

                using (var gpe = gaplessEmitters.GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        gpe.Current.pEmitter.maxSize = Mathf.MoveTowards(gpe.Current.pEmitter.maxSize, 0, 0.005f);
                        gpe.Current.pEmitter.minSize = Mathf.MoveTowards(gpe.Current.pEmitter.minSize, 0, 0.008f);
                        gpe.Current.pEmitter.worldVelocity = ParticleTurbulence.Turbulence;
                    }
                yield return new WaitForFixedUpdate();
            }

            using (var pe2 = pEmitters.GetEnumerator())
                while (pe2.MoveNext())
                {
                    if (pe2.Current == null) continue;
                    pe2.Current.emit = false;
                }

            using (var gpe2 = gaplessEmitters.GetEnumerator())
                while (gpe2.MoveNext())
                {
                    if (gpe2.Current == null) continue;
                    gpe2.Current.emit = false;
                }
        }

        [KSPField]
        public float beamCorrectionFactor;

        [KSPField]
        public float beamCorrectionDamping;

        Ray previousBeam;

        void BeamRideGuidance()
        {
            if (!targetingPod)
            {
                guidanceActive = false;
                return;
            }

            if (RadarUtils.TerrainCheck(targetingPod.cameraParentTransform.position, transform.position))
            {
                guidanceActive = false;
                return;
            }
            Ray laserBeam = new Ray(targetingPod.cameraParentTransform.position + (targetingPod.vessel.Velocity() * Time.fixedDeltaTime), targetingPod.targetPointPosition - targetingPod.cameraParentTransform.position);
            Vector3 target = MissileGuidance.GetBeamRideTarget(laserBeam, part.transform.position, vessel.Velocity(), beamCorrectionFactor, beamCorrectionDamping, (TimeIndex > 0.25f ? previousBeam : laserBeam));
            previousBeam = laserBeam;
            DrawDebugLine(part.transform.position, target);
            DoAero(target);
        }

        void CruiseGuidance()
        {
            if (this._guidance == null)
            {
                this._guidance = new CruiseGuidance(this);
            }

            Vector3 cruiseTarget = Vector3.zero;

            cruiseTarget = this._guidance.GetDirection(this, TargetPosition, TargetVelocity);

            Vector3 upDirection = VectorUtils.GetUpDirection(transform.position);

            //axial rotation
            if (rotationTransform)
            {
                Quaternion originalRotation = transform.rotation;
                Quaternion originalRTrotation = rotationTransform.rotation;
                transform.rotation = Quaternion.LookRotation(transform.forward, upDirection);
                rotationTransform.rotation = originalRTrotation;
                Vector3 lookUpDirection = Vector3.ProjectOnPlane(cruiseTarget - transform.position, transform.forward) * 100;
                lookUpDirection = transform.InverseTransformPoint(lookUpDirection + transform.position);

                lookUpDirection = new Vector3(lookUpDirection.x, 0, 0);
                lookUpDirection += 10 * Vector3.up;

                rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation, Quaternion.LookRotation(Vector3.forward, lookUpDirection), 0.04f);
                Quaternion finalRotation = rotationTransform.rotation;
                transform.rotation = originalRotation;
                rotationTransform.rotation = finalRotation;

                vesselReferenceTransform.rotation = Quaternion.LookRotation(-rotationTransform.up, rotationTransform.forward);
            }
            DoAero(cruiseTarget);
            CheckMiss();
        }

        void AAMGuidance()
        {
            Vector3 aamTarget;
            if (TargetAcquired)
            {
                DrawDebugLine(transform.position + (part.rb.velocity * Time.fixedDeltaTime), TargetPosition);
                float timeToImpact;
                aamTarget = MissileGuidance.GetAirToAirTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, out timeToImpact, optimumAirspeed);
                TimeToImpact = timeToImpact;
                if (Vector3.Angle(aamTarget - transform.position, transform.forward) > maxOffBoresight * 0.75f)
                {
                    aamTarget = TargetPosition;
                }

                //proxy detonation
                if (proxyDetonate && ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (transform.position)).sqrMagnitude < Mathf.Pow(GetBlastRadius() * 0.5f, 2))
                {
                    part.Destroy();
                }
            }
            else
            {
                aamTarget = transform.position + (20 * vessel.Velocity().normalized);
            }

            if (TimeIndex > dropTime + 0.25f)
            {
                DoAero(aamTarget);
                CheckMiss();
            }

        }

        void AGMGuidance()
        {
            if (TargetingMode != TargetingModes.Gps)
            {
                if (TargetAcquired)
                {
                    //lose lock if seeker reaches gimbal limit
                    float targetViewAngle = Vector3.Angle(transform.forward, TargetPosition - transform.position);

                    if (targetViewAngle > maxOffBoresight)
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: AGM Missile guidance failed - target out of view");
                        guidanceActive = false;
                    }
                    CheckMiss();
                }
                else
                {
                    if (TargetingMode == TargetingModes.Laser)
                    {
                        //keep going straight until found laser point
                        TargetPosition = laserStartPosition + (20000 * startDirection);
                    }
                }
            }

            Vector3 agmTarget = MissileGuidance.GetAirToGroundTarget(TargetPosition, TargetVelocity, vessel, agmDescentRatio);
            DoAero(agmTarget);
        }

        void SLWGuidance()
        {
            Vector3 SLWTarget;
            if (TargetAcquired)
            {
                DrawDebugLine(transform.position + (part.rb.velocity * Time.fixedDeltaTime), TargetPosition);
                float timeToImpact;
                SLWTarget = MissileGuidance.GetAirToAirTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, out timeToImpact, optimumAirspeed);
                TimeToImpact = timeToImpact;
                if (Vector3.Angle(SLWTarget - transform.position, transform.forward) > maxOffBoresight * 0.75f)
                {
                    SLWTarget = TargetPosition;
                }

                //proxy detonation
                if (proxyDetonate && ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (transform.position)).sqrMagnitude < Mathf.Pow(GetBlastRadius() * 0.5f, 2))
                {
                    part.Destroy();
                }
            }
            else
            {
                SLWTarget = transform.position + (20 * vessel.Velocity().normalized);
            }

            if (TimeIndex > dropTime + 0.25f)
            {
                DoAero(SLWTarget);
            }

            if (SLWTarget.y > 0f) SLWTarget.y = getSWLWOffset;

            CheckMiss();

        }

        void DoAero(Vector3 targetPosition)
        {
            aeroTorque = MissileGuidance.DoAeroForces(this, targetPosition, liftArea, controlAuthority * steerMult, aeroTorque, finalMaxTorque, maxAoA);
        }

        void AGMBallisticGuidance()
        {
            DoAero(CalculateAGMBallisticGuidance(this, TargetPosition));
        }

        public override void Detonate()
        {
            if (HasExploded || !HasFired) return;

            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: Detonate Triggered");

            BDArmorySetup.numberOfParticleEmitters--;
            HasExploded = true;

            if (legacyTargetVessel != null)
            {
                using (var wpm = VesselModuleRegistry.GetModules<MissileFire>(legacyTargetVessel).GetEnumerator())
                    while (wpm.MoveNext())
                    {
                        if (wpm.Current == null) continue;
                        wpm.Current.missileIsIncoming = false;
                    }
            }

            if (SourceVessel == null) SourceVessel = vessel;

            if (part.FindModuleImplementing<BDExplosivePart>() != null)
            {
                part.FindModuleImplementing<BDExplosivePart>().DetonateIfPossible();
            }
            else if (part.FindModuleImplementing<BDModuleNuke>() != null)
            {
                part.FindModuleImplementing<BDModuleNuke>().Detonate();
            }
            else //TODO: Remove this backguard compatibility
            {
                Vector3 position = transform.position;//+rigidbody.velocity*Time.fixedDeltaTime;

                ExplosionFx.CreateExplosion(position, blastPower, explModelPath, explSoundPath, ExplosionSourceType.Missile, 0, part, SourceVessel.vesselName, part.FindModuleImplementing<EngageableWeapon>().GetShortName(), default(Vector3), -1, false, part.mass * 1000);
            }

            List<BDAGaplessParticleEmitter>.Enumerator e = gaplessEmitters.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current == null) continue;
                e.Current.gameObject.AddComponent<BDAParticleSelfDestruct>();
                e.Current.transform.parent = null;
                if (e.Current.GetComponent<Light>())
                {
                    e.Current.GetComponent<Light>().enabled = false;
                }
            }
            e.Dispose();

            if (part != null)
            {
                part.Destroy();
                part.explode();
            }
        }

        public override Vector3 GetForwardTransform()
        {
            return MissileReferenceTransform.forward;
        }

        protected override void PartDie(Part p)
        {
            if (p == part)
            {
                Detonate();
                BDATargetManager.FiredMissiles.Remove(this);
                GameEvents.onPartDie.Remove(PartDie);
            }
        }

        public static bool CheckIfMissile(Part p)
        {
            return p.GetComponent<MissileLauncher>();
        }

        void WarnTarget()
        {
            if (legacyTargetVessel == null) return;
            var wpm = VesselModuleRegistry.GetMissileFire(legacyTargetVessel, true);
            if (wpm != null) wpm.MissileWarning(Vector3.Distance(transform.position, legacyTargetVessel.transform.position), this);
        }

        void SetupRCS()
        {
            rcsFiredTimes = new float[] { 0, 0, 0, 0 };
            rcsTransforms = new KSPParticleEmitter[] { upRCS, leftRCS, rightRCS, downRCS };
        }

        void DoRCS()
        {
            try
            {
                Vector3 relV = TargetVelocity - vessel.obt_velocity;

                for (int i = 0; i < 4; i++)
                {
                    //float giveThrust = Mathf.Clamp(-localRelV.z, 0, rcsThrust);
                    float giveThrust = Mathf.Clamp(Vector3.Project(relV, rcsTransforms[i].transform.forward).magnitude * -Mathf.Sign(Vector3.Dot(rcsTransforms[i].transform.forward, relV)), 0, rcsThrust);
                    part.rb.AddForce(-giveThrust * rcsTransforms[i].transform.forward);

                    if (giveThrust > rcsRVelThreshold)
                    {
                        rcsAudioMinInterval = UnityEngine.Random.Range(0.15f, 0.25f);
                        if (Time.time - rcsFiredTimes[i] > rcsAudioMinInterval)
                        {
                            if (sfAudioSource == null) SetupAudio();
                            sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/popThrust"));
                            rcsTransforms[i].emit = true;
                            rcsFiredTimes[i] = Time.time;
                        }
                    }
                    else
                    {
                        rcsTransforms[i].emit = false;
                    }

                    //turn off emit
                    if (Time.time - rcsFiredTimes[i] > rcsAudioMinInterval * 0.75f)
                    {
                        rcsTransforms[i].emit = false;
                    }
                }
            }
            catch (Exception e)
            {

                Debug.LogError("[BDArmory.MissileLauncher]: DEBUG " + e.Message);
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null part?: " + (part == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG part: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null part.rb?: " + (part.rb == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG part.rb: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null vessel?: " + (vessel == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG vessel: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null sfAudioSource?: " + (sfAudioSource == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: sfAudioSource: " + e2.Message); }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null rcsTransforms?: " + (rcsTransforms == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG rcsTransforms: " + e2.Message); }
                if (rcsTransforms != null)
                {
                    for (int i = 0; i < 4; ++i)
                        try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null rcsTransforms[" + i + "]?: " + (rcsTransforms[i] == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG rcsTransforms[" + i + "]: " + e2.Message); }
                }
                try { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG null rcsFiredTimes?: " + (rcsFiredTimes == null)); } catch (Exception e2) { Debug.LogError("[BDArmory.MissileLauncher]: DEBUG rcsFiredTimes: " + e2.Message); }
                throw; // Re-throw the exception so behaviour is unchanged so we see it.
            }
        }

        public void KillRCS()
        {
            if (upRCS) upRCS.emit = false;
            if (downRCS) downRCS.emit = false;
            if (leftRCS) leftRCS.emit = false;
            if (rightRCS) rightRCS.emit = false;
        }

        void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                try
                {
                    drawLabels();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BDArmory.MissileLauncher]: Exception thrown in OnGUI: " + e.Message + "\n" + e.StackTrace);
                }
            }
        }

        void AntiSpin()
        {
            part.rb.angularDrag = 0;
            part.angularDrag = 0;
            Vector3 spin = Vector3.Project(part.rb.angularVelocity, part.rb.transform.forward);// * 8 * Time.fixedDeltaTime;
            part.rb.angularVelocity -= spin;
            //rigidbody.maxAngularVelocity = 7;

            if (guidanceActive)
            {
                part.rb.angularVelocity -= 0.6f * part.rb.angularVelocity;
            }
            else
            {
                part.rb.angularVelocity -= 0.02f * part.rb.angularVelocity;
            }
        }

        void SimpleDrag()
        {
            part.dragModel = Part.DragModel.NONE;
            if (part.rb == null || part.rb.mass == 0) return;
            //float simSpeedSquared = (float)vessel.Velocity.sqrMagnitude;
            float simSpeedSquared = (part.rb.GetPointVelocity(part.transform.TransformPoint(simpleCoD)) + (Vector3)Krakensbane.GetFrameVelocity()).sqrMagnitude;
            Vector3 currPos = transform.position;
            float drag = deployed ? deployedDrag : simpleDrag;
            float dragMagnitude = (0.008f * part.rb.mass) * drag * 0.5f * simSpeedSquared * (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPos), FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody);
            Vector3 dragForce = dragMagnitude * vessel.Velocity().normalized;
            part.rb.AddForceAtPosition(-dragForce, transform.TransformPoint(simpleCoD));

            Vector3 torqueAxis = -Vector3.Cross(vessel.Velocity(), part.transform.forward).normalized;
            float AoA = Vector3.Angle(part.transform.forward, vessel.Velocity());
            AoA /= 20;
            part.rb.AddTorque(AoA * simpleStableTorque * dragMagnitude * torqueAxis);
        }

        void ParseModes()
        {
            homingType = homingType.ToLower();
            switch (homingType)
            {
                case "aam":
                    GuidanceMode = GuidanceModes.AAMLead;
                    break;

                case "aamlead":
                    GuidanceMode = GuidanceModes.AAMLead;
                    break;

                case "aampure":
                    GuidanceMode = GuidanceModes.AAMPure;
                    break;

                case "agm":
                    GuidanceMode = GuidanceModes.AGM;
                    break;

                case "agmballistic":
                    GuidanceMode = GuidanceModes.AGMBallistic;
                    break;

                case "cruise":
                    GuidanceMode = GuidanceModes.Cruise;
                    break;

                case "sts":
                    GuidanceMode = GuidanceModes.STS;
                    break;

                case "rcs":
                    GuidanceMode = GuidanceModes.RCS;
                    break;

                case "beamriding":
                    GuidanceMode = GuidanceModes.BeamRiding;
                    break;

                case "slw":
                    GuidanceMode = GuidanceModes.SLW;
                    break;

                default:
                    GuidanceMode = GuidanceModes.None;
                    break;
            }

            targetingType = targetingType.ToLower();
            switch (targetingType)
            {
                case "radar":
                    TargetingMode = TargetingModes.Radar;
                    break;

                case "heat":
                    TargetingMode = TargetingModes.Heat;
                    break;

                case "laser":
                    TargetingMode = TargetingModes.Laser;
                    break;

                case "gps":
                    TargetingMode = TargetingModes.Gps;
                    maxOffBoresight = 360;
                    break;

                case "antirad":
                    TargetingMode = TargetingModes.AntiRad;
                    break;

                default:
                    TargetingMode = TargetingModes.None;
                    break;
            }

            terminalGuidanceType = terminalGuidanceType.ToLower();
            switch (terminalGuidanceType)
            {
                case "radar":
                    TargetingModeTerminal = TargetingModes.Radar;
                    break;

                case "heat":
                    TargetingModeTerminal = TargetingModes.Heat;
                    break;

                case "laser":
                    TargetingModeTerminal = TargetingModes.Laser;
                    break;

                case "gps":
                    TargetingModeTerminal = TargetingModes.Gps;
                    maxOffBoresight = 360;
                    break;

                case "antirad":
                    TargetingModeTerminal = TargetingModes.AntiRad;
                    break;

                default:
                    TargetingModeTerminal = TargetingModes.None;
                    break;
            }
        }

        private string GetBrevityCode()
        {
            //torpedo: determine subtype
            if (missileType.ToLower() == "torpedo")
            {
                if ((TargetingMode == TargetingModes.Radar) && (activeRadarRange > 0))
                    return "Active Sonar";

                if ((TargetingMode == TargetingModes.Radar) && (activeRadarRange <= 0))
                    return "Passive Sonar";

                if ((TargetingMode == TargetingModes.Laser) || (TargetingMode == TargetingModes.Gps))
                    return "Optical/wireguided";

                if ((TargetingMode == TargetingModes.Heat))
                    return "Heat guided";

                if ((TargetingMode == TargetingModes.None))
                    return "Unguided";
            }

            if (missileType.ToLower() == "bomb")
            {
                if ((TargetingMode == TargetingModes.Laser) || (TargetingMode == TargetingModes.Gps))
                    return "JDAM";

                if ((TargetingMode == TargetingModes.None))
                    return "Unguided";
            }

            //else: missiles:

            if (TargetingMode == TargetingModes.Radar)
            {
                //radar: determine subtype
                if (activeRadarRange <= 0)
                    return "SARH";
                if (activeRadarRange > 0 && activeRadarRange < maxStaticLaunchRange)
                    return "Mixed SARH/F&F";
                if (activeRadarRange >= maxStaticLaunchRange)
                    return "Fire&Forget";
            }

            if (TargetingMode == TargetingModes.AntiRad)
                return "Fire&Forget";

            if (TargetingMode == TargetingModes.Heat)
                return "Fire&Forget";

            if (TargetingMode == TargetingModes.Laser)
                return "SALH";

            if (TargetingMode == TargetingModes.Gps)
            {
                return TargetingModeTerminal != TargetingModes.None ? "GPS/Terminal" : "GPS";
            }

            // default:
            return "Unguided";
        }

        // RMB info in editor
        public override string GetInfo()
        {
            ParseModes();

            StringBuilder output = new StringBuilder();
            output.AppendLine($"{missileType.ToUpper()} - {GetBrevityCode()}");
            output.Append(Environment.NewLine);
            output.AppendLine($"Targeting Type: {targetingType.ToLower()}");
            output.AppendLine($"Guidance Mode: {homingType.ToLower()}");
            if (missileRadarCrossSection != RadarUtils.RCS_MISSILES)
            {
                output.AppendLine($"Detectable cross section: {missileRadarCrossSection} m^2");
            }
            output.AppendLine($"Min Range: {minStaticLaunchRange} m");
            output.AppendLine($"Max Range: {maxStaticLaunchRange} m");

            if (TargetingMode == TargetingModes.Radar)
            {
                if (activeRadarRange > 0)
                {
                    output.AppendLine($"Active Radar Range: {activeRadarRange} m");
                    if (activeRadarLockTrackCurve.maxTime > 0)
                        output.AppendLine($"- Lock/Track: {activeRadarLockTrackCurve.Evaluate(activeRadarLockTrackCurve.maxTime)} m^2 @ {activeRadarLockTrackCurve.maxTime} km");
                    else
                        output.AppendLine($"- Lock/Track: {RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS} m^2 @ {activeRadarRange / 1000} km");
                    output.AppendLine($"- LOAL: {radarLOAL}");
                }
                output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                output.AppendLine($"Locked FOV: {lockedSensorFOV}");
            }

            if (TargetingMode == TargetingModes.Heat)
            {
                output.AppendLine($"All Aspect: {allAspect}");
                output.AppendLine($"Min Heat threshold: {heatThreshold}");
                output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                output.AppendLine($"Locked FOV: {lockedSensorFOV}");
            }

            if (TargetingMode == TargetingModes.Gps)
            {
                output.AppendLine($"Terminal Maneuvering: {terminalManeuvering}");
                if (terminalGuidanceType != "")
                {
                    output.AppendLine($"Terminal guidance: {terminalGuidanceType} @ distance: {terminalGuidanceDistance} m");

                    if (TargetingModeTerminal == TargetingModes.Radar)
                    {
                        output.AppendLine($"Active Radar Range: {activeRadarRange} m");
                        if (activeRadarLockTrackCurve.maxTime > 0)
                            output.AppendLine($"- Lock/Track: {activeRadarLockTrackCurve.Evaluate(activeRadarLockTrackCurve.maxTime)} m^2 @ {activeRadarLockTrackCurve.maxTime} km");
                        else
                            output.AppendLine($"- Lock/Track: {RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS} m^2 @ {activeRadarRange / 1000} km");
                        output.AppendLine($"- LOAL: {radarLOAL}");
                        output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                        output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                    }

                    if (TargetingModeTerminal == TargetingModes.Heat)
                    {
                        output.AppendLine($"All Aspect: {allAspect}");
                        output.AppendLine($"Min Heat threshold: {heatThreshold}");
                        output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                        output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                    }
                }
            }

            IEnumerator<PartModule> partModules = part.Modules.GetEnumerator();
            output.AppendLine($"Warhead:");
            while (partModules.MoveNext())
            {
                if (partModules.Current == null) continue;
                if (partModules.Current.moduleName == "BDExplosivePart")
                {
                    ((BDExplosivePart)partModules.Current).ParseWarheadType();
                    if (clusterbomb > 1)
                    {
                        output.AppendLine($"Cluster Bomb:");
                        output.AppendLine($"- Sub-Munition Count: {clusterbomb} ");
                    }
                    float tntMass = ((BDExplosivePart)partModules.Current).tntMass;
                    output.AppendLine($"- Blast radius: {Math.Round(BlastPhysicsUtils.CalculateBlastRange(tntMass), 2)} m");
                    output.AppendLine($"- tnt Mass: {tntMass} kg");
                    output.AppendLine($"- {((BDExplosivePart)partModules.Current).warheadReportingName} warhead");
                }
                else if (partModules.Current.moduleName == "ModuleEMP")
                {
                    float proximity = ((ModuleEMP)partModules.Current).proximity;
                    output.AppendLine($"- EMP Blast Radius: {proximity} m");
                }
                else if (partModules.Current.moduleName == "BDModuleNuke")
                {
                    float yield = ((BDModuleNuke)partModules.Current).yield;
                    float radius = ((BDModuleNuke)partModules.Current).thermalRadius;
                    output.AppendLine($"- Yield: {yield} kT");
                    output.AppendLine($"- Max radius: {radius} m");
                }
                else continue;
                break;
            }
            partModules.Dispose();

            return output.ToString();
        }

        #region ExhaustPrefabPooling
        static Dictionary<string, ObjectPool> exhaustPrefabPool = new Dictionary<string, ObjectPool>();
        List<GameObject> exhaustPrefabs = new List<GameObject>();

        static void AttachExhaustPrefab(string prefabPath, MissileLauncher missileLauncher, Transform exhaustTransform)
        {
            CreateExhaustPool(prefabPath);
            var exhaustPrefab = exhaustPrefabPool[prefabPath].GetPooledObject();
            exhaustPrefab.SetActive(true);
            using (var emitter = exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    emitter.Current.emit = false;
                }
            exhaustPrefab.transform.parent = exhaustTransform;
            exhaustPrefab.transform.localPosition = Vector3.zero;
            exhaustPrefab.transform.localRotation = Quaternion.identity;
            missileLauncher.exhaustPrefabs.Add(exhaustPrefab);
            missileLauncher.part.OnJustAboutToDie += missileLauncher.DetachExhaustPrefabs;
            missileLauncher.part.OnJustAboutToBeDestroyed += missileLauncher.DetachExhaustPrefabs;
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: Exhaust prefab " + exhaustPrefab.name + " added to " + missileLauncher.shortName + " on " + (missileLauncher.vessel != null ? missileLauncher.vessel.vesselName : "unknown"));
        }

        static void CreateExhaustPool(string prefabPath)
        {
            if (exhaustPrefabPool == null)
            { exhaustPrefabPool = new Dictionary<string, ObjectPool>(); }
            if (!exhaustPrefabPool.ContainsKey(prefabPath) || exhaustPrefabPool[prefabPath] == null || exhaustPrefabPool[prefabPath].poolObject == null)
            {
                var exhaustPrefabTemplate = GameDatabase.Instance.GetModel(prefabPath);
                exhaustPrefabTemplate.SetActive(false);
                exhaustPrefabPool[prefabPath] = ObjectPool.CreateObjectPool(exhaustPrefabTemplate, 1, true, true);
            }
        }

        void DetachExhaustPrefabs()
        {
            if (part != null)
            {
                part.OnJustAboutToDie -= DetachExhaustPrefabs;
                part.OnJustAboutToBeDestroyed -= DetachExhaustPrefabs;
            }
            foreach (var exhaustPrefab in exhaustPrefabs)
            {
                if (exhaustPrefab == null) continue;
                exhaustPrefab.transform.parent = null;
                exhaustPrefab.SetActive(false);
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.MissileLauncher]: Exhaust prefab " + exhaustPrefab.name + " removed from " + shortName + " on " + (vessel != null ? vessel.vesselName : "unknown"));
            }
            exhaustPrefabs.Clear();
        }
        #endregion
    }
}
