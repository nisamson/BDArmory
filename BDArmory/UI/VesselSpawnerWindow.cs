﻿using KSP.Localization;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Utils;
using BDArmory.Misc;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselSpawnerWindow : MonoBehaviour
    {
        #region Fields
        public static VesselSpawnerWindow Instance;
        private int _guiCheckIndex;
        private static readonly float _buttonSize = 20;
        private static readonly float _margin = 5;
        private static readonly float _lineHeight = _buttonSize;
        private float _windowHeight; //auto adjusting
        private float _windowWidth;
        public bool _ready = false;
        private bool _vesselsSpawned = false;
        Dictionary<string, NumericInputField> spawnFields;

        private GUIContent[] planetGUI;
        private GUIContent planetText;
        private BDGUIComboBox planetBox;
        private int previous_index = 1;
        private bool planetslist = false;
        int selected_index = 1;
        #endregion
        #region GUI strings
        string tournamentStyle = "RNG";
        #endregion

        #region Styles
        Rect SLineRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth - 2 * _margin, _lineHeight);
        }

        Rect SLeftRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth / 2 - _margin - _margin / 4, _lineHeight);
        }

        Rect SRightRect(float line)
        {
            return new Rect(_windowWidth / 2 + _margin / 4, line * _lineHeight, _windowWidth / 2 - _margin - _margin / 4, _lineHeight);
        }

        Rect SLeftSliderRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth / 2 + _margin / 2, _lineHeight);
        }

        Rect SRightSliderRect(float line)
        {
            return new Rect(_margin + _windowWidth / 2 + _margin / 2, line * _lineHeight, _windowWidth / 2 - 7 / 2 * _margin, _lineHeight);
        }

        Rect SLeftButtonRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, (_windowWidth - 2 * _margin) / 2 - _margin / 4, _lineHeight);
        }

        Rect SRightButtonRect(float line)
        {
            return new Rect(_windowWidth / 2 + _margin / 4, line * _lineHeight, (_windowWidth - 2 * _margin) / 2 - _margin / 4, _lineHeight);
        }

        Rect SQuarterRect(float line, int pos)
        {
            return new Rect(_margin + (pos % 4) * (_windowWidth - 2f * _margin) / 4f, (line + (int)(pos / 4)) * _lineHeight, (_windowWidth - 2.5f * _margin) / 4f, _lineHeight);
        }

        List<Rect> SRight2Rects(float line)
        {
            var rectGap = _margin / 2;
            var rectWidth = ((_windowWidth - 2 * _margin) / 2 - 2 * rectGap) / 2;
            var rects = new List<Rect>();
            rects.Add(new Rect(_windowWidth / 2 + rectGap / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + rectWidth + rectGap * 3 / 2, line * _lineHeight, rectWidth, _lineHeight));
            return rects;
        }

        List<Rect> SRight3Rects(float line)
        {
            var rectGap = _margin / 3;
            var rectWidth = ((_windowWidth - 2 * _margin) / 2 - 3 * rectGap) / 3;
            var rects = new List<Rect>();
            rects.Add(new Rect(_windowWidth / 2 + rectGap / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + rectWidth + rectGap * 3 / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + 2 * rectWidth + rectGap * 5 / 2, line * _lineHeight, rectWidth, _lineHeight));
            return rects;
        }

        GUIStyle leftLabel;
        #endregion
        private string txtName = string.Empty;
        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;
        }

        private void Start()
        {
            _ready = false;
            StartCoroutine(WaitForBdaSettings());

            leftLabel = new GUIStyle();
            leftLabel.alignment = TextAnchor.UpperLeft;
            leftLabel.normal.textColor = Color.white;

            // Spawn fields
            spawnFields = new Dictionary<string, NumericInputField> {
                { "lat", gameObject.AddComponent<NumericInputField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, -90, 90) },
                { "lon", gameObject.AddComponent<NumericInputField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, -180, 180) },
                { "alt", gameObject.AddComponent<NumericInputField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_ALTITUDE) },
            };
            selected_index = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.flightGlobalsIndex : 1;
        }

        private IEnumerator WaitForBdaSettings()
        {
            while (BDArmorySetup.Instance == null)
                yield return null;

            BDArmorySetup.Instance.hasVesselSpawner = true;
            _guiCheckIndex = Utils.RegisterGUIRect(new Rect());
            _ready = true;
        }

        private void FillPlanetList()
        {
            planetGUI = new GUIContent[FlightGlobals.Bodies.Count];
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                GUIContent gui = new GUIContent(FlightGlobals.Bodies[i].name);
                planetGUI[i] = gui;
            }

            planetText = new GUIContent();
            planetText.text = Localizer.Format("#LOC_BDArmory_Settings_Planet");//"Select Planet"
        }

        private void Update()
        {
            HotKeys();
        }

        private void OnGUI()
        {
            if (!(_ready && BDArmorySetup.GAME_UI_ENABLED && BDArmorySetup.Instance.showVesselSpawnerGUI))
                return;

            _windowWidth = BDArmorySettings.VESSEL_SPAWNER_WINDOW_WIDTH;

            SetNewHeight(_windowHeight);
            BDArmorySetup.WindowRectVesselSpawner = new Rect(
                BDArmorySetup.WindowRectVesselSpawner.x,
                BDArmorySetup.WindowRectVesselSpawner.y,
                _windowWidth,
                _windowHeight
            );
            BDArmorySetup.WindowRectVesselSpawner = GUI.Window(
                GetInstanceID(), // InstanceID should be unique. FIXME All GUI.Windows should use the same method of generating unique IDs to avoid duplicates.
                BDArmorySetup.WindowRectVesselSpawner,
                WindowVesselSpawner,
                Localizer.Format("#LOC_BDArmory_BDAVesselSpawner_Title"),//"BDA Vessel Spawner"
                BDArmorySetup.BDGuiSkin.window
            );
            Utils.UpdateGUIRect(BDArmorySetup.WindowRectVesselSpawner, _guiCheckIndex);
        }

        void HotKeys()
        {
            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.TOURNAMENT_SETUP))
                BDATournament.Instance.SetupTournament(BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION, BDArmorySettings.TOURNAMENT_ROUNDS, BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT);
            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.TOURNAMENT_RUN))
                BDATournament.Instance.RunTournament();
        }

        void ParseAllSpawnFieldsNow()
        {
            spawnFields["lat"].tryParseValueNow();
            spawnFields["lon"].tryParseValueNow();
            spawnFields["alt"].tryParseValueNow();
            BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x = spawnFields["lat"].currentValue;
            BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y = spawnFields["lon"].currentValue;
            BDArmorySettings.VESSEL_SPAWN_WORLDINDEX = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.flightGlobalsIndex : 1; //selected_index?
            BDArmorySettings.VESSEL_SPAWN_ALTITUDE = (float)spawnFields["alt"].currentValue;
        }

        private void SetNewHeight(float windowHeight)
        {
            var previousWindowHeight = BDArmorySetup.WindowRectVesselSpawner.height;
            BDArmorySetup.WindowRectVesselSpawner.height = windowHeight;
            if (BDArmorySettings.STRICT_WINDOW_BOUNDARIES && windowHeight < previousWindowHeight && Mathf.RoundToInt(BDArmorySetup.WindowRectVesselSpawner.y + previousWindowHeight) == Screen.height) // Window shrunk while being at edge of screen.
                BDArmorySetup.WindowRectVesselSpawner.y = Screen.height - BDArmorySetup.WindowRectVesselSpawner.height;
            BDGUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectVesselSpawner);
        }

        private void WindowVesselSpawner(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _windowWidth - _buttonSize - _margin, _buttonSize + _margin));
            if (GUI.Button(new Rect(_windowWidth - _buttonSize - (_margin - 2), _margin, _buttonSize - 2, _buttonSize - 2), "X", BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySetup.Instance.showVesselSpawnerGUI = false;
                BDArmorySetup.SaveConfig();
            }

            float line = 0.25f;
            var rects = new List<Rect>();

            if (GUI.Button(SLineRect(++line), (BDArmorySettings.SHOW_SPAWN_OPTIONS ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_SpawnOptions"), BDArmorySettings.SHOW_SPAWN_OPTIONS ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Show/hide spawn options
            {
                BDArmorySettings.SHOW_SPAWN_OPTIONS = !BDArmorySettings.SHOW_SPAWN_OPTIONS;
            }
            if (BDArmorySettings.SHOW_SPAWN_OPTIONS)
            {
                if (BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE)
                { // Absolute distance
                    var value = BDArmorySettings.VESSEL_SPAWN_DISTANCE < 100 ? BDArmorySettings.VESSEL_SPAWN_DISTANCE / 10 : BDArmorySettings.VESSEL_SPAWN_DISTANCE < 1000 ? 9 + BDArmorySettings.VESSEL_SPAWN_DISTANCE / 100 : BDArmorySettings.VESSEL_SPAWN_DISTANCE < 10000 ? 18 + BDArmorySettings.VESSEL_SPAWN_DISTANCE / 1000 : 26 + BDArmorySettings.VESSEL_SPAWN_DISTANCE / 5000;
                    var displayValue = BDArmorySettings.VESSEL_SPAWN_DISTANCE < 1000 ? BDArmorySettings.VESSEL_SPAWN_DISTANCE.ToString("0") + "m" : (BDArmorySettings.VESSEL_SPAWN_DISTANCE / 1000).ToString("0") + "km";
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnDistance")}:  ({displayValue})", leftLabel);//Spawn Distance
                    value = Mathf.Round(GUI.HorizontalSlider(SRightSliderRect(line), value, 1f, 30f));
                    BDArmorySettings.VESSEL_SPAWN_DISTANCE = value < 10 ? 10 * value : value < 19 ? 100 * (value - 9) : value < 28 ? 1000 * (value - 18) : 5000 * (value - 26);
                }
                else
                { // Distance factor
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnDistanceFactor")}:  ({BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR})", leftLabel);//Spawn Distance Factor
                    BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR = Mathf.Round(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR / 10f, 1f, 10f) * 10f);
                }
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnEaseInSpeed")}:  ({BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED})", leftLabel);//Spawn Ease In Speed
                BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED = Mathf.Round(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, 0.1f, 1f) * 10f) / 10f;

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnConcurrentVessels")}:  ({(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS.ToString() : "Inf")})", leftLabel);//Max Concurrent Vessels (CS)
                BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS, 0f, 20f));

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnLivesPerVessel")}:  ({(BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL > 0 ? BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL.ToString() : "Inf")})", leftLabel);//Respawns (CS)
                BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL, 0f, 20f));

                var outOfAmmoKillTimeStr = "never";
                if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME > -1 && BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 60)
                    outOfAmmoKillTimeStr = BDArmorySettings.OUT_OF_AMMO_KILL_TIME.ToString("G0") + "s";
                else if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME > 59 && BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 61)
                    outOfAmmoKillTimeStr = "1min";
                else if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME > 60)
                    outOfAmmoKillTimeStr = Mathf.RoundToInt(BDArmorySettings.OUT_OF_AMMO_KILL_TIME / 60f).ToString("G0") + "mins";
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_OutOfAmmoKillTime")}: ({outOfAmmoKillTimeStr})", leftLabel); // Out of ammo kill timer for continuous spawning mode.
                float outOfAmmoKillTime;
                switch (Mathf.RoundToInt(BDArmorySettings.OUT_OF_AMMO_KILL_TIME))
                {
                    case 0:
                        outOfAmmoKillTime = 1f;
                        break;
                    case 10:
                        outOfAmmoKillTime = 2f;
                        break;
                    case 20:
                        outOfAmmoKillTime = 3f;
                        break;
                    case 30:
                        outOfAmmoKillTime = 4f;
                        break;
                    case 45:
                        outOfAmmoKillTime = 5f;
                        break;
                    case 60:
                        outOfAmmoKillTime = 6f;
                        break;
                    case 120:
                        outOfAmmoKillTime = 7f;
                        break;
                    case 300:
                        outOfAmmoKillTime = 8f;
                        break;
                    default:
                        outOfAmmoKillTime = 9f;
                        break;
                }
                outOfAmmoKillTime = GUI.HorizontalSlider(SRightSliderRect(line), outOfAmmoKillTime, 1f, 9f);
                switch (Mathf.RoundToInt(outOfAmmoKillTime))
                {
                    case 1:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 0f; // 0s
                        break;
                    case 2:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 10f; // 10s
                        break;
                    case 3:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 20f; // 20s
                        break;
                    case 4:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 30f; // 30s
                        break;
                    case 5:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 45f; // 45s
                        break;
                    case 6:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 60f; // 1 min
                        break;
                    case 7:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 120f;// 2 mins
                        break;
                    case 8:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 300f; // 5 mins
                        break;
                    default:
                        BDArmorySettings.OUT_OF_AMMO_KILL_TIME = -1f; // Never
                        break;
                }

                string fillSeats = "";
                switch (BDArmorySettings.VESSEL_SPAWN_FILL_SEATS)
                {
                    case 0:
                        fillSeats = Localizer.Format("#LOC_BDArmory_Settings_SpawnFillSeats_Minimal");
                        break;
                    case 1:
                        fillSeats = Localizer.Format("#LOC_BDArmory_Settings_SpawnFillSeats_Default"); // Full cockpits or the first combat seat if no cockpits are found.
                        break;
                    case 2:
                        fillSeats = Localizer.Format("#LOC_BDArmory_Settings_SpawnFillSeats_AllControlPoints");
                        break;
                    case 3:
                        fillSeats = Localizer.Format("#LOC_BDArmory_Settings_SpawnFillSeats_Cabins");
                        break;
                }
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnFillSeats")}:  ({fillSeats})", leftLabel); // Fill Seats
                BDArmorySettings.VESSEL_SPAWN_FILL_SEATS = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_FILL_SEATS, 0f, 3f));

                string numberOfTeams;
                switch (BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS)
                {
                    case 0: // FFA
                        numberOfTeams = Localizer.Format("#LOC_BDArmory_Settings_Teams_FFA");
                        break;
                    case 1: // Folders
                        numberOfTeams = Localizer.Format("#LOC_BDArmory_Settings_Teams_Folders");
                        break;
                    default: // Specified directly
                        numberOfTeams = Localizer.Format("#LOC_BDArmory_Settings_Teams_SplitEvenly") + " " + BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS.ToString("0");
                        break;
                }
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_Teams")}:  ({numberOfTeams})", leftLabel); // Number of teams.
                BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS, 0f, 10f));

                GUI.Label(SLeftRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnFilesLocation")} (AutoSpawn{Path.DirectorySeparatorChar}): ", leftLabel); // Craft files location
                BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION = GUI.TextField(SRightRect(line), BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION);

                BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE = GUI.Toggle(SLeftRect(++line), BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE, Localizer.Format("#LOC_BDArmory_Settings_SpawnDistanceToggle"));  // Toggle between distance factor and absolute distance.
                BDArmorySettings.VESSEL_SPAWN_REASSIGN_TEAMS = GUI.Toggle(SRightRect(line), BDArmorySettings.VESSEL_SPAWN_REASSIGN_TEAMS, Localizer.Format("#LOC_BDArmory_Settings_SpawnReassignTeams")); // Reassign Teams
                BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING = GUI.Toggle(SLeftRect(++line), BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING, Localizer.Format("#LOC_BDArmory_Settings_SpawnContinueSingleSpawning"));  // Spawn craft again after single spawn competition finishes.
                BDArmorySettings.VESSEL_SPAWN_DUMP_LOG_EVERY_SPAWN = GUI.Toggle(SRightRect(line), BDArmorySettings.VESSEL_SPAWN_DUMP_LOG_EVERY_SPAWN, Localizer.Format("#LOC_BDArmory_Settings_SpawnDumpLogsEverySpawn")); //Dump logs every spawn.
                BDArmorySettings.VESSEL_SPAWN_RANDOM_ORDER = GUI.Toggle(SLeftRect(++line), BDArmorySettings.VESSEL_SPAWN_RANDOM_ORDER, Localizer.Format("#LOC_BDArmory_Settings_SpawnRandomOrder"));  // Toggle between random spawn order or fixed.

                if (GUI.Button(SLeftButtonRect(++line), Localizer.Format("#LOC_BDArmory_Settings_VesselSpawnGeoCoords"), BDArmorySetup.BDGuiSkin.button)) //"Vessel Spawning Location"
                {
                    Ray ray = new Ray(FlightCamera.fetch.mainCamera.transform.position, FlightCamera.fetch.mainCamera.transform.forward);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 10000, (int)LayerMasks.Scenery))
                    {
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(hit.point);
                        spawnFields["lat"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x;
                        spawnFields["lon"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y;
                    }
                }
                rects = SRight3Rects(line);
                spawnFields["lat"].tryParseValue(GUI.TextField(rects[0], spawnFields["lat"].possibleValue, 8));
                spawnFields["lon"].tryParseValue(GUI.TextField(rects[1], spawnFields["lon"].possibleValue, 8));
                spawnFields["alt"].tryParseValue(GUI.TextField(rects[2], spawnFields["alt"].possibleValue, 8));
                BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x = spawnFields["lat"].currentValue;
                BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y = spawnFields["lon"].currentValue;
                BDArmorySettings.VESSEL_SPAWN_WORLDINDEX = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.flightGlobalsIndex : 1;
                BDArmorySettings.VESSEL_SPAWN_ALTITUDE = (float)spawnFields["alt"].currentValue;

                txtName = GUI.TextField(SRightButtonRect(++line), txtName);
                if (GUI.Button(SLeftButtonRect(line), Localizer.Format("#LOC_BDArmory_Settings_SaveSpawnLoc"), BDArmorySetup.BDGuiSkin.button))
                {
                    string newName = string.IsNullOrEmpty(txtName.Trim()) ? "New Location" : txtName.Trim();
                    VesselSpawner.spawnLocations.Add(new SpawnLocation(newName, new Vector2d(spawnFields["lat"].currentValue, spawnFields["lon"].currentValue), selected_index));
                    VesselSpawnerField.Save();
                }

                if (GUI.Button(SLeftButtonRect(++line), Localizer.Format("#LOC_BDArmory_Settings_ClearDebrisNow"), BDArmorySetup.BDGuiSkin.button))
                {
                    // Clean up debris now
                    BDACompetitionMode.Instance.RemoveDebrisNow();
                }
                if (GUI.Button(SRightButtonRect(line), Localizer.Format("#LOC_BDArmory_Settings_ClearBystandersNow"), BDArmorySetup.BDGuiSkin.button))
                {
                    // Clean up bystanders now
                    BDACompetitionMode.Instance.RemoveNonCompetitors(true);
                }
                line += 0.3f;
            }

            if (GUI.Button(SLineRect(++line), (BDArmorySettings.SHOW_SPAWN_LOCATIONS ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_SpawnLocations"), BDArmorySettings.SHOW_SPAWN_LOCATIONS ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Show/hide spawn locations
            {
                BDArmorySettings.SHOW_SPAWN_LOCATIONS = !BDArmorySettings.SHOW_SPAWN_LOCATIONS;
            }
            if (BDArmorySettings.SHOW_SPAWN_LOCATIONS)
            {
                line++;
                ///////////////////
                if (!planetslist)
                {
                    FillPlanetList();
                    GUIStyle listStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
                    listStyle.fixedHeight = 18; //make list contents slightly smaller
                    planetBox = new BDGUIComboBox(SLeftButtonRect(line), SLeftButtonRect(line), planetText, planetGUI, _lineHeight * 6, listStyle);
                    planetslist = true;
                }
                planetBox.UpdateRect(SLeftButtonRect(line));
                selected_index = planetBox.Show();
                if (GUI.Button(SRightButtonRect(line), Localizer.Format("#LOC_BDArmory_Settings_WarpHere"), BDArmorySetup.BDGuiSkin.button))
                {
                    VesselSpawner.Instance.ShowSpawnPoint(selected_index, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, 20);
                }
                line += 1.3f;
                if (planetBox.isOpen)
                {
                    line += 6.3f;
                }
                if (selected_index != previous_index)
                {
                    if (selected_index != -1)
                    {
                        //selectedWorld = FlightGlobals.Bodies[selected_index].name;
                        //Debug.Log("selected World Index: " + selected_index);
                        BDArmorySettings.VESSEL_SPAWN_WORLDINDEX = selected_index;
                    }
                    previous_index = selected_index;
                }
                if (selected_index == -1)
                {
                    selected_index = 1;
                    previous_index = 1;
                }
                ////////////////////
                int i = 0;
                foreach (var spawnLocation in VesselSpawner.spawnLocations)
                {
                    if (spawnLocation.worldIndex != selected_index) continue;
                    if (GUI.Button(SQuarterRect(line, i++), spawnLocation.name, BDArmorySetup.BDGuiSkin.button))
                    {
                        switch (Event.current.button)
                        {
                            case 1: // right click
                                VesselSpawner.spawnLocations.Remove(spawnLocation);
                                break;
                            default:
                                BDArmorySettings.VESSEL_SPAWN_GEOCOORDS = spawnLocation.location;
                                BDArmorySettings.VESSEL_SPAWN_WORLDINDEX = spawnLocation.worldIndex;
                                spawnFields["lat"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x;
                                spawnFields["lon"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y;
                                VesselSpawner.Instance.ShowSpawnPoint(selected_index, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, 20);
                                break;
                        }
                    }
                }
                line += (int)((i - 1) / 4);
                line += 0.3f;
            }

            if (GUI.Button(SLineRect(++line), (BDArmorySettings.SHOW_TOURNAMENT_OPTIONS ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_TournamentOptions"), BDArmorySettings.SHOW_TOURNAMENT_OPTIONS ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Show/hide tournament options
            {
                BDArmorySettings.SHOW_TOURNAMENT_OPTIONS = !BDArmorySettings.SHOW_TOURNAMENT_OPTIONS;
            }
            if (BDArmorySettings.SHOW_TOURNAMENT_OPTIONS)
            {
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentDelayBetweenHeats")}: ({BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS}s)", leftLabel); // Delay between heats
                BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS, 0f, 15f));

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentTimeWarpBetweenRounds")}: ({(BDArmorySettings.TOURNAMENT_TIMEWARP_BETWEEN_ROUNDS > 0 ? BDArmorySettings.TOURNAMENT_TIMEWARP_BETWEEN_ROUNDS + "min" : "Off")})", leftLabel); // TimeWarp Between Rounds
                BDArmorySettings.TOURNAMENT_TIMEWARP_BETWEEN_ROUNDS = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TOURNAMENT_TIMEWARP_BETWEEN_ROUNDS / 5f, 0f, 72f)) * 5;

                switch (BDArmorySettings.TOURNAMENT_STYLE)
                {
                    case 0:
                        tournamentStyle = "RNG";
                        break;
                    case 1:
                        tournamentStyle = "N-choose-K";
                        break;
                }
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentStyle")}: ({tournamentStyle})", leftLabel); // Tournament Style
                BDArmorySettings.TOURNAMENT_STYLE = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TOURNAMENT_STYLE, 0f, 1f));

                var value = BDArmorySettings.TOURNAMENT_ROUNDS <= 20 ? BDArmorySettings.TOURNAMENT_ROUNDS : BDArmorySettings.TOURNAMENT_ROUNDS <= 100 ? (16 + BDArmorySettings.TOURNAMENT_ROUNDS / 5) : 37;
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentRounds")}:  ({BDArmorySettings.TOURNAMENT_ROUNDS})", leftLabel); // Rounds
                value = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), value, 1f, 37f));
                BDArmorySettings.TOURNAMENT_ROUNDS = value <= 20 ? value : value <= 36 ? (value - 16) * 5 : BDArmorySettings.TOURNAMENT_ROUNDS_CUSTOM;

                if (BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS == 0) // FFA
                {
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentVesselsPerHeat")}:  ({(BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT > 1 ? BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT.ToString() : (BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT == 0 ? "Auto" : "Inf"))})", leftLabel); // Vessels Per Heat
                    BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT, 0f, 20f));
                }
                else // Teams
                {
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentTeamsPerHeat")}:  ({(BDArmorySettings.TOURNAMENT_TEAMS_PER_HEAT > 1 ? BDArmorySettings.TOURNAMENT_TEAMS_PER_HEAT.ToString() : (BDArmorySettings.TOURNAMENT_TEAMS_PER_HEAT == 0 ? "Auto" : "Inf"))})", leftLabel); // Teams Per Heat
                    BDArmorySettings.TOURNAMENT_TEAMS_PER_HEAT = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TOURNAMENT_TEAMS_PER_HEAT, 2f, 8f));

                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TournamentVesselsPerTeam")}:  ({BDArmorySettings.TOURNAMENT_VESSELS_PER_TEAM.ToString()})", leftLabel); // Vessels Per Team
                    BDArmorySettings.TOURNAMENT_VESSELS_PER_TEAM = Mathf.RoundToInt(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TOURNAMENT_VESSELS_PER_TEAM, 1f, 8f));

                    BDArmorySettings.TOURNAMENT_FULL_TEAMS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.TOURNAMENT_FULL_TEAMS, Localizer.Format("#LOC_BDArmory_Settings_TournamentFullTeams"));  // Re-use craft to fill teams
                }

                // Tournament status
                if (BDATournament.Instance.tournamentType == TournamentType.FFA)
                {
                    GUI.Label(SLineRect(++line), $"ID: {BDATournament.Instance.tournamentID}, {BDATournament.Instance.vesselCount} vessels, {BDATournament.Instance.numberOfRounds} rounds, {BDATournament.Instance.numberOfHeats} heats per round ({BDATournament.Instance.heatsRemaining} remaining).", leftLabel);
                }
                else
                {
                    GUI.Label(SLineRect(++line), $"ID: {BDATournament.Instance.tournamentID}, {BDATournament.Instance.teamCount} teams, {BDATournament.Instance.numberOfRounds} rounds, {BDATournament.Instance.teamsPerHeat} teams per heat, {BDATournament.Instance.numberOfHeats} heats per round,", leftLabel);
                    GUI.Label(SLineRect(++line), $"{BDATournament.Instance.vesselCount} vessels,{(BDATournament.Instance.fullTeams ? "" : " up to")} {BDATournament.Instance.vesselsPerTeam} vessels per team per heat, {BDATournament.Instance.heatsRemaining} heats remaining.", leftLabel);
                }
                switch (BDATournament.Instance.tournamentStatus)
                {
                    case TournamentStatus.Running:
                    case TournamentStatus.Waiting:
                        if (GUI.Button(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_TournamentStop"), BDArmorySetup.BDGuiSkin.button)) // Stop tournament
                            BDATournament.Instance.StopTournament();
                        GUI.Label(SRightRect(line), $" Status: {BDATournament.Instance.tournamentStatus},  Round {BDATournament.Instance.currentRound},  Heat {BDATournament.Instance.currentHeat}");
                        break;

                    default:
                        if (GUI.Button(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_TournamentSetup"), BDArmorySetup.BDGuiSkin.button)) // Setup tournament
                        {
                            ParseAllSpawnFieldsNow();
                            BDATournament.Instance.SetupTournament(
                                BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION,
                                BDArmorySettings.TOURNAMENT_ROUNDS,
                                BDArmorySettings.TOURNAMENT_VESSELS_PER_HEAT,
                                BDArmorySettings.TOURNAMENT_TEAMS_PER_HEAT,
                                BDArmorySettings.TOURNAMENT_VESSELS_PER_TEAM,
                                BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS,
                                BDArmorySettings.TOURNAMENT_STYLE
                            );
                            BDArmorySetup.SaveConfig();
                        }

                        if (BDATournament.Instance.tournamentStatus != TournamentStatus.Completed)
                        {
                            if (GUI.Button(SRightRect(line), Localizer.Format("#LOC_BDArmory_Settings_TournamentRun"), BDArmorySetup.BDGuiSkin.button)) // Run tournament
                            {
                                BDATournament.Instance.RunTournament();
                                if (BDArmorySettings.VESSEL_SPAWNER_WINDOW_WIDTH < 480 && BDATournament.Instance.numberOfRounds * BDATournament.Instance.numberOfHeats > 99) // Expand the window a bit to compensate for long tournaments.
                                {
                                    Debug.Log("DEBUG widening window");
                                    BDArmorySettings.VESSEL_SPAWNER_WINDOW_WIDTH = 480;
                                }
                            }
                        }
                        break;
                }
            }

            ++line;
            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_SingleSpawn"), _vesselsSpawned ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                BDATournament.Instance.StopTournament();
                ParseAllSpawnFieldsNow();
                if (!_vesselsSpawned && !VesselSpawner.Instance.vesselsSpawningContinuously && Event.current.button == 0) // Left click
                {
                    if (BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING)
                        VesselSpawner.Instance.SpawnAllVesselsOnceContinuously(BDArmorySettings.VESSEL_SPAWN_WORLDINDEX, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE ? BDArmorySettings.VESSEL_SPAWN_DISTANCE : BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, true, BDArmorySettings.VESSEL_SPAWN_REASSIGN_TEAMS, BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS, null, null, BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION); // Spawn vessels.
                    else
                        VesselSpawner.Instance.SpawnAllVesselsOnce(BDArmorySettings.VESSEL_SPAWN_WORLDINDEX, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE ? BDArmorySettings.VESSEL_SPAWN_DISTANCE : BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, true, BDArmorySettings.VESSEL_SPAWN_REASSIGN_TEAMS, BDArmorySettings.VESSEL_SPAWN_NUMBER_OF_TEAMS, null, null, BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION); // Spawn vessels.
                    _vesselsSpawned = true;
                }
                else if (Event.current.button == 2) // Middle click, add a new spawn of vessels to the currently spawned vessels.
                {
                    VesselSpawner.Instance.SpawnAllVesselsOnce(BDArmorySettings.VESSEL_SPAWN_WORLDINDEX, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE ? BDArmorySettings.VESSEL_SPAWN_DISTANCE : BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, false, false, 0, null, null, BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION); // Spawn vessels, without killing off other vessels or changing camera positions.
                }
            }
            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_ContinuousSpawning"), VesselSpawner.Instance.vesselsSpawningContinuously ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                BDATournament.Instance.StopTournament();
                ParseAllSpawnFieldsNow();
                if (!VesselSpawner.Instance.vesselsSpawningContinuously && !_vesselsSpawned && Event.current.button == 0) // Left click
                {
                    VesselSpawner.Instance.SpawnVesselsContinuously(BDArmorySettings.VESSEL_SPAWN_WORLDINDEX, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE ? BDArmorySettings.VESSEL_SPAWN_DISTANCE : BDArmorySettings.VESSEL_SPAWN_DISTANCE_FACTOR, BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE, true, BDArmorySettings.VESSEL_SPAWN_FILES_LOCATION); // Spawn vessels continuously at 1km above terrain.
                }
            }

            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_CancelSpawning"), (_vesselsSpawned || VesselSpawner.Instance.vesselsSpawningContinuously) ? BDArmorySetup.BDGuiSkin.button : BDArmorySetup.BDGuiSkin.box))
            {
                if (_vesselsSpawned)
                    Debug.Log("[BDArmory.VesselSpawnerWindow]: Resetting spawning vessel button.");
                _vesselsSpawned = false;
                if (VesselSpawner.Instance.vesselsSpawningContinuously)
                    Debug.Log("[BDArmory.VesselSpawnerWindow]: Resetting continuous spawning button.");
                BDATournament.Instance.StopTournament();
                VesselSpawner.Instance.CancelVesselSpawn();
            }

            line += 1.25f; // Bottom internal margin
            _windowHeight = (line * _lineHeight);
        }
    }
}
