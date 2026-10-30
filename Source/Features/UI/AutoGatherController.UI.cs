using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Crafting;
using Durango.Logic.Combat;
using Durango.Logic.Item;
using Durango.UI.Popup;
using Yaml;
using L10N;
using Shared.Battle;

namespace AutoGather {
    public partial class AutoGatherController {
                private void CheckMenuToggleHotkey() {

            try {

                bool insertDown = Input.GetKeyDown(KeyCode.Insert);

                bool f11Down = Input.GetKeyDown(KeyCode.F11);

                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                bool key3Down = Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);

                bool key1Down = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);

                bool key3Held = Input.GetKey(KeyCode.Alpha3) || Input.GetKey(KeyCode.Keypad3);

                bool key1Held = Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1);

                bool keyHDown = Input.GetKeyDown(KeyCode.H);

                float now = Time.time;

                if (ctrl && key3Down) _lastCtrl3Time = now;

                if (ctrl && key1Down) _lastCtrl1Time = now;

                bool triggered = false;

                // 1. Tombol cepat 1 tombol: Insert atau F11

                if (insertDown || f11Down) {

                    triggered = true;

                }

                // 2. Shortcut alternatif: Ctrl + H

                else if (ctrl && keyHDown) {

                    triggered = true;

                }

                // 3. Chord combo: Ctrl ditahan, 3 & 1 ditekan bersamaan

                else if (ctrl && ((key3Held && key1Down) || (key1Held && key3Down))) {

                    triggered = true;

                }

                // 4. Sequential combo: Ctrl ditahan, tekan 3 lalu tekan 1 (atau sebaliknya dalam rentang 1.5 detik)

                else if (ctrl && key1Down && (now - _lastCtrl3Time <= 1.5f)) {

                    triggered = true;

                } else if (ctrl && key3Down && (now - _lastCtrl1Time <= 1.5f)) {

                    triggered = true;

                }

                if (triggered && (now - _lastToggleTriggerTime > 0.35f)) {

                    _lastToggleTriggerTime = now;

                    _lastCtrl3Time = 0f;

                    _lastCtrl1Time = 0f;

                    CycleMenuState();

                }

            } catch {}

        }

                private void HandleWindowDrag(Rect dragArea) {

            try {

                Event ev = Event.current;

                if (ev == null) return;

                // Safety jika mouse dilepas di luar game window

                if (_isDragging && !Input.GetMouseButton(0)) {

                    _isDragging = false;

                    RequestSaveSettings();

                    return;

                }

                if (ev.type == EventType.MouseDown && ev.button == 0 && dragArea.Contains(ev.mousePosition)) {

                    _isDragging = true;

                    _dragOffset = ev.mousePosition - _windowPos;

                    ev.Use();

                } else if (ev.type == EventType.MouseDrag && _isDragging) {

                    _windowPos = ev.mousePosition - _dragOffset;

                    _windowPos.x = Mathf.Clamp(_windowPos.x, 0, Mathf.Max(0, Screen.width - 200));

                    _windowPos.y = Mathf.Clamp(_windowPos.y, 0, Mathf.Max(0, Screen.height - 60));

                    ev.Use();

                } else if (ev.type == EventType.MouseUp && _isDragging) {

                    _isDragging = false;

                    RequestSaveSettings();

                    ev.Use();

                }

            } catch {}

        }

        public void CycleMenuState() {

            if (MenuState == MenuDisplayState.Hidden) {

                MenuState = MenuDisplayState.Normal;

            } else {

                MenuState = MenuDisplayState.Hidden;

            }

            RequestSaveSettings();

        }

        public void CycleToggleKey() {

            if (ToggleKey == KeyCode.F1) ToggleKey = KeyCode.F2;

            else if (ToggleKey == KeyCode.F2) ToggleKey = KeyCode.F4;

            else if (ToggleKey == KeyCode.F4) ToggleKey = KeyCode.F7;

            else if (ToggleKey == KeyCode.F7) ToggleKey = KeyCode.F8;

            else if (ToggleKey == KeyCode.F8) ToggleKey = KeyCode.F9;

            else if (ToggleKey == KeyCode.F9) ToggleKey = KeyCode.F12;

            else if (ToggleKey == KeyCode.F12) ToggleKey = KeyCode.BackQuote;

            else if (ToggleKey == KeyCode.BackQuote) ToggleKey = KeyCode.G;

            else ToggleKey = KeyCode.F1;

            if (!IsEnabled) {

                _statusText = string.Format("Standby (Tekan [{0}] untuk Aktifkan)", ToggleKey);

            }

            RequestSaveSettings();

        }

        // ================= HELPER SMART LEARN & AUTO-GATHER =================


        void OnGUI() {

            try {

                InitGUIStyles();

                RenderCounterAssistOverlay();

                float oldScan = ScanRadiusMeters;

                ResourceFilterMode oldFilter = CurrentFilter;

                GatherTargetMode oldTargetMode = TargetMode;

                bool oldRecCut = EnableRecoveryCut;

                float oldRecDelay = RecoveryCutDelay;

                bool oldStagger = EnableAntiStagger;

                bool oldMob = EnableMobileAttack;

                bool oldSniper = EnableFastSniperStance;

                float oldSniperSpeed = FastSniperSpeedMultiplier;

                bool oldCrossbowReload = EnableFastCrossbowReload;

                float oldCrossbowSpeed = CrossbowReloadSpeed;

                bool oldNoclip = EnableDinoNoclip;

                bool oldAutoExit = AutoExitCombatOnKill;
                bool oldCounterAssist = EnableCounterAssist;
                float oldCounterMax = CounterAssistWindowMax;
                bool oldCounterSound = EnableCounterAssistSound;
                bool oldCounterHUD = EnableCounterAssistCenterHUD;
                bool oldCounterTargetOnly = CounterAssistTargetOnly;
                bool oldCounterUnderChar = CounterAssistUnderCharacter;

                bool oldNoclipCombat = DinoNoclipOnlyInCombat;

                bool oldStealth = EnableStealthAntiAggro;

                float oldMoveSpeed = MoveSpeedMultiplier;

                bool oldLockRun = LockDefaultRunAnim;

                float oldMountSpeed = MountSpeedMultiplier;

                int oldPlanQty = PlanQuantity;

                bool oldAutoEat = EnableAutoEatFood;

                bool oldHoldAttack = EnableDPSMaximizer;

                float oldHoldInterval = HoldAttackInterval;

                bool oldArchi = EnableArchitectureCheat;

                if (_boxStyle == null) return;

                                // 1. STATE: HIDDEN - Hilang sepenuhnya dari layar (zero GUI rendered)

                if (MenuState == MenuDisplayState.Hidden) {

                    return;

                }

                // Safety: Pastikan koordinat jendela berada di dalam batas layar

                if (_windowPos.x < 0 || _windowPos.x > Screen.width - 150) _windowPos.x = 20;

                if (_windowPos.y < 0 || _windowPos.y > Screen.height - 60) _windowPos.y = 85;

                // 2. STATE: MINIMIZED - Mini Bar yang bisa digeser bebas

                if (MenuState == MenuDisplayState.Minimized) {

                    Rect miniDragArea = new Rect(_windowPos.x, _windowPos.y, 390 - 65, 30);

                    HandleWindowDrag(miniDragArea);

                    Rect miniRect = new Rect(_windowPos.x, _windowPos.y, 390, 56);

                    GUI.Box(miniRect, GUIContent.none, _boxStyle);

                    GUILayout.BeginArea(miniRect);

                    GUILayout.Space(2);

                    GUILayout.BeginHorizontal();

                    string statusColor = IsEnabled ? "#2ecc71" : (IsCombatBotEnabled ? "#3498db" : "#e74c3c");

                    string statusName = IsEnabled ? "GATHER [ON]" : (IsCombatBotEnabled ? "COMBAT [ON]" : "OFF");

                    KeyCode activeKey = IsCombatBotEnabled ? CombatToggleKey : ToggleKey;

                    GUILayout.Label(string.Format("<b>[XD LANDS]</b> <color={0}>[{1}]</color> ({2})", statusColor, statusName, activeKey), _labelStyle ?? GUI.skin.label, GUILayout.Width(170));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("□", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {

                        MenuState = MenuDisplayState.Normal;

                        RequestSaveSettings();

                    }

                    if (GUILayout.Button("X", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {

                        MenuState = MenuDisplayState.Hidden;

                        RequestSaveSettings();

                    }

                    GUILayout.EndHorizontal();

                    string miniDetail = "";

                    if (IsCombatBotEnabled) {

                        string tName = (_currentCombatTarget != null && _currentCombatTarget.IsAlive) ? _currentCombatTarget.GetName() : "Mencari...";

                        miniDetail = string.Format("Combat: <b>{0}</b> | <size=10>{1}</size>", tName, _combatStatusText);

                    } else if (IsCraftPlanActive && PlanRecipe != null) {

                        string hName = (_currentHuntingSlot != null) 

                            ? (!string.IsNullOrEmpty(_currentHuntingSlot.ParentName) ? (_currentHuntingSlot.Name + " (" + _currentHuntingSlot.ParentName + ")") : _currentHuntingSlot.Name) 

                            : "Lengkap";

                        miniDetail = string.Format("Plan: <b>{0} x{1}</b> | Buru: <color=#f1c40f>{2}</color>", PlanRecipe.Name, PlanQuantity, hName);

                    } else {

                        miniDetail = string.Format("Target: <b>{0}</b> ({1:F1}m) | <size=10>{2}</size>", _targetName, _targetDistance / 100f, _statusText);

                    }

                    GUILayout.Label("  " + miniDetail, _labelStyle ?? GUI.skin.label);

                    GUILayout.EndArea();

                    return;

                }

                // 3. STATE: NORMAL - Jendela Tab Penuh yang bisa digeser bebas di mana saja seperti Tab Windows

                Rect dragHeaderArea = new Rect(_windowPos.x, _windowPos.y, 430 - 125, 32);

                HandleWindowDrag(dragHeaderArea);

                Rect panelRect = new Rect(_windowPos.x, _windowPos.y, 430, 495);

                GUI.Box(panelRect, GUIContent.none, _boxStyle);

                GUILayout.BeginArea(panelRect);

                GUILayout.Space(6);

                                // Header bar (klik & geser untuk memposisikan jendela di mana saja)

                GUILayout.BeginHorizontal();

                if (_headerStyle != null) GUILayout.Label("  [XD LANDS] MULTI-BOT SYSTEM", _headerStyle);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("-", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {

                    MenuState = MenuDisplayState.Minimized;

                    RequestSaveSettings();

                }

                if (GUILayout.Button("X", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {

                    MenuState = MenuDisplayState.Hidden;

                    RequestSaveSettings();

                }

                GUILayout.EndHorizontal();

                GUILayout.Space(2);

                // Status Bar Utama

                string statusColorMain = IsEnabled ? "#2ecc71" : (IsCombatBotEnabled ? "#3498db" : "#e74c3c");

                string statusNameMain = IsEnabled ? "GATHER [ON]" : (IsCombatBotEnabled ? "COMBAT [ON]" : "NONAKTIF [OFF]");

                if (_labelStyle != null) {

                                        if (ActiveTab == 2) {
                        GUILayout.Label("  Status: <b>FITUR TEMPUR & KECEPATAN GERAK</b> | Menu: [Insert] / [Ctrl+H] / [-] / [X]", _labelStyle);
                        GUILayout.Label("  Info: <size=11><color=#2ecc71>Aktif otomatis sesuai tombol toggle di bawah</color></size>", _labelStyle);
                    } else {
                        GUILayout.Label(string.Format("  Status: <color={0}><b>{1}</b></color> (Gather: [{2}] | Menu: [Ctrl+3+1] / [-] / [X])", statusColorMain, statusNameMain, ToggleKey), _labelStyle);

                        if (IsCraftPlanActive && PlanRecipe != null) {
                            string huntInfo = (_currentHuntingSlot != null) 
                                ? ("Berburu: " + (!string.IsNullOrEmpty(_currentHuntingSlot.ParentName) ? (_currentHuntingSlot.Name + " (" + _currentHuntingSlot.ParentName + ")") : _currentHuntingSlot.Name)) 
                                : "Bahan Lengkap";
                            GUILayout.Label(string.Format("  Plan: <color=#f1c40f><b>{0} x{1}</b></color> ({2})", PlanRecipe.Name, PlanQuantity, huntInfo), _labelStyle);
                        } else {
                            GUILayout.Label(string.Format("  Target: <b>{0}</b>", _targetName), _labelStyle);
                        }

                        if (IsEnabled && _currentTargetObj != null) {
                            GUILayout.Label(string.Format("  Jarak: <b>{0:F1} m</b> | Node: <color=#f1c40f>{1}</color>", _targetDistance / 100f, _targetAvailableResources), _labelStyle);
                        }

                        GUILayout.Label(string.Format("  Info: <size=11>{0}</size>", _statusText), _labelStyle);
                    }
                }

                GUILayout.Space(4);

                // TAB SELECTOR: [Panen Manual] | [Craft Planner] | [Auto Combat]
                GUILayout.BeginHorizontal();

                DrawTabButton("Panen", 0, 130);

                string planTabLabel = IsCraftPlanActive ? "Craft [ON]" : "Craft Plan";

                DrawTabButton(planTabLabel, 1, 145);

                DrawTabButton("Tempur", 2, 130);

                GUILayout.EndHorizontal();

                GUILayout.Space(6);

                if (ActiveTab == 0) {

                    // ================= TAB 0: SMART AUTO-PANEN =================

                    if (_subHeaderStyle != null) GUILayout.Label("  Target Pembelajaran Otomatis (Smart Learn):", _subHeaderStyle);

                    

                    if (_hasLearnedTarget) {
                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label(string.Format("  Node Terkunci: <b>{0}</b> <color=#7f8c8d>({1})</color>", _learnedNodeDisplayName, _learnedNodeBaseName), _labelStyle ?? GUI.skin.label);
                        if (!string.IsNullOrEmpty(_learnedNodePoolName)) {
                            GUILayout.Label(string.Format("  Prefab Pool: <color=#95a5a6>{0}</color>", _learnedNodePoolName), _labelStyle ?? GUI.skin.label);
                        }
                        GUILayout.Label(string.Format("  Kategori Node: <color=#3498db><b>{0}</b></color>", _learnedFilter), _labelStyle ?? GUI.skin.label);
                        
                        GUILayout.Space(4);
                        GUILayout.Label("  Target Item Panen (Urutan Prioritas):", _subHeaderStyle ?? GUI.skin.label);

                        int itemToRemove = -1;
                        if (_learnedTargetItems != null && _learnedTargetItems.Count > 0) {
                            for (int i = 0; i < _learnedTargetItems.Count; i++) {
                                LearnedTargetItem item = _learnedTargetItems[i];
                                if (item == null) continue;
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(string.Format("    {0}. <color=#2ecc71><b>{1}</b></color> <color=#95a5a6>({2})</color>", (i + 1), item.ActionName, item.GeneratorId), _labelStyle ?? GUI.skin.label);
                                if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(18))) {
                                    itemToRemove = i;
                                }
                                GUILayout.EndHorizontal();
                            }
                        } else if (!string.IsNullOrEmpty(_learnedActionName)) {
                            GUILayout.Label(string.Format("    1. <color=#2ecc71><b>{0}</b></color>", _learnedActionName), _labelStyle ?? GUI.skin.label);
                        }

                        if (itemToRemove >= 0 && _learnedTargetItems != null && itemToRemove < _learnedTargetItems.Count) {
                            _learnedTargetItems.RemoveAt(itemToRemove);
                            List<string> actNames = new List<string>();
                            for (int k = 0; k < _learnedTargetItems.Count; k++) {
                                if (_learnedTargetItems[k] != null && !string.IsNullOrEmpty(_learnedTargetItems[k].ActionName)) {
                                    actNames.Add(_learnedTargetItems[k].ActionName);
                                }
                            }
                            _learnedActionName = string.Join(", ", actNames.ToArray());
                            if (_learnedTargetItems.Count == 0) {
                                ResetLearnedTarget();
                            } else {
                                RequestSaveSettings();
                            }
                        }

                        GUILayout.EndVertical();

                        GUILayout.Space(2);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Deteksi Target Baru / Reset", _selectedBtnStyle ?? GUI.skin.button, GUILayout.Height(24), GUILayout.Width(220))) {
                            ResetLearnedTarget();
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Label("  <color=#bdc3c7><i>* Tips: Anda bisa memanen lebih dari 1 item dari pohon yang sama untuk menambah target sekaligus!</i></color>", _hintStyle ?? GUI.skin.label);
                    } else {
                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label("  <color=#f1c40f><b>[Menunggu Anda Memanen 1x di Game...]</b></color>", _labelStyle ?? GUI.skin.label);
                        GUILayout.Label("  • Dekati pohon, batu, bambu, semak, atau tanaman apa pun,", _hintStyle ?? GUI.skin.label);
                        GUILayout.Label("  • Klik panen manual pada item yang Anda inginkan (misal: Ranting),", _hintStyle ?? GUI.skin.label);
                        GUILayout.Label("  • Klik item ke-2 pada pohon yang sama jika ingin ambil item tambahan (misal: Daun),", _hintStyle ?? GUI.skin.label);
                        GUILayout.Label("  • Bot akan otomatis mengunci node & item-item tersebut secara dinamis!", _hintStyle ?? GUI.skin.label);
                        GUILayout.EndVertical();
                    }

                    GUILayout.Space(4);

                    // Kontrol Radius Scan

                    GUILayout.BeginHorizontal();

                    if (_labelStyle != null) GUILayout.Label(string.Format("  Radius Scan: <b>{0:F0}m</b>", ScanRadiusMeters), _labelStyle, GUILayout.Width(130));

                    if (_smallBtnStyle != null) {

                        if (GUILayout.Button("- 10m", _smallBtnStyle, GUILayout.Width(46), GUILayout.Height(20))) {

                            ScanRadiusMeters = Mathf.Max(10f, ScanRadiusMeters - 10f);

                            _currentTargetObj = null;

                        }

                        if (GUILayout.Button("+ 10m", _smallBtnStyle, GUILayout.Width(46), GUILayout.Height(20))) {

                            ScanRadiusMeters = Mathf.Min(250f, ScanRadiusMeters + 10f);

                            _currentTargetObj = null;

                        }

                        if (GUILayout.Button("30m", _smallBtnStyle, GUILayout.Width(38), GUILayout.Height(20))) {

                            ScanRadiusMeters = 30f;

                            _currentTargetObj = null;

                        }

                        if (GUILayout.Button("50m", _smallBtnStyle, GUILayout.Width(38), GUILayout.Height(20))) {

                            ScanRadiusMeters = 50f;

                            _currentTargetObj = null;

                        }

                        if (GUILayout.Button("100m", _smallBtnStyle, GUILayout.Width(44), GUILayout.Height(20))) {

                            ScanRadiusMeters = 100f;

                            _currentTargetObj = null;

                        }

                                            if (GUILayout.Button("200m", _smallBtnStyle, GUILayout.Width(44), GUILayout.Height(20))) {

                        ScanRadiusMeters = 200f;

                        _currentTargetObj = null;

                    }

                }

                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("  <b>🌊 & 🌋 Auto Loot Air & Lahar (Looping Otomatis):</b>", _subHeaderStyle ?? GUI.skin.label);

                // BARIS 1: LAHAR (LAVA)
                GUILayout.BeginHorizontal();
                int emptyLava = CountEmptyLavaContainers();
                string lavaLoopBtnText = IsLavaLoopActive ? "⏹ Hentikan Loop Lahar" : string.Format("🌋 Mulai Loop Lahar ({0} Ember)", emptyLava);
                GUIStyle lavaStyle = IsLavaLoopActive ? (_selectedBtnStyle ?? GUI.skin.button) : (_btnStyle ?? GUI.skin.button);

                if (GUILayout.Button(lavaLoopBtnText, lavaStyle, GUILayout.Height(26))) {
                    if (IsLavaLoopActive) StopLavaLoop();
                    else StartLavaLoop();
                }

                if (GUILayout.Button("Ambil 1x", _btnStyle ?? GUI.skin.button, GUILayout.Height(26), GUILayout.Width(75))) {
                    DrawLavaFromShore();
                }
                GUILayout.EndHorizontal();

                // BARIS 2: AIR (WATER)
                GUILayout.BeginHorizontal();
                int emptyWater = CountEmptyWaterContainers();
                string waterLoopBtnText = IsWaterLoopActive ? "⏹ Hentikan Loop Air" : string.Format("🌊 Mulai Loop Air ({0} Wadah)", emptyWater);
                GUIStyle waterStyle = IsWaterLoopActive ? (_selectedBtnStyle ?? GUI.skin.button) : (_btnStyle ?? GUI.skin.button);

                if (GUILayout.Button(waterLoopBtnText, waterStyle, GUILayout.Height(26))) {
                    if (IsWaterLoopActive) StopWaterLoop();
                    else StartWaterLoop();
                }

                if (GUILayout.Button("Ambil 1x", _btnStyle ?? GUI.skin.button, GUILayout.Height(26), GUILayout.Width(75))) {
                    DrawWaterFromShore();
                }
                GUILayout.EndHorizontal();

                // STATUS / HINT
                if (IsLavaLoopActive) {
                    GUILayout.Label(string.Format("  <color=#f39c12><b>[LAVA LOOP BERJALAN]</b></color> Terisi: {0} ember | Sisa kosong: {1}", _lavaScoopSuccessCount, emptyLava), _labelStyle ?? GUI.skin.label);
                } else if (IsWaterLoopActive) {
                    GUILayout.Label(string.Format("  <color=#3498db><b>[AIR LOOP BERJALAN]</b></color> Terisi: {0} wadah | Sisa kosong: {1}", _waterScoopSuccessCount, emptyWater), _labelStyle ?? GUI.skin.label);
                } else {
                    GUILayout.Label("  <size=10><color=#95a5a6>• Berdiri di tepi lahar / air, lalu klik 'Mulai Loop'. Bot akan otomatis menyendok ke semua wadah kosong di tas sampai penuh atau bergerak menjauh.</color></size>", _hintStyle ?? GUI.skin.label);
                }
                GUILayout.EndVertical();

                                GUILayout.Space(4);

                GUILayout.Space(5);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("  <b>Sistem Navigasi & Keamanan Jelajah Map:</b>", _subHeaderStyle ?? GUI.skin.label);

                // Baris 1: Toggle Jelajah Map & Hindari Lahar
                GUILayout.BeginHorizontal();
                bool newSweep = GUILayout.Toggle(EnableIslandSweep, " Jelajah Keliling 1 Map", GUILayout.Width(200));
                if (newSweep != EnableIslandSweep) { EnableIslandSweep = newSweep; RequestSaveSettings(); }

                bool newLava = GUILayout.Toggle(EnableLavaAvoidance, " Hindari Ubin Lahar / Lava", GUILayout.Width(200));
                if (newLava != EnableLavaAvoidance) { EnableLavaAvoidance = newLava; RequestSaveSettings(); }
                GUILayout.EndHorizontal();

                // Baris 2: Toggle Detour Gunung & Kabur Dino
                GUILayout.BeginHorizontal();
                bool newObst = GUILayout.Toggle(EnableObstacleDetour, " Putari Tebing / Gunung", GUILayout.Width(200));
                if (newObst != EnableObstacleDetour) { EnableObstacleDetour = newObst; RequestSaveSettings(); }

                bool newFlee = GUILayout.Toggle(EnableDinoFlee, " Kabur dari Kejaran Dino", GUILayout.Width(200));
                if (newFlee != EnableDinoFlee) { EnableDinoFlee = newFlee; RequestSaveSettings(); }
                GUILayout.EndHorizontal();

                // Baris 3: Durasi Blacklist Node
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("  Cooldown Node Habis: <b>{0:F0} Menit</b>", NodeBlacklistDuration / 60f), _labelStyle ?? GUI.skin.label, GUILayout.Width(210));
                if (GUILayout.Button("5 Menit", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(60), GUILayout.Height(20))) {
                    NodeBlacklistDuration = 300f; RequestSaveSettings();
                }
                if (GUILayout.Button("8 Menit", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(60), GUILayout.Height(20))) {
                    NodeBlacklistDuration = 480f; RequestSaveSettings();
                }
                if (GUILayout.Button("12 Menit", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(65), GUILayout.Height(20))) {
                    NodeBlacklistDuration = 720f; RequestSaveSettings();
                }
                GUILayout.EndHorizontal();

                GUILayout.Label("  <size=10><color=#95a5a6>* Bot akan menyusuri pulau, memutari tebing terjal, tidak menyentuh lahar, dan otomatis lari menjauhi wilayah dino buas jika diincar!</color></size>", _hintStyle ?? GUI.skin.label);
                GUILayout.EndVertical();

                GUILayout.Space(4);

                GUILayout.Label("  <color=#2ecc71><b>✓ Pengaturan Otomatis Disimpan (Auto-Save)</b></color>", _hintStyle ?? GUI.skin.label);

            } else if (ActiveTab == 1) {

                    // ================= TAB 1: CRAFT PLANNER =================

                    // 1. DETAIL TARGET RESEP & JUMLAH

                    GUILayout.BeginHorizontal();

                    string recName = (PlanRecipe != null) ? PlanRecipe.Name : "<color=#e74c3c>Belum Dipilih</color>";

                    if (_labelStyle != null) GUILayout.Label(string.Format("  Target Resep: <b>{0}</b>", recName), _labelStyle, GUILayout.Width(220));

                    if (_smallBtnStyle != null) {

                        if (GUILayout.Button("-", _smallBtnStyle, GUILayout.Width(24), GUILayout.Height(20))) {

                            PlanQuantity = Mathf.Max(1, PlanQuantity - 1);

                            UpdatePlanRequirements();

                        }

                        GUILayout.Label(string.Format("<b>x{0}</b>", PlanQuantity), _labelStyle ?? GUI.skin.label, GUILayout.Width(30));

                        if (GUILayout.Button("+", _smallBtnStyle, GUILayout.Width(24), GUILayout.Height(20))) {

                            PlanQuantity = Mathf.Min(99, PlanQuantity + 1);

                            UpdatePlanRequirements();

                        }

                        if (GUILayout.Button("+5", _smallBtnStyle, GUILayout.Width(30), GUILayout.Height(20))) {

                            PlanQuantity = Mathf.Min(99, PlanQuantity + 5);

                            UpdatePlanRequirements();

                        }

                    }

                    GUILayout.EndHorizontal();

                    // Status Bahan Lengkap / Kurang

                    if (_labelStyle != null) {

                        GUILayout.Label("  Status: " + _planStatusSummary, _labelStyle);

                    }

                    // 2. CHECKLIST MATERIAL SLOTS (HIERARKI & REALTIME)

                    if (_planSlotInfos.Count > 0) {

                        GUILayout.BeginHorizontal();

                        if (_subHeaderStyle != null) GUILayout.Label("  Checklist Bahan & Hierarki:", _subHeaderStyle);

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Perbarui", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(65), GUILayout.Height(18))) {

                            UpdatePlanRequirements();

                        }

                        GUILayout.EndHorizontal();

                        _materialScrollPos = GUILayout.BeginScrollView(_materialScrollPos, GUILayout.Height(135));

                        for (int i = 0; i < _planSlotInfos.Count; i++) {

                            SlotRequirementInfo slot = _planSlotInfos[i];

                            if (slot == null) continue;

                            bool isReady = (slot.Missing == 0);

                            bool isHunting = (IsCraftPlanActive && _currentHuntingSlot == slot);

                            string lvlTag = (slot.RequiredLevel > 0) ? string.Format(" <color=#f39c12>[Lv.{0}+]</color>", slot.RequiredLevel) : "";

                            string huntingTag = isHunting ? " <color=#f1c40f><- [SEDANG DICARI]</color>" : "";

                            if (isReady) {

                                // Bahan utama sudah ada di inventory!

                                string line = string.Format("    <color=#2ecc71>[✓] <b>{0}</b>: {1}/{2} (Lengkap)</color>{3}", 

                                    slot.Name, slot.Available, slot.Required, lvlTag);

                                GUILayout.Label(line, _labelStyle ?? GUI.skin.label);

                            } else {

                                // Bahan utama masih kurang

                                if (slot.IsCraftable && slot.SubRecipe != null) {

                                    bool subReady = AreSubMaterialsReady(slot);

                                    GUILayout.BeginHorizontal();

                                    string craftIcon = subReady ? "<color=#2ecc71>[⚙]</color>" : "<color=#e67e22>[✗]</color>";

                                    string craftStatus = subReady ? "<color=#2ecc71>(Bahan Lengkap! Siap Craft)</color>" : "<color=#e67e22>(Perlu Dibuat)</color>";

                                    string parentLine = string.Format("    {0} <b>{1}</b>: {2}/{3} {4}{5}", craftIcon, slot.Name, slot.Available, slot.Required, craftStatus, lvlTag);

                                    GUILayout.Label(parentLine, _labelStyle ?? GUI.skin.label);

                                    

                                    if (subReady && _smallBtnStyle != null) {

                                        string subName = !string.IsNullOrEmpty(slot.SubRecipe.Name) ? slot.SubRecipe.Name : slot.Name;

                                        if (GUILayout.Button(string.Format("Craft {0}", subName), _selectedBtnStyle ?? _smallBtnStyle, GUILayout.Height(18))) {

                                            OpenCraftUIInGame(slot.SubRecipe.Id);

                                        }

                                    }

                                    GUILayout.EndHorizontal();

                                    // Info Bahan & Atribut untuk sub-recipe utama ini jika ada

                                    if (!string.IsNullOrEmpty(slot.MaterialName) || !string.IsNullOrEmpty(slot.AttributeNames)) {

                                        string matPart = !string.IsNullOrEmpty(slot.MaterialName) ? ("Bahan: <b>" + slot.MaterialName + "</b>") : "";

                                        string tagPart = !string.IsNullOrEmpty(slot.AttributeNames) ? ("Atribut: <b>" + slot.AttributeNames + "</b>") : "";

                                        string joiner = (!string.IsNullOrEmpty(matPart) && !string.IsNullOrEmpty(tagPart)) ? " | " : "";

                                        GUILayout.Label(string.Format("        <color=#bdc3c7><size=10>├ {0}{1}{2}</size></color>", matPart, joiner, tagPart), _labelStyle ?? GUI.skin.label);

                                    }

                                    if (!string.IsNullOrEmpty(slot.HowToObtain)) {

                                        string[] obLines = slot.HowToObtain.Split('\n');

                                        for (int l = 0; l < obLines.Length; l++) {

                                            if (!string.IsNullOrEmpty(obLines[l])) {

                                                GUILayout.Label(string.Format("        <color=#5dade2><size=10>└ {0}</size></color>", obLines[l].Trim()), _labelStyle ?? GUI.skin.label);

                                            }

                                        }

                                    }

                                    // Render sub-materials (hierarki anak)

                                    if (slot.SubMaterials != null) {

                                        for (int s = 0; s < slot.SubMaterials.Count; s++) {

                                            SlotRequirementInfo sub = slot.SubMaterials[s];

                                            if (sub == null) continue;

                                            bool subItemReady = (sub.Missing == 0);

                                            bool isSubHunting = (IsCraftPlanActive && _currentHuntingSlot == sub);

                                            string subCol = subItemReady ? "#2ecc71" : "#f39c12";

                                            string subMark = subItemReady ? "[✓]" : "[ ]";

                                            string subHuntTag = isSubHunting ? " <color=#f1c40f><- [TERDEKAT]</color>" : "";

                                            string subLvlTag = (sub.RequiredLevel > 0) ? string.Format(" <color=#f39c12>[Lv.{0}+]</color>", sub.RequiredLevel) : "";

                                            string subLine = string.Format("        └ <color={0}>{1} <b>{2}</b>: {3}/{4}</color>{5}{6}", 

                                                subCol, subMark, sub.Name, sub.Available, sub.Required, subLvlTag, subHuntTag);

                                            GUILayout.Label(subLine, _labelStyle ?? GUI.skin.label);

                                            if (!subItemReady) {

                                                if (!string.IsNullOrEmpty(sub.MaterialName) || !string.IsNullOrEmpty(sub.AttributeNames)) {

                                                    string sMatPart = !string.IsNullOrEmpty(sub.MaterialName) ? ("Bahan: <b>" + sub.MaterialName + "</b>") : "";

                                                    string sTagPart = !string.IsNullOrEmpty(sub.AttributeNames) ? ("Atribut: <b>" + sub.AttributeNames + "</b>") : "";

                                                    string sJoiner = (!string.IsNullOrEmpty(sMatPart) && !string.IsNullOrEmpty(sTagPart)) ? " | " : "";

                                                    GUILayout.Label(string.Format("            <color=#bdc3c7><size=10>├ {0}{1}{2}</size></color>", sMatPart, sJoiner, sTagPart), _labelStyle ?? GUI.skin.label);

                                                }

                                                if (!string.IsNullOrEmpty(sub.HowToObtain)) {

                                                    string[] sObLines = sub.HowToObtain.Split('\n');

                                                    for (int l = 0; l < sObLines.Length; l++) {

                                                        if (!string.IsNullOrEmpty(sObLines[l])) {

                                                            GUILayout.Label(string.Format("            <color=#5dade2><size=10>└ {0}</size></color>", sObLines[l].Trim()), _labelStyle ?? GUI.skin.label);

                                                        }

                                                    }

                                                }

                                            }

                                        }

                                    }

                                } else {

                                    // Bahan mentah langsung (tanpa sub-resep)

                                    string line = string.Format("    <color=#e67e22>[✗] <b>{0}</b>: {1}/{2}</color>{3}{4}", 

                                        slot.Name, slot.Available, slot.Required, lvlTag, huntingTag);

                                    GUILayout.Label(line, _labelStyle ?? GUI.skin.label);

                                    if (!string.IsNullOrEmpty(slot.MaterialName) || !string.IsNullOrEmpty(slot.AttributeNames)) {

                                        string matPart = !string.IsNullOrEmpty(slot.MaterialName) ? ("Bahan: <b>" + slot.MaterialName + "</b>") : "";

                                        string tagPart = !string.IsNullOrEmpty(slot.AttributeNames) ? ("Atribut: <b>" + slot.AttributeNames + "</b>") : "";

                                        string joiner = (!string.IsNullOrEmpty(matPart) && !string.IsNullOrEmpty(tagPart)) ? " | " : "";

                                        GUILayout.Label(string.Format("        <color=#bdc3c7><size=10>├ {0}{1}{2}</size></color>", matPart, joiner, tagPart), _labelStyle ?? GUI.skin.label);

                                    }

                                    if (!string.IsNullOrEmpty(slot.HowToObtain)) {

                                        string[] obLines = slot.HowToObtain.Split('\n');

                                        for (int l = 0; l < obLines.Length; l++) {

                                            if (!string.IsNullOrEmpty(obLines[l])) {

                                                GUILayout.Label(string.Format("        <color=#5dade2><size=10>└ {0}</size></color>", obLines[l].Trim()), _labelStyle ?? GUI.skin.label);

                                            }

                                        }

                                    }

                                }

                            }

                        }

                        GUILayout.EndScrollView();

                    } else {

                        GUILayout.Space(2);

                        if (_hintStyle != null) GUILayout.Label("  Silakan pilih resep dari daftar atau klik [Ambil Dari Game]", _hintStyle);

                    }

                    GUILayout.Space(3);

                    // 3. PANDUAN & BUKA CRAFT IN-GAME

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Buka Craft In-Game", _btnStyle ?? GUI.skin.button, GUILayout.Width(250), GUILayout.Height(22))) {

                        if (PlanRecipe != null) OpenCraftUIInGame(PlanRecipeId);

                        else SyncRecipeFromGame();

                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Reset Plan", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(80), GUILayout.Height(22))) {

                        PlanRecipe = null;

                        PlanRecipeId = "";

                        _planSlotInfos.Clear();

                        _searchResults.Clear();

                        _planStatusSummary = "Pilih resep untuk memulai checklist";

                    }

                    GUILayout.EndHorizontal();

                    if (_hintStyle != null) {

                        GUILayout.Label("  <color=#95a5a6><i>*Untuk mengumpulkan bahan di atas: Buka tab [Auto Panen], nyalakan bot, lalu panen manual 1 bahan di game!</i></color>", _hintStyle);

                    }

                    GUILayout.Space(4);

                    // 4. PRESET RESEP POPULER (SURVIVAL DASAR)

                    if (_subHeaderStyle != null) GUILayout.Label("  Preset Resep Populer:", _subHeaderStyle);

                    GUILayout.BeginHorizontal();

                    DrawPresetButton("Perbaikan", 64);

                    DrawPresetButton("Paku", 42);

                    DrawPresetButton("Batu Tajam", 64);

                    DrawPresetButton("Kapak", 46);

                    DrawPresetButton("Pisau", 44);

                    DrawPresetButton("Tali", 38);

                    DrawPresetButton("Api", 34);

                    DrawPresetButton("Tombak", 50);

                    GUILayout.EndHorizontal();

                    GUILayout.Space(3);

                    // 5. CARI RESEP & DETEKSI DARI IN-GAME

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Ambil Dari Game", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(110), GUILayout.Height(20))) {

                        SyncRecipeFromGame();

                    }

                    RecipeSearchKeyword = GUILayout.TextField(RecipeSearchKeyword, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(180), GUILayout.Height(20));

                    if (GUILayout.Button("Cari", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(45), GUILayout.Height(20))) {

                        SearchRecipes(RecipeSearchKeyword);

                    }

                    if (GUILayout.Button("X", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(20))) {

                        RecipeSearchKeyword = "";

                        _searchResults.Clear();

                    }

                    GUILayout.EndHorizontal();

                    // Hasil Pencarian Resep

                    if (_searchResults.Count > 0) {

                        _searchScrollPos = GUILayout.BeginScrollView(_searchScrollPos, GUILayout.Height(65));

                        for (int i = 0; i < _searchResults.Count; i++) {

                            Crafting.Recipe r = _searchResults[i];

                            if (r == null) continue;

                            GUILayout.BeginHorizontal();

                            GUILayout.Label(string.Format("  • {0}", r.Name), _labelStyle ?? GUI.skin.label, GUILayout.Width(300));

                            if (GUILayout.Button("Pilih", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(50), GUILayout.Height(18))) {

                                SetPlanRecipeById(r.Id);

                            }

                            GUILayout.EndHorizontal();

                        }

                        GUILayout.EndScrollView();

                    }

                                } else if (ActiveTab == 2) {
                    // ================= TAB 2: FITUR TEMPUR & GERAK =================
                    if (_subHeaderStyle != null) GUILayout.Label("  Fitur Tempur & Kecepatan Gerak:", _subHeaderStyle);

                    _combatScrollPos = GUILayout.BeginScrollView(_combatScrollPos, GUILayout.Height(330));

                    GUILayout.BeginVertical(GUI.skin.box);

                    // 1. KECEPATAN GERAK KARAKTER & DINO
                    bool isRidingNow = false;
                    try {
                        PlayerBehavior pl = PlayerBehavior.LocalPlayer;
                        if (pl != null && pl.IsRiding) isRidingNow = true;
                    } catch {}

                    string speedStatusHeader = isRidingNow ? "<color=#f1c40f><b>[SEDANG NAIK DINO]</b></color>" : "<color=#2ecc71><b>[JALAN KAKI]</b></color>";
                    GUILayout.Label(string.Format("  <b>1. Kecepatan Gerak (Speed Multiplier)</b>  {0}", speedStatusHeader), _subHeaderStyle ?? GUI.skin.label);

                    // Kecepatan Player
                    GUILayout.BeginHorizontal();
                    string pTag = !isRidingNow ? "<color=#2ecc71><b>[AKTIF]</b></color>" : "<color=#7f8c8d>[OFF]</color>";
                    GUILayout.Label(string.Format("  🏃 Player {0}: <color=#2ecc71><b>x{1:F2}</b></color>", pTag, MoveSpeedMultiplier), _labelStyle ?? GUI.skin.label, GUILayout.Width(170));
                    if (GUILayout.Button("-", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(22), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = Mathf.Max(0.5f, (float)Math.Round(MoveSpeedMultiplier - 0.05f, 2));
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("+", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(22), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = Mathf.Min(3.0f, (float)Math.Round(MoveSpeedMultiplier + 0.05f, 2));
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.0x", Mathf.Abs(MoveSpeedMultiplier - 1.0f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 1.0f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.2x", Mathf.Abs(MoveSpeedMultiplier - 1.2f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 1.2f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.35x", Mathf.Abs(MoveSpeedMultiplier - 1.35f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(44), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 1.35f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.5x", Mathf.Abs(MoveSpeedMultiplier - 1.5f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 1.5f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("2.0x", Mathf.Abs(MoveSpeedMultiplier - 2.0f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 2.0f;
                        UpdateMoveSpeedMultiplier();
                    }
                    GUILayout.EndHorizontal();

                    // Kunci Animasi Lari Normal (*_Run)
                    GUILayout.BeginHorizontal();
                    string lockCol = LockDefaultRunAnim ? "#2ecc71" : "#e74c3c";
                    string lockTxt = LockDefaultRunAnim ? "DIKUNCI [ON]" : "NORMAL [OFF]";
                    GUILayout.Label(string.Format("  Kunci Animasi Lari (*_Run): <color={0}><b>{1}</b></color>", lockCol, lockTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(240));
                    if (GUILayout.Button(LockDefaultRunAnim ? "Buka Kunci" : "Kunci Animasi", _smallBtnStyle ?? GUI.skin.button, GUILayout.Height(20), GUILayout.Width(130))) {
                        LockDefaultRunAnim = !LockDefaultRunAnim;
                        ApplyRunAnimationLock(LockDefaultRunAnim);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Mencegah animasi lari berubah jadi sprint/battle run aneh saat speed naik.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(2);

                    // Kecepatan Dino / Pet
                    GUILayout.BeginHorizontal();
                    string dTag = isRidingNow ? "<color=#f1c40f><b>[AKTIF]</b></color>" : "<color=#7f8c8d>[OFF]</color>";
                    GUILayout.Label(string.Format("  🦖 Dino / Pet {0}: <color=#2ecc71><b>x{1:F2}</b></color>", dTag, MountSpeedMultiplier), _labelStyle ?? GUI.skin.label, GUILayout.Width(170));
                    if (GUILayout.Button("-", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(22), GUILayout.Height(20))) {
                        MountSpeedMultiplier = Mathf.Max(0.5f, (float)Math.Round(MountSpeedMultiplier - 0.05f, 2));
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("+", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(22), GUILayout.Height(20))) {
                        MountSpeedMultiplier = Mathf.Min(4.0f, (float)Math.Round(MountSpeedMultiplier + 0.05f, 2));
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.0x", Mathf.Abs(MountSpeedMultiplier - 1.0f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MountSpeedMultiplier = 1.0f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.3x", Mathf.Abs(MountSpeedMultiplier - 1.3f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MountSpeedMultiplier = 1.3f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.5x", Mathf.Abs(MountSpeedMultiplier - 1.5f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MountSpeedMultiplier = 1.5f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("1.8x", Mathf.Abs(MountSpeedMultiplier - 1.8f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MountSpeedMultiplier = 1.8f;
                        UpdateMoveSpeedMultiplier();
                    }
                    if (GUILayout.Button("2.0x", Mathf.Abs(MountSpeedMultiplier - 2.0f) < 0.01f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(38), GUILayout.Height(20))) {
                        MountSpeedMultiplier = 2.0f;
                        UpdateMoveSpeedMultiplier();
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();

                    GUILayout.Space(3);

                    GUILayout.BeginVertical(GUI.skin.box);

                    // 2. ANTI-STAGGER & ANTI-STUN
                    GUILayout.BeginHorizontal();
                    string stagCol = EnableAntiStagger ? "#2ecc71" : "#e74c3c";
                    string stagTxt = EnableAntiStagger ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>2. Anti-Stagger (Kebal Interupsi):</b> <color={0}><b>{1}</b></color>", stagCol, stagTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableAntiStagger ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableAntiStagger = !EnableAntiStagger;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Bypass Blow & Knockback DamagedProcesser. Kebal hentakan & guncangan pukulan dino.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 3. MOVING ATTACK (Jalan saat Serang / Cancel Animasi)
                    GUILayout.BeginHorizontal();
                    string mobColor = EnableMobileAttack ? "#2ecc71" : "#e74c3c";
                    string mobTxt = EnableMobileAttack ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>3. Moving Attack (Cancel Animasi):</b> <color={0}><b>{1}</b></color>", mobColor, mobTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableMobileAttack ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableMobileAttack = !EnableMobileAttack;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Lepas MoveLock saat menebas. Bisa bebas jalan (WASD) dan cancel jeda animasi tebasan.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 3b. FAST SNIPER STANCE (Anti-Merunduk & Lari Cepat Bow/Crossbow)
                    GUILayout.BeginHorizontal();
                    string sniperColor = EnableFastSniperStance ? "#2ecc71" : "#e74c3c";
                    string sniperTxt = EnableFastSniperStance ? string.Format("AKTIF [{0:0.0}x]", FastSniperSpeedMultiplier) : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>3b. Fast Sniper Stance (Bow/Xbow):</b> <color={0}><b>{1}</b></color>", sniperColor, sniperTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableFastSniperStance ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableFastSniperStance = !EnableFastSniperStance;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Kecepatan Sniper:", _labelStyle ?? GUI.skin.label, GUILayout.Width(130));
                    if (GUILayout.Button("1.5x", Mathf.Abs(FastSniperSpeedMultiplier - 1.5f) < 0.05f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(50), GUILayout.Height(20))) {
                        FastSniperSpeedMultiplier = 1.5f;
                    }
                    if (GUILayout.Button("1.7x", Mathf.Abs(FastSniperSpeedMultiplier - 1.7f) < 0.05f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(50), GUILayout.Height(20))) {
                        FastSniperSpeedMultiplier = 1.7f;
                    }
                    if (GUILayout.Button("2.0x", Mathf.Abs(FastSniperSpeedMultiplier - 2.0f) < 0.05f ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(50), GUILayout.Height(20))) {
                        FastSniperSpeedMultiplier = 2.0f;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Label(string.Format("  <size=10><color=#95a5a6>• Menghilangkan animasi merunduk lambat saat Sikap Sniper. Karakter berdiri & lari normal ({0:0.0}x).</color></size>", FastSniperSpeedMultiplier), _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 3c. FAST CROSSBOW RELOAD (Ngokang Cepat)
                    GUILayout.BeginHorizontal();
                    string reloadColor = EnableFastCrossbowReload ? "#2ecc71" : "#e74c3c";
                    string reloadTxt = EnableFastCrossbowReload ? string.Format("AKTIF [{0:0.0}x]", CrossbowReloadSpeed) : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>3c. Fast Xbow Reload:</b> <color={0}><b>{1}</b></color>", reloadColor, reloadTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableFastCrossbowReload ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableFastCrossbowReload = !EnableFastCrossbowReload;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Mempercepat animasi ngokang Crossbow (1.5x). Moving attack dinonaktifkan khusus saat reload agar tampak wajar.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    
                    GUILayout.Space(3);

                    // 3d. AUTO OUT COMBAT (Auto Lepas Combat Saat Dino Mati)
                    GUILayout.BeginHorizontal();
                    string outCombatColor = AutoExitCombatOnKill ? "#2ecc71" : "#e74c3c";
                    string outCombatTxt = AutoExitCombatOnKill ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>3d. Auto Out Combat:</b> <color={0}><b>{1}</b></color>", outCombatColor, outCombatTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(AutoExitCombatOnKill ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        AutoExitCombatOnKill = !AutoExitCombatOnKill;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Otomatis keluar dari mode combat (BattleView -> Normal) dan melepas target segera setelah target dino mati.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 3e. COUNTER ASSIST (Visual Timing Alert)
                    GUILayout.BeginHorizontal();
                    string assistColor = EnableCounterAssist ? "#2ecc71" : "#e74c3c";
                    string assistTxt = EnableCounterAssist ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>3e. Counter Assist Alert:</b> <color={0}><b>{1}</b></color>", assistColor, assistTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableCounterAssist ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableCounterAssist = !EnableCounterAssist;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Jeda Alert:", _labelStyle ?? GUI.skin.label, GUILayout.Width(75));
                    if (GUILayout.Button("Standar (0.38s)", (EnableCounterAssist && Mathf.Abs(CounterAssistWindowMax - 0.38f) < 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(105), GUILayout.Height(20))) {
                        EnableCounterAssist = true;
                        CounterAssistWindowMax = 0.38f;
                    }
                    if (GUILayout.Button("Cepat (0.45s)", (EnableCounterAssist && Mathf.Abs(CounterAssistWindowMax - 0.45f) < 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(95), GUILayout.Height(20))) {
                        EnableCounterAssist = true;
                        CounterAssistWindowMax = 0.45f;
                    }
                    if (GUILayout.Button("Presisi (0.30s)", (EnableCounterAssist && Mathf.Abs(CounterAssistWindowMax - 0.30f) < 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(95), GUILayout.Height(20))) {
                        EnableCounterAssist = true;
                        CounterAssistWindowMax = 0.30f;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Target Filter:", _labelStyle ?? GUI.skin.label, GUILayout.Width(95));
                    string targetOnlyTxt = CounterAssistTargetOnly ? "🎯 Hanya Dino Target [ON]" : "🎯 Semua Dino [OFF]";
                    if (GUILayout.Button(targetOnlyTxt, CounterAssistTargetOnly ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(170), GUILayout.Height(20))) {
                        CounterAssistTargetOnly = !CounterAssistTargetOnly;
                    }
                    string posTxt = CounterAssistUnderCharacter ? "📍 Di Bawah Karakter" : "📍 Tengah Bawah Layar";
                    if (GUILayout.Button(posTxt, CounterAssistUnderCharacter ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(160), GUILayout.Height(20))) {
                        CounterAssistUnderCharacter = !CounterAssistUnderCharacter;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Fitur Tambahan:", _labelStyle ?? GUI.skin.label, GUILayout.Width(95));
                    string soundTxt = EnableCounterAssistSound ? "🔊 Suara [ON]" : "🔇 Suara [OFF]";
                    if (GUILayout.Button(soundTxt, EnableCounterAssistSound ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(115), GUILayout.Height(20))) {
                        EnableCounterAssistSound = !EnableCounterAssistSound;
                    }
                    string hudTxt = EnableCounterAssistCenterHUD ? "🖥️ Banner HUD [ON]" : "🖥️ Banner HUD [OFF]";
                    if (GUILayout.Button(hudTxt, EnableCounterAssistCenterHUD ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(140), GUILayout.Height(20))) {
                        EnableCounterAssistCenterHUD = !EnableCounterAssistCenterHUD;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Label("  <size=10><color=#95a5a6>• Memberi sinyal visual & audio berkedip di tombol Counter saat serangan dino akan mendarat (Fase 2). Membantu eksekusi Counter Attack di timing paling pas tanpa perlu menebak jeda serangan.</color></size>", _hintStyle ?? GUI.skin.label);

                    // 4. TEMBUS HALANGAN DINO (DINO NOCLIP)
                    GUILayout.BeginHorizontal();
                    string noclipColor = EnableDinoNoclip ? "#2ecc71" : "#e74c3c";
                    string noclipTxt = EnableDinoNoclip ? (DinoNoclipOnlyInCombat ? "AKTIF [HANYA COMBAT]" : "AKTIF [SELALU ON]") : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>4. Tembus Dino (Dino Noclip):</b> <color={0}><b>{1}</b></color>", noclipColor, noclipTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableDinoNoclip ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableDinoNoclip = !EnableDinoNoclip;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Mode Noclip:", _labelStyle ?? GUI.skin.label, GUILayout.Width(90));
                    if (GUILayout.Button("Selalu Aktif", (EnableDinoNoclip && !DinoNoclipOnlyInCombat) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(110), GUILayout.Height(20))) {
                        EnableDinoNoclip = true;
                        DinoNoclipOnlyInCombat = false;
                    }
                    if (GUILayout.Button("Hanya Saat Combat", (EnableDinoNoclip && DinoNoclipOnlyInCombat) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(130), GUILayout.Height(20))) {
                        EnableDinoNoclip = true;
                        DinoNoclipOnlyInCombat = true;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Menembus tubuh/hitbox dino saat bergerak atau reposisi tanpa terhalang atau tersangkut.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 5. TURBO ASPD (RECOVERY CUT)
                    GUILayout.BeginHorizontal();
                    string recColor = EnableRecoveryCut ? "#2ecc71" : "#e74c3c";
                    string recTxt = EnableRecoveryCut ? string.Format("AKTIF [{0:F2}s]", RecoveryCutDelay) : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>5. Turbo ASPD (Recovery Cut):</b> <color={0}><b>{1}</b></color>", recColor, recTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableRecoveryCut ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableRecoveryCut = !EnableRecoveryCut;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Jeda Cut:", _labelStyle ?? GUI.skin.label, GUILayout.Width(70));
                    if (GUILayout.Button("Turbo (0.02s)", (EnableRecoveryCut && RecoveryCutDelay <= 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(110), GUILayout.Height(20))) {
                        EnableRecoveryCut = true;
                        RecoveryCutDelay = 0.02f;
                    }
                    if (GUILayout.Button("Fast (0.08s)", (EnableRecoveryCut && RecoveryCutDelay > 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(100), GUILayout.Height(20))) {
                        EnableRecoveryCut = true;
                        RecoveryCutDelay = 0.08f;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Memotong jeda animasi tebasan agar ayunan berikutnya langsung siap instan.</color></size>", _hintStyle ?? GUI.skin.label);

                    // 6. STEALTH ANTI-AGGRO DINO
                    GUILayout.BeginHorizontal();
                    string stlCol = EnableStealthAntiAggro ? "#2ecc71" : "#e74c3c";
                    string stlTxt = EnableStealthAntiAggro ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>6. Kamuflase Semak (Anti-Aggro):</b> <color={0}><b>{1}</b></color>", stlCol, stlTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableStealthAntiAggro ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableStealthAntiAggro = !EnableStealthAntiAggro;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Selalu dianggap bersembunyi di semak-semak sehingga dino liar tidak menyerang lebih dulu.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 7. AUTO LOOT AIR & LAHAR (LOOPING)
                    GUILayout.BeginHorizontal();
                    int t2EmptyLava = CountEmptyLavaContainers();
                    string t2LavaTxt = IsLavaLoopActive ? "⏹ Stop Lahar" : string.Format("🌋 Loop Lahar ({0})", t2EmptyLava);
                    if (GUILayout.Button(t2LavaTxt, IsLavaLoopActive ? (_selectedBtnStyle ?? GUI.skin.button) : (_btnStyle ?? GUI.skin.button), GUILayout.Height(22))) {
                        if (IsLavaLoopActive) StopLavaLoop();
                        else StartLavaLoop();
                    }

                    int t2EmptyWater = CountEmptyWaterContainers();
                    string t2WaterTxt = IsWaterLoopActive ? "⏹ Stop Air" : string.Format("🌊 Loop Air ({0})", t2EmptyWater);
                    if (GUILayout.Button(t2WaterTxt, IsWaterLoopActive ? (_selectedBtnStyle ?? GUI.skin.button) : (_btnStyle ?? GUI.skin.button), GUILayout.Height(22))) {
                        if (IsWaterLoopActive) StopWaterLoop();
                        else StartWaterLoop();
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Looping otomatis menyendok lahar atau air ke seluruh wadah di tas sampai penuh.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(4);

                    // 8. SIMPAN KONFIGURASI KE FILE DISK
                    GUILayout.Label("  <b>8. Simpan Konfigurasi (Save Config):</b>", _subHeaderStyle ?? GUI.skin.label);
                    if (GUILayout.Button("💾 SIMPAN KONFIGURASI SEKARANG (AutoGatherSettings.ini)", _selectedBtnStyle ?? GUI.skin.button, GUILayout.Height(28))) {
                        SaveSettings();
                        _saveBannerTime = Time.time + 4.0f;
                        _statusText = "<color=#2ecc71><b>✓ Konfigurasi Berhasil Disimpan ke AutoGatherSettings.ini!</b></color>";
                    }
                    if (Time.time < _saveBannerTime) {
                        GUILayout.Label("  <color=#2ecc71><b>✓ Konfigurasi Berhasil Disimpan ke AutoGatherSettings.ini!</b></color>", _labelStyle ?? GUI.skin.label);
                    } else {
                        GUILayout.Label("  <size=10><color=#95a5a6>• Konfigurasi auto-save saat toggle diubah & bisa disimpan manual ke AutoGatherSettings.ini.</color></size>", _hintStyle ?? GUI.skin.label);
                    }

                    GUILayout.EndVertical();

                    GUILayout.EndScrollView();
                }

                GUILayout.FlexibleSpace();

                // 4. TOMBOL TOGGLE ON/OFF & PILIH HOTKEY SESUAI TAB
                GUILayout.BeginHorizontal();

                if (_btnStyle != null) {
                    if (ActiveTab == 2) {
                        if (GUILayout.Button("💾 Simpan Pengaturan Sekarang", _btnStyle ?? GUI.skin.button, GUILayout.Height(26), GUILayout.Width(405))) {
                            SaveSettings();
                        }
                    } else {

                        string btnText = IsEnabled ? string.Format("Matikan Bot [{0}]", ToggleKey) : string.Format("Nyalakan Bot [{0}]", ToggleKey);

                        if (GUILayout.Button(btnText, _btnStyle, GUILayout.Height(26), GUILayout.Width(290))) {

                            ToggleMod();

                        }

                        if (GUILayout.Button("Key: [" + ToggleKey + "]", _smallBtnStyle ?? GUI.skin.button, GUILayout.Height(26), GUILayout.Width(110))) {

                            CycleToggleKey();

                        }

                    }

                }

                GUILayout.EndHorizontal();

                GUILayout.Space(6);

                // Cek apakah ada setting yang berubah selama frame GUI ini

                if (oldScan != ScanRadiusMeters ||

                    oldFilter != CurrentFilter ||

                    oldTargetMode != TargetMode ||

                    oldRecCut != EnableRecoveryCut ||

                    oldRecDelay != RecoveryCutDelay ||

                    oldStagger != EnableAntiStagger ||

                    oldMob != EnableMobileAttack ||

                    oldSniper != EnableFastSniperStance ||

                    Mathf.Abs(oldSniperSpeed - FastSniperSpeedMultiplier) > 0.01f ||

                    oldCrossbowReload != EnableFastCrossbowReload ||

                    Mathf.Abs(oldCrossbowSpeed - CrossbowReloadSpeed) > 0.01f ||

                    oldNoclip != EnableDinoNoclip ||

                    oldAutoExit != AutoExitCombatOnKill ||
                    oldCounterAssist != EnableCounterAssist ||
                    Mathf.Abs(oldCounterMax - CounterAssistWindowMax) > 0.001f ||
                    oldCounterSound != EnableCounterAssistSound ||
                    oldCounterHUD != EnableCounterAssistCenterHUD ||
                    oldCounterTargetOnly != CounterAssistTargetOnly ||
                    oldCounterUnderChar != CounterAssistUnderCharacter ||

                    oldNoclipCombat != DinoNoclipOnlyInCombat ||

                    oldStealth != EnableStealthAntiAggro ||

                    oldMoveSpeed != MoveSpeedMultiplier ||

                    oldLockRun != LockDefaultRunAnim ||

                    oldMountSpeed != MountSpeedMultiplier ||

                    oldPlanQty != PlanQuantity ||

                    oldAutoEat != AutoEatWhenCrafting ||

                    oldHoldAttack != EnableHoldToAttack ||

                    oldHoldInterval != HoldAttackInterval ||

                    oldArchi != UnlockAllArchipelagoIslands) {

                    RequestSaveSettings();

                }

                GUILayout.EndArea();

            } catch {}

        }

        private void DrawTabButton(string label, int tabIdx, float width) {

            bool isSelected = (ActiveTab == tabIdx);

            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;

            string display = isSelected ? ("[" + label + "]") : label;

            if (GUILayout.Button(display, st ?? GUI.skin.button, GUILayout.Width(width), GUILayout.Height(22))) {

                ActiveTab = tabIdx;

                if (tabIdx == 1 && PlanRecipe != null) {

                    UpdatePlanRequirements();

                }

                RequestSaveSettings();

            }

        }

        private void DrawPresetButton(string keyword, float width) {

            if (GUILayout.Button(keyword, _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(width), GUILayout.Height(20))) {

                SetPlanPreset(keyword);

                RequestSaveSettings();

            }

        }

        private void DrawFilterButton(string label, ResourceFilterMode mode, float width) {

            bool isSelected = (CurrentFilter == mode);

            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;

            string display = isSelected ? ("[" + label + "]") : label;

            if (GUILayout.Button(display, st, GUILayout.Width(width), GUILayout.Height(20))) {

                CurrentFilter = mode;

                _currentTargetObj = null; // Reset target agar scan ulang sesuai filter baru

                RequestSaveSettings();

            }

        }

        private void DrawTargetModeButton(string label, GatherTargetMode mode, float width) {

            bool isSelected = (TargetMode == mode && string.IsNullOrEmpty(CustomKeyword.Trim()));

            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;

            string display = isSelected ? ("[" + label + "]") : label;

            if (GUILayout.Button(display, st, GUILayout.Width(width), GUILayout.Height(20))) {

                TargetMode = mode;

                CustomKeyword = "";

                RequestSaveSettings();

            }

        }

        private void DrawStaminaReserveButton(string label, float val, float width) {

            bool isSelected = Mathf.Approximately(StaminaReservePercent, val);

            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;

            string display = isSelected ? ("[" + label + "]") : label;

            if (GUILayout.Button(display, st ?? GUI.skin.button, GUILayout.Width(width), GUILayout.Height(20))) {

                StaminaReservePercent = val;

            }

        }

        private void DrawCombatTargetModeButton(string label, CombatTargetMode mode, float width) {

            bool isSelected = (CombatTargetFilter == mode);

            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;

            string display = isSelected ? ("[" + label + "]") : label;

            if (GUILayout.Button(display, st ?? GUI.skin.button, GUILayout.Width(width), GUILayout.Height(20))) {

                CombatTargetFilter = mode;

                _currentCombatTarget = null;

            }

        }

        private void DrawCombatRadiusButton(string label, float radius, float width) {

            bool isSelected = Mathf.Approximately(CombatScanRadiusMeters, radius);

            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;

            string display = isSelected ? ("[" + label + "]") : label;

            if (GUILayout.Button(display, st ?? GUI.skin.button, GUILayout.Width(width), GUILayout.Height(20))) {

                CombatScanRadiusMeters = radius;

                _currentCombatTarget = null;

            }

        }

        private void InitGUIStyles() {

            if (_boxStyle != null) return;

            try {

                if (GUI.skin == null) return;

                _bgTexture = new Texture2D(1, 1);

                _bgTexture.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.12f, 0.92f));

                _bgTexture.Apply();

                _boxStyle = new GUIStyle();

                _boxStyle.normal.background = _bgTexture;

                _headerStyle = new GUIStyle();

                _headerStyle.fontSize = 13;

                _headerStyle.fontStyle = FontStyle.Bold;

                _headerStyle.normal.textColor = new Color(0.95f, 0.77f, 0.25f);

                _subHeaderStyle = new GUIStyle();

                _subHeaderStyle.fontSize = 10;

                _subHeaderStyle.fontStyle = FontStyle.Bold;

                _subHeaderStyle.normal.textColor = new Color(0.80f, 0.85f, 0.95f);

                _hintStyle = new GUIStyle();

                _hintStyle.fontSize = 9;

                _hintStyle.fontStyle = FontStyle.Italic;

                _hintStyle.normal.textColor = new Color(0.95f, 0.77f, 0.25f);

                _labelStyle = new GUIStyle();

                _labelStyle.fontSize = 11;

                _labelStyle.richText = true;

                _labelStyle.normal.textColor = Color.white;

                if (GUI.skin.button != null) {

                    _btnStyle = new GUIStyle(GUI.skin.button);

                    _btnStyle.fontSize = 11;

                    _btnStyle.fontStyle = FontStyle.Bold;

                    _smallBtnStyle = new GUIStyle(GUI.skin.button);

                    _smallBtnStyle.fontSize = 10;

                    _selectedBtnStyle = new GUIStyle(GUI.skin.button);

                    _selectedBtnStyle.fontSize = 10;

                    _selectedBtnStyle.fontStyle = FontStyle.Bold;

                    _selectedBtnStyle.normal.textColor = new Color(0.95f, 0.77f, 0.25f); // Emas/Kuning

                }

                if (GUI.skin.textField != null) {

                    _textFieldStyle = new GUIStyle(GUI.skin.textField);

                    _textFieldStyle.fontSize = 10;

                    _textFieldStyle.normal.textColor = Color.yellow;

                }

                if (_counterBadgeStyle == null && GUI.skin != null) {
                    _counterBadgeStyle = new GUIStyle(GUI.skin.label);
                    _counterBadgeStyle.alignment = TextAnchor.MiddleCenter;
                    _counterBadgeStyle.fontSize = 11;
                    _counterBadgeStyle.fontStyle = FontStyle.Bold;
                    _counterBadgeStyle.richText = true;
                    _counterBadgeStyle.normal.textColor = Color.white;
                }

                if (_counterHudTitleStyle == null && GUI.skin != null) {
                    _counterHudTitleStyle = new GUIStyle(GUI.skin.label);
                    _counterHudTitleStyle.alignment = TextAnchor.MiddleCenter;
                    _counterHudTitleStyle.fontSize = 12;
                    _counterHudTitleStyle.fontStyle = FontStyle.Bold;
                    _counterHudTitleStyle.richText = true;
                    _counterHudTitleStyle.normal.textColor = Color.white;
                }

                if (_counterHudSubStyle == null && GUI.skin != null) {
                    _counterHudSubStyle = new GUIStyle(GUI.skin.label);
                    _counterHudSubStyle.alignment = TextAnchor.MiddleCenter;
                    _counterHudSubStyle.fontSize = 10;
                    _counterHudSubStyle.richText = true;
                    _counterHudSubStyle.normal.textColor = Color.white;
                }

                if (_counterHudCenterStyle == null && GUI.skin != null) {
                    _counterHudCenterStyle = new GUIStyle(GUI.skin.label);
                    _counterHudCenterStyle.alignment = TextAnchor.MiddleCenter;
                    _counterHudCenterStyle.fontSize = 12;
                    _counterHudCenterStyle.fontStyle = FontStyle.Bold;
                    _counterHudCenterStyle.richText = true;
                    _counterHudCenterStyle.normal.textColor = Color.white;
                }

            } catch {}

        }

        void OnDestroy() {

            try {

                UnhookInventoryEvent();

                UnhookCombatEvent();

                if (_lastHookedCombatSystem != null) {
                    try { _lastHookedCombatSystem.AttackAlerted -= OnEnemyAttackAlerted; } catch {}
                    _lastHookedCombatSystem = null;
                }
                if (CombatSystem.HasInstance()) {
                    CombatSystem cs = CombatSystem.Instance();
                    if (cs != null) {
                        try { cs.AttackAlerted -= OnEnemyAttackAlerted; } catch {}
                    }
                }

                if (_bgTexture != null) {

                    Destroy(_bgTexture);

                }

                try {

                    PlayerController pc = GetPlayerController();

                    if (pc != null) {

                        pc.CheatMoveSpeedMultiply = 1.0f;

                    }

                    if (_savedPushUnits != null && _lastPushAreaInstance != null && _pushUnitsField != null) {

                        _pushUnitsField.SetValue(_lastPushAreaInstance, _savedPushUnits);

                        _savedPushUnits = null;

                    }

                } catch {}

            } catch {}

        }


    }
}
