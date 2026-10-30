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
    public partial class AutoGatherController : MonoBehaviour {

        // ===== TRIPLE STRIKE BURST SETTINGS =====

        public bool EnableTripleStrike = false;

        public int TripleStrikeHits = 3;

        public int TripleStrikeCount = 3;

        public float TripleStrikeInterval = 0.05f;

        private int _tripleStrikeHitCount = 0;

        private bool _isBurstingTripleStrike = false;

        // ===== ESP / RADAR SETTINGS =====

        public bool EnableEsp = false;

        public bool EspShowDinos = true;

        public bool EspShowCorpses = false;

        public bool EspShowRareOnly = false;

        public bool EspShowCraters = false;

        public bool EspShowWaterSources = false;

        public bool EspShowSnaplines = false;

        public bool EnableEdgeRadar = false;

        public float EspMaxDistance = 150f;

        private List<EspTargetInfo> _espTargets = new List<EspTargetInfo>();

        private GUIStyle _espTagStyle = null;

        private GUIStyle _espEdgeStyle = null;

        // ===== COMBAT & SURVIVAL SETTINGS =====

        private bool _isCombatEventHooked = false;

        private bool _isAttackPacketHooked = false;

// removed duplicate EnableMobileAttack

        public bool EnableAutoEatFood = true;

        public bool EnableDPSMaximizer = true;

// removed duplicate HoldAttackInterval

        public bool EnableArchitectureCheat = false;

        public bool EnableBushStealth = true;

        public bool EnableAutoConsumeHunt = true;

        public bool EnableAutoDisengageToEat = true;

        public bool EnableEmergencyAutoDisengage = true;

        public float AutoConsumeStaminaThreshold = 0.5f;

        public float AutoConsumeMinStaminaAbsolute = 30f;

        public float AutoConsumeHealthThreshold = 0.5f;

        public float AutoConsumeHuntMinFoodEnergy = 50f;

        [DllImport("user32.dll")]

        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_E = 0x45;

        private const byte SCAN_E = 0x12;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static AutoGatherController Instance;

        public bool IsEnabled = false;

        public MenuDisplayState MenuState = MenuDisplayState.Normal;

        private float _lastCtrl3Time = 0f;

        private float _lastCtrl1Time = 0f;

        private float _lastToggleTriggerTime = 0f;

        public float ScanRadiusMeters = 50f; // Dalam meter in-game (1m = 100 unit Durango)

        public KeyCode ToggleKey = KeyCode.F1;

        // Pengaturan Filter & Target Panen

        public ResourceFilterMode CurrentFilter = ResourceFilterMode.All;

        public GatherTargetMode TargetMode = GatherTargetMode.Auto;

        public string CustomKeyword = "";

        // ===== AUTO COMBAT & DODGE SYSTEM =====

        public bool IsCombatBotEnabled = false;

        public KeyCode CombatToggleKey = KeyCode.F8;

        public CombatTargetMode CombatTargetFilter = CombatTargetMode.AllNear;

        public float CombatScanRadiusMeters = 25f;

        public float StaminaReservePercent = 30f;

        public bool AutoDodgeEnabled = true;

        public bool EvadeWhenCooldown = true;

        public bool AutoHarvestKilledDino = true;

        public bool AutoCastSkills = true;

        private DamageableEntity _currentCombatTarget = null;

        private float _lastAttackTime = 0f;

        private float _lastDodgeTime = 0f;

        private float _lastCombatScanTime = 0f;

        private float _lastCombatMoveTime = 0f;

        private string _combatStatusText = "Standby (Tekan [F2] untuk Aktifkan Auto Combat)";

        private string _combatActionStatus = "-";

        private bool _isCombatAlertHooked = false;

        private float _lastAttackAlertTime = 0f;

        private Messages.AttackAlerted _lastAttackAlertMsg;

        private bool _hasActiveAttackAlert = false;

        private float _alertClearTime = 0f;

        // Pacing & Animation Lock Tracking (1 Aksi dalam 1 Saat)

        private float _attackAnimationFinishTime = 0f;

        private float _nextAttackAllowedTime = 0f;

        private bool _pendingDodgeOnAlert = false;

        private float _pendingEscapeYaw = 0f;

        private Vector3 _lastSafeEscapePos = Vector3.zero;

        private bool _isPlayerInsideHitbox = false;

        private string _hitboxShapeInfo = "None";

        // ===== CRAFT PLANNER SYSTEM =====

        public int ActiveTab = 0; // 0 = Manual Gather, 1 = Craft Planner, 2 = Auto Combat

        public bool IsCraftPlanActive = false;

        public string PlanRecipeId = "";

        public Crafting.Recipe PlanRecipe = null;

        public int PlanQuantity = 1;

        public bool AutoCraftWhenReady = true;

        public string RecipeSearchKeyword = "";

        private List<Crafting.Recipe> _searchResults = new List<Crafting.Recipe>();

        private Vector2 _searchScrollPos = Vector2.zero;

        private Vector2 _materialScrollPos = Vector2.zero;

        private float _nextPlanCheckTime = 0f;

        private string _planStatusSummary = "Pilih resep untuk memulai plan";

        private List<SlotRequirementInfo> _planSlotInfos = new List<SlotRequirementInfo>();

        private SlotRequirementInfo _currentHuntingSlot = null;

        private bool _allMaterialsReady = false;

        private bool _isInventoryHooked = false;

        private static Dictionary<string, Crafting.Recipe> _subRecipeCache = new Dictionary<string, Crafting.Recipe>();

        private GameObject _currentTargetObj = null;

        private Vector3 _currentTargetPos = Vector3.zero;

        private float _currentTargetInteractDist = 220f; // 2.2 meter in Durango units

        private string _targetName = "-";

        private float _targetDistance = 0f;

        private string _targetAvailableResources = "-";

        private string _statusText = "Standby (Tekan [F1] untuk Aktifkan)";

        private float _nextScanTime = 0f;

        private float _nextMoveCallTime = 0f;

        private bool _isGathering = false;

        private float _gatherCooldownUntil = 0f;

        private int _totalCandidatesFound = 0;

        // Stuck prevention detection

        private Vector3 _lastPlayerPos = Vector3.zero;

        private float _lastPositionCheckTime = 0f;

        private float _approachTargetStartTime = 0f;

        // Blacklist objek yang baru dipanen agar tidak bolak-balik

        private Dictionary<GameObject, float> _recentlyGathered = new Dictionary<GameObject, float>();

        // ================= SISTEM TARGET PEMBELAJARAN OTOMATIS =================

        private List<LearnedTargetItem> _learnedTargetItems = new List<LearnedTargetItem>();

        private string _learnedActionName = "";

        private string _learnedGeneratorId = "";

        private string _learnedNodeBaseName = "";

        private string _learnedNodeDisplayName = "";

        private string _learnedNodePoolName = "";

        private int _learnedNodeEntityType = -1;

        private ResourceFilterMode _learnedFilter = ResourceFilterMode.All;

        private bool _hasLearnedTarget = false;

        private GameObject _lastInteractedObject = null;

        private Durango.UI.InteractionMenuListWidget_PC _hookedMenuList = null;

        private static FieldInfo _menuTargetField = typeof(Durango.UI.InteractionMenuListWidgetBase).GetField("MenuTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo _erwQuantitySelectorField = typeof(Durango.UI.ExpectResultWidget).GetField("_quantitySelector", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo _erwCraftField = typeof(Durango.UI.ExpectResultWidget).GetField("_craft", BindingFlags.NonPublic | BindingFlags.Instance);

        private Durango.UI.Control.IntSelector _hookedIntSelector = null;

        private Durango.UI.ExpectResultWidget _hookedExpectResultWidget = null;

        // ===== AUTO-EAT SYSTEM SAAT CRAFTING =====

        public bool AutoEatWhenCrafting = true;

        private float _nextAutoEatCheckTime = 0f;

        private float _lastEatTime = 0f;

        private string _lastEatenFoodName = "";

        private static FieldInfo _confirmPopupTextLabelField = typeof(Durango.UI.Popup.ConfirmPopup).GetField("TextLabel", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo _confirmPopupActionsField = typeof(Durango.UI.Popup.ConfirmPopup).GetField("_onActions", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo _csCraftQueueField = typeof(CraftSystem).GetField("_craftQueue", BindingFlags.NonPublic | BindingFlags.Instance);

        // ===== COMBAT & SPEED SYSTEM =====

        public float MoveSpeedMultiplier = 1.0f;

        public float MountSpeedMultiplier = 1.5f; // Kecepatan Pet saat ditunggangi / mount

        public bool LockDefaultRunAnim = true; // Kunci animasi lari normal (*_Run) agar tidak berubah jadi sprint (*_BattleRun)

        public bool EnableMobileAttack = true;

        public bool EnableFastSniperStance = true; // Gerak cepat & anti-merunduk saat sikap sniper (Bow/Crossbow)

        public float FastSniperSpeedMultiplier = 1.7f; // Multiplier kecepatan gerak saat sikap sniper (1.7x)

        public bool EnableFastCrossbowReload = true; // Percepat animasi ngokang/reload Crossbow (1.5x) & pertahankan MoveLock saat reload agar tidak mencolok

        public float CrossbowReloadSpeed = 1.5f; // Multiplier kecepatan reload crossbow (1.5x)

        public bool EnableRecoveryCut = true;

        public bool EnableAntiStagger = true;

        public bool EnableDinoNoclip = true;

        public bool DinoNoclipOnlyInCombat = false;

        public bool AutoExitCombatOnKill = true; // Otomatis keluar combat & lepas target saat target dino mati

        // ===== COUNTER ASSIST (VISUAL TIMING ALERT) =====
        public bool EnableCounterAssist = true;                // Master toggle Counter Assist
        public float CounterAssistWindowMax = 0.38f;          // Waktu mulai golden window (detik sebelum pukulan mendarat)
        public float CounterAssistWindowMin = 0.05f;          // Waktu akhir golden window (detik sebelum benturan)
        public bool EnableCounterAssistSound = true;           // Suara audio cue saat window terbuka
        public bool EnableCounterAssistCenterHUD = true;       // Banner visual alert di tengah bawah layar
        public bool CounterAssistDodgeFallback = true;        // Fallback ke Dodge Roll jika senjata 2-Hand / non-counter stance
        public bool CounterAssistTargetOnly = true;           // Hanya deteksi serangan dari dino yang sedang jadi target
        public bool CounterAssistUnderCharacter = true;        // Tampilkan UI alert pas di bawah kaki karakter

        private class IncomingAttackAlert {
            public Messages.AttackAlerted Alert;
            public string AttackerId;
            public string AttackerName;
            public double EventAt;
            public double AttackTime;
            public float ReceivedLocalTime;
            public bool SoundPlayed;
            public bool HasTelegraphHitbox;     // Apakah serangan memiliki telegraph area merah (Rect/Circle/Arc)
            public bool IsPlayerInside;          // Apakah posisi player berada di dalam area merah ini
            public float DistanceToCenter;       // Jarak ke pusat hitbox (cm)
            public string HitboxDesc;            // Deskripsi bentuk hitbox
        }

        private readonly List<IncomingAttackAlert> _activeAttackAlerts = new List<IncomingAttackAlert>();
        private IncomingAttackAlert _currentImminentAlert = null;
        private float _currentImminentTimeUntilHit = 999f;
        private bool _isCounterGoldenWindowActive = false;
        private bool _isCounterStanceActiveFeedback = false;
        private bool _isPlayerInsideActiveHitbox = false; // Player berada di dalam area merah ancaman
        private int _totalActiveHitboxes = 0;             // Total area merah aktif dari penyerang
        private int _threatHitboxesCount = 0;             // Jumlah area merah yang mengenai posisi player
        private static Texture2D _counterWhiteTexture = null;
        private GUIStyle _counterBadgeStyle = null;
        private GUIStyle _counterHudTitleStyle = null;
        private GUIStyle _counterHudSubStyle = null;
        private GUIStyle _counterHudCenterStyle = null;

        public float RecoveryCutDelay = 0.02f; // 0.02s = Turbo, 0.08s = Fast, 0f = Off

        // ===== BASIC ATTACK SPAM (HOLD-TO-ATTACK) =====

        public bool EnableHoldToAttack = true;    // Tahan tombol serang untuk spam basic attack otomatis

        public float HoldAttackInterval = 0.12f;  // Interval trigger serangan saat tombol ditahan

        private float _lastHoldAttackTime = 0f;

        private static FieldInfo _babsPressedField = null;

        private static FieldInfo _babsContainerField = null;

        private static FieldInfo _babIsPressedField = null;

        private static FieldInfo _lmuLockRunAnimField = null;

        private static FieldInfo _lmuCurrentMotionClipField = null;

        private static FieldInfo _lmuMotionUpdatedField = null;

        private Dictionary<Durango.Player.Animation.PlayerAnimationStateClip, string> _originalRunClips = new Dictionary<Durango.Player.Animation.PlayerAnimationStateClip, string>();

        private bool _runAnimationLocked = false;

        // ===== ARCHIPELAGO ISLAND UNLOCKER =====

        public bool UnlockAllArchipelagoIslands = true;

        private static FieldInfo _archiUnlockField = null;

        // ===== STEALTH HUNT & ANTI-AGGRO SYSTEM =====

        public bool EnableStealthAntiAggro = true;

        private bool _playerInitiatedCombat = false;

        private static FieldInfo _csUsingActionField = typeof(CombatSystem).GetField("_usingAction", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo _uaStateFinishTimeField = null;

        private static FieldInfo _uaStateField = null;

        private static FieldInfo _uaActionField = null;

        private static FieldInfo _dpControllLostUntilField = null;

                private static FieldInfo _dpDisableStaggerField = null;

        private static FieldInfo _uaDisableMoveLockField = null;

        private static FieldInfo _csDisableMoveCancelField = null;

        private static FieldInfo _pcStopMoveField = null;

                private static FieldInfo _pcManualMoveAllowedField = null;

        private static FieldInfo _lupaNoclipField = null;

        private float _saveBannerTime = 0f;

        private static FieldInfo _dpListField = null;

        private static FieldInfo _pcMoveOperatorField = null;

        private static FieldInfo _lmoUnitPushAreaField = null;

        private static FieldInfo _pushUnitsField = null;

        private static FieldInfo _pBoneFlinchField = null;

        private static FieldInfo _bfcDurField = null;

        private static FieldInfo _bfcDegField = null;

        private static FieldInfo _pEquipmentAnimField = typeof(PlayerBehavior).GetField("_equipmentAnim", BindingFlags.NonPublic | BindingFlags.Instance);

        private static BattleAction _lastCrossbowAction = null;

        private static float _crossbowActionStartTime = 0f;

        private static float _originalCrossbowActionLength = 1.8f;

        private static float _crossbowDamageTime = 0.2f;

        private static bool _crossbowState3Active = false;



        private IDictionary _savedPushUnits = null;

        private IDictionary _emptyPushUnits = null;

        private object _lastPushAreaInstance = null;

        private Vector2 _combatScrollPos = Vector2.zero;

        private static Vector2 _windowPos = new Vector2(20, 85);

        private bool _isDragging = false;

        private Vector2 _dragOffset = Vector2.zero;

        private Texture2D _whiteTex = null;

        // ===== SETTINGS PERSISTENCE =====

        private bool _settingsDirty = false;

        private float _nextRecipeRestoreCheckTime = 0f;

        private string _lastSavedFeedback = "";

        private float _lastSavedFeedbackTime = 0f;

        // Cache reflection & GUI

        private FieldInfo _navField = null;

        private MethodInfo _clickHotKeyMethod = null;

        private GUIStyle _boxStyle = null;

        private GUIStyle _labelStyle = null;

        private GUIStyle _subHeaderStyle = null;

        private GUIStyle _headerStyle = null;

        private GUIStyle _btnStyle = null;

        private GUIStyle _smallBtnStyle = null;

        private GUIStyle _selectedBtnStyle = null;

        private GUIStyle _hintStyle = null;

        private GUIStyle _textFieldStyle = null;

        private Texture2D _bgTexture = null;

        public static void EnsureLoaded() {

            try {

                if (Instance != null) return;

                GameObject host = new GameObject("AutoGatherHost");

                UnityEngine.Object.DontDestroyOnLoad(host);

                host.AddComponent<AutoGatherController>();

                Debug.Log("[AutoGather] AutoGatherController berhasil dibuat via EnsureLoaded!");

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] EnsureLoaded error: " + ex);

            }

        }

                
        void Awake() {
            try {
                if (Instance != null && Instance != this) {
                    DestroyImmediate(this);
                    return;
                }
                Instance = this;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                GetSettingsFilePath(); // Pre-cache lokasi file pengaturan
                _navField = typeof(PlayerController).GetField("_localMoveNavigator", BindingFlags.NonPublic | BindingFlags.Instance);
                
                try {
                    _clickHotKeyMethod = typeof(Durango.UI.InteractionMenuListWidget_PC).GetMethod(
                        "ClickHotKey", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                } catch {}

                try {
                    if (_csUsingActionField != null) {
                        Type uaType = _csUsingActionField.FieldType;
                        _uaStateFinishTimeField = uaType.GetField("_stateFinishTime", BindingFlags.NonPublic | BindingFlags.Instance);
                        _uaStateField = uaType.GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
                        _uaActionField = uaType.GetField("_action", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    Type dpType = typeof(Durango.Logic.Combat.DamagedProcesser);
                    if (dpType != null) {
                        _dpControllLostUntilField = dpType.GetField("<ControllLostUntil>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                        _dpDisableStaggerField = dpType.GetField("DisableStagger", BindingFlags.Public | BindingFlags.Static);
                    }
                    Type usingActType = typeof(Durango.Logic.Combat.UsingAction);
                    if (usingActType != null) {
                        _uaDisableMoveLockField = usingActType.GetField("DisableAttackMoveLock", BindingFlags.Public | BindingFlags.Static);
                    }
                    Type csType = typeof(CombatSystem);
                    if (csType != null) {
                        _csDisableMoveCancelField = csType.GetField("DisableMoveCancel", BindingFlags.Public | BindingFlags.Static);
                    }
                    Type pcType = typeof(PlayerController);
                    if (pcType != null) {
                        _pcStopMoveField = pcType.GetField("_stopMoveReserved", BindingFlags.NonPublic | BindingFlags.Instance);
                        _pcManualMoveAllowedField = pcType.GetField("_manualMoveAllowed", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    Type lupaType = typeof(LocalUnitPushArea);
                    if (lupaType != null) {
                        _lupaNoclipField = lupaType.GetField("EnableDinoNoclip", BindingFlags.Public | BindingFlags.Static);
                    }
                } catch {}

                Debug.Log("[AutoGather] AutoGatherController Awake selesai!");

                LoadSettings();
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Awake error: " + ex);
            }
        }


        void Update() {

            if (Instance != this) return;

            try {

                // Auto-save settings jika ada perubahan tertunda

                if (_settingsDirty) {

                    _settingsDirty = false;

                    SaveSettings();

                }

                // Restore PlanRecipe dari PlanRecipeId yang dimuat dari file begitu RecipeSystem siap

                if (!string.IsNullOrEmpty(PlanRecipeId) && PlanRecipe == null && Time.time >= _nextRecipeRestoreCheckTime) {

                    _nextRecipeRestoreCheckTime = Time.time + 3f;

                    try {

                        SetPlanRecipeById(PlanRecipeId);

                    } catch {}

                }

                // Hotkey toggle tampilan menu (Ctrl + 3 + 1)

                CheckMenuToggleHotkey();

                // Pastikan hook event inventory aktif untuk update real-time

                TryHookInventoryEvent();

                                // Hook event serangan combat Durango untuk auto dodge

                TryHookCombatAlert();

                // Jalankan engine tempur, moving attack, speed multiplier, dino noclip, anti-stagger setiap frame

                HandleCombatDPSMaximizer();

                HandleManualCounterCancel();

                HandleAutoExitCombatOnTargetDead();

                UpdateCounterAssistEngine();

                // Loop otomatis pengambilan lahar & air
                UpdateAutoLootLava();
                UpdateAutoLootWater();

                // Deteksi panen manual player untuk Smart Learn (Hanya saat bot TIDAK aktif)
                if (!IsEnabled && !_isGathering) {
                    CapturePlayerManualGathering();
                }

                // Hotkey toggle Bot Gather
                if (Input.GetKeyDown(ToggleKey)) {
                    bool isMenuShowing = Durango.UI.InteractionMenuListWidgetBase.IsShow;
                    bool isGamePetalKey = (ToggleKey >= KeyCode.F1 && ToggleKey <= KeyCode.F6);
                    if (!isMenuShowing || !isGamePetalKey) {
                        ToggleMod();
                    }
                }

                // Hotkey toggle Bot Combat (Default: F8)
                if (Input.GetKeyDown(CombatToggleKey)) {
                    bool isMenuShowing = Durango.UI.InteractionMenuListWidgetBase.IsShow;
                    bool isGamePetalKey = (CombatToggleKey >= KeyCode.F1 && CombatToggleKey <= KeyCode.F6);
                    if (!isMenuShowing || !isGamePetalKey) {
                        ToggleCombatMod();
                    }
                }

                // Jalankan loop auto combat jika aktif

                if (IsCombatBotEnabled) {

                    UpdateCombat();

                    return; // Saat mode tempur aktif, lewati loop panen

                }

                // Update Craft Planner status secara berkala (real-time live inventory tracking)

                if (IsCraftPlanActive || PlanRecipe != null || ActiveTab == 1) {

                    if (Time.time >= _nextPlanCheckTime) {

                        _nextPlanCheckTime = Time.time + 0.15f;

                        UpdatePlanRequirements();

                    }

                }

                // JIKA BAHAN LENGKAP PADA CRAFT PLANNER -> STOP TOTAL DAN JANGAN LOOT LAGI!

                if ((IsCraftPlanActive || (ActiveTab == 1 && PlanRecipe != null)) && _allMaterialsReady) {

                    if (IsEnabled || IsCraftPlanActive) {

                        StopBotForCraftReady(string.Format("Bahan Lengkap! Siap Crafting {0}!", PlanRecipe != null ? PlanRecipe.Name : ""));

                    }

                    return;

                }

                if (!IsEnabled) return;

                float now = Time.time;

                // Bersihkan blacklist yang sudah expired

                List<GameObject> toRemove = new List<GameObject>();

                foreach (KeyValuePair<GameObject, float> kvp in _recentlyGathered) {

                    if (now > kvp.Value) toRemove.Add(kvp.Key);

                }

                for (int i = 0; i < toRemove.Count; i++) {

                    _recentlyGathered.Remove(toRemove[i]);

                }

                // Tunggu jeda animasi panen

                if (now < _gatherCooldownUntil) {

                    _statusText = "Sedang Memanen...";

                    return;

                }

                if (_isGathering) {

                    return;

                }

                PlayerBehavior player = PlayerBehavior.LocalPlayer;

                if (player == null) {

                    _statusText = "Menunggu Karakter...";

                    return;

                }

                PlayerController controller = GetPlayerController();

                if (controller == null) {

                    _statusText = "Controller tidak ditemukan";

                    return;

                }

                LocalMoveNavigator nav = GetNavigator(controller);

                // Validasi target aktif saat ini

                if (IsCraftPlanActive && (_allMaterialsReady || (GetAllMissingGatherables().Count == 0 && _currentHuntingSlot == null))) {

                    IsEnabled = false;

                    IsCraftPlanActive = false;

                    _currentTargetObj = null;

                    return;

                }

                // JIKA BELUM ADA TARGET PEMBELAJARAN (DAN BUKAN CRAFT PLAN): JANGAN BUKA NODE APAPUN!
                if (!IsCraftPlanActive && !_hasLearnedTarget) {
                    _statusText = "<color=#f1c40f><b>Menunggu Anda Memanen 1x...</b></color> (Dekati & panen objek di game)";
                    _currentTargetObj = null;
                    return;
                }

                if (_currentTargetObj == null || !_currentTargetObj.activeInHierarchy || _recentlyGathered.ContainsKey(_currentTargetObj)) {

                    if (Time.time >= _nextScanTime) {

                        _nextScanTime = Time.time + 0.35f;

                        _currentTargetObj = FindClosestTarget(player);

                        _approachTargetStartTime = Time.time;

                    }

                }

                if (_currentTargetObj != null) {

                    // Hitung jarak real-time (100 unit Durango = 1 meter)

                    _targetDistance = GetObjectDistance(player, _currentTargetObj, _currentTargetPos);

                    // Deteksi jika karakter menabrak collider objek / terhalang (stuck di dekat target)

                    bool isStuckNearTarget = false;

                    Vector3 curPPos = player.CurrentPosition;

                    if (curPPos == Vector3.zero) curPPos = player.transform.position;

                    if (Time.time - _lastPositionCheckTime > 0.5f) {

                        float movedDist = Vector3.Distance(curPPos, _lastPlayerPos);

                        _lastPlayerPos = curPPos;

                        _lastPositionCheckTime = Time.time;

                        // Terhalang collider fisik pohon/batu hanya jika jarak sudah sangat dekat (< 2.2m)

                        if (movedDist < 12f && _targetDistance < 220f && (Time.time - _approachTargetStartTime > 1.0f)) {

                            isStuckNearTarget = true;

                        }

                    }

                    // Karakter WAJIB berjalan mendekati node terlebih dahulu!

                    // Hanya buka menu & cek isi node jika jarak sudah dekat (<= 2.2m) atau stuck di collider node

                    bool hasArrived = (_targetDistance <= _currentTargetInteractDist) || isStuckNearTarget;

                    if (hasArrived) {

                        // Sudah sampai tepat di depan node -> lakukan pengecekan isi & panen

                        if (!_isGathering) {

                            if (IsCraftPlanActive && (_allMaterialsReady || (GetAllMissingGatherables().Count == 0 && _currentHuntingSlot == null))) {

                                IsEnabled = false;

                                IsCraftPlanActive = false;

                                _currentTargetObj = null;

                                return;

                            }

                            StartCoroutine(GatherRoutine(_currentTargetObj, nav, controller));

                        }

                    } else {

                        // Masih jauh: Bergerak berjalan mendekati target (dengan stop distance 100 unit = 1.0 meter)

                        // Tutup menu interaksi jika masih terbuka dari node sebelumnya

                        if (Durango.UI.InteractionMenuListWidgetBase.IsShow && !_isGathering) {

                            ForceCloseInteractionMenu();

                        }

                        float distMeters = _targetDistance / 100f;

                        _statusText = string.Format("Berjalan ke {0} ({1:F1}m)...", _targetName, distMeters);

                        if (nav != null && Time.time >= _nextMoveCallTime) {

                            _nextMoveCallTime = Time.time + 0.25f;

                            try {

                                nav.MoveToTarget(_currentTargetObj, null, 100f, true);

                            } catch {

                                nav.MoveToPosition(_currentTargetPos, null, 100f, true);

                            }

                        }

                    }

                }

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] Update error: " + ex);

            }

        }

        private float GetObjectDistance(PlayerBehavior player, GameObject target, Vector3 targetFallbackPos) {

            float d = 9999f;

            if (target != null) {

                try {

                    d = InteractionObject.GetDistance(target);

                } catch {}

            }

            if (d <= 0.001f || d >= 9000f) {

                Vector3 pPos = player.CurrentPosition;

                if (pPos == Vector3.zero) pPos = player.transform.position;

                Vector3 tPos = (target != null) ? target.transform.position : targetFallbackPos;

                float dx = pPos.x - tPos.x;

                float dz = pPos.z - tPos.z;

                d = Mathf.Sqrt(dx * dx + dz * dz);

            }

            return d;

        }

        private PlayerController GetPlayerController() {

            try {

                if (PlayerController.HasInstance()) return PlayerController.Instance();

            } catch {}

            return UnityEngine.Object.FindObjectOfType<PlayerController>();

        }

        private LocalMoveNavigator GetNavigator(PlayerController controller) {

            if (_navField != null && controller != null) {

                try {

                    return _navField.GetValue(controller) as LocalMoveNavigator;

                } catch {}

            }

            return null;

        }


    }
}
