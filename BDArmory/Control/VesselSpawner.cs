using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.UI.Screens;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Core.Utils;
using BDArmory.Modules;
using BDArmory.Misc;
using BDArmory.UI;

namespace BDArmory.Control
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselSpawner : MonoBehaviour
    {
        public static VesselSpawner Instance;

        // Interesting spawn locations on Kerbin.
        private static string _spawnProbeLocation = null;
        public static string spawnProbeLocation
        {
            get
            {
                if (_spawnProbeLocation != null) return _spawnProbeLocation;
                _spawnProbeLocation = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "BDArmory", "craft", "SpawnProbe.craft"); // SpaceDock location
                if (!File.Exists(_spawnProbeLocation)) _spawnProbeLocation = Path.Combine(KSPUtil.ApplicationRootPath, "Ships", "SPH", "SpawnProbe.craft"); // CKAN location
                if (!File.Exists(_spawnProbeLocation))
                {
                    _spawnProbeLocation = null;
                    var message = "SpawnProbe.craft is missing. Your installation is likely corrupt.";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.LogError("[BDArmory.VesselSpawner]: " + message);
                }
                return _spawnProbeLocation;
            }
        }
        [VesselSpawnerField] public static bool UpdateSpawnLocations = true;
        [VesselSpawnerField] public static List<SpawnLocation> spawnLocations;

        private string message;
        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        void Start()
        {
            VesselSpawnerField.Load();
            spawnLocationCamera = new GameObject("StationaryCameraParent");
            spawnLocationCamera = (GameObject)Instantiate(spawnLocationCamera, Vector3.zero, Quaternion.identity);
            spawnLocationCamera.SetActive(false);
            if (BDArmorySettings.HACK_INTAKES) HackIntakesOnNewVessels(true);
        }

        void OnDestroy()
        {
            VesselSpawnerField.Save();
            Destroy(spawnLocationCamera);
            HackIntakesOnNewVessels(false);
        }

        #region Camera Adjustment
        GameObject spawnLocationCamera;
        Transform originalCameraParentTransform;
        float originalCameraNearClipPlane;
        Coroutine delayedShowSpawnPointCoroutine;
        public void ShowSpawnPoint(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 100, bool spawning = false, bool recurse = true)
        {
            if (BDArmorySettings.ASTEROID_RAIN) { AsteroidRain.Instance.Reset(); }
            if (BDArmorySettings.ASTEROID_FIELD) { AsteroidField.Instance.Reset(); }
            if (!spawning && (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD))
            {
                if (!recurse)
                {
                    Debug.LogWarning($"[BDArmory.VesselSpawner]: No active vessel, unable to show spawn point.");
                    return;
                }
                Debug.LogWarning($"[BDArmory.VesselSpawner]: Active vessel is dead or packed, spawning a new one.");
                if (delayedShowSpawnPointCoroutine != null) { StopCoroutine(delayedShowSpawnPointCoroutine); delayedShowSpawnPointCoroutine = null; }
                delayedShowSpawnPointCoroutine = StartCoroutine(DelayedShowSpawnPoint(worldIndex, latitude, longitude, altitude, distance, spawning));
                return;
            }
            if (!spawning)
            {
                var overLand = (worldIndex != -1 ? FlightGlobals.Bodies[worldIndex] : FlightGlobals.currentMainBody).TerrainAltitude(latitude, longitude) > 0;
                FlightGlobals.fetch.SetVesselPosition(worldIndex != -1 ? worldIndex : FlightGlobals.currentMainBody.flightGlobalsIndex, latitude, longitude, overLand ? Math.Max(5, altitude) : altitude, FlightGlobals.ActiveVessel.vesselType == VesselType.Plane ? 0 : 90, 0, true, overLand); // FIXME This should be using the vessel reference transform to determine the inclination. Also below.
                FlightCamera.fetch.SetDistance(distance);
                VehiclePhysics.Gravity.Refresh();
            }
            else
            {
                FlightGlobals.fetch.SetVesselPosition(worldIndex != -1 ? worldIndex : FlightGlobals.currentMainBody.flightGlobalsIndex, latitude, longitude, altitude, 0, 0, true);
                var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(latitude, longitude);
                var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitude, longitude, terrainAltitude + altitude);
                var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
                var refDirection = Math.Abs(Vector3.Dot(Vector3.up, radialUnitVector)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
                var flightCamera = FlightCamera.fetch;
                var cameraPosition = Vector3.RotateTowards(distance * radialUnitVector, Vector3.Cross(radialUnitVector, refDirection), 70f * Mathf.Deg2Rad, 0);
                if (!spawnLocationCamera.activeSelf)
                {
                    spawnLocationCamera.SetActive(true);
                    originalCameraParentTransform = flightCamera.transform.parent;
                    originalCameraNearClipPlane = BDGUIUtils.GetMainCamera().nearClipPlane;
                }
                spawnLocationCamera.transform.position = spawnPoint;
                spawnLocationCamera.transform.rotation = Quaternion.LookRotation(-cameraPosition, radialUnitVector);
                flightCamera.transform.parent = spawnLocationCamera.transform;
                flightCamera.SetTarget(spawnLocationCamera.transform);
                flightCamera.transform.position = spawnPoint + cameraPosition;
                flightCamera.transform.rotation = Quaternion.LookRotation(-flightCamera.transform.position, radialUnitVector);
                flightCamera.SetDistance(distance);
            }
        }

        IEnumerator DelayedShowSpawnPoint(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 100, bool spawning = false)
        {
            var dummyVar = EditorFacility.None;
            Vector3d dummySpawnCoords;
            FlightGlobals.currentMainBody.GetLatLonAlt(FlightCamera.fetch.transform.position + 1000f * (FlightCamera.fetch.transform.position - FlightGlobals.currentMainBody.transform.position).normalized, out dummySpawnCoords.x, out dummySpawnCoords.y, out dummySpawnCoords.z);
            Vessel spawnProbe = SpawnVesselFromCraftFile(spawnProbeLocation, dummySpawnCoords, 0, 0f, out dummyVar);
            spawnProbe.Landed = false;
            // spawnProbe.situation = Vessel.Situations.FLYING;
            // spawnProbe.IgnoreGForces(240);
            yield return new WaitWhile(() => spawnProbe != null && (!spawnProbe.loaded || spawnProbe.packed));
            FlightGlobals.ForceSetActiveVessel(spawnProbe);
            var wait = new WaitForFixedUpdate();
            while (spawnProbe != null && FlightGlobals.ActiveVessel != spawnProbe)
            {
                spawnProbe.SetWorldVelocity(Vector3d.zero);
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnProbe);
                yield return wait;
            }
            ShowSpawnPoint(worldIndex, latitude, longitude, altitude, distance, spawning, false);
        }

        public void RevertSpawnLocationCamera(bool keepTransformValues = true)
        {
            if (!spawnLocationCamera.activeSelf) return;
            if (delayedShowSpawnPointCoroutine != null) { StopCoroutine(delayedShowSpawnPointCoroutine); delayedShowSpawnPointCoroutine = null; }
            var flightCamera = FlightCamera.fetch;
            if (originalCameraParentTransform != null)
            {
                if (keepTransformValues && flightCamera.transform != null && flightCamera.transform.parent != null)
                {
                    originalCameraParentTransform.position = flightCamera.transform.parent.position;
                    originalCameraParentTransform.rotation = flightCamera.transform.parent.rotation;
                    originalCameraNearClipPlane = BDGUIUtils.GetMainCamera().nearClipPlane;
                }
                flightCamera.transform.parent = originalCameraParentTransform;
                BDGUIUtils.GetMainCamera().nearClipPlane = originalCameraNearClipPlane;
            }
            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.state != Vessel.State.DEAD)
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(FlightGlobals.ActiveVessel); // Update the camera.
            spawnLocationCamera.SetActive(false);
        }
        #endregion

        #region Utils
        public Dictionary<string, string> originalTeams = new Dictionary<string, string>();
        public void SaveTeams()
        {
            originalTeams.Clear();
            foreach (var weaponManager in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList())
            {
                originalTeams[weaponManager.vessel.vesselName] = weaponManager.Team.Name;
            }
        }

        public int CountActiveEngines(Vessel vessel)
        {
            return VesselModuleRegistry.GetModules<ModuleEngines>(vessel).Where(engine => engine.EngineIgnited).ToList().Count + FireSpitter.CountActiveEngines(vessel);
        }

        public void ActivateAllEngines(Vessel vessel, bool activate = true)
        {
            foreach (var engine in VesselModuleRegistry.GetModules<ModuleEngines>(vessel))
            {
                var mme = engine.part.FindModuleImplementing<MultiModeEngine>();
                if (mme == null)
                {
                    if (activate) engine.Activate();
                    else engine.Shutdown();
                }
                else
                {
                    if (mme.runningPrimary)
                    {
                        if (activate && !mme.PrimaryEngine.EngineIgnited)
                        {
                            mme.PrimaryEngine.Activate();
                        }
                        else if (!activate && mme.PrimaryEngine.EngineIgnited)
                        {
                            mme.PrimaryEngine.Shutdown();
                        }
                    }
                    else
                    {
                        if (activate && !mme.SecondaryEngine.EngineIgnited)
                        {
                            mme.SecondaryEngine.Activate();
                        }
                        else if (!activate && mme.SecondaryEngine.EngineIgnited)
                        {
                            mme.SecondaryEngine.Shutdown();
                        }
                    }
                }
            }
            FireSpitter.ActivateFSEngines(vessel, activate);
        }

        public void HackIntakesOnNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(HackIntakesEventHandler);
                GameEvents.OnVesselRollout.Add(HackIntakes);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(HackIntakesEventHandler);
                GameEvents.OnVesselRollout.Remove(HackIntakes);
            }
        }
        void HackIntakesEventHandler(Vessel vessel) => HackIntakes(vessel, true);

        public void HackIntakes(Vessel vessel, bool enable)
        {
            if (vessel == null || !vessel.loaded) return;
            if (enable)
            {
                foreach (var intake in VesselModuleRegistry.GetModules<ModuleResourceIntake>(vessel))
                    intake.checkForOxygen = false;
            }
            else
            {
                foreach (var intake in VesselModuleRegistry.GetModules<ModuleResourceIntake>(vessel))
                {
                    var checkForOxygen = ConfigNodeUtils.FindPartModuleConfigNodeValue(intake.part.partInfo.partConfig, "ModuleResourceIntake", "checkForOxygen");
                    if (!string.IsNullOrEmpty(checkForOxygen)) // Use the default value from the part.
                    {
                        try
                        {
                            intake.checkForOxygen = bool.Parse(checkForOxygen);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BDArmory.BDArmorySetup]: Failed to parse checkForOxygen configNode of {intake.name}: {e.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[BDArmory.BDArmorySetup]: No default value for checkForOxygen found in partConfig for {intake.name}, defaulting to true.");
                        intake.checkForOxygen = true;
                    }
                }
            }
        }
        public void HackIntakes(ShipConstruct ship) // This version only needs to enable the hack.
        {
            if (ship == null) return;
            foreach (var part in ship.Parts)
            {
                var intakes = part.FindModulesImplementing<ModuleResourceIntake>();
                if (intakes.Count() > 0)
                {
                    foreach (var intake in intakes)
                        intake.checkForOxygen = false;
                }
            }
        }
        #endregion

        public enum SpawnFailureReason { None, NoCraft, NoTerrain, InvalidVessel, VesselLostParts, VesselFailedToSpawn, TimedOut };
        public SpawnFailureReason spawnFailureReason = SpawnFailureReason.None;
        public bool vesselsSpawning = false;
        public bool vesselSpawnSuccess = false;
        public int spawnedVesselCount = 0;

        // Cancel all spawning modes.
        public void CancelVesselSpawn()
        {
            // Single spawn
            if (vesselsSpawning)
            {
                vesselsSpawning = false;
                message = "Vessel spawning cancelled.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
            }
            if (spawnAllVesselsOnceCoroutine != null)
            {
                StopCoroutine(spawnAllVesselsOnceCoroutine);
                spawnAllVesselsOnceCoroutine = null;
            }

            // Continuous spawn
            if (vesselsSpawningContinuously)
            {
                vesselsSpawningContinuously = false;
                if (continuousSpawningScores != null)
                    DumpContinuousSpawningScores();
                continuousSpawningScores = null;
                message = "Continuous vessel spawning cancelled.";
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                BDACompetitionMode.Instance.ResetCompetitionStuff();
            }
            if (spawnVesselsContinuouslyCoroutine != null)
            {
                StopCoroutine(spawnVesselsContinuouslyCoroutine);
                spawnVesselsContinuouslyCoroutine = null;
            }

            // Continuous single spawn
            if (vesselsSpawningOnceContinuously)
            {
                vesselsSpawningOnceContinuously = false;
                message = "Continuous single spawning cancelled.";
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }
            if (spawnAllVesselsOnceContinuouslyCoroutine != null)
            {
                StopCoroutine(spawnAllVesselsOnceContinuouslyCoroutine);
                spawnAllVesselsOnceContinuouslyCoroutine = null;
            }

            RevertSpawnLocationCamera(true);
        }

        #region Single spawning
        public void SpawnAllVesselsOnce(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 10f, bool absDistanceOrFactor = false, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string spawnFolder = null, List<string> craftFiles = null)
        {
            SpawnAllVesselsOnce(new SpawnConfig(worldIndex, latitude, longitude, altitude, distance, absDistanceOrFactor, easeInSpeed, killEverythingFirst, assignTeams, numberOfTeams, teamCounts, teamsSpecific, spawnFolder, craftFiles));
        }

        public void SpawnAllVesselsOnce(SpawnConfig spawnConfig)
        {
            //Reset gravity
            if (BDArmorySettings.GRAVITY_HACKS)
            {
                PhysicsGlobals.GraviticForceMultiplier = 1d;
                VehiclePhysics.Gravity.Refresh();
            }

            // If we're on another planetary body, first switch to the proper one.
            if (spawnConfig.worldIndex != FlightGlobals.currentMainBody.flightGlobalsIndex)
            { ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 20); }

            vesselsSpawning = true; // Signal that we've started the spawning vessels routine.
            vesselSpawnSuccess = false; // Set our success flag to false for now.
            spawnFailureReason = SpawnFailureReason.None;
            if (spawnAllVesselsOnceCoroutine != null)
                StopCoroutine(spawnAllVesselsOnceCoroutine);
            RevertSpawnLocationCamera(true);
            spawnAllVesselsOnceCoroutine = StartCoroutine(SpawnAllVesselsOnceCoroutine(spawnConfig));
            Debug.Log("[BDArmory.VesselSpawner]: Triggering vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
        }

        private Coroutine spawnAllVesselsOnceCoroutine;
        // Spawns all vessels in an outward facing ring and lowers them to the ground. An altitude of 5m should be suitable for most cases.
        private IEnumerator SpawnAllVesselsOnceCoroutine(int worldIndex, double latitude, double longitude, double altitude, float spawnDistanceFactor, bool absDistanceOrFactor, float easeInSpeed, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string folder = null, List<string> craftFiles = null)
        {
            yield return SpawnAllVesselsOnceCoroutine(new SpawnConfig(worldIndex, latitude, longitude, altitude, spawnDistanceFactor, absDistanceOrFactor, easeInSpeed, killEverythingFirst, assignTeams, numberOfTeams, teamCounts, teamsSpecific, folder, craftFiles));
        }
        private IEnumerator SpawnAllVesselsOnceCoroutine(SpawnConfig spawnConfig)
        {
            #region Initialisation and sanity checks
            // Tally up the craft to spawn.
            if (spawnConfig.teamsSpecific == null)
            {
                if (spawnConfig.numberOfTeams == 1) // Scan subfolders
                {
                    spawnConfig.teamsSpecific = new List<List<string>>();
                    var teamDirs = Directory.GetDirectories(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder));
                    if (teamDirs.Length == 0) // Make teams from each vessel in the spawn folder.
                    {
                        spawnConfig.numberOfTeams = -1; // Flag for treating craft files as folder names.
                        spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder)).Where(f => f.EndsWith(".craft")).ToList();
                        spawnConfig.teamsSpecific = spawnConfig.craftFiles.Select(f => new List<string> { f }).ToList();
                    }
                    else
                    {
                        var stripStartCount = Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn").Length;
                        Debug.Log("[BDArmory.VesselSpawner]: Spawning teams from folders " + string.Join(", ", teamDirs.Select(d => d.Substring(stripStartCount))));
                        foreach (var teamDir in teamDirs)
                        {
                            spawnConfig.teamsSpecific.Add(Directory.GetFiles(teamDir).Where(f => f.EndsWith(".craft")).ToList());
                        }
                        spawnConfig.craftFiles = spawnConfig.teamsSpecific.SelectMany(v => v.ToList()).ToList();
                    }
                }
                else // Just the specified folder.
                {
                    if (spawnConfig.craftFiles == null) // Prioritise the list of craftFiles if we're given them.
                        spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder)).Where(f => f.EndsWith(".craft")).ToList();
                }
            }
            else // Spawn the specific vessels.
            {
                spawnConfig.craftFiles = spawnConfig.teamsSpecific.SelectMany(v => v.ToList()).ToList();
            }
            if (spawnConfig.craftFiles.Count == 0)
            {
                message = "Vessel spawning: found no craft files in " + Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder);
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                vesselsSpawning = false;
                spawnFailureReason = SpawnFailureReason.NoCraft;
                yield break;
            }
            bool useOriginalTeamNames = spawnConfig.assignTeams && (spawnConfig.numberOfTeams == 1 || spawnConfig.numberOfTeams == -1); // We'll be using the folders or craft filenames as team names in the originalTeams dictionary.
            if (spawnConfig.teamsSpecific != null && !useOriginalTeamNames)
            {
                spawnConfig.teamCounts = spawnConfig.teamsSpecific.Select(tl => tl.Count).ToList();
            }
            if (BDArmorySettings.VESSEL_SPAWN_RANDOM_ORDER) spawnConfig.craftFiles.Shuffle(); // Randomise the spawn order.
            spawnedVesselCount = 0; // Reset our spawned vessel count.
            message = "Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m" + (spawnConfig.craftFiles.Count > 8 ? ", this may take some time..." : ".");
            Debug.Log("[BDArmory.VesselSpawner]: " + message);
            var spawnAirborne = spawnConfig.altitude > 10;
            var spawnDistance = spawnConfig.craftFiles.Count > 1 ? (spawnConfig.absDistanceOrFactor ? spawnConfig.distance : (spawnConfig.distance + spawnConfig.distance * spawnConfig.craftFiles.Count)) : 0f; // If it's a single craft, spawn it at the spawn point.
            if (BDACompetitionMode.Instance) // Reset competition stuff.
            {
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                BDACompetitionMode.Instance.LogResults("due to spawning", "auto-dump-from-spawning"); // Log results first.
                BDACompetitionMode.Instance.StopCompetition();
                BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset competition scores.
                BDACompetitionMode.Instance.RemoveDebrisNow(); // Remove debris and space junk.
            }
            yield return new WaitForFixedUpdate();
            #endregion

            #region Pre-spawning
            if (spawnConfig.killEverythingFirst)
            {
                var vesselsToKill = FlightGlobals.Vessels.ToList();
                // Spawn in the SpawnProbe at the camera position and switch to it so that we can clean up the other vessels properly.
                var dummyVar = EditorFacility.None;
                Vector3d dummySpawnCoords;
                FlightGlobals.currentMainBody.GetLatLonAlt(FlightCamera.fetch.transform.position + 100f * (FlightCamera.fetch.transform.position - FlightGlobals.currentMainBody.transform.position).normalized, out dummySpawnCoords.x, out dummySpawnCoords.y, out dummySpawnCoords.z);
                if (spawnProbeLocation == null) yield break;
                Vessel spawnProbe = SpawnVesselFromCraftFile(spawnProbeLocation, dummySpawnCoords, 0, 0f, out dummyVar);
                if (spawnProbe == null)
                {
                    message = "Failed to spawn SpawnProbe!";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.LogError("[BDArmory.VesselSpawner]: " + message);
                    yield break;
                }
                spawnProbe.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                yield return new WaitWhile(() => spawnProbe != null && (!spawnProbe.loaded || spawnProbe.packed));
                while (spawnProbe != null && FlightGlobals.ActiveVessel != spawnProbe)
                {
                    LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnProbe);
                    yield return new WaitForFixedUpdate();
                }
                // Kill all other vessels (including debris).
                foreach (var vessel in vesselsToKill)
                    RemoveVessel(vessel);
                // Finally, remove the SpawnProbe
                RemoveVessel(spawnProbe);
                originalTeams.Clear();
            }
            while (removeVesselsPending > 0)
                yield return new WaitForFixedUpdate();
            #endregion

            #region Spawning
            // Get the spawning point in world position coordinates.
            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
            var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            var localSurfaceNormal = radialUnitVector;
            Ray ray;
            RaycastHit hit;

            if (spawnConfig.killEverythingFirst)
            {
                // Update the floating origin offset, so that the vessels spawn within range of the physics.
                FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
                ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 2 * spawnDistance, true);
                // Re-acquire the spawning point after the floating origin shift.
                terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
                radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;

                if (terrainAltitude > 0) // Not over the ocean or on a surfaceless body.
                {
                    // Wait for the terrain to load in before continuing.
                    var testPosition = spawnPoint + 1000f * radialUnitVector;
                    var terrainDistance = 1000f + (float)spawnConfig.altitude;
                    var lastTerrainDistance = terrainDistance;
                    var distanceToCoMainBody = (testPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                    ray = new Ray(testPosition, -radialUnitVector);
                    message = "Waiting up to 10s for terrain to settle.";
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    var startTime = Planetarium.GetUniversalTime();
                    double lastStableTimeStart = startTime;
                    double stableTime = 0;
                    do
                    {
                        lastTerrainDistance = terrainDistance;
                        yield return new WaitForFixedUpdate();
                        terrainDistance = Physics.Raycast(ray, out hit, 2f * (float)(spawnConfig.altitude + distanceToCoMainBody), (int)LayerMasks.Scenery) ? hit.distance : -1f; // Oceans shouldn't be more than 10km deep...
                        if (terrainDistance < 0f) // Raycast is failing to find terrain.
                        {
                            if (Planetarium.GetUniversalTime() - startTime < 1) continue; // Give the terrain renderer a chance to spawn the terrain.
                            else break;
                        }
                        if (Math.Abs(lastTerrainDistance - terrainDistance) > 0.1f)
                            lastStableTimeStart = Planetarium.GetUniversalTime(); // Reset the stable time tracker.
                        stableTime = Planetarium.GetUniversalTime() - lastStableTimeStart;
                    } while (Planetarium.GetUniversalTime() - startTime < 10 && stableTime < 1f);
                    if (terrainDistance < 0)
                    {
                        if (!spawnAirborne)
                        {
                            message = "Failed to find terrain at the spawning point! Try increasing the spawn altitude.";
                            Debug.Log("[BDArmory.VesselSpawner]: " + message);
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            vesselsSpawning = false;
                            spawnFailureReason = SpawnFailureReason.NoTerrain;
                            yield break;
                        }
                    }
                    else
                    {
                        spawnPoint = hit.point + (float)spawnConfig.altitude * hit.normal;
                        localSurfaceNormal = hit.normal;
                    }
                }
            }
            else if ((spawnPoint - FloatingOrigin.fetch.offset).magnitude > 100e3)
            {
                message = "WARNING The spawn point is " + ((spawnPoint - FloatingOrigin.fetch.offset).magnitude / 1000).ToString("G4") + "km away. Expect vessels to be killed immediately.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }

            // Spawn the craft in an outward facing ring.
            Debug.Log("[BDArmory.VesselSpawner]: Spawning vessels...");
            var spawnedVessels = new Dictionary<string, Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>>();
            Vector3d craftGeoCoords;
            Vector3 craftSpawnPosition;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, radialUnitVector)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            string failedVessels = "";
            var shipFacility = EditorFacility.None;
            List<List<string>> teamVesselNames = null;
            if (spawnConfig.teamsSpecific == null)
            {
                foreach (var craftUrl in spawnConfig.craftFiles) // First spawn the vessels in the air.
                {
                    var heading = 360f * spawnedVesselCount / spawnConfig.craftFiles.Count;
                    var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(heading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                    craftSpawnPosition = spawnPoint + 1000f * radialUnitVector + spawnDistance * direction; // Spawn 1000m higher than asked for, then adjust the altitude later once the craft's loaded.
                    FlightGlobals.currentMainBody.GetLatLonAlt(craftSpawnPosition, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
                    Vessel vessel = null;
                    try
                    {
                        vessel = SpawnVesselFromCraftFile(craftUrl, craftGeoCoords, 0, 0f, out shipFacility); // SPAWN
                    }
                    catch { vessel = null; }
                    if (vessel == null)
                    {
                        var craftName = craftUrl.Substring(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder).Length + 1);
                        Debug.LogWarning("[BDArmory.VesselSpawner]: Failed to spawn craft " + craftName);
                        failedVessels += "\n  -  " + craftName;
                        continue;
                    }
                    vessel.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                    vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
                    if (spawnedVessels.ContainsKey(vessel.vesselName))
                    {
                        var count = 1;
                        var potentialName = vessel.vesselName + "_" + count;
                        while (spawnedVessels.ContainsKey(potentialName))
                            potentialName = vessel.vesselName + "_" + (++count);
                        vessel.vesselName = potentialName;
                    }
                    spawnedVessels.Add(vessel.vesselName, new Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>(vessel, craftSpawnPosition, direction, vessel.GetHeightFromTerrain() - 35f, shipFacility)); // Store the vessel, its spawning point (which is different from its position) and height from the terrain!
                    ++spawnedVesselCount;
                }
            }
            else
            {
                teamVesselNames = new List<List<string>>();
                var currentTeamNames = new List<string>();
                int spawnedTeamCount = 0;
                Vector3 teamSpawnPosition;
                foreach (var team in spawnConfig.teamsSpecific)
                {
                    currentTeamNames.Clear();
                    var teamHeading = 360f * spawnedTeamCount / spawnConfig.teamsSpecific.Count;
                    var teamDirection = Vector3.ProjectOnPlane(Quaternion.AngleAxis(teamHeading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                    teamSpawnPosition = spawnPoint + 1000f * radialUnitVector + spawnDistance * teamDirection; // Spawn 1000m hight than asked for, then adjust the altitude later once the craft's loaded.
                    int teamSpawnCount = 0;
                    foreach (var craftUrl in team)
                    {
                        var heading = 360f / team.Count * (teamSpawnCount - (team.Count - 1) / 2f) / team.Count + teamHeading;
                        var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(heading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                        craftSpawnPosition = teamSpawnPosition + spawnDistance / 4f * direction; // Spawn in clusters around the team spawn points.
                        FlightGlobals.currentMainBody.GetLatLonAlt(craftSpawnPosition, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
                        Vessel vessel = null;
                        try
                        {
                            vessel = SpawnVesselFromCraftFile(craftUrl, craftGeoCoords, 0, 0f, out shipFacility); // SPAWN
                        }
                        catch { vessel = null; }
                        if (vessel == null)
                        {
                            var craftName = craftUrl.Substring(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder).Length + 1);
                            Debug.Log("[BDArmory.VesselSpawner]: Failed to spawn craft " + craftName);
                            failedVessels += "\n  -  " + craftName;
                            continue;
                        }
                        vessel.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                        vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
                        if (spawnedVessels.ContainsKey(vessel.vesselName))
                        {
                            var count = 1;
                            var potentialName = vessel.vesselName + "_" + count;
                            while (spawnedVessels.ContainsKey(potentialName))
                                potentialName = vessel.vesselName + "_" + (++count);
                            vessel.vesselName = potentialName;
                        }
                        currentTeamNames.Add(vessel.vesselName);
                        if (spawnConfig.assignTeams)
                        {
                            switch (spawnConfig.numberOfTeams)
                            {
                                case 1: // Assign team names based on folders.
                                    {
                                        var paths = craftUrl.Split(Path.DirectorySeparatorChar);
                                        originalTeams[vessel.vesselName] = paths[paths.Length - 2];
                                        break;
                                    }
                                case -1: // Assign team names based on craft filename. We can't use vessel name as that can get adjusted above to avoid conflicts.
                                    {
                                        var paths = craftUrl.Split(Path.DirectorySeparatorChar);
                                        originalTeams[vessel.vesselName] = paths[paths.Length - 1].Substring(0, paths[paths.Length - 1].Length - 6);
                                        break;
                                    }
                            }
                        }
                        spawnedVessels.Add(vessel.vesselName, new Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>(vessel, craftSpawnPosition, direction, vessel.GetHeightFromTerrain() - 35f, shipFacility)); // Store the vessel, its spawning point (which is different from its position) and height from the terrain!
                        ++spawnedVesselCount;
                        ++teamSpawnCount;
                    }
                    teamVesselNames.Add(currentTeamNames.ToList());
                    ++spawnedTeamCount;
                }
            }
            if (failedVessels != "")
            {
                message = "Some vessels failed to spawn: " + failedVessels;
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
            }

            // Wait for an update so that the vessels' parts list gets updated.
            yield return new WaitForFixedUpdate();

            // Count the vessels' parts for checking later.
            var spawnedVesselPartCounts = new Dictionary<string, int>();
            foreach (var vesselName in spawnedVessels.Keys)
                spawnedVesselPartCounts.Add(vesselName, spawnedVessels[vesselName].Item1.parts.Count);

            // Wait another update so that the reference transforms get updated.
            yield return new WaitForFixedUpdate();

            // Now rotate them and put them at the right altitude.
            var finalSpawnPositions = new Dictionary<string, Vector3d>();
            var finalSpawnRotations = new Dictionary<string, Quaternion>();
            {
                var startTime = Time.time;
                // Sometimes if a vessel camera switch occurs, the craft appears unloaded for a couple of frames. This avoids NREs for control surfaces triggered by the change in reference transform.
                while (spawnedVessels.Values.All(vessel => vessel.Item1 != null && (vessel.Item1.ReferenceTransform == null || vessel.Item1.rootPart == null || vessel.Item1.rootPart.GetReferenceTransform() == null)) && (Time.time - startTime < 1f)) yield return new WaitForFixedUpdate();
                var nullVessels = spawnedVessels.Where(kvp => kvp.Value.Item1 == null).Select(kvp => kvp.Key).ToList();
                if (nullVessels.Count() > 0)
                {
                    message = string.Join(", ", nullVessels) + " disappeared during spawning!";
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    vesselsSpawning = false;
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                var noRootVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.rootPart == null).Select(kvp => kvp.Key).ToList();
                if (noRootVessels.Count() > 0)
                {
                    message = string.Join(", ", noRootVessels) + " had no root part during spawning!";
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    vesselsSpawning = false;
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                foreach (var vessel in spawnedVessels.Values.Select(sv => sv.Item1))
                {
                    vessel.SetReferenceTransform(vessel.rootPart); // Set the reference transform to the root part's transform.
                }
            }
            foreach (var vesselName in spawnedVessels.Keys)
            {
                var vessel = spawnedVessels[vesselName].Item1;
                if (vessel == null)
                {
                    message = "A vessel disappeared during spawning!";
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    vesselsSpawning = false;
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                craftSpawnPosition = spawnedVessels[vesselName].Item2;
                var direction = spawnedVessels[vesselName].Item3;
                var heightFromTerrain = spawnedVessels[vesselName].Item4;
                shipFacility = spawnedVessels[vesselName].Item5;
                var localRadialUnitVector = (craftSpawnPosition - FlightGlobals.currentMainBody.transform.position).normalized;
                ray = new Ray(craftSpawnPosition, -localRadialUnitVector);
                var distanceToCoMainBody = (craftSpawnPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                float distance;
                if (terrainAltitude > 0 && Physics.Raycast(ray, out hit, (float)(spawnConfig.altitude + distanceToCoMainBody), (int)LayerMasks.Scenery))
                {
                    distance = hit.distance;
                    localSurfaceNormal = hit.normal;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.VesselSpawner]: found terrain for spawn adjustments");
                }
                else
                {
                    distance = FlightGlobals.getAltitudeAtPos(craftSpawnPosition) - (float)terrainAltitude; // If the raycast fails or we're spawning over water, use the value from FlightGlobals and terrainAltitude of the original spawn point.
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.VesselSpawner]: failed to find terrain for spawn adjustments");
                }

                if (!spawnAirborne || BDArmorySettings.SF_GRAVITY) //spawn parallel to ground if surface or space spawning
                {
                    vessel.SetRotation(Quaternion.FromToRotation(shipFacility == EditorFacility.SPH ? -vessel.ReferenceTransform.forward : vessel.ReferenceTransform.up, localSurfaceNormal) * vessel.transform.rotation); // Re-orient the vessel to the terrain normal.
                    vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(shipFacility == EditorFacility.SPH ? vessel.ReferenceTransform.up : -vessel.ReferenceTransform.forward, direction, localSurfaceNormal), localSurfaceNormal) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
                }
                else
                {
                    vessel.SetRotation(Quaternion.FromToRotation(-vessel.ReferenceTransform.up, localRadialUnitVector) * vessel.transform.rotation); // Re-orient the vessel to the local gravity direction.
                    vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(-vessel.ReferenceTransform.forward, direction, localRadialUnitVector), localRadialUnitVector) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
                    vessel.SetRotation(Quaternion.AngleAxis(-10f, vessel.ReferenceTransform.right) * vessel.transform.rotation); // Tilt 10° outwards.
                }
                finalSpawnRotations[vesselName] = vessel.transform.rotation;
                if (FlightGlobals.currentMainBody.hasSolidSurface)
                    finalSpawnPositions[vesselName] = craftSpawnPosition + localRadialUnitVector * (float)(spawnConfig.altitude + heightFromTerrain - distance);
                else
                    finalSpawnPositions[vesselName] = craftSpawnPosition - 1000f * localRadialUnitVector;
                if (vessel.mainBody.ocean) // Check for being under water.
                {
                    var distanceUnderWater = -FlightGlobals.getAltitudeAtPos(finalSpawnPositions[vesselName]);
                    if (distanceUnderWater >= 0) // Under water, move the vessel to the surface.
                    {
                        vessel.Splashed = true; // Set the vessel as splashed.
                    }
                }
                vessel.SetPosition(finalSpawnPositions[vesselName]);
                if (BDArmorySettings.SPACE_HACKS)
                {
                    var SF = vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>();
                    if (SF == null)
                    {
                        SF = (ModuleSpaceFriction)vessel.rootPart.AddModule("ModuleSpaceFriction");
                    }
                }
                if (BDArmorySettings.MUTATOR_MODE)
                {
                    var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
                    if (MM == null)
                    {
                        MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                    }
                }
                if (BDArmorySettings.HACK_INTAKES)
                {
                    HackIntakes(vessel, true);
                }
                Debug.Log("[BDArmory.VesselSpawner]: Vessel " + vessel.vesselName + " spawned!");
            }
            #endregion

            #region Post-spawning
            yield return new WaitForFixedUpdate();
            // Revert the camera and focus on one as it lowers to the terrain.
            RevertSpawnLocationCamera(true);
            if ((FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD) && spawnedVessels.Count > 0)
            {
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnedVessels.First().Value.Item1); // Update the camera.
                FlightCamera.fetch.SetDistance(50);
            }

            // Validate vessels and wait for weapon managers to be registered.
            var postSpawnCheckStartTime = Planetarium.GetUniversalTime();
            var allWeaponManagersAssigned = false;
            var vesselsToCheck = spawnedVessels.Select(v => v.Value.Item1).ToList();
            if (vesselsToCheck.Count > 0)
            {
                List<Tuple<string, BDACompetitionMode.InvalidVesselReason>> invalidVessels;
                // Check that the spawned vessels are valid craft
                do
                {
                    yield return new WaitForFixedUpdate();
                    invalidVessels = vesselsToCheck.Select(vessel => new Tuple<string, BDACompetitionMode.InvalidVesselReason>(vessel.vesselName, BDACompetitionMode.Instance.IsValidVessel(vessel))).Where(t => t.Item2 != BDACompetitionMode.InvalidVesselReason.None).ToList();
                } while (invalidVessels.Count > 0 && Planetarium.GetUniversalTime() - postSpawnCheckStartTime < 1); // Give it up to 1s for KSP to populate the vessel's AI and WM.
                if (invalidVessels.Count > 0)
                {
                    BDACompetitionMode.Instance.competitionStatus.Add("The following vessels are invalid:\n - " + string.Join("\n - ", invalidVessels.Select(t => t.Item1 + " : " + t.Item2)));
                    Debug.Log("[BDArmory.VesselSpawner]: Invalid vessels: " + string.Join(", ", invalidVessels.Select(t => t.Item1 + ":" + t.Item2)));
                    spawnFailureReason = SpawnFailureReason.InvalidVessel;
                }
                else
                {
                    do
                    {
                        yield return new WaitForFixedUpdate();
                        CheckForRenamedVessels(spawnedVessels);

                        // Check that none of the vessels have lost parts.
                        if (spawnedVessels.Any(kvp => kvp.Value.Item1 == null || kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]))
                        {
                            var offendingVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]);
                            message = "One of the vessels lost parts after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value.Item1 != null ? kvp.Value.Item1.vesselName : null));
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            Debug.Log("[BDArmory.VesselSpawner]: " + message);
                            spawnFailureReason = SpawnFailureReason.VesselLostParts;
                            break;
                        }

                        // Wait for all the weapon managers to be added to LoadedVesselSwitcher.
                        LoadedVesselSwitcher.Instance.UpdateList();
                        var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
                        foreach (var vessel in vesselsToCheck.ToList())
                        {
                            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                            if (weaponManager != null && weaponManagers.Contains(weaponManager)) // The weapon manager has been added, let's go!
                            {
                                // Turn on the brakes.
                                spawnedVessels[vessel.vesselName].Item1.ActionGroups.SetGroup(KSPActionGroup.Brakes, false); // Disable them first to make sure they trigger on toggling.
                                spawnedVessels[vessel.vesselName].Item1.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);

                                vesselsToCheck.Remove(vessel);
                            }
                        }
                        if (vesselsToCheck.Count == 0)
                            allWeaponManagersAssigned = true;

                        if (allWeaponManagersAssigned)
                        {
                            if (spawnConfig.numberOfTeams != 1 && spawnConfig.numberOfTeams != -1) // Already assigned.
                                SaveTeams();
                            break;
                        }
                    } while (Planetarium.GetUniversalTime() - postSpawnCheckStartTime < 10); // Give it up to 10s for the weapon managers to get added to the LoadedVesselSwitcher's list.
                    if (!allWeaponManagersAssigned && spawnFailureReason == SpawnFailureReason.None)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Timed out waiting for weapon managers to appear in the Vessel Switcher.");
                        spawnFailureReason = SpawnFailureReason.TimedOut;
                    }
                }
            }

            // Reset craft positions and rotations as sometimes KSP packs and unpacks vessels between frames and resets things!
            foreach (var vesselName in spawnedVessels.Keys)
            {
                if (spawnedVessels[vesselName].Item1 == null) continue;
                spawnedVessels[vesselName].Item1.SetPosition(finalSpawnPositions[vesselName]);
                spawnedVessels[vesselName].Item1.SetRotation(finalSpawnRotations[vesselName]);
            }

            if (allWeaponManagersAssigned)
            {
                if (spawnConfig.altitude >= 0 && !spawnAirborne)
                {
                    // Prevent the vessels from falling too fast and check if their velocities in the surface normal direction is below a threshold.
                    var vesselsHaveLanded = spawnedVessels.Keys.ToDictionary(v => v, v => (int)0); // 1=started moving, 2=landed.
                    var landingStartTime = Planetarium.GetUniversalTime();
                    do
                    {
                        yield return new WaitForFixedUpdate();
                        foreach (var vesselName in spawnedVessels.Keys)
                        {
                            var vessel = spawnedVessels[vesselName].Item1;
                            if (vessel.LandedOrSplashed && Utils.GetRadarAltitudeAtPos(vessel.transform.position) <= 0) // Wait for the vessel to settle a bit in the water. The 15s buffer should be more than sufficient.
                            {
                                vesselsHaveLanded[vesselName] = 2;
                            }
                            if (vesselsHaveLanded[vesselName] == 0 && Vector3.Dot(vessel.srf_velocity, radialUnitVector) < 0) // Check that vessel has started moving.
                                vesselsHaveLanded[vesselName] = 1;
                            if (vesselsHaveLanded[vesselName] == 1 && Vector3.Dot(vessel.srf_velocity, radialUnitVector) >= 0) // Check if the vessel has landed.
                            {
                                vesselsHaveLanded[vesselName] = 2;
                                if (Utils.GetRadarAltitudeAtPos(vessel.transform.position) > 0)
                                    vessel.Landed = true; // Tell KSP that the vessel is landed.
                                else
                                    vessel.Splashed = true; // Tell KSP that the vessel is splashed.
                            }
                            if (vesselsHaveLanded[vesselName] == 1 && vessel.srf_velocity.sqrMagnitude > spawnConfig.easeInSpeed) // While the vessel hasn't landed, prevent it from moving too fast.
                                vessel.SetWorldVelocity(0.99 * spawnConfig.easeInSpeed * vessel.srf_velocity); // Move at VESSEL_SPAWN_EASE_IN_SPEED m/s at most.
                        }

                        // Check that none of the vessels have lost parts.
                        if (spawnedVessels.Any(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]))
                        {
                            var offendingVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]);
                            message = "One of the vessels lost parts after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value.Item1 != null ? kvp.Value.Item1.vesselName : null));
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            spawnFailureReason = SpawnFailureReason.VesselLostParts;
                            break;
                        }

                        if (vesselsHaveLanded.Values.All(v => v == 2))
                        {
                            vesselSpawnSuccess = true;
                            message = "Vessel spawning SUCCEEDED!";
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            break;
                        }
                    } while (Planetarium.GetUniversalTime() - landingStartTime < 15 + spawnConfig.altitude / spawnConfig.easeInSpeed); // Give the vessels up to (15 + altitude / VESSEL_SPAWN_EASE_IN_SPEED) seconds to land.
                    if (!vesselSpawnSuccess && spawnFailureReason == SpawnFailureReason.None)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Timed out waiting for the vessels to land.");
                        spawnFailureReason = SpawnFailureReason.TimedOut;
                    }
                }
                else
                {
                    // Check that none of the vessels have lost parts.
                    if (spawnedVessels.Any(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]))
                    {
                        var offendingVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]);
                        message = "One of the vessels lost parts after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value.Item1 != null ? kvp.Value.Item1.vesselName : null));
                        BDACompetitionMode.Instance.competitionStatus.Add(message);
                        spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    }
                    else
                    {
                        foreach (var vessel in spawnedVessels.Select(v => v.Value.Item1))
                        {
                            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                            if (!weaponManager) continue; // Safety check in case the vessel got destroyed.

                            // Activate the vessel with AG10, or failing that, staging.
                            vessel.ActionGroups.ToggleGroup(BDACompetitionMode.KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
                            weaponManager.AI.ActivatePilot();
                            weaponManager.AI.CommandTakeOff();
                            if (weaponManager.guardMode)
                            {
                                Debug.Log($"[BDArmory.VesselSpawner]: Disabling guardMode on {vessel.vesselName}.");
                                weaponManager.ToggleGuardMode(); // Disable guard mode (in case someone enabled it on AG10 or in the SPH).
                                weaponManager.SetTarget(null);
                            }

                            if (!BDArmorySettings.NO_ENGINES && CountActiveEngines(vessel) == 0) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.VesselSpawner]: " + vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.");
                                ActivateAllEngines(vessel);
                            }
                            else if (BDArmorySettings.NO_ENGINES && CountActiveEngines(vessel) > 0) // Vessel had some active engines. Turn them off if possible.
                            {
                                ActivateAllEngines(vessel, false);
                            }
                        }

                        vesselSpawnSuccess = true;
                    }
                }
                foreach (var vessel in spawnedVessels.Select(v => v.Value.Item1))
                    vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
            }
            if (!vesselSpawnSuccess)
            {
                message = "Vessel spawning FAILED! Reason: " + spawnFailureReason;
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }
            else
            {
                CheckForRenamedVessels(spawnedVessels);
                if (spawnConfig.assignTeams)
                {
                    // Assign the vessels to teams.
                    Debug.Log("[BDArmory.VesselSpawner]: Assigning vessels to teams.");
                    if (spawnConfig.teamsSpecific == null && spawnConfig.teamCounts == null && spawnConfig.numberOfTeams > 1)
                    {
                        int numberPerTeam = spawnedVesselCount / spawnConfig.numberOfTeams;
                        int residue = spawnedVesselCount - numberPerTeam * spawnConfig.numberOfTeams;
                        spawnConfig.teamCounts = new List<int>();
                        for (int team = 0; team < spawnConfig.numberOfTeams; ++team)
                            spawnConfig.teamCounts.Add(numberPerTeam + (team < residue ? 1 : 0));
                    }
                    LoadedVesselSwitcher.Instance.MassTeamSwitch(true, useOriginalTeamNames, spawnConfig.teamCounts, teamVesselNames);
                    yield return new WaitForFixedUpdate();
                }
            }

            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                foreach (var vesselName in spawnedVessels.Keys)
                {
                    CheckAIWMCounts(spawnedVessels[vesselName].Item1);
                    CheckAIWMPlacement(spawnedVessels[vesselName].Item1);
                }
            }
            #endregion

            Debug.Log("[BDArmory.VesselSpawner]: Vessel spawning " + (vesselSpawnSuccess ? "SUCCEEDED!" : "FAILED! " + spawnFailureReason));
            vesselsSpawning = false;
        }

        private bool vesselsSpawningOnceContinuously = false;
        public Coroutine spawnAllVesselsOnceContinuouslyCoroutine = null;

        public void SpawnAllVesselsOnceContinuously(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 10f, bool absDistanceOrFactor = false, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string spawnFolder = null, List<string> craftFiles = null)
        {
            SpawnAllVesselsOnceContinuously(new SpawnConfig(worldIndex, latitude, longitude, altitude, distance, absDistanceOrFactor, easeInSpeed, killEverythingFirst, assignTeams, numberOfTeams, teamCounts, teamsSpecific, spawnFolder, craftFiles));
        }
        public void SpawnAllVesselsOnceContinuously(SpawnConfig spawnConfig)
        {
            vesselsSpawningOnceContinuously = true;
            if (spawnAllVesselsOnceContinuouslyCoroutine != null)
                StopCoroutine(spawnAllVesselsOnceContinuouslyCoroutine);
            spawnAllVesselsOnceContinuouslyCoroutine = StartCoroutine(SpawnAllVesselsOnceContinuouslyCoroutine(spawnConfig));
            Debug.Log("[BDArmory.VesselSpawner]: Triggering vessel spawning (continuous single) at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
        }

        public IEnumerator SpawnAllVesselsOnceContinuouslyCoroutine(SpawnConfig spawnConfig)
        {
            while ((vesselsSpawningOnceContinuously) && (BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING))
            {
                SpawnAllVesselsOnce(spawnConfig);
                while (vesselsSpawning)
                    yield return new WaitForFixedUpdate();
                if (!vesselSpawnSuccess)
                {
                    vesselsSpawningOnceContinuously = false;
                    yield break;
                }
                yield return new WaitForFixedUpdate();

                // NOTE: runs in separate coroutine
                BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
                yield return new WaitForFixedUpdate(); // Give the competition start a frame to get going.

                // start timer coroutine for the duration specified in settings UI
                var duration = Core.BDArmorySettings.COMPETITION_DURATION * 60f;
                message = "Starting " + (duration > 0 ? "a " + duration.ToString("F0") + "s" : "an unlimited") + " duration competition.";
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                while (BDACompetitionMode.Instance.competitionStarting)
                    yield return new WaitForFixedUpdate(); // Wait for the competition to actually start.
                if (!BDACompetitionMode.Instance.competitionIsActive)
                {
                    var message = "Competition failed to start.";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    vesselsSpawningOnceContinuously = false;
                    yield break;
                }
                while (BDACompetitionMode.Instance.competitionIsActive) // Wait for the competition to finish (limited duration and log dumping is handled directly by the competition now).
                    yield return new WaitForSeconds(1);

                // Wait 10s for any user action
                double startTime = Planetarium.GetUniversalTime();
                if ((vesselsSpawningOnceContinuously) && (BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING))
                {
                    while ((Planetarium.GetUniversalTime() - startTime) < BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Waiting " + (BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS - (Planetarium.GetUniversalTime() - startTime)).ToString("0") + "s, then respawning pilots");
                        yield return new WaitForSeconds(1);
                    }
                }
            }
            vesselsSpawningOnceContinuously = false; // For when VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING gets toggled.
        }

        /// <summary>
        /// If the VESSELNAMING tag exists in the craft file, then KSP renames the vessel at some point after spawning.
        /// This function checks for renamed vessels and sets the name back to what it was.
        /// This must be called once after a yield, before using vessel.vesselName as an index in spawnedVessels.Keys.
        /// </summary>
        /// <param name="spawnedVessels"></param>
        void CheckForRenamedVessels(Dictionary<string, Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>> spawnedVessels)
        {
            foreach (var vesselName in spawnedVessels.Keys.ToList())
            {
                if (vesselName != spawnedVessels[vesselName].Item1.vesselName)
                {
                    spawnedVessels[vesselName].Item1.vesselName = vesselName;
                }
            }
        }
        #endregion

        #region Continuous spawning
        public bool vesselsSpawningContinuously = false;
        int continuousSpawnedVesselCount = 0;
        public void SpawnVesselsContinuously(int worldIndex, double latitude, double longitude, double altitude = 1000, float spawnDistanceFactor = 20f, bool absDistanceOrFactor = false, bool killEverythingFirst = true, string spawnFolder = null)
        {
            //Reset gravity
            if (BDArmorySettings.GRAVITY_HACKS)
            {
                PhysicsGlobals.GraviticForceMultiplier = 1d;
                VehiclePhysics.Gravity.Refresh();
            }

            vesselsSpawningContinuously = true;
            spawnFailureReason = SpawnFailureReason.None;
            continuousSpawningScores = new Dictionary<string, ContinuousSpawningScores>();
            if (spawnVesselsContinuouslyCoroutine != null)
                StopCoroutine(spawnVesselsContinuouslyCoroutine);
            RevertSpawnLocationCamera(true);
            spawnVesselsContinuouslyCoroutine = StartCoroutine(SpawnVesselsContinuouslyCoroutine(worldIndex, latitude, longitude, altitude, spawnDistanceFactor, absDistanceOrFactor, killEverythingFirst, spawnFolder));
            Debug.Log("[BDArmory.VesselSpawner]: Triggering continuous vessel spawning at " + BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.ToString("G6") + " at altitude " + altitude + "m.");
        }
        private Coroutine spawnVesselsContinuouslyCoroutine;
        HashSet<Vessel> vesselsToActivate = new HashSet<Vessel>();
        // Spawns all vessels in a downward facing ring and activates them (autopilot and AG10, then stage if no engines are firing), then respawns any that die. An altitude of 1000m should be plenty.
        // Note: initial vessel separation tends towards 2*pi*spawnDistanceFactor from above for >3 vessels.
        private IEnumerator SpawnVesselsContinuouslyCoroutine(int worldIndex, double latitude, double longitude, double altitude, float distance, bool absDistanceOrFactor, bool killEverythingFirst, string spawnFolder = null)
        {
            yield return SpawnVesselsContinuouslyCoroutine(new SpawnConfig(worldIndex, latitude, longitude, altitude, distance, absDistanceOrFactor, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, killEverythingFirst, true, 1, null, null, spawnFolder));
        }
        private IEnumerator SpawnVesselsContinuouslyCoroutine(SpawnConfig spawnConfig)
        {
            #region Initialisation and sanity checks
            // Tally up the craft to spawn.
            if (spawnConfig.craftFiles == null) // Prioritise the list of craftFiles if we're given them.
                spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder)).Where(f => f.EndsWith(".craft")).ToList();
            if (spawnConfig.craftFiles.Count == 0)
            {
                message = "Vessel spawning: found no craft files in " + Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder);
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                vesselsSpawning = false;
                spawnFailureReason = SpawnFailureReason.NoCraft;
                yield break;
            }
            spawnConfig.craftFiles.Shuffle(); // Randomise the spawn order.
            spawnConfig.altitude = Math.Max(100, spawnConfig.altitude); // Don't spawn too low.
            var spawnDistance = spawnConfig.craftFiles.Count > 1 ? (spawnConfig.absDistanceOrFactor ? spawnConfig.distance : spawnConfig.distance * (1 + (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count))) : 0f; // If it's a single craft, spawn it at the spawn point.
            continuousSpawnedVesselCount = 0; // Reset our spawned vessel count.
            if (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS == 0)
                message = "Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + (spawnConfig.craftFiles.Count > 8 ? "m, this may take some time..." : "m.");
            else
                message = "Spawning " + Math.Min(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS, spawnConfig.craftFiles.Count) + " of " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m with rolling-spawning.";
            Debug.Log("[BDArmory.VesselSpawner]: " + message);
            if (BDACompetitionMode.Instance) // Reset competition stuff.
            {
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                BDACompetitionMode.Instance.LogResults("due to continuous spawning", "auto-dump-from-spawning"); // Log results first.
                BDACompetitionMode.Instance.StopCompetition();
                BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset competition scores.
            }
            vesselsToActivate.Clear(); // Clear any pending vessel activations.
            yield return new WaitForFixedUpdate();
            #endregion

            #region Pre-spawning
            if (spawnConfig.killEverythingFirst)
            {
                // Kill all vessels (including debris).
                var vesselsToKill = FlightGlobals.Vessels.ToList();
                foreach (var vessel in vesselsToKill)
                    RemoveVessel(vessel);
            }
            while (removeVesselsPending > 0)
                yield return new WaitForFixedUpdate();
            #endregion

            #region Spawning
            // Get the spawning point in world position coordinates.
            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
            var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            Ray ray;
            RaycastHit hit;

            if (spawnConfig.killEverythingFirst)
            {
                // Update the floating origin offset, so that the vessels spawn within range of the physics. The terrain takes several frames to load, so we need to wait for the terrain to settle.
                FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
                ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 2 * spawnDistance, true);

                if (terrainAltitude > 0) // Not over the ocean or on a surfaceless body.
                {
                    // Wait for the terrain to load in before continuing.
                    var testPosition = 1000f * radialUnitVector;
                    var terrainDistance = 1000f + (float)spawnConfig.altitude;
                    var lastTerrainDistance = terrainDistance;
                    var distanceToCoMainBody = (testPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                    ray = new Ray(testPosition, -radialUnitVector);
                    message = "Waiting up to 10s for terrain to settle.";
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    var startTime = Planetarium.GetUniversalTime();
                    double lastStableTimeStart = startTime;
                    double stableTime = 0;
                    do
                    {
                        lastTerrainDistance = terrainDistance;
                        yield return new WaitForFixedUpdate();
                        terrainDistance = Physics.Raycast(ray, out hit, 2f * (float)(spawnConfig.altitude + distanceToCoMainBody), (int)LayerMasks.Scenery) ? hit.distance : -1f; // Oceans shouldn't be more than 10km deep...
                        if (terrainDistance < 0f || Math.Abs(lastTerrainDistance - terrainDistance) > 0.1f)
                            lastStableTimeStart = Planetarium.GetUniversalTime(); // Reset the stable time tracker.
                        stableTime = Planetarium.GetUniversalTime() - lastStableTimeStart;
                    } while (Planetarium.GetUniversalTime() - startTime < 10 && stableTime < 1f);
                }
            }
            else if ((spawnPoint - FloatingOrigin.fetch.offset).magnitude > 100e3)
            {
                message = "WARNING The spawn point is " + ((spawnPoint - FloatingOrigin.fetch.offset).magnitude / 1000).ToString("G4") + "km away. Expect vessels to be killed immediately.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }

            var craftURLToVesselName = new Dictionary<string, string>();
            var invalidVesselCount = new Dictionary<string, int>();
            Vector3d craftGeoCoords;
            Vector3 craftSpawnPosition;
            var shipFacility = EditorFacility.None;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, radialUnitVector)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            var geeDirection = FlightGlobals.getGeeForceAtPosition(Vector3.zero);
            var spawnSlots = OptimiseSpawnSlots(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count);
            var spawnCounts = spawnConfig.craftFiles.ToDictionary(c => c, c => 0);
            var spawnQueue = new Queue<string>();
            var craftToSpawn = new Queue<string>();
            bool initialSpawn = true;
            double currentUpdateTick;
            while (vesselsSpawningContinuously)
            {
                currentUpdateTick = BDACompetitionMode.Instance.nextUpdateTick;
                // Reacquire the spawn point as the local coordinate system may have changed (floating origin adjustments, local body rotation, etc.).
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
                radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
                // Check if sliders have changed.
                if (spawnSlots.Count != (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count))
                {
                    spawnSlots = OptimiseSpawnSlots(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count);
                    continuousSpawnedVesselCount %= spawnSlots.Count;
                }
                // Add any craft that hasn't been spawned or has died to the spawn queue if it isn't already in the queue. Note: we need to also check that the vessel isn't null as Unity makes it a fake null!
                foreach (var craftURL in spawnConfig.craftFiles.Where(craftURL => (BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL > 0 ? spawnCounts[craftURL] < BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL : true) && !spawnQueue.Contains(craftURL) && (!craftURLToVesselName.ContainsKey(craftURL) || (BDACompetitionMode.Instance.Scores.Players.Contains(craftURLToVesselName[craftURL]) && BDACompetitionMode.Instance.Scores.ScoreData[craftURLToVesselName[craftURL]].deathTime >= 0))))
                {
                    spawnQueue.Enqueue(craftURL);
                    ++spawnCounts[craftURL];
                }
                LoadedVesselSwitcher.Instance.UpdateList();
                var currentlyActive = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList().Count;
                if (spawnQueue.Count + vesselsToActivate.Count == 0 && currentlyActive < 2)// Nothing left to spawn or activate and only 1 vessel surviving. Time to call it quits and let the competition end.
                {
                    message = "Spawn queue is empty and not enough vessels are active, ending competition.";
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.StopCompetition();
                    break;
                }
                while (craftToSpawn.Count + vesselsToActivate.Count + currentlyActive < spawnSlots.Count && spawnQueue.Count > 0)
                    craftToSpawn.Enqueue(spawnQueue.Dequeue());
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    var missing = spawnConfig.craftFiles.Where(craftURL => craftURLToVesselName.ContainsKey(craftURL) && !craftToSpawn.Contains(craftURL) && !FlightGlobals.Vessels.Where(v => !VesselModuleRegistry.ignoredVesselTypes.Contains(v.vesselType) && VesselModuleRegistry.GetModuleCount<MissileFire>(v) > 0).Select(v => v.vesselName).Contains(craftURLToVesselName[craftURL])).ToList();
                    if (missing.Count > 0)
                    {
                        Debug.Log("[BDArmory.VesselSpawner]: MISSING vessels: " + string.Join(", ", craftURLToVesselName.Where(c => missing.Contains(c.Key)).Select(c => c.Value)));
                    }
                }
                if (craftToSpawn.Count > 0)
                {
                    VesselModuleRegistry.CleanRegistries(); // Clean out any old entries.
                    // Spawn the craft in a downward facing ring.
                    string failedVessels = "";
                    foreach (var craftURL in craftToSpawn)
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselSpawner]: Spawning vessel from {craftURL}");
                        var heading = 360f * spawnSlots[continuousSpawnedVesselCount] / spawnSlots.Count;
                        var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(heading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                        craftSpawnPosition = spawnPoint + spawnDistance * direction;
                        FlightGlobals.currentMainBody.GetLatLonAlt(craftSpawnPosition, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
                        Vessel vessel = null;
                        try
                        {
                            vessel = SpawnVesselFromCraftFile(craftURL, craftGeoCoords, 0, 0f, out shipFacility); // SPAWN
                        }
                        catch { vessel = null; }
                        if (vessel == null)
                        {
                            var craftName = craftURL.Substring(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder).Length + 1);
                            Debug.Log("[BDArmory.VesselSpawner]: Failed to spawn craft " + craftName);
                            failedVessels += "\n  -  " + craftName;
                            continue;
                        }
                        vessel.Landed = false; // Tell KSP that it's not landed.
                        vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
                        if (!craftURLToVesselName.ContainsKey(craftURL))
                        {
                            if (craftURLToVesselName.ContainsValue(vessel.vesselName)) // Avoid duplicate names.
                            {
                                var count = 1;
                                var potentialName = vessel.vesselName + "_" + count;
                                while (craftURLToVesselName.ContainsValue(potentialName))
                                    potentialName = vessel.vesselName + "_" + (++count);
                                vessel.vesselName = potentialName;
                            }
                            craftURLToVesselName.Add(craftURL, vessel.vesselName); // Store the craftURL -> vessel name.
                        }
                        vessel.vesselName = craftURLToVesselName[craftURL]; // Assign the same (potentially modified) name to the craft each time.
                        // If a competition is active, update the scoring structure.
                        if ((BDACompetitionMode.Instance.competitionStarting || BDACompetitionMode.Instance.competitionIsActive) && !BDACompetitionMode.Instance.Scores.Players.Contains(vessel.vesselName))
                        {
                            BDACompetitionMode.Instance.Scores.AddPlayer(vessel);
                        }
                        if (!vesselsToActivate.Contains(vessel))
                            vesselsToActivate.Add(vessel);
                        if (!continuousSpawningScores.ContainsKey(vessel.vesselName))
                            continuousSpawningScores.Add(vessel.vesselName, new ContinuousSpawningScores());
                        continuousSpawningScores[vessel.vesselName].vessel = vessel; // Update some values in the scoring structure.
                        continuousSpawningScores[vessel.vesselName].outOfAmmoTime = 0;
                        ++continuousSpawnedVesselCount;
                        continuousSpawnedVesselCount %= spawnSlots.Count;
                        Debug.Log("[BDArmory.VesselSpawner]: Vessel " + vessel.vesselName + " spawned!");
                        BDACompetitionMode.Instance.competitionStatus.Add("Spawned " + vessel.vesselName);
                    }
                    craftToSpawn.Clear(); // Clear the queue since we just spawned all those vessels.
                    if (failedVessels != "")
                    {
                        message = "Some vessels failed to spawn, aborting: " + failedVessels;
                        Debug.Log("[BDArmory.VesselSpawner]: " + message);
                        BDACompetitionMode.Instance.competitionStatus.Add(message);
                        spawnFailureReason = SpawnFailureReason.VesselFailedToSpawn;
                        break;
                    }

                    // Wait for a couple of updates so that the spawned vessels' parts list and reference transform gets updated.
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();

                    // Fix control point orientation by setting the reference transformations to that of the root parts and re-orient the vessels accordingly.
                    foreach (var vessel in vesselsToActivate)
                    {
                        int count = 0;
                        // Sometimes if a vessel camera switch occurs, the craft appears unloaded for a couple of frames. This avoids NREs for control surfaces triggered by the change in reference transform.
                        while (vessel != null && vessel.rootPart != null && (vessel.ReferenceTransform == null || vessel.rootPart.GetReferenceTransform() == null) && ++count < 5) yield return new WaitForFixedUpdate();
                        if (vessel == null || vessel.rootPart == null)
                        {
                            Debug.Log($"[BDArmory.VesselSpawner]: " + (vessel == null ? "Spawned vessel was destroyed before it could be activated!" : $"{vessel.vesselName} has no root part!"));
                            continue; // In case the vessel got destroyed in the mean time.
                        }
                        vessel.SetReferenceTransform(vessel.rootPart);
                        if (BDArmorySettings.SF_GRAVITY)
                        {
                            vessel.SetRotation(Quaternion.FromToRotation(shipFacility == EditorFacility.SPH ? -vessel.ReferenceTransform.forward : vessel.ReferenceTransform.up, -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the gravity direction.
                            vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(shipFacility == EditorFacility.SPH ? vessel.ReferenceTransform.up : -vessel.ReferenceTransform.forward, vessel.transform.position - spawnPoint, -geeDirection), -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the point outwards.
                        }
                        else
                        {
                            vessel.SetRotation(Quaternion.FromToRotation(-vessel.ReferenceTransform.up, -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the local gravity direction.
                            vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(-vessel.ReferenceTransform.forward, vessel.transform.position - spawnPoint, -geeDirection), -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
                            vessel.SetRotation(Quaternion.AngleAxis(-10f, vessel.ReferenceTransform.right) * vessel.transform.rotation); // Tilt 10° outwards.
                        }
                        if (BDArmorySettings.SPACE_HACKS)
                        {
                            var SF = vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>();
                            if (SF == null)
                            {
                                SF = (ModuleSpaceFriction)vessel.rootPart.AddModule("ModuleSpaceFriction");
                            }
                        }
                    }
                }
                // Activate the AI and fire up any new weapon managers that appeared.
                if (vesselsToActivate.Count > 0)
                {
                    // Wait for an update so that the spawned vessels' FindPart... functions have time to have their internal data updated.
                    yield return new WaitForFixedUpdate();

                    var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
                    var vesselsToCheck = vesselsToActivate.ToList(); // Take a copy to avoid modifying the original while iterating over it.
                    foreach (var vessel in vesselsToCheck)
                    {
                        if (!vessel.loaded || vessel.packed) continue;
                        // Make sure the vessel module registry is up to date.
                        VesselModuleRegistry.OnVesselModified(vessel, true);
                        // Check that the vessel is valid.
                        var invalidReason = BDACompetitionMode.Instance.IsValidVessel(vessel);
                        if (invalidReason != BDACompetitionMode.InvalidVesselReason.None)
                        {
                            bool killIt = false;
                            var craftURL = craftURLToVesselName.ToDictionary(i => i.Value, i => i.Key)[vessel.vesselName];
                            if (invalidVesselCount.ContainsKey(craftURL))
                                ++invalidVesselCount[craftURL];
                            else
                                invalidVesselCount.Add(craftURL, 1);
                            if (invalidVesselCount[craftURL] == 3) // After 3 attempts try spawning it again.
                            {
                                message = vessel.vesselName + " is INVALID due to " + invalidReason + ", attempting to respawn it.";
                                craftToSpawn.Enqueue(craftURL); // Re-add the craft to the vessels to spawn.
                                killIt = true;
                            }
                            if (invalidVesselCount[craftURL] > 5) // After 3 more attempts, mark it as defunct.
                            {
                                message = vessel.vesselName + " is STILL INVALID due to " + invalidReason + ", removing it.";
                                spawnConfig.craftFiles.Remove(craftURL);
                                killIt = true;
                            }
                            if (killIt)
                            {
                                BDACompetitionMode.Instance.competitionStatus.Add(message);
                                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                                vesselsToActivate.Remove(vessel);
                                BDACompetitionMode.Instance.Scores.RemovePlayer(vessel.vesselName);
                                RemoveVessel(vessel); // Remove the vessel
                            }
                            continue;
                        }

                        // Check if the weapon manager has been added to the weapon managers list.
                        var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                        if (weaponManager != null && weaponManagers.Contains(weaponManager)) // The weapon manager has been added, let's go!
                        {
                            // Activate the vessel with AG10.
                            vessel.ActionGroups.ToggleGroup(BDACompetitionMode.KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
                            weaponManager.AI.ActivatePilot();
                            weaponManager.AI.CommandTakeOff();
                            if (!BDArmorySettings.NO_ENGINES && CountActiveEngines(vessel) == 0) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
                            {
                                Debug.Log("[BDArmory.VesselSpawner]: " + vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.");
                                ActivateAllEngines(vessel);
                            }
                            else if (BDArmorySettings.NO_ENGINES && CountActiveEngines(vessel) > 0) // Vessel had some active engines. Turn them off if possible.
                            {
                                ActivateAllEngines(vessel, false);
                            }
                            if (BDArmorySettings.TAG_MODE && !string.IsNullOrEmpty(BDACompetitionMode.Instance.Scores.currentlyIT))
                            { weaponManager.SetTeam(BDTeam.Get("NO")); }
                            else
                            {
                                // Assign the vessel to an unassigned team.
                                var currentTeams = weaponManagers.Where(wm => wm != weaponManager).Select(wm => wm.Team).ToHashSet(); // Current teams, excluding us.
                                char team = 'A';
                                while (currentTeams.Contains(BDTeam.Get(team.ToString())))
                                    ++team;
                                weaponManager.SetTeam(BDTeam.Get(team.ToString()));
                            }
                            weaponManager.ForceScan();
                            var craftURL = craftURLToVesselName.ToDictionary(i => i.Value, i => i.Key)[vessel.vesselName];
                            // Enable guard mode if a competition is active.
                            if (BDACompetitionMode.Instance.competitionIsActive)
                                if (!weaponManager.guardMode)
                                    weaponManager.ToggleGuardMode();
                            weaponManager.AI.ReleaseCommand();
                            vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
                            // Adjust BDACompetitionMode's scoring structures.
                            UpdateCompetitionScores(vessel, true);
                            ++continuousSpawningScores[vessel.vesselName].spawnCount;
                            if (invalidVesselCount.ContainsKey(craftURL))// Reset the invalid spawn counter.
                                invalidVesselCount.Remove(craftURL);
                            // Update the ramming information for the new vessel.
                            if (BDACompetitionMode.Instance.rammingInformation != null)
                            { BDACompetitionMode.Instance.AddPlayerToRammingInformation(vessel); }
                            vesselsToActivate.Remove(vessel);
                            RevertSpawnLocationCamera(true); // Undo the camera adjustment and reset the camera distance. This has an internal check so that it only occurs once.
                            if (initialSpawn || FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD)
                            {
                                initialSpawn = false;
                                LoadedVesselSwitcher.Instance.ForceSwitchVessel(vessel); // Update the camera.
                                FlightCamera.fetch.SetDistance(50);
                            }
                        }
                        if (BDArmorySettings.MUTATOR_MODE)
                        {
                            var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
                            if (MM == null)
                            {
                                MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                            }
                            if (BDArmorySettings.MUTATOR_LIST.Count > 0)
                            {
                                if (BDArmorySettings.MUTATOR_APPLY_GLOBAL) //same mutator for all craft
                                {
                                    MM.EnableMutator(BDACompetitionMode.Instance.currentMutator);
                                }
                                if (!BDArmorySettings.MUTATOR_APPLY_GLOBAL && BDArmorySettings.MUTATOR_APPLY_TIMER) //mutator applied on a per-craft basis
                                {
                                    MM.EnableMutator(); //random mutator
                                }
                            }
                        }
                        if (BDArmorySettings.HACK_INTAKES)
                        {
                            HackIntakes(vessel, true);
                        }
                    }
                }

                // Kill off vessels that are out of ammo for too long if we're in continuous spawning mode and a competition is active.
                if (BDACompetitionMode.Instance.competitionIsActive)
                    KillOffOutOfAmmoVessels();

                // Wait for any pending vessel removals.
                while (removeVesselsPending > 0)
                    yield return new WaitForFixedUpdate();

                if (BDACompetitionMode.Instance.competitionIsActive)
                {
                    yield return new WaitUntil(() => Planetarium.GetUniversalTime() > currentUpdateTick); // Wait for the current update tick in BDACompetitionMode so that spawning occurs after checks for dead vessels there.
                    yield return new WaitForFixedUpdate();
                }
                else
                {
                    yield return new WaitForSeconds(1); // 1s between checks. Nothing much happens if nothing needs spawning.
                }
            }
            #endregion
            vesselsSpawningContinuously = false;
            Debug.Log("[BDArmory.VesselSpawner]: Continuous vessel spawning ended.");
        }

        // For tracking scores across multiple spawns.
        public class ContinuousSpawningScores
        {
            public Vessel vessel; // The vessel.
            public int spawnCount = 0; // The number of times a craft has been spawned.
            public double outOfAmmoTime = 0; // The time the vessel ran out of ammo.
            public Dictionary<int, ScoringData> scoreData = new Dictionary<int, ScoringData>();
            public double cumulativeTagTime = 0;
            public int cumulativeHits = 0;
            public int cumulativeDamagedPartsDueToRamming = 0;
            public int cumulativeDamagedPartsDueToRockets = 0;
            public int cumulativeDamagedPartsDueToMissiles = 0;
        };
        public Dictionary<string, ContinuousSpawningScores> continuousSpawningScores;
        public void UpdateCompetitionScores(Vessel vessel, bool newSpawn = false)
        {
            var vesselName = vessel.vesselName;
            if (!continuousSpawningScores.ContainsKey(vesselName)) return;
            var spawnCount = continuousSpawningScores[vesselName].spawnCount - 1;
            if (spawnCount < 0) return; // Initial spawning after scores were reset.
            var scoreData = continuousSpawningScores[vesselName].scoreData;
            if (BDACompetitionMode.Instance.Scores.Players.Contains(vesselName))
            {
                scoreData[spawnCount] = BDACompetitionMode.Instance.Scores.ScoreData[vesselName]; // Save the Score instance for the vessel.
                if (newSpawn)
                {
                    continuousSpawningScores[vesselName].cumulativeTagTime = scoreData.Sum(kvp => kvp.Value.tagTotalTime);
                    continuousSpawningScores[vesselName].cumulativeHits = scoreData.Sum(kvp => kvp.Value.hits);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRamming = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToRamming);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRockets = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToRockets);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToMissiles = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToMissiles);
                    BDACompetitionMode.Instance.Scores.RemovePlayer(vesselName);
                    BDACompetitionMode.Instance.Scores.AddPlayer(vessel);
                    BDACompetitionMode.Instance.Scores.ScoreData[vesselName].lastDamageTime = scoreData[spawnCount].lastDamageTime;
                    BDACompetitionMode.Instance.Scores.ScoreData[vesselName].lastPersonWhoDamagedMe = scoreData[spawnCount].lastPersonWhoDamagedMe;
                }
            }
        }

        public void DumpContinuousSpawningScores(string tag = "")
        {
            var logStrings = new List<string>();

            if (continuousSpawningScores == null || continuousSpawningScores.Count == 0) return;
            foreach (var vesselName in continuousSpawningScores.Keys)
                UpdateCompetitionScores(continuousSpawningScores[vesselName].vessel);
            BDACompetitionMode.Instance.competitionStatus.Add("Dumping scores for competition " + BDACompetitionMode.Instance.CompetitionID.ToString() + (tag != "" ? " " + tag : ""));
            logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]: Dumping Results at " + (int)(Planetarium.GetUniversalTime() - BDACompetitionMode.Instance.competitionStartTime) + "s");
            foreach (var vesselName in continuousSpawningScores.Keys)
            {
                var vesselScore = continuousSpawningScores[vesselName];
                var scoreData = vesselScore.scoreData;
                logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]: Name:" + vesselName);
                logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  DEATHCOUNT:" + scoreData.Values.Where(v => v.deathTime >= 0).Count());
                var deathTimes = string.Join(";", scoreData.Values.Where(v => v.deathTime >= 0).Select(v => v.deathTime.ToString("0.0")));
                if (deathTimes != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  DEATHTIMES:" + deathTimes);
                #region Bullets
                var whoShotMeScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.hitCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.hitCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoShotMeScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSHOTME:" + whoShotMeScores);
                var whoDamagedMeWithBulletsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromGuns.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromGuns.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithBulletsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHBULLETS:" + whoDamagedMeWithBulletsScores);
                #endregion
                #region Rockets
                var whoStruckMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rocketStrikeCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rocketStrikeCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoStruckMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSTRUCKMEWITHROCKETS:" + whoStruckMeWithRocketsScores);
                var whoPartsHitMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rocketPartDamageCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rocketPartDamageCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoPartsHitMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOPARTSHITMEWITHROCKETS:" + whoPartsHitMeWithRocketsScores);
                var whoDamagedMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromRockets.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromRockets.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHROCKETS:" + whoDamagedMeWithRocketsScores);
                #endregion
                #region Missiles
                var whoStruckMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.missileHitCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.missileHitCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoStruckMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSTRUCKMEWITHMISSILES:" + whoStruckMeWithMissilesScores);
                var whoPartsHitMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.missilePartDamageCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.missilePartDamageCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoPartsHitMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOPARTSHITMEWITHMISSILES:" + whoPartsHitMeWithMissilesScores);
                var whoDamagedMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromMissiles.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromMissiles.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHMISSILES:" + whoDamagedMeWithMissilesScores);
                #endregion
                #region Rams
                var whoRammedMeScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rammingPartLossCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rammingPartLossCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoRammedMeScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHORAMMEDME:" + whoRammedMeScores);
                #endregion
                #region Kills
                var GMKills = string.Join(", ", scoreData.Where(kvp => kvp.Value.gmKillReason != GMKillReason.None).Select(kvp => kvp.Key + ":" + kvp.Value.gmKillReason));
                if (GMKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  GMKILL:" + GMKills);
                var specialKills = new HashSet<AliveState> { AliveState.CleanKill, AliveState.HeadShot, AliveState.KillSteal }; // FIXME expand these to the separate special kill types
                var cleanKills = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Guns).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANKILL:" + cleanKills);
                var cleanFrags = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Rockets).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanFrags != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANFRAG:" + cleanFrags);
                var cleanRams = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Ramming).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanRams != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANRAM:" + cleanRams);
                var cleanMissileKills = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Missiles).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanMissileKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANMISSILEKILL:" + cleanMissileKills);
                #endregion
                var accuracy = string.Join(", ", scoreData.Select(kvp => kvp.Key + ":" + kvp.Value.hits + "/" + kvp.Value.shotsFired + ":" + kvp.Value.rocketStrikes + "/" + kvp.Value.rocketsFired));
                if (accuracy != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  ACCURACY:" + accuracy);
                if (BDArmorySettings.TAG_MODE)
                {
                    if (scoreData.Sum(kvp => kvp.Value.tagScore) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TAGSCORE:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagScore > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagScore.ToString("0.0"))));
                    if (scoreData.Sum(kvp => kvp.Value.tagTotalTime) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TIMEIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagTotalTime > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagTotalTime.ToString("0.0"))));
                    if (scoreData.Sum(kvp => kvp.Value.tagKillsWhileIt) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  KILLSWHILEIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagKillsWhileIt > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagKillsWhileIt)));
                    if (scoreData.Sum(kvp => kvp.Value.tagTimesIt) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TIMESIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagTimesIt > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagTimesIt)));
                }
            }

            // Dump the log results to a file.
            if (BDACompetitionMode.Instance.CompetitionID > 0)
            {
                var folder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "BDArmory", "Logs");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, BDACompetitionMode.Instance.CompetitionID.ToString() + (tag != "" ? "-" + tag : "") + ".log"), logStrings);
            }
        }
        #endregion

        [Serializable]
        public class SpawnConfig
        {
            public SpawnConfig(int worldIndex, double latitude, double longitude, double altitude, float distance, bool absDistanceOrFactor, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string folder = "", List<string> craftFiles = null)
            {
                this.worldIndex = worldIndex;
                this.latitude = latitude;
                this.longitude = longitude;
                this.altitude = altitude;
                this.distance = distance;
                this.absDistanceOrFactor = absDistanceOrFactor;
                this.easeInSpeed = easeInSpeed;
                this.killEverythingFirst = killEverythingFirst;
                this.assignTeams = assignTeams;
                this.numberOfTeams = numberOfTeams;
                this.teamCounts = teamCounts; if (teamCounts != null) this.numberOfTeams = this.teamCounts.Count;
                this.teamsSpecific = teamsSpecific;
                this.folder = folder ?? "";
                this.craftFiles = craftFiles;
            }
            public SpawnConfig(SpawnConfig other)
            {
                this.worldIndex = other.worldIndex;
                this.latitude = other.latitude;
                this.longitude = other.longitude;
                this.altitude = other.altitude;
                this.distance = other.distance;
                this.absDistanceOrFactor = other.absDistanceOrFactor;
                this.easeInSpeed = other.easeInSpeed;
                this.killEverythingFirst = other.killEverythingFirst;
                this.assignTeams = other.assignTeams;
                this.numberOfTeams = other.numberOfTeams;
                this.teamCounts = other.teamCounts;
                this.teamsSpecific = other.teamsSpecific;
                this.folder = other.folder;
                this.craftFiles = other.craftFiles?.ToList();
            }
            public int worldIndex;
            public double latitude;
            public double longitude;
            public double altitude;
            public float distance;
            public bool absDistanceOrFactor; // If true, the distance value is used as-is, otherwise it is used as a factor giving the actual distance: (N+1)*distance, where N is the number of vessels.
            public float easeInSpeed;
            public bool killEverythingFirst = true;
            public bool assignTeams = true;
            public int numberOfTeams = 0; // Number of teams (or FFA, Folders or Inf). For evenly (as possible) splitting vessels into teams.
            public List<int> teamCounts; // List of team numbers. For unevenly splitting vessels into teams based on their order in the tournament state file for the round. E.g., when spawning from folders.
            public List<List<string>> teamsSpecific; // Dictionary of vessels and teams. For splitting specific vessels into specific teams.
            public string folder = "";
            public List<string> craftFiles = null;
        }

        #region Team Spawning
        public void TeamSpawn(List<SpawnConfig> spawnConfigs, bool startCompetition = false, double competitionStartDelay = 0d, bool startCompetitionNow = false)
        {
            vesselsSpawning = true; // Indicate that vessels are spawning here to avoid timing issues with Update in other modules.
            RevertSpawnLocationCamera(true);
            if (teamSpawnCoroutine != null)
                StopCoroutine(teamSpawnCoroutine);
            teamSpawnCoroutine = StartCoroutine(TeamsSpawnCoroutine(spawnConfigs, startCompetition, competitionStartDelay, startCompetitionNow));
        }
        private Coroutine teamSpawnCoroutine;
        public IEnumerator TeamsSpawnCoroutine(List<SpawnConfig> spawnConfigs, bool startCompetition = false, double competitionStartDelay = 0d, bool startCompetitionNow = false)
        {
            bool killAllFirst = true;
            List<int> spawnCounts = new List<int>();
            spawnFailureReason = SpawnFailureReason.None;
            // Spawn each team.
            foreach (var spawnConfig in spawnConfigs)
            {
                vesselsSpawning = true; // Gets set to false each time spawning is finished, so we need to re-enable it again.
                vesselSpawnSuccess = false;
                yield return SpawnAllVesselsOnceCoroutine(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, spawnConfig.distance, spawnConfig.absDistanceOrFactor, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, killAllFirst, false, 0, null, null, spawnConfig.folder, spawnConfig.craftFiles);
                if (!vesselSpawnSuccess)
                {
                    message = "Vessel spawning failed, aborting.";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    yield break;
                }
                spawnCounts.Add(spawnedVesselCount);
                // LoadedVesselSwitcher.Instance.MassTeamSwitch(false); // Reset everyone to team 'A' so that the order doesn't get messed up.
                killAllFirst = false;
            }
            yield return new WaitForFixedUpdate();
            SaveTeams(); // Save the teams in case they've been pre-configured.
            LoadedVesselSwitcher.Instance.MassTeamSwitch(false, false, spawnCounts); // Assign teams based on the groups of spawns. Right click the 'T' to revert to the original team names if they were defined.
            if (startCompetition) // Start the competition.
            {
                var competitionStartDelayStart = Planetarium.GetUniversalTime();
                while (Planetarium.GetUniversalTime() - competitionStartDelayStart < competitionStartDelay - Time.fixedDeltaTime)
                {
                    var timeLeft = competitionStartDelay - (Planetarium.GetUniversalTime() - competitionStartDelayStart);
                    if ((int)(timeLeft - Time.fixedDeltaTime) < (int)timeLeft)
                        BDACompetitionMode.Instance.competitionStatus.Add("Competition starting in T-" + timeLeft.ToString("0") + "s");
                    yield return new WaitForFixedUpdate();
                }
                BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
                if (startCompetitionNow)
                {
                    yield return new WaitForFixedUpdate();
                    BDACompetitionMode.Instance.StartCompetitionNow();
                }
            }
        }
        #endregion

        // Stagger the spawn slots to avoid consecutive craft being launched too close together.
        private static List<int> OptimiseSpawnSlots(int slotCount)
        {
            var availableSlots = Enumerable.Range(0, slotCount).ToList();
            if (slotCount < 4) return availableSlots; // Can't do anything about it for < 4 craft.
            var separation = Mathf.CeilToInt(slotCount / 3f); // Start with approximately 120° separation.
            var pos = 0;
            var optimisedSlots = new List<int>();
            while (optimisedSlots.Count < slotCount)
            {
                while (optimisedSlots.Contains(pos)) { ++pos; pos %= slotCount; }
                optimisedSlots.Add(pos);
                pos += separation;
                pos %= slotCount;
            }
            return optimisedSlots;
        }

        public Vessel SpawnSpawnProbe()
        {
            // Spawn in the SpawnProbe at the camera position and switch to it so that we can clean up the other vessels properly.
            var dummyVar = EditorFacility.None;
            Vector3d dummySpawnCoords;
            FlightGlobals.currentMainBody.GetLatLonAlt(FlightCamera.fetch.transform.position, out dummySpawnCoords.x, out dummySpawnCoords.y, out dummySpawnCoords.z);
            if (spawnProbeLocation == null) return null;
            Vessel spawnProbe = SpawnVesselFromCraftFile(spawnProbeLocation, dummySpawnCoords, 0, 0f, out dummyVar);
            return spawnProbe;
        }

        public int removeVesselsPending = 0;
        // Remove a vessel and clean up any remaining parts. This fixes the case where the currently focussed vessel refuses to die properly.
        public void RemoveVessel(Vessel vessel)
        {
            if (vessel == null) return;
            if (BDArmorySettings.ASTEROID_RAIN && vessel.vesselType == VesselType.SpaceObject) return; // Don't remove asteroids we're using.
            if (BDArmorySettings.ASTEROID_FIELD && vessel.vesselType == VesselType.SpaceObject) return; // Don't remove asteroids we're using.
            StartCoroutine(RemoveVesselCoroutine(vessel));
        }
        public IEnumerator RemoveVesselCoroutine(Vessel vessel)
        {
            if (vessel == null) yield break;
            ++removeVesselsPending;
            if (vessel != FlightGlobals.ActiveVessel && vessel.vesselType != VesselType.SpaceObject)
            {
                if (KerbalSafetyManager.Instance.safetyLevel != KerbalSafetyLevel.Off)
                    KerbalSafetyManager.Instance.RecoverVesselNow(vessel);
                else
                    ShipConstruction.RecoverVesselFromFlight(vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
            }
            else
            {
                if (vessel.vesselType == VesselType.SpaceObject)
                {
                    if (BDArmorySettings.ASTEROID_RAIN && AsteroidRain.IsManagedAsteroid(vessel)) yield break; // Don't remove asteroids when we're using them.
                    if (BDArmorySettings.ASTEROID_FIELD && AsteroidField.IsManagedAsteroid(vessel)) yield break; // Don't remove asteroids when we're using them.
                    var cometVessel = vessel.FindVesselModuleImplementing<CometVessel>();
                    if (cometVessel) { Destroy(cometVessel); }
                }
                vessel.Die(); // Kill the vessel
                yield return new WaitForFixedUpdate();
                if (vessel != null)
                {
                    var partsToKill = vessel.parts.ToList(); // If it left any parts, kill them. (This occurs when the currently focussed vessel gets killed.)
                    foreach (var part in partsToKill)
                        part.Die();
                }
                yield return new WaitForFixedUpdate();
            }
            --removeVesselsPending;
        }

        public void KillOffOutOfAmmoVessels()
        {
            if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 0) return; // Never
            var now = Planetarium.GetUniversalTime();
            Vessel vessel;
            MissileFire weaponManager;
            ContinuousSpawningScores score;
            foreach (var vesselName in continuousSpawningScores.Keys)
            {
                score = continuousSpawningScores[vesselName];
                vessel = score.vessel;
                if (vessel == null) continue; // Vessel hasn't been respawned yet.
                weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                if (weaponManager == null) continue; // Weapon manager hasn't registered yet.
                if (score.outOfAmmoTime == 0 && !weaponManager.HasWeaponsAndAmmo())
                    score.outOfAmmoTime = Planetarium.GetUniversalTime();
                if (score.outOfAmmoTime > 0 && now - score.outOfAmmoTime > BDArmorySettings.OUT_OF_AMMO_KILL_TIME)
                {
                    var m = "Killing off " + vesselName + " as they exceeded the out-of-ammo kill time.";
                    BDACompetitionMode.Instance.competitionStatus.Add(m);
                    Debug.Log("[BDArmory.VesselSpawner]: " + m);
                    BDACompetitionMode.Instance.Scores.RegisterDeath(vesselName, GMKillReason.OutOfAmmo); // Indicate that it was us who killed it and remove any "clean" kills.
                    RemoveVessel(vessel);
                }
            }
        }

        public bool CheckAIWMPlacement(Vessel vessel)
        {
            var message = "";
            List<string> failureStrings = new List<string>();
            var AI = VesselModuleRegistry.GetBDModulePilotAI(vessel, true);
            var WM = VesselModuleRegistry.GetMissileFire(vessel, true);
            if (AI == null) message = " has no AI";
            if (WM == null) message += (AI == null ? " or WM" : " has no WM");
            if (AI != null || WM != null)
            {
                int count = 0;
                if (AI != null && (AI.part.parent == null || AI.part.vessel.rootPart != AI.part.parent))
                {
                    message += (WM == null ? " and its AI" : "'s AI");
                    ++count;
                }
                if (WM != null && (WM.part.parent == null || WM.part.vessel.rootPart != WM.part.parent))
                {
                    message += (AI == null ? " and its WM" : (count > 0 ? " and WM" : "'s WM"));
                    ++count;
                };
                if (count > 0) message += (count > 1 ? " are" : " is") + " not attached to its root part";
            }

            if (!string.IsNullOrEmpty(message))
            {
                message = $"{vessel.vesselName}" + message + ".";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.LogWarning("[BDArmory.VesselSpawner]: " + message);
                return false;
            }
            return true;
        }

        public void CheckAIWMCounts(Vessel vessel)
        {
            var numberOfAIs = VesselModuleRegistry.GetModuleCount<BDModulePilotAI>(vessel);
            var numberOfWMs = VesselModuleRegistry.GetModuleCount<MissileFire>(vessel);
            string message = null;
            if (numberOfAIs != 1 && numberOfWMs != 1) message = $"{vessel.vesselName} has {numberOfAIs} AIs and {numberOfWMs} WMs";
            else if (numberOfAIs != 1) message = $"{vessel.vesselName} has {numberOfAIs} AIs";
            else if (numberOfWMs != 1) message = $"{vessel.vesselName} has {numberOfWMs} WMs";
            if (message != null)
            {
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.LogWarning("[BDArmory.VesselSpawner]: " + message);
            }
        }

        #region Actual spawning of individual craft
        // THE FOLLOWING STOLEN FROM VESSEL MOVER via BenBenWilde's autospawn (and tweaked slightly)
        private Vessel SpawnVesselFromCraftFile(string craftURL, Vector3d gpsCoords, float heading, float pitch, out EditorFacility shipFacility, List<ProtoCrewMember> crewData = null)
        {
            VesselData newData = new VesselData();

            newData.craftURL = craftURL;
            newData.latitude = gpsCoords.x;
            newData.longitude = gpsCoords.y;
            newData.altitude = gpsCoords.z;

            newData.body = FlightGlobals.currentMainBody;
            newData.heading = heading;
            newData.pitch = pitch;
            newData.orbiting = false;
            newData.flagURL = HighLogic.CurrentGame.flagURL;
            newData.owned = true;
            newData.vesselType = VesselType.Ship;

            newData.crew = new List<CrewData>();

            return SpawnVessel(newData, out shipFacility, crewData);
        }

        private Vessel SpawnVessel(VesselData vesselData, out EditorFacility shipFacility, List<ProtoCrewMember> crewData = null)
        {
            shipFacility = EditorFacility.None;
            //Set additional info for landed vessels
            bool landed = false;
            if (!vesselData.orbiting)
            {
                landed = true;
                if (vesselData.altitude == null || vesselData.altitude < 0)
                {
                    vesselData.altitude = 35;
                }

                Vector3d pos = vesselData.body.GetRelSurfacePosition(vesselData.latitude, vesselData.longitude, vesselData.altitude.Value);

                vesselData.orbit = new Orbit(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, vesselData.body);
                vesselData.orbit.UpdateFromStateVectors(pos, vesselData.body.getRFrmVel(pos), vesselData.body, Planetarium.GetUniversalTime());
            }
            else
            {
                vesselData.orbit.referenceBody = vesselData.body;
            }

            ConfigNode[] partNodes;
            ShipConstruct shipConstruct = null;
            if (!string.IsNullOrEmpty(vesselData.craftURL))
            {
                var craftNode = ConfigNode.Load(vesselData.craftURL);
                shipConstruct = new ShipConstruct();
                if (!shipConstruct.LoadShip(craftNode))
                {
                    Debug.LogError("[BDArmory.VesselSpawner]: Ship file error!");
                    return null;
                }

                // Set the name
                if (string.IsNullOrEmpty(vesselData.name))
                {
                    vesselData.name = shipConstruct.shipName;
                }

                // Sort the parts into top-down tree order.
                shipConstruct.parts = SortPartTree(shipConstruct.parts);

                // Set some parameters that need to be at the part level
                uint missionID = (uint)Guid.NewGuid().GetHashCode();
                uint launchID = HighLogic.CurrentGame.launchID++;
                foreach (Part p in shipConstruct.parts)
                {
                    p.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                    p.missionID = missionID;
                    p.launchID = launchID;
                    p.flagURL = vesselData.flagURL ?? HighLogic.CurrentGame.flagURL;

                    // Had some issues with this being set to -1 for some ships - can't figure out
                    // why.  End result is the vessel exploding, so let's just set it to a positive
                    // value.
                    p.temperature = 1.0;
                }

                // Add crew
                List<Part> crewParts;
                ModuleWeapon crewedWeapon;
                switch (BDArmorySettings.VESSEL_SPAWN_FILL_SEATS)
                {
                    case 0: // Minimal plus crewable weapons.
                        {
                            crewParts = shipConstruct.parts.FindAll(p => p.protoModuleCrew.Count < p.CrewCapacity && (crewedWeapon = p.FindModuleImplementing<ModuleWeapon>()) && crewedWeapon.crewserved).ToList(); // Crewed weapons.
                            var part = shipConstruct.parts.Find(p => p.protoModuleCrew.Count < p.CrewCapacity && !p.FindModuleImplementing<ModuleWeapon>() && (p.FindModuleImplementing<ModuleCommand>() || p.FindModuleImplementing<KerbalSeat>())); // A non-weapon crewed command part.
                            if (part) crewParts.Add(part);
                            break;
                        }
                    case 1: // All cockpits or the first combat seat if no cockpits are found, plus crewable weapons.
                        {
                            crewParts = shipConstruct.parts.FindAll(p => p.protoModuleCrew.Count < p.CrewCapacity && p.FindModuleImplementing<ModuleCommand>()).ToList(); // Crewable cockpits.
                            if (crewParts.Count() == 0)
                            {
                                var part = shipConstruct.parts.Find(p => p.protoModuleCrew.Count < p.CrewCapacity && p.FindModuleImplementing<KerbalSeat>() && p.FindModuleImplementing<MissileFire>()); // The first combat seat if no cockpits were found.
                                if (part) crewParts.Add(part);
                            }
                            crewParts.AddRange(shipConstruct.parts.FindAll(p => p.protoModuleCrew.Count < p.CrewCapacity && ((crewedWeapon = p.FindModuleImplementing<ModuleWeapon>()) && crewedWeapon.crewserved))); // Crewable weapons.
                            break;
                        }
                    case 2: // All crewable control points plus crewable weapons.
                        {
                            crewParts = shipConstruct.parts.FindAll(p => p.protoModuleCrew.Count < p.CrewCapacity && (p.FindModuleImplementing<ModuleCommand>() || p.FindModuleImplementing<KerbalSeat>() || ((crewedWeapon = p.FindModuleImplementing<ModuleWeapon>()) && crewedWeapon.crewserved))).ToList();
                            break;
                        }
                    case 3: // All crewable parts.
                        {
                            crewParts = shipConstruct.parts.FindAll(p => p.protoModuleCrew.Count < p.CrewCapacity).ToList();
                            break;
                        }
                    default:
                        throw new IndexOutOfRangeException("Invalid Fill Seats value");
                }
                if (BDArmorySettings.RUNWAY_PROJECT && BDArmorySettings.RUNWAY_PROJECT_ROUND == 42) // Fly the Unfriendly Skies
                { crewParts = shipConstruct.parts.FindAll(p => p.protoModuleCrew.Count < p.CrewCapacity).ToList(); }
                foreach (var part in crewParts)
                {
                    int crewToAdd = (BDArmorySettings.VESSEL_SPAWN_FILL_SEATS > 0 || (BDArmorySettings.RUNWAY_PROJECT && BDArmorySettings.RUNWAY_PROJECT_ROUND == 42)) ? part.CrewCapacity - part.protoModuleCrew.Count : 1;
                    for (int crewCount = 0; crewCount < crewToAdd; ++crewCount)
                    {
                        // Create the ProtoCrewMember
                        ProtoCrewMember crewMember = HighLogic.CurrentGame.CrewRoster.GetNextOrNewKerbal(ProtoCrewMember.KerbalType.Crew);
                        KerbalRoster.SetExperienceTrait(crewMember, KerbalRoster.pilotTrait); // Make the kerbal a pilot (so they can use SAS properly).
                        KerbalRoster.SetExperienceLevel(crewMember, KerbalRoster.GetExperienceMaxLevel()); // Make them experienced.
                        crewMember.isBadass = true; // Make them bad-ass (likes nearby explosions).

                        // Add them to the part
                        part.AddCrewmemberAt(crewMember, part.protoModuleCrew.Count);
                    }
                }

                // Create a dummy ProtoVessel, we will use this to dump the parts to a config node.
                // We can't use the config nodes from the .craft file, because they are in a
                // slightly different format than those required for a ProtoVessel (seriously
                // Squad?!?).
                ConfigNode empty = new ConfigNode();
                ProtoVessel dummyProto = new ProtoVessel(empty, null);
                Vessel dummyVessel = new Vessel();
                dummyVessel.parts = shipConstruct.Parts;
                dummyProto.vesselRef = dummyVessel;

                // Create the ProtoPartSnapshot objects and then initialize them
                foreach (Part p in shipConstruct.parts)
                {
                    dummyVessel.loaded = false;
                    p.vessel = dummyVessel;

                    dummyProto.protoPartSnapshots.Add(new ProtoPartSnapshot(p, dummyProto, true));
                    Destroy(p.gameObject); // Destroy the prefab.
                }
                foreach (ProtoPartSnapshot p in dummyProto.protoPartSnapshots)
                {
                    p.storePartRefs();
                }

                // Create the ship's parts
                List<ConfigNode> partNodesL = new List<ConfigNode>();
                foreach (ProtoPartSnapshot snapShot in dummyProto.protoPartSnapshots)
                {
                    ConfigNode node = new ConfigNode("PART");
                    snapShot.Save(node);
                    partNodesL.Add(node);
                }
                partNodes = partNodesL.ToArray();
            }
            else
            {
                // Create crew member array
                ProtoCrewMember[] crewArray = new ProtoCrewMember[vesselData.crew.Count];
                int i = 0;
                foreach (CrewData cd in vesselData.crew)
                {
                    // Create the ProtoCrewMember
                    ProtoCrewMember crewMember = HighLogic.CurrentGame.CrewRoster.GetNextOrNewKerbal(ProtoCrewMember.KerbalType.Crew);
                    if (cd.name != null)
                    {
                        crewMember.KerbalRef.name = cd.name;
                    }
                    KerbalRoster.SetExperienceTrait(crewMember, KerbalRoster.pilotTrait); // Make the kerbal a pilot (so they can use SAS properly).
                    KerbalRoster.SetExperienceLevel(crewMember, KerbalRoster.GetExperienceMaxLevel()); // Make them experienced.
                    crewMember.isBadass = true; // Make them bad-ass (likes nearby explosions).

                    crewArray[i++] = crewMember;
                }

                // Create part nodes
                uint flightId = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                partNodes = new ConfigNode[1];
                partNodes[0] = ProtoVessel.CreatePartNode(vesselData.craftPart.name, flightId, crewArray);

                // Default the size class
                //sizeClass = UntrackedObjectClass.A;

                // Set the name
                if (string.IsNullOrEmpty(vesselData.name))
                {
                    vesselData.name = vesselData.craftPart.name;
                }
            }

            // Create additional nodes
            ConfigNode[] additionalNodes = new ConfigNode[0];

            // Create the config node representation of the ProtoVessel
            int rootPartIndex = 0;
            ConfigNode protoVesselNode = ProtoVessel.CreateVesselNode(vesselData.name, vesselData.vesselType, vesselData.orbit, rootPartIndex, partNodes, additionalNodes);


            // Additional settings for a landed vessel
            if (!vesselData.orbiting)
            {
                Vector3d norm = vesselData.body.GetRelSurfaceNVector(vesselData.latitude, vesselData.longitude);

                bool splashed = false;// = landed && terrainHeight < 0.001;

                // Create the config node representation of the ProtoVessel
                // Note - flying is experimental, and so far doesn't work
                protoVesselNode.SetValue("sit", (splashed ? Vessel.Situations.SPLASHED : landed ?
                    Vessel.Situations.LANDED : Vessel.Situations.FLYING).ToString());
                protoVesselNode.SetValue("landed", (landed && !splashed).ToString());
                protoVesselNode.SetValue("splashed", splashed.ToString());
                protoVesselNode.SetValue("lat", vesselData.latitude.ToString());
                protoVesselNode.SetValue("lon", vesselData.longitude.ToString());
                protoVesselNode.SetValue("alt", vesselData.altitude.ToString());
                protoVesselNode.SetValue("landedAt", vesselData.body.name);

                // Figure out the additional height to subtract
                float lowest = float.MaxValue;
                if (shipConstruct != null)
                {
                    foreach (Part p in shipConstruct.parts)
                    {
                        foreach (Collider collider in p.GetComponentsInChildren<Collider>())
                        {
                            if (collider.gameObject.layer != 21 && collider.enabled)
                            {
                                lowest = Mathf.Min(lowest, collider.bounds.min.y);
                            }
                        }
                    }
                }
                else
                {
                    foreach (Collider collider in vesselData.craftPart.partPrefab.GetComponentsInChildren<Collider>())
                    {
                        if (collider.gameObject.layer != 21 && collider.enabled)
                        {
                            lowest = Mathf.Min(lowest, collider.bounds.min.y);
                        }
                    }
                }

                if (lowest == float.MaxValue)
                {
                    lowest = 0;
                }

                // Figure out the surface height and rotation
                Quaternion normal = Quaternion.LookRotation((Vector3)norm);// new Vector3((float)norm.x, (float)norm.y, (float)norm.z));
                Quaternion rotation = Quaternion.identity;
                float heading = vesselData.heading;
                if (shipConstruct == null)
                {
                    rotation = rotation * Quaternion.FromToRotation(Vector3.up, Vector3.back); //FIXME add a check if spawning in null-atmo to have craft spawn horizontal, not nose-down
                }
                else if (shipConstruct.shipFacility == EditorFacility.SPH)
                {
                    rotation = rotation * Quaternion.FromToRotation(Vector3.forward, -Vector3.forward);
                    heading += 180.0f;
                }
                else
                {
                    rotation = rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                    rotation = Quaternion.FromToRotation(Vector3.up, -Vector3.up) * rotation;

                    vesselData.heading = 0;
                    vesselData.pitch = 0;
                }

                rotation = rotation * Quaternion.AngleAxis(heading, Vector3.back);
                rotation = rotation * Quaternion.AngleAxis(vesselData.roll, Vector3.down);
                rotation = rotation * Quaternion.AngleAxis(vesselData.pitch, Vector3.left);

                // Set the height and rotation
                if (landed || splashed)
                {
                    float hgt = (shipConstruct != null ? shipConstruct.parts[0] : vesselData.craftPart.partPrefab).localRoot.attPos0.y - lowest;
                    hgt += vesselData.height + 35;
                    protoVesselNode.SetValue("hgt", hgt.ToString(), true);
                }
                protoVesselNode.SetValue("rot", KSPUtil.WriteQuaternion(normal * rotation), true);

                // Set the normal vector relative to the surface
                Vector3 nrm = (rotation * Vector3.forward);
                protoVesselNode.SetValue("nrm", nrm.x + "," + nrm.y + "," + nrm.z, true);
                protoVesselNode.SetValue("prst", false.ToString(), true);
            }

            // Add vessel to the game
            ProtoVessel protoVessel = HighLogic.CurrentGame.AddVessel(protoVesselNode);

            // Set the vessel size (FIXME various other vessel fields appear to not be set, e.g. CoM)
            protoVessel.vesselRef.vesselSize = shipConstruct.shipSize;
            shipFacility = shipConstruct.shipFacility;
            switch (shipFacility)
            {
                case EditorFacility.SPH:
                    protoVessel.vesselRef.vesselType = VesselType.Plane;
                    break;
                case EditorFacility.VAB:
                    protoVessel.vesselRef.vesselType = VesselType.Ship;
                    break;
                default:
                    break;
            }

            // Store the id for later use
            vesselData.id = protoVessel.vesselRef.id;
            // StartCoroutine(PlaceSpawnedVessel(protoVessel.vesselRef));

            return protoVessel.vesselRef;
        }

        List<Part> SortPartTree(List<Part> parts)
        {
            List<Part> Parts = parts.Where(p => p.parent == null).ToList(); // There can be only one.
            while (Parts.Count() < parts.Count())
            {
                var partsToAdd = parts.Where(p => !Parts.Contains(p) && Parts.Contains(p.parent));
                if (partsToAdd.Count() == 0)
                {
                    Debug.Log($"[BDArmory.VesselSpawner]: Part count mismatch when sorting the part-tree: {Parts.Count()} vs {parts.Count()}");
                    break;
                }
                Parts.AddRange(partsToAdd);
            }
            return Parts;
        }

        private IEnumerator PlaceSpawnedVessel(Vessel v)
        {
            v.isPersistent = true;
            v.Landed = false;
            v.situation = Vessel.Situations.FLYING;
            while (v.packed)
            {
                yield return null;
            }
            v.SetWorldVelocity(Vector3d.zero);

            // yield return null;
            // FlightGlobals.ForceSetActiveVessel(v);
            yield return null;
            v.Landed = true;
            v.situation = Vessel.Situations.PRELAUNCH;
            v.GoOffRails();
            // v.IgnoreGForces(240);

            StageManager.BeginFlight();
        }

        internal class CrewData
        {
            public string name = null;
            public ProtoCrewMember.Gender? gender = null;
            public bool addToRoster = true;

            public CrewData() { }
            public CrewData(CrewData cd)
            {
                name = cd.name;
                gender = cd.gender;
                addToRoster = cd.addToRoster;
            }
        }
        private class VesselData
        {
            public string name = null;
            public Guid? id = null;
            public string craftURL = null;
            public AvailablePart craftPart = null;
            public string flagURL = null;
            public VesselType vesselType = VesselType.Ship;
            public CelestialBody body = null;
            public Orbit orbit = null;
            public double latitude = 0.0;
            public double longitude = 0.0;
            public double? altitude = null;
            public float height = 0.0f;
            public bool orbiting = false;
            public bool owned = false;
            public List<CrewData> crew = new List<CrewData>();
            public PQSCity pqsCity = null;
            public Vector3d pqsOffset = Vector3d.zero;
            public float heading = 0f;
            public float pitch = 0f;
            public float roll = 0f;
        }
        #endregion
    }
}