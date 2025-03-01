﻿using KSP.Localization;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System;
using UnityEngine;

using BDArmory.Control;
using BDArmory.Core.Extension;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.Modules;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LoadedVesselSwitcher : MonoBehaviour
    {
        private readonly float _buttonGap = 1;
        private readonly float _buttonHeight = 20;

        private int _guiCheckIndex;
        public static LoadedVesselSwitcher Instance;
        private readonly float _margin = 5;

        private bool _ready;
        private bool _showGui;
        private bool _autoCameraSwitch = false;

        private readonly float _titleHeight = 30;
        private double lastCameraSwitch = 0;
        private double lastCameraCheck = 0;
        private Vessel lastActiveVessel = null;
        private bool currentVesselDied = false;
        private double currentVesselDiedAt = 0;
        private float updateTimer = 0;

        //gui params
        private float _windowHeight; //auto adjusting

        private SortedList<string, List<MissileFire>> weaponManagers = new SortedList<string, List<MissileFire>>();
        private Dictionary<string, float> cameraScores = new Dictionary<string, float>();

        private bool upToDateWMs = false;
        public SortedList<string, List<MissileFire>> WeaponManagers
        {
            get
            {
                if (!upToDateWMs)
                    UpdateList();
                return weaponManagers;
            }
        }

        // booleans to track state of buttons affecting everyone
        private bool _teamsAssigned = false;
        private bool _autoPilotEnabled = false;
        private bool _guardModeEnabled = false;
        public bool vesselTraceEnabled = false;

        // Vessel spawning
        // private bool _vesselsSpawned = false;
        // private bool _continuousVesselSpawning = false;

        // button styles for info buttons
        private static GUIStyle redLight = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
        private static GUIStyle yellowLight = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
        private static GUIStyle greenLight = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
        private static GUIStyle blueLight = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
        private static GUIStyle ItVessel = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
        private static GUIStyle ItVesselSelected = new GUIStyle(BDArmorySetup.BDGuiSkin.box);

        public static GUISkin VSPUISkin = HighLogic.Skin;

        private static System.Random rng;

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;

            redLight.normal.textColor = Color.red;
            yellowLight.normal.textColor = Color.yellow;
            greenLight.normal.textColor = Color.green;
            blueLight.normal.textColor = Color.blue;
            ItVessel.normal.textColor = Color.cyan;
            ItVesselSelected.normal.textColor = Color.cyan;
            redLight.fontStyle = FontStyle.Bold;
            yellowLight.fontStyle = FontStyle.Bold;
            greenLight.fontStyle = FontStyle.Bold;
            blueLight.fontStyle = FontStyle.Bold;
            ItVessel.fontStyle = FontStyle.Bold;
            ItVesselSelected.fontStyle = FontStyle.Bold;
            rng = new System.Random();
        }

        private void Start()
        {
            UpdateList();
            GameEvents.onVesselCreate.Add(VesselEventUpdate);
            GameEvents.onVesselDestroy.Add(VesselEventUpdate);
            GameEvents.onVesselGoOffRails.Add(VesselEventUpdate);
            GameEvents.onVesselGoOnRails.Add(VesselEventUpdate);
            GameEvents.onVesselWillDestroy.Add(CurrentVesselWillDestroy);
            MissileFire.OnChangeTeam += MissileFireOnToggleTeam;

            _ready = false;

            StartCoroutine(WaitForBdaSettings());

            // TEST
            FloatingOrigin.fetch.threshold = 20000; //20km
            FloatingOrigin.fetch.thresholdSqr = 20000 * 20000; //20km
            // Debug.Log($"[BDArmory.LoadedVesselSwitcher]: FLOATINGORIGIN: threshold is {FloatingOrigin.fetch.threshold}");

            //BDArmorySetup.WindowRectVesselSwitcher = new Rect(10, Screen.height / 6f, BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH, 10);
        }

        private void OnDestroy()
        {
            GameEvents.onVesselCreate.Remove(VesselEventUpdate);
            GameEvents.onVesselDestroy.Remove(VesselEventUpdate);
            GameEvents.onVesselGoOffRails.Remove(VesselEventUpdate);
            GameEvents.onVesselGoOnRails.Remove(VesselEventUpdate);
            GameEvents.onVesselWillDestroy.Remove(CurrentVesselWillDestroy);
            MissileFire.OnChangeTeam -= MissileFireOnToggleTeam;

            _ready = false;

            // TEST
            // Debug.Log($"[BDArmory.LoadedVesselSwitcher]: FLOATINGORIGIN: threshold is {FloatingOrigin.fetch.threshold}");
        }

        private IEnumerator WaitForBdaSettings()
        {
            while (BDArmorySetup.Instance == null)
                yield return null;

            _ready = true;
            BDArmorySetup.Instance.hasVesselSwitcher = true;
            _guiCheckIndex = Utils.RegisterGUIRect(new Rect());
        }

        private void MissileFireOnToggleTeam(MissileFire wm, BDTeam team)
        {
            if (_showGui)
                UpdateList();
        }

        private void VesselEventUpdate(Vessel v)
        {
            if (_showGui)
                UpdateList();
        }

        private void Update()
        {
            if (_ready)
            {
                upToDateWMs = false;
                if (BDArmorySetup.Instance.showVesselSwitcherGUI != _showGui)
                {
                    updateTimer -= Time.fixedDeltaTime;
                    _showGui = BDArmorySetup.Instance.showVesselSwitcherGUI;
                    if (_showGui && updateTimer < 0)
                    {
                        UpdateList();
                        updateTimer = 0.5f;    //next update in half a sec only
                    }
                }

                if (_showGui)
                {
                    Hotkeys();
                }

                // check for camera changes
                if (_autoCameraSwitch)
                {
                    UpdateCamera();
                }
            }
        }

        void FixedUpdate()
        {
            if (vesselTraceEnabled)
            {
                if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
                    floatingOriginCorrection += FloatingOrigin.OffsetNonKrakensbane;
                var survivingVessels = weaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).Select(wm => wm.vessel).ToList();
                foreach (var vessel in survivingVessels)
                {
                    if (vessel == null) continue;
                    if (!vesselTraces.ContainsKey(vessel.vesselName)) vesselTraces[vessel.vesselName] = new List<Tuple<float, Vector3, Quaternion>>();
                    vesselTraces[vessel.vesselName].Add(new Tuple<float, Vector3, Quaternion>(Time.time, referenceRotationCorrection * (vessel.transform.position + floatingOriginCorrection), referenceRotationCorrection * vessel.transform.rotation));
                }
                if (survivingVessels.Count == 0) vesselTraceEnabled = false;
            }
        }

        private void Hotkeys()
        {
            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.VS_SWITCH_NEXT))
                SwitchToNextVessel();
            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.VS_SWITCH_PREV))
                SwitchToPreviousVessel();
        }

        public void UpdateList()
        {
            weaponManagers.Clear();

            if (FlightGlobals.Vessels == null) return;
            using (var v = FlightGlobals.Vessels.GetEnumerator())
                while (v.MoveNext())
                {
                    if (v.Current == null || !v.Current.loaded || v.Current.packed) continue;
                    if (VesselModuleRegistry.ignoredVesselTypes.Contains(v.Current.vesselType)) continue;
                    var wms = VesselModuleRegistry.GetMissileFire(v.Current);
                    if (wms != null)
                    {
                        if (weaponManagers.TryGetValue(wms.Team.Name, out var teamManagers))
                            teamManagers.Add(wms);
                        else
                            weaponManagers.Add(wms.Team.Name, new List<MissileFire> { wms });
                    }
                }
            upToDateWMs = true;
        }

        private void ToggleGuardModes()
        {
            _guardModeEnabled = !_guardModeEnabled;
            using (var teamManagers = weaponManagers.GetEnumerator())
                while (teamManagers.MoveNext())
                    using (var wm = teamManagers.Current.Value.GetEnumerator())
                        while (wm.MoveNext())
                        {
                            if (wm.Current == null) continue;
                            wm.Current.guardMode = _guardModeEnabled;
                        }
        }

        private void ToggleAutopilots()
        {
            // toggle the state
            _autoPilotEnabled = !_autoPilotEnabled;
            var autopilotsToToggle = weaponManagers.SelectMany(tm => tm.Value).ToList(); // Get a copy in case activating stages causes the weaponManager list to change.
            foreach (var weaponManager in autopilotsToToggle)
            {
                if (weaponManager == null) continue;
                if (weaponManager.AI == null) continue;
                if (_autoPilotEnabled)
                {
                    weaponManager.AI.ActivatePilot();
                    Utils.fireNextNonEmptyStage(weaponManager.vessel);
                }
                else
                {
                    weaponManager.AI.DeactivatePilot();
                }
            }
        }

        private void OnGUI()
        {
            if (_ready)
            {
                if (_showGui && BDArmorySetup.GAME_UI_ENABLED || (BDArmorySettings.VESSEL_SWITCHER_PERSIST_UI && !BDArmorySetup.GAME_UI_ENABLED))
                {
                    string windowTitle = Localizer.Format("#LOC_BDArmory_BDAVesselSwitcher_Title");
                    if (BDArmorySettings.GRAVITY_HACKS)
                        windowTitle = windowTitle + " (" + BDACompetitionMode.gravityMultiplier.ToString("0.0") + "G)";

                    SetNewHeight(_windowHeight);
                    // this Rect initialization ensures any save issues with height or width of the window are resolved
                    BDArmorySetup.WindowRectVesselSwitcher = new Rect(BDArmorySetup.WindowRectVesselSwitcher.x, BDArmorySetup.WindowRectVesselSwitcher.y, BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH, _windowHeight);
                    BDArmorySetup.WindowRectVesselSwitcher = GUI.Window(10293444, BDArmorySetup.WindowRectVesselSwitcher, WindowVesselSwitcher, windowTitle, BDArmorySetup.BDGuiSkin.window); //"BDA Vessel Switcher"
                    Utils.UpdateGUIRect(BDArmorySetup.WindowRectVesselSwitcher, _guiCheckIndex);
                }
                else
                {
                    Utils.UpdateGUIRect(new Rect(), _guiCheckIndex);
                }
            }
        }

        private void SetNewHeight(float windowHeight)
        {
            var previousWindowHeight = BDArmorySetup.WindowRectVesselSwitcher.height;
            BDArmorySetup.WindowRectVesselSwitcher.height = windowHeight;
            if (BDArmorySettings.STRICT_WINDOW_BOUNDARIES && windowHeight < previousWindowHeight && Mathf.RoundToInt(BDArmorySetup.WindowRectVesselSwitcher.y + previousWindowHeight) == Screen.height) // Window shrunk while being at edge of screen.
                BDArmorySetup.WindowRectVesselSwitcher.y = Screen.height - BDArmorySetup.WindowRectVesselSwitcher.height;
            BDGUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectVesselSwitcher);
        }

        private void WindowVesselSwitcher(int id)
        {
            int numButtons = 10;
            int numButtonsOnLeft = 5;
            GUI.DragWindow(new Rect(numButtonsOnLeft * _buttonHeight + _margin, 0f, BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - numButtons * _buttonHeight - 3f * _margin, _titleHeight));

            if (GUI.Button(new Rect(0f * _buttonHeight + _margin, 4f, _buttonHeight, _buttonHeight), "><", BDArmorySetup.BDGuiSkin.button)) // Don't get so small that the buttons get hidden.
            {
                BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH -= 50f;
                if (BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 50f < 2f * _margin + numButtons * _buttonHeight)
                    BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH = 2f * _margin + numButtons * _buttonHeight;
                BDArmorySetup.SaveConfig();
            }
            if (GUI.Button(new Rect(1f * _buttonHeight + _margin, 4f, _buttonHeight, _buttonHeight), "<>", BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH += 50f;
                if (BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH > Screen.width) // Don't go off the screen.
                    BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH = Screen.width;
                BDArmorySetup.SaveConfig();
            }
            if (GUI.Button(new Rect(2f * _buttonHeight + _margin, 4f, _buttonHeight, _buttonHeight), "↕", BDArmorySettings.VESSEL_SWITCHER_WINDOW_SORTING ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySettings.VESSEL_SWITCHER_WINDOW_SORTING = !BDArmorySettings.VESSEL_SWITCHER_WINDOW_SORTING;
                BDArmorySetup.SaveConfig();
            }
            if (GUI.Button(new Rect(3f * _buttonHeight + _margin, 4f, _buttonHeight, _buttonHeight), "t", BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE = !BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE;
                BDArmorySetup.SaveConfig();
            }
            if (GUI.Button(new Rect(4f * _buttonHeight + _margin, 4f, _buttonHeight, _buttonHeight), "UI", BDArmorySettings.VESSEL_SWITCHER_PERSIST_UI ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySettings.VESSEL_SWITCHER_PERSIST_UI = !BDArmorySettings.VESSEL_SWITCHER_PERSIST_UI;
                BDArmorySetup.SaveConfig();
            }
            if (GUI.Button(new Rect(BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 6 * _buttonHeight - _margin, 4, _buttonHeight, _buttonHeight), "M", BDACompetitionMode.Instance.killerGMenabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                if (Event.current.button == 1)
                {
                    // start the slowboat killer GM
                    if (BDArmorySettings.RUNWAY_PROJECT)
                        BDACompetitionMode.Instance.killerGMenabled = !BDACompetitionMode.Instance.killerGMenabled;
                }
                else
                {
                    BDACompetitionMode.Instance.LogResults();
                }
            }

            if (GUI.Button(new Rect(BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 5 * _buttonHeight - _margin, 4, _buttonHeight, _buttonHeight), "A", _autoCameraSwitch ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                // set/disable automatic camera switching
                _autoCameraSwitch = !_autoCameraSwitch;
                Debug.Log("[BDArmory.LoadedVesselSwitcher]: Setting AutoCameraSwitch");
            }

            if (GUI.Button(new Rect(BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 4 * _buttonHeight - _margin, 4, _buttonHeight, _buttonHeight), "G", _guardModeEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                // switch everyon onto different teams
                ToggleGuardModes();
            }

            if (GUI.Button(new Rect(BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 3 * _buttonHeight - _margin, 4, _buttonHeight, _buttonHeight), "P", _autoPilotEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                // Toggle autopilots for everyone
                ToggleAutopilots();
            }

            if (GUI.Button(new Rect(BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 2 * _buttonHeight - _margin, 4, _buttonHeight, _buttonHeight), "T", _teamsAssigned ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                if (Event.current.button == 1) // Right click => original teams.
                {
                    _teamsAssigned = true;
                    MassTeamSwitch(false, true);
                }
                else
                {
                    // switch everyone onto different teams
                    _teamsAssigned = !_teamsAssigned;
                    MassTeamSwitch(_teamsAssigned);
                }
            }

            if (GUI.Button(new Rect(BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - _buttonHeight - _margin, 4, _buttonHeight, _buttonHeight), "X", BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySetup.Instance.showVesselSwitcherGUI = false;
                return;
            }

            float height = _titleHeight;
            float vesselButtonWidth = BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 2 * _margin - (!BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE || BDArmorySettings.TAG_MODE ? 6f : 5f) * _buttonHeight;
            float teamMargin = (!BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE && weaponManagers.All(tm => tm.Value.Count() == 1)) ? 0 : _margin;

            // Show all the active vessels
            if (BDArmorySettings.VESSEL_SWITCHER_WINDOW_SORTING)
            {
                if (BDArmorySettings.TAG_MODE)
                { // Sort vessels based on total tag time or tag scores.
                    var orderedWMs = weaponManagers.SelectMany(tm => tm.Value, (tm, weaponManager) => new Tuple<string, MissileFire>(tm.Key, weaponManager)).ToList(); // Use a local copy.
                    if (VesselSpawner.Instance.vesselsSpawningContinuously && orderedWMs.All(mf => mf != null && BDACompetitionMode.Instance.Scores.ScoreData.ContainsKey(mf.Item2.vessel.vesselName) && VesselSpawner.Instance.continuousSpawningScores.ContainsKey(mf.Item2.vessel.vesselName)))
                        orderedWMs.Sort((mf1, mf2) => ((VesselSpawner.Instance.continuousSpawningScores[mf2.Item2.vessel.vesselName].cumulativeTagTime + BDACompetitionMode.Instance.Scores.ScoreData[mf2.Item2.vessel.vesselName].tagTotalTime).CompareTo(VesselSpawner.Instance.continuousSpawningScores[mf1.Item2.vessel.vesselName].cumulativeTagTime + BDACompetitionMode.Instance.Scores.ScoreData[mf1.Item2.vessel.vesselName].tagTotalTime)));
                    else if (orderedWMs.All(mf => mf != null && BDACompetitionMode.Instance.Scores.ScoreData.ContainsKey(mf.Item2.vessel.vesselName)))
                        orderedWMs.Sort((mf1, mf2) => (BDACompetitionMode.Instance.Scores.ScoreData[mf2.Item2.vessel.vesselName].tagScore.CompareTo(BDACompetitionMode.Instance.Scores.ScoreData[mf1.Item2.vessel.vesselName].tagScore)));
                    foreach (var weaponManagerPair in orderedWMs)
                    {
                        if (weaponManagerPair.Item2 == null) continue;
                        try
                        {
                            AddVesselSwitcherWindowEntry(weaponManagerPair.Item2, weaponManagerPair.Item1, height, vesselButtonWidth);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("[BDArmory.LoadedVesselSwitcher]: AddVesselSwitcherWindowEntry threw an exception trying to add " + weaponManagerPair.Item2.vessel.vesselName + " on team " + weaponManagerPair.Item1 + " to the list: " + e.Message);
                        }
                        height += _buttonHeight + _buttonGap;
                    }
                }
                else // Sorting of teams by hit counts.
                {
                    var orderedTeamManagers = weaponManagers.Select(tm => new Tuple<string, List<MissileFire>>(tm.Key, tm.Value)).ToList();
                    if (VesselSpawner.Instance.vesselsSpawningContinuously)
                    {
                        foreach (var teamManager in orderedTeamManagers)
                            teamManager.Item2.Sort((wm1, wm2) => ((VesselSpawner.Instance.continuousSpawningScores.ContainsKey(wm2.vessel.vesselName) ? VesselSpawner.Instance.continuousSpawningScores[wm2.vessel.vesselName].cumulativeHits : 0) + (BDACompetitionMode.Instance.Scores.Players.Contains(wm2.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm2.vessel.vesselName].hits : 0)).CompareTo((VesselSpawner.Instance.continuousSpawningScores.ContainsKey(wm1.vessel.vesselName) ? VesselSpawner.Instance.continuousSpawningScores[wm1.vessel.vesselName].cumulativeHits : 0) + (BDACompetitionMode.Instance.Scores.Players.Contains(wm1.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm1.vessel.vesselName].hits : 0))); // Sort within each team by cumulative hits.
                        orderedTeamManagers.Sort((tm1, tm2) => (tm2.Item2.Sum(wm => (VesselSpawner.Instance.continuousSpawningScores.ContainsKey(wm.vessel.vesselName) ? VesselSpawner.Instance.continuousSpawningScores[wm.vessel.vesselName].cumulativeHits : 0) + (BDACompetitionMode.Instance.Scores.Players.Contains(wm.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm.vessel.GetName()].hits : 0)).CompareTo(tm1.Item2.Sum(wm => (VesselSpawner.Instance.continuousSpawningScores.ContainsKey(wm.vessel.vesselName) ? VesselSpawner.Instance.continuousSpawningScores[wm.vessel.vesselName].cumulativeHits : 0) + (BDACompetitionMode.Instance.Scores.Players.Contains(wm.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm.vessel.GetName()].hits : 0))))); // Sort teams by total cumulative hits.
                    }
                    else
                    {
                        foreach (var teamManager in orderedTeamManagers)
                            teamManager.Item2.Sort((wm1, wm2) => (BDACompetitionMode.Instance.Scores.Players.Contains(wm2.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm2.vessel.vesselName].hits : 0).CompareTo(BDACompetitionMode.Instance.Scores.Players.Contains(wm1.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm1.vessel.vesselName].hits : 0)); // Sort within each team by hits.
                        orderedTeamManagers.Sort((tm1, tm2) => (tm2.Item2.Sum(wm => BDACompetitionMode.Instance.Scores.Players.Contains(wm.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm.vessel.GetName()].hits : 0).CompareTo(tm1.Item2.Sum(wm => BDACompetitionMode.Instance.Scores.Players.Contains(wm.vessel.vesselName) ? BDACompetitionMode.Instance.Scores.ScoreData[wm.vessel.GetName()].hits : 0)))); // Sort teams by total hits.
                    }
                    foreach (var teamManager in orderedTeamManagers)
                    {
                        height += teamMargin;
                        bool teamNameShowing = false;
                        foreach (var weaponManager in teamManager.Item2)
                        {
                            if (weaponManager == null) continue;
                            if (BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE && !teamNameShowing)
                            {
                                if (BDTISetup.Instance.ColorAssignments.ContainsKey(teamManager.Item1))
                                {
                                    BDTISetup.TILabel.normal.textColor = BDTISetup.Instance.ColorAssignments[teamManager.Item1];
                                }                                                                                                                                          
                                GUI.Label(new Rect(_margin, height, BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 2 * _margin, _buttonHeight), $"{teamManager.Item1}:" + (weaponManager.Team.Neutral ? (weaponManager.Team.Name != "Neutral" ? "(Neutral)" : "") : ""), BDTISetup.TILabel);

                                teamNameShowing = true;
                                height += _buttonHeight + _buttonGap;
                            }
                            try
                            {
                                AddVesselSwitcherWindowEntry(weaponManager, teamManager.Item1, height, vesselButtonWidth);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.LoadedVesselSwitcher]: AddVesselSwitcherWindowEntry threw an exception trying to add " + weaponManager.vessel.vesselName + " on team " + teamManager.Item1 + " to the list: " + e.Message);
                            }
                            height += _buttonHeight + _buttonGap;
                        }
                    }
                }
            }
            else // Regular sorting.
                foreach (var teamManagers in weaponManagers.ToList()) // Use a copy as something seems to be modifying the list occassionally.
                {
                    height += teamMargin;
                    bool teamNameShowing = false;
                    foreach (var weaponManager in teamManagers.Value)
                    {
                        if (weaponManager == null) continue;
                        if (BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE && !teamNameShowing)
                        {
                            if (BDTISetup.Instance.ColorAssignments.ContainsKey(teamManagers.Key))
                            {
                                BDTISetup.TILabel.normal.textColor = BDTISetup.Instance.ColorAssignments[teamManagers.Key];
                            }
                            GUI.Label(new Rect(_margin, height, BDArmorySettings.VESSEL_SWITCHER_WINDOW_WIDTH - 2 * _margin, _buttonHeight), $"{teamManagers.Key}:" + (weaponManager.team != "Neutral" ? (weaponManager.Team.Neutral ? "(Neutral)" : "") : ""), BDTISetup.TILabel);
                            teamNameShowing = true;
                            height += _buttonHeight + _buttonGap;
                        }
                        try
                        {
                            AddVesselSwitcherWindowEntry(weaponManager, teamManagers.Key, height, vesselButtonWidth);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("[BDArmory.LoadedVesselSwitcher]: AddVesselSwitcherWindowEntry threw an exception trying to add " + weaponManager.vessel.vesselName + " on team " + teamManagers.Key + " to the list: " + e.Message);
                        }
                        height += _buttonHeight + _buttonGap;
                    }
                }

            height += _margin;
            // add all the lost pilots at the bottom
            if (!VesselSpawner.Instance.vesselsSpawningContinuously) // Don't show the dead vessels when continuously spawning. (Especially as command seats trigger all vessels as showing up as dead.)
            {
                foreach (var player in BDACompetitionMode.Instance.Scores.deathOrder)
                {
                    string statusString = "";
                    // DEAD <death order>:<death time>: vesselName(<Score>[, <MissileScore>][, <RammingScore>])[ KILLED|RAMMED BY <otherVesselName>], where <Score> is the number of hits made  <RammingScore> is the number of parts destroyed.
                    statusString += "DEAD " + BDACompetitionMode.Instance.Scores.ScoreData[player].deathOrder + ":" + BDACompetitionMode.Instance.Scores.ScoreData[player].deathTime.ToString("0.0") + " : " + player + " (" + BDACompetitionMode.Instance.Scores.ScoreData[player].hits.ToString();
                    if (BDACompetitionMode.Instance.Scores.ScoreData[player].totalDamagedPartsDueToRockets > 0)
                        statusString += ", " + BDACompetitionMode.Instance.Scores.ScoreData[player].totalDamagedPartsDueToRockets;
                    if (BDACompetitionMode.Instance.Scores.ScoreData[player].totalDamagedPartsDueToMissiles > 0)
                        statusString += ", " + BDACompetitionMode.Instance.Scores.ScoreData[player].totalDamagedPartsDueToMissiles;
                    if (BDACompetitionMode.Instance.Scores.ScoreData[player].totalDamagedPartsDueToRamming > 0)
                        statusString += ", " + BDACompetitionMode.Instance.Scores.ScoreData[player].totalDamagedPartsDueToRamming;
                    if (VesselSpawner.Instance.vesselsSpawningContinuously && BDACompetitionMode.Instance.Scores.ScoreData[player].tagTotalTime > 0)
                        statusString += ", " + BDACompetitionMode.Instance.Scores.ScoreData[player].tagTotalTime.ToString("0.0");
                    else if (BDACompetitionMode.Instance.Scores.ScoreData[player].tagScore > 0)
                        statusString += ", " + BDACompetitionMode.Instance.Scores.ScoreData[player].tagScore.ToString("0.0");
                    switch (BDACompetitionMode.Instance.Scores.ScoreData[player].lastDamageWasFrom)
                    {
                        case DamageFrom.Guns:
                            statusString += ") KILLED BY " + BDACompetitionMode.Instance.Scores.ScoreData[player].lastPersonWhoDamagedMe;
                            break;
                        case DamageFrom.Rockets:
                            statusString += ") FRAGGED BY " + BDACompetitionMode.Instance.Scores.ScoreData[player].lastPersonWhoDamagedMe;
                            break;
                        case DamageFrom.Missiles:
                            statusString += ") EXPLODED BY " + BDACompetitionMode.Instance.Scores.ScoreData[player].lastPersonWhoDamagedMe;
                            break;
                        case DamageFrom.Ramming:
                            statusString += ") RAMMED BY " + BDACompetitionMode.Instance.Scores.ScoreData[player].lastPersonWhoDamagedMe;
                            break;
                        case DamageFrom.Incompetence:
                            statusString += ") CRASHED AND BURNED!";
                            break;
                        case DamageFrom.None:
                            statusString += ") " + BDACompetitionMode.Instance.Scores.ScoreData[player].gmKillReason;
                            break;
                        default: // Note: All the cases ought to be covered above.
                            statusString += ")";
                            break;
                    }
                    switch (BDACompetitionMode.Instance.Scores.ScoreData[player].aliveState)
                    {
                        case AliveState.CleanKill:
                            statusString += " (Clean-Kill!)";
                            break;
                        case AliveState.HeadShot:
                            statusString += " (Head-Shot!)";
                            break;
                        case AliveState.KillSteal:
                            statusString += " (Kill-Steal!)";
                            break;
                        case AliveState.AssistedKill:
                            statusString += ", et al.";
                            break;
                        case AliveState.Dead:
                            break;
                    }
                    GUI.Label(new Rect(_margin, height, vesselButtonWidth, _buttonHeight), statusString, BDArmorySetup.BDGuiSkin.label);
                    height += _buttonHeight + _buttonGap;
                }
            }
            // Piñata killers.
            if (!BDACompetitionMode.Instance.pinataAlive)
            {
                string postString = "";
                foreach (var player in BDACompetitionMode.Instance.Scores.Players)
                {
                    if (BDACompetitionMode.Instance.Scores.ScoreData[player].PinataHits > 0)
                    {
                        postString += " " + player;
                    }
                }
                if (postString != "")
                {
                    GUI.Label(new Rect(_margin, height, vesselButtonWidth, _buttonHeight), "Pinata Killers: " + postString, BDArmorySetup.BDGuiSkin.label);
                    height += _buttonHeight + _buttonGap;
                }
            }

            height += _margin;
            _windowHeight = height;
        }

        void AddVesselSwitcherWindowEntry(MissileFire wm, string team, float height, float vesselButtonWidth)
        {
            float _offset = 0;
            if (!BDArmorySettings.VESSEL_SWITCHER_WINDOW_OLD_DISPLAY_STYLE || BDArmorySettings.TAG_MODE)
            {
                if (BDTISetup.Instance.ColorAssignments.ContainsKey(team))
                {
                    BDTISetup.TILabel.normal.textColor = BDTISetup.Instance.ColorAssignments[team];
                }
                GUI.Label(new Rect(_margin, height, _buttonHeight, _buttonHeight), $"{(team.Length > 2 ? team.Remove(2) : team)}", BDTISetup.TILabel);
                _offset = _buttonHeight;
            }
            Rect buttonRect = new Rect(_margin + _offset, height, vesselButtonWidth, _buttonHeight);
            GUIStyle vButtonStyle = team == "IT" ? (wm.vessel.isActiveVessel ? ItVesselSelected : ItVessel) : wm.vessel.isActiveVessel ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            string vesselName = wm.vessel.GetName();
            BDArmory.Control.ScoringData scoreData = null;
            string status = UpdateVesselStatus(wm, vButtonStyle);
            int currentScore = 0;
            int currentRocketScore = 0;
            int currentRamScore = 0;
            int currentMissileScore = 0;
            double currentTagTime = 0;
            double currentTagScore = 0;
            int currentTimesIt = 0;

            if (BDACompetitionMode.Instance.Scores.Players.Contains(vesselName))
            {
                scoreData = BDACompetitionMode.Instance.Scores.ScoreData[vesselName];
                currentScore = scoreData.hits;
                currentRocketScore = scoreData.totalDamagedPartsDueToRockets;
                currentRamScore = scoreData.totalDamagedPartsDueToRamming;
                currentMissileScore = scoreData.totalDamagedPartsDueToMissiles;
                if (BDArmorySettings.TAG_MODE)
                {
                    currentTagTime = scoreData.tagTotalTime;
                    currentTagScore = scoreData.tagScore;
                    currentTimesIt = scoreData.tagTimesIt;
                }
            }
            if (VesselSpawner.Instance.vesselsSpawningContinuously)
            {
                if (VesselSpawner.Instance.continuousSpawningScores.ContainsKey(vesselName))
                {
                    currentScore += VesselSpawner.Instance.continuousSpawningScores[vesselName].cumulativeHits;
                    currentRocketScore += VesselSpawner.Instance.continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRockets;
                    currentRamScore += VesselSpawner.Instance.continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRamming;
                    currentMissileScore += VesselSpawner.Instance.continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToMissiles;
                }
                if (BDArmorySettings.TAG_MODE && VesselSpawner.Instance.continuousSpawningScores.ContainsKey(wm.vessel.vesselName))
                    currentTagTime += VesselSpawner.Instance.continuousSpawningScores[wm.vessel.vesselName].cumulativeTagTime;
            }

            // current target 
            string targetName = "";
            Vessel targetVessel = wm.vessel;
            bool incomingThreat = false;
            if (wm.incomingThreatVessel != null)
            {
                incomingThreat = true;
                targetName = "<<<" + wm.incomingThreatVessel.GetName();
                targetVessel = wm.incomingThreatVessel;
            }
            else if (wm.currentTarget)
            {
                targetName = ">>>" + wm.currentTarget.Vessel.GetName();
                targetVessel = wm.currentTarget.Vessel;
            }

            string postStatus = " (" + currentScore.ToString();
            if (currentRocketScore > 0) postStatus += ", " + currentRocketScore.ToString();
            if (currentRamScore > 0) postStatus += ", " + currentRamScore.ToString();
            if (currentMissileScore > 0) postStatus += ", " + currentMissileScore.ToString();
            if (BDArmorySettings.TAG_MODE)
                postStatus += ", " + (VesselSpawner.Instance.vesselsSpawningContinuously ? currentTagTime.ToString("0.0") : currentTagScore.ToString("0.0"));
            postStatus += ")";

            if (wm.AI != null && wm.AI.currentStatus != null)
            {
                postStatus += " " + wm.AI.currentStatus;
            }
            float targetDistance = 5000;
            if (wm.currentTarget != null)
            {
                targetDistance = Vector3.Distance(wm.vessel.GetWorldPos3D(), wm.currentTarget.position);
            }

            //postStatus += " :" + Convert.ToInt32(wm.vessel.srfSpeed).ToString();
            // display killerGM stats
            //if ((BDACompetitionMode.Instance.killerGMenabled) && BDACompetitionMode.Instance.FireCount.ContainsKey(vesselName))
            //{
            //    postStatus += " " + (BDACompetitionMode.Instance.FireCount[vesselName] + BDACompetitionMode.Instance.FireCount2[vesselName]).ToString() + ":" + Convert.ToInt32(BDACompetitionMode.Instance.AverageSpeed[vesselName] / BDACompetitionMode.Instance.averageCount).ToString();
            //}

            if (BDACompetitionMode.Instance.KillTimer.ContainsKey(vesselName))
            {
                postStatus += " x" + BDACompetitionMode.Instance.KillTimer[vesselName].ToString() + "x";
            }

            if (targetName != "")
            {
                postStatus += " " + targetName;
            }

            /*if (cameraScores.ContainsKey(vesselName))
            {
                int sc = (int)(cameraScores[vesselName]);
                postStatus += " [" + sc.ToString() + "]";
            }
            */

            if (GUI.Button(buttonRect, vesselName + status + postStatus, vButtonStyle))
                ForceSwitchVessel(wm.vessel);

            // selects current target
            if (targetName != "")
            {
                Rect targetingButtonRect = new Rect(_margin + vesselButtonWidth + _offset, height, _buttonHeight, _buttonHeight);
                GUIStyle targButton = BDArmorySetup.BDGuiSkin.button;
                if (wm.currentGun != null && wm.currentGun.recentlyFiring)
                {
                    if (targetDistance < 500)
                    {
                        targButton = redLight;
                    }
                    else if (targetDistance < 1000)
                    {
                        targButton = yellowLight;
                    }
                    else
                    {
                        targButton = blueLight;
                    }
                }
                if (GUI.Button(targetingButtonRect, incomingThreat ? "><" : "[]", targButton))
                    ForceSwitchVessel(targetVessel);
            }

            //guard toggle
            GUIStyle guardStyle = wm.guardMode ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
            Rect guardButtonRect = new Rect(_margin + vesselButtonWidth + _offset + _buttonHeight, height, _buttonHeight, _buttonHeight);
            if (GUI.Button(guardButtonRect, "G", guardStyle))
                wm.ToggleGuardMode();

            //AI toggle
            if (wm.AI != null)
            {
                GUIStyle aiStyle = new GUIStyle(wm.AI.pilotEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);
                if (wm.underFire)
                {
                    var distance = Vector3.Distance(wm.vessel.GetWorldPos3D(), wm.incomingThreatPosition);
                    if (distance < 500)
                    {
                        aiStyle.normal.textColor = Color.red;
                    }
                    else if (distance < 1000)
                    {
                        aiStyle.normal.textColor = Color.yellow;
                    }
                    else
                    {
                        aiStyle.normal.textColor = Color.blue;
                    }
                }
                Rect aiButtonRect = new Rect(_margin + vesselButtonWidth + _offset + 2 * _buttonHeight, height, _buttonHeight,
                    _buttonHeight);
                if (GUI.Button(aiButtonRect, "P", aiStyle))
                    wm.AI.TogglePilot();
            }

            //team toggle
            Rect teamButtonRect = new Rect(_margin + vesselButtonWidth + _offset + 3 * _buttonHeight, height,
                _buttonHeight, _buttonHeight);
            if (GUI.Button(teamButtonRect, "T", BDArmorySetup.BDGuiSkin.button))
            {
                if (Event.current.button == 1)
                {
                    BDTeamSelector.Instance.Open(wm, new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
                }
                else if (Event.current.button == 2)
                {
                    //wm.SetTeam(BDTeam.Get("Neutral"));
                    //if (wm.Team.Name != "Neutral" && wm.Team.Name != "A" && wm.Team.Name != "B") wm.Team.Neutral = !wm.Team.Neutral;
                    wm.NextTeam(true);
                }
                else
                {
                    wm.NextTeam();
                }
            }

            // boom
            Rect killButtonRect = new Rect(_margin + vesselButtonWidth + _offset + 4 * _buttonHeight, height, _buttonHeight, _buttonHeight);
            GUIStyle xStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
            var currentParts = wm.vessel.parts.Count;
            if (scoreData != null)
            {
                if (currentParts < scoreData.previousPartCount)
                {
                    xStyle.normal.textColor = Color.red;
                }
                else if (Planetarium.GetUniversalTime() - scoreData.lastDamageTime < 4)
                {
                    xStyle.normal.textColor = Color.yellow;
                }
            }
            if (wm.vessel != null && GUI.Button(killButtonRect, "X", xStyle))
            {
                // must use right button
                if (Event.current.button == 1)
                {
                    if (scoreData != null)
                    {
                        if (scoreData.lastPersonWhoDamagedMe == "")
                        {
                            scoreData.lastPersonWhoDamagedMe = "BIG RED BUTTON"; // only do this if it's not already damaged
                        }
                        BDACompetitionMode.Instance.Scores.RegisterDeath(vesselName, GMKillReason.BigRedButton); // Indicate that it was us who killed it.
                        BDACompetitionMode.Instance.competitionStatus.Add(vesselName + " was killed by the BIG RED BUTTON.");
                    }
                    Utils.ForceDeadVessel(wm.vessel);
                }
            }
        }

        private string UpdateVesselStatus(MissileFire wm, GUIStyle vButtonStyle)
        {
            string status = "";
            if (wm.vessel.LandedOrSplashed)
            {
                status = " ";
                if (wm.vessel.Landed)
                    status += Localizer.Format("#LOC_BDArmory_VesselStatus_Landed");//"(Landed)"
                else if (wm.vessel.IsUnderwater())
                    status += Localizer.Format("#LOC_BDArmory_VesselStatus_Underwater"); // "(Underwater)"
                else
                    status += Localizer.Format("#LOC_BDArmory_VesselStatus_Splashed");//"(Splashed)"
                vButtonStyle.fontStyle = FontStyle.Italic;
            }
            else
            {
                vButtonStyle.fontStyle = FontStyle.Normal;
            }
            return status;
        }

        private void SwitchToNextVessel()
        {
            if (weaponManagers.Count == 0) return;

            bool switchNext = false;

            using (var teamManagers = weaponManagers.GetEnumerator())
                while (teamManagers.MoveNext())
                    using (var wm = teamManagers.Current.Value.GetEnumerator())
                        while (wm.MoveNext())
                        {
                            if (wm.Current.vessel.isActiveVessel)
                                switchNext = true;
                            else if (switchNext)
                            {
                                ForceSwitchVessel(wm.Current.vessel);
                                return;
                            }
                        }
            var firstVessel = weaponManagers.Values[0][0].vessel;
            if (!firstVessel.isActiveVessel)
                ForceSwitchVessel(firstVessel);
        }

        /* If groups or specific are specified, then they take preference.
         * groups is a list of ints of the number of vessels to assign to each team.
         * specific is a list of lists of craft names.
         * If the sum of groups is less than the number of vessels, then the extras get assigned to their own team.
         * If specific does not contain all the vessel names, then the unmentioned vessels get assigned to team 'A'.
         */
        public void MassTeamSwitch(bool separateTeams = false, bool originalTeams = false, List<int> groups = null, List<List<string>> specific = null)
        {
            if (originalTeams)
            {
                foreach (var weaponManager in weaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).ToList()) // Get a copy in case activating stages causes the weaponManager list to change.
                {
                    if (VesselSpawner.Instance.originalTeams.ContainsKey(weaponManager.vessel.vesselName))
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.LoadedVesselSwitcher]: assigning " + weaponManager.vessel.GetDisplayName() + " to team " + VesselSpawner.Instance.originalTeams[weaponManager.vessel.vesselName]);
                        weaponManager.SetTeam(BDTeam.Get(VesselSpawner.Instance.originalTeams[weaponManager.vessel.vesselName]));
                    }
                }
                return;
            }
            char T = 'A';
            if (specific != null)
            {
                var weaponManagersByName = weaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).ToDictionary(wm => wm.vessel.vesselName);
                foreach (var craftList in specific)
                {
                    foreach (var craftName in craftList)
                    {
                        if (weaponManagersByName.ContainsKey(craftName))
                            weaponManagersByName[craftName].SetTeam(BDTeam.Get(T.ToString()));
                        else
                            Debug.Log("[BDArmory.LoadedVesselSwitcher]: Specified vessel (" + craftName + ") not found amongst active vessels.");
                        weaponManagersByName.Remove(craftName); // Remove the vessel from our dictionary once it's assigned.
                    }
                    ++T;
                }
                foreach (var craftName in weaponManagersByName.Keys)
                {
                    Debug.Log("[BDArmory.LoadedVesselSwitcher]: Vessel " + craftName + " was not specified to be part of a team, but is active. Assigning to team " + T.ToString() + ".");
                    weaponManagersByName[craftName].SetTeam(BDTeam.Get(T.ToString())); // Assign anyone who wasn't specified to a separate team.
                    weaponManagersByName[craftName].Team.Neutral = false;
                }
                return;
            }
            if (groups != null)
            {
                int groupIndex = 0;
                int count = 0;
                foreach (var weaponManager in weaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).ToList())
                {
                    while (groupIndex < groups.Count && count == groups[groupIndex])
                    {
                        ++groupIndex;
                        count = 0;
                        ++T;
                    }
                    weaponManager.SetTeam(BDTeam.Get(T.ToString())); // Otherwise, assign them to team T.
                    weaponManager.Team.Neutral = false;
                    ++count;
                }
                return;
            }
            // switch everyone to their own teams
            foreach (var weaponManager in weaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).ToList()) // Get a copy in case activating stages causes the weaponManager list to change.
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.LoadedVesselSwitcher]: assigning " + weaponManager.vessel.GetDisplayName() + " to team " + T.ToString());
                weaponManager.SetTeam(BDTeam.Get(T.ToString()));
                weaponManager.Team.Neutral = false;
                if (separateTeams) T++;
            }
        }

        private void SwitchToPreviousVessel()
        {
            if (weaponManagers.Count == 0) return;

            Vessel previousVessel = weaponManagers.Values[weaponManagers.Count - 1][weaponManagers.Values[weaponManagers.Count - 1].Count - 1].vessel;

            using (var teamManagers = weaponManagers.GetEnumerator())
                while (teamManagers.MoveNext())
                    using (var wm = teamManagers.Current.Value.GetEnumerator())
                        while (wm.MoveNext())
                        {
                            if (wm.Current.vessel.isActiveVessel)
                            {
                                ForceSwitchVessel(previousVessel);
                                return;
                            }
                            previousVessel = wm.Current.vessel;
                        }
            if (!previousVessel.isActiveVessel)
                ForceSwitchVessel(previousVessel);
        }

        void CurrentVesselWillDestroy(Vessel v)
        {
            if (_autoCameraSwitch && lastActiveVessel == v)
            {
                currentVesselDied = true;
                currentVesselDiedAt = Planetarium.GetUniversalTime();
            }
        }

        private void UpdateCamera()
        {
            var now = Planetarium.GetUniversalTime();
            double timeSinceLastCheck = now - lastCameraCheck;
            if (currentVesselDied)
            {
                if (now - currentVesselDiedAt < (BDArmorySettings.DEATH_CAMERA_SWITCH_INHIBIT_PERIOD == 0 ? BDArmorySettings.CAMERA_SWITCH_FREQUENCY / 2f : BDArmorySettings.DEATH_CAMERA_SWITCH_INHIBIT_PERIOD)) // Prevent camera changes for a bit.
                    return;
                else
                {
                    currentVesselDied = false;
                    lastCameraSwitch = 0;
                }
            }

            if (timeSinceLastCheck > 0.25)
            {
                lastCameraCheck = now;

                // first check to see if we've changed the vessel recently
                if (lastActiveVessel != null)
                {
                    if (!lastActiveVessel.isActiveVessel)
                    {
                        // active vessel was changed 
                        lastCameraSwitch = now;
                    }
                }
                lastActiveVessel = FlightGlobals.ActiveVessel;
                double timeSinceChange = now - lastCameraSwitch;

                float bestScore = 10000000;
                Vessel bestVessel = null;
                bool foundActiveVessel = false;
                // redo the math
                using (var v = FlightGlobals.Vessels.GetEnumerator())
                    // check all the planes
                    while (v.MoveNext())
                    {
                        if (v.Current == null || !v.Current.loaded || v.Current.packed) continue;
                        if (VesselModuleRegistry.ignoredVesselTypes.Contains(v.Current.vesselType)) continue;
                        if ((v.Current.GetCrewCapacity()) > 0 && (v.Current.GetCrewCount() == 0)) continue; //They're dead, Jim
                        using (var wms = VesselModuleRegistry.GetModules<MissileFire>(v.Current).GetEnumerator())
                            while (wms.MoveNext())
                                if (wms.Current != null && wms.Current.vessel != null)
                                {
                                    float vesselScore = 1000;
                                    float targetDistance = 5000 + (float)(rng.NextDouble() * 100.0);
                                    float crashTime = 30;
                                    string vesselName = v.Current.GetName();
                                    // avoid lingering on dying things
                                    bool recentlyDamaged = false;
                                    bool recentlyLanded = false;

                                    // check for damage & landed status

                                    if (BDACompetitionMode.Instance.Scores.Players.Contains(vesselName))
                                    {
                                        var currentParts = v.Current.parts.Count;
                                        var vdat = BDACompetitionMode.Instance.Scores.ScoreData[vesselName];
                                        if (now - vdat.lastLostPartTime < 5d) // Lost parts within the last 5s.
                                        {
                                            recentlyDamaged = true;
                                        }

                                        if (vdat.landedState)
                                        {
                                            var timeSinceLanded = now - vdat.lastLandedTime;
                                            if (timeSinceLanded < 2)
                                            {
                                                recentlyLanded = true;
                                            }
                                        }
                                    }
                                    vesselScore = Math.Abs(vesselScore);
                                    float HP = 0;
                                    float WreckFactor = 0;
                                    var AI = VesselModuleRegistry.GetBDModulePilotAI(v.Current, true);

                                    HP = (wms.Current.currentHP / wms.Current.totalHP) * 100;
                                    if (HP < 100)
                                    {
                                        WreckFactor += (100 - HP) / 100; //the less plane remaining, the greater the chance it's a wreck
                                    }
                                    if (v.Current.verticalSpeed < -30) //falling out of the sky? Could be an intact plane diving to default alt, could be a cockpit
                                    {
                                        WreckFactor += 0.5f;
                                        if (AI == null || v.Current.radarAltitude < AI.defaultAltitude) //craft is uncontrollably diving, not returning from high alt to cruising alt
                                        {
                                            WreckFactor += 0.5f;
                                        }
                                    }
                                    if (VesselModuleRegistry.GetModuleCount<ModuleEngines>(v.Current) > 0)
                                    {
                                        int engineOut = 0;
                                        foreach (var engine in VesselModuleRegistry.GetModules<ModuleEngines>(v.Current))
                                        {
                                            if (engine == null || engine.flameout || engine.finalThrust <= 0)
                                                engineOut++;
                                        }
                                        WreckFactor += (engineOut / VesselModuleRegistry.GetModuleCount<ModuleEngines>(v.Current)) / 2;
                                    }
                                    else
                                    {
                                        WreckFactor += 0.5f; //could be a glider, could be missing engines
                                    }
                                    if (WreckFactor > 1f) // 'wrecked' requires some combination of diving, no engines, and missing parts
                                    {
                                        WreckFactor *= 2;
                                        vesselScore *= WreckFactor; //disincentivise switching to wrecks
                                    }
                                    if (!recentlyLanded && v.Current.verticalSpeed < -15) // Vessels gently floating to the ground aren't interesting
                                    {
                                        crashTime = (float)(-Math.Abs(v.Current.radarAltitude) / v.Current.verticalSpeed);
                                    }
                                    if (crashTime < 30)
                                    {
                                        vesselScore *= crashTime / 30;
                                    }
                                    if (wms.Current.currentTarget != null)
                                    {
                                        targetDistance = Vector3.Distance(wms.Current.vessel.GetWorldPos3D(), wms.Current.currentTarget.position);
                                        if (!wms.Current.HasWeaponsAndAmmo()) // no remaining weapons
                                        {
                                            if (!BDArmorySettings.DISABLE_RAMMING && AI != null && AI.allowRamming) //ramming's fun to watch
                                            {
                                                vesselScore *= (0.031623f * Mathf.Sqrt(targetDistance) / 2);
                                            }
                                            else
                                            {
                                                vesselScore *= 3; //ramming disabled. Boring!
                                            }
                                        }
                                        //else got weapons and engaging
                                    }
                                    vesselScore *= 0.031623f * Mathf.Sqrt(targetDistance); // Equal to 1 at 1000m
                                    if (wms.Current.currentGun != null)
                                    {
                                        if (wms.Current.currentGun.recentlyFiring)
                                        {
                                            // shooting at things is more interesting
                                            vesselScore *= 0.25f;
                                        }
                                    }
                                    if (wms.Current.guardFiringMissile)
                                    {
                                        // firing a missile at things is more interesting
                                        vesselScore *= 0.2f;
                                    }
                                    // scoring for automagic camera check should not be in here
                                    if (wms.Current.underAttack || wms.Current.underFire)
                                    {
                                        vesselScore *= 0.5f;
                                        var distance = Vector3.Distance(wms.Current.vessel.GetWorldPos3D(), wms.Current.incomingThreatPosition);
                                        vesselScore *= 0.031623f * Mathf.Sqrt(distance); // Equal to 1 at 1000m, we don't want to overly disadvantage craft that are super far away, but could be firing missiles or doing other interesting things
                                        //we're very interested when threat and target are the same
                                        if (wms.Current.incomingThreatVessel != null && wms.Current.currentTarget != null)
                                        {
                                            if (wms.Current.incomingThreatVessel.GetName() == wms.Current.currentTarget.Vessel.GetName())
                                            {
                                                vesselScore *= 0.25f;
                                            }
                                        }

                                    }
                                    if (wms.Current.incomingMissileVessel != null)
                                    {
                                        float timeToImpact = wms.Current.incomingMissileTime;
                                        vesselScore *= Mathf.Clamp(0.0005f * timeToImpact * timeToImpact, 0, 1); // Missiles about to hit are interesting, scale score with time to impact

                                        if (wms.Current.isFlaring || wms.Current.isChaffing)
                                            vesselScore *= 0.8f;
                                    }
                                    if (recentlyDamaged)
                                    {
                                        vesselScore *= 0.3f; // because taking hits is very interesting;
                                    }
                                    if (!recentlyLanded && wms.Current.vessel.LandedOrSplashed)
                                    {
                                        if (v.Current.srfSpeed > 2) //margin for physics jitter
                                        {
                                            vesselScore *= Mathf.Min(((80 / (float)v.Current.srfSpeed) / 2), 4); //srf Ai driven stuff thats still mobile
                                        }
                                        else
                                            vesselScore *= 4; // not interesting.
                                    }
                                    // if we're the active vessel add a penalty over time to force it to switch away eventually
                                    if (wms.Current.vessel.isActiveVessel)
                                    {
                                        vesselScore = (float)(vesselScore * timeSinceChange / 8.0);
                                        foundActiveVessel = true;
                                    }
                                    if ((BDArmorySettings.TAG_MODE) && (wms.Current.Team.Name == "IT"))
                                    {
                                        vesselScore = 0f; // Keep camera focused on "IT" vessel during tag
                                    }


                                    // if the score is better then update this
                                    if (vesselScore < bestScore)
                                    {
                                        bestVessel = wms.Current.vessel;
                                        bestScore = vesselScore;
                                    }
                                    cameraScores[wms.Current.vessel.GetName()] = vesselScore;
                                }
                    }
                if (!foundActiveVessel)
                {
                    var score = 100 * timeSinceChange;
                    if (score < bestScore)
                    {
                        bestVessel = null; // stop switching
                    }
                }
                if (timeSinceChange > BDArmorySettings.CAMERA_SWITCH_FREQUENCY)
                {
                    if (bestVessel != null && bestVessel.loaded && !bestVessel.packed && !(bestVessel.isActiveVessel)) // if a vessel dies it'll use a default score for a few seconds
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.LoadedVesselSwitcher]: Switching vessel to " + bestVessel.GetDisplayName());
                        ForceSwitchVessel(bestVessel);
                    }
                }
            }
        }

        public void EnableAutoVesselSwitching(bool enable)
        {
            _autoCameraSwitch = enable;
        }

        // Extracted method, so we dont have to call these two lines everywhere
        public void ForceSwitchVessel(Vessel v)
        {
            if (v == null || !v.loaded)
                return;
            lastCameraSwitch = Planetarium.GetUniversalTime();
            FlightGlobals.ForceSetActiveVessel(v);
            FlightInputHandler.ResumeVesselCtrlState(v);
        }

        public void TriggerSwitchVessel(float delay)
        {
            lastCameraSwitch = delay > 0 ? Planetarium.GetUniversalTime() - (BDArmorySettings.CAMERA_SWITCH_FREQUENCY - delay) : 0f;
            lastCameraCheck = 0f;
            UpdateCamera();
        }

        /// <summary>
        ///     Creates a 1x1 texture
        /// </summary>
        /// <param name="Background">Color of the texture</param>
        /// <returns></returns>
        internal static Texture2D CreateColorPixel(Color32 Background)
        {
            Texture2D retTex = new Texture2D(1, 1);
            retTex.SetPixel(0, 0, Background);
            retTex.Apply();
            return retTex;
        }

        #region Vessel Tracing
        Vector3d floatingOriginCorrection = Vector3d.zero;
        Quaternion referenceRotationCorrection = Quaternion.identity;
        Dictionary<string, List<Tuple<float, Vector3, Quaternion>>> vesselTraces = new Dictionary<string, List<Tuple<float, Vector3, Quaternion>>>();

        public void StartVesselTracing()
        {
            if (vesselTraceEnabled) return;
            vesselTraceEnabled = true;
            Debug.Log("[BDArmory.LoadedVesselSwitcher]: Starting vessel tracing.");
            vesselTraces.Clear();

            // Set the reference Up and Rotation based on the current FloatingOrigin.
            var geoCoords = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(Vector3.zero);
            var altitude = FlightGlobals.getAltitudeAtPos(Vector3.zero);
            var localUp = -FlightGlobals.getGeeForceAtPosition(Vector3.zero).normalized;
            var q1 = Quaternion.FromToRotation(Vector3.up, localUp);
            var q2 = Quaternion.AngleAxis(Vector3.SignedAngle(q1 * Vector3.forward, Vector3.up, localUp), localUp);
            var referenceRotation = q2 * q1; // Plane tangential to the surface and aligned with north,
            referenceRotationCorrection = Quaternion.Inverse(referenceRotation);
            floatingOriginCorrection = altitude * localUp;

            // Record starting points
            var survivingVessels = weaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).Select(wm => wm.vessel).ToList();
            foreach (var vessel in survivingVessels)
            {
                if (vessel == null) continue;
                vesselTraces[vessel.vesselName] = new List<Tuple<float, Vector3, Quaternion>>();
                vesselTraces[vessel.vesselName].Add(new Tuple<float, Vector3, Quaternion>(Time.time, new Vector3((float)geoCoords.x, (float)geoCoords.y, altitude), referenceRotation));
            }
        }
        public void StopVesselTracing()
        {
            if (!vesselTraceEnabled) return;
            vesselTraceEnabled = false;
            Debug.Log("[BDArmory.LoadedVesselSwitcher]: Stopping vessel tracing.");
            var folder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "BDArmory", "Logs", "VesselTraces");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            foreach (var vesselName in vesselTraces.Keys)
            {
                var traceFile = Path.Combine(folder, vesselName + "-" + vesselTraces[vesselName][0].Item1.ToString("0.000") + ".json");
                Debug.Log("[BDArmory.LoadedVesselSwitcher]: Dumping trace for " + vesselName + " to " + traceFile);
                List<string> strings = new List<string>();
                strings.Add("[");
                strings.Add(string.Join(",\n", vesselTraces[vesselName].Select(entry => "  { \"time\": " + entry.Item1.ToString("0.000") + ", \"position\": [" + entry.Item2.x.ToString("0.0") + ", " + entry.Item2.y.ToString("0.0") + ", " + entry.Item2.z.ToString("0.0") + "], \"rotation\": [" + entry.Item3.x.ToString("0.000") + ", " + entry.Item3.y.ToString("0.000") + ", " + entry.Item3.z.ToString("0.000") + ", " + entry.Item3.w.ToString("0.000") + "] }")));
                strings.Add("]");
                File.WriteAllLines(traceFile, strings);
            }
            vesselTraces.Clear();
        }
        #endregion
    }
}
