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

using Durango.Logic.Combat;

using Shared.Battle;

namespace AutoGather {

    public enum ResourceFilterMode {

        All,        // Semua Resource

        Wood,       // Pohon / Kayu / Ranting

        Stone,      // Batu / Tambang / Mineral

        Bush,       // Semak / Daun / Buah

        Plant,      // Tanaman / Rumput / Reed

        Corpse      // Bangkai Hewan Buruan

    }

    public enum GatherTargetMode {

        Auto,       // Habiskan semua bagian sampai node bersih total

        Daun,       // Cari Daun (Leaf) di slot mana pun (F1-F6)

        Ranting,    // Cari Ranting / Cabang (Branch) di slot mana pun

        Batang,     // Cari Batang / Kayu / Tebang (Log / Wood) di slot mana pun

        Batu,       // Cari Batu / Tambang (Stone / Ore) di slot mana pun

        Rumput,     // Cari Rumput / Tanaman / Reed / Flax / Serat di slot mana pun

        Daging,     // Cari Daging / Sayat (Meat) di bangkai

        Kulit,      // Cari Kulit / Bark / Kulit Kayu

        Buah,       // Cari Buah / Berry / Harvest

        SlotF1,     // Paksa Slot F1

        SlotF2,     // Paksa Slot F2

        SlotF3,     // Paksa Slot F3

        SlotF4,     // Paksa Slot F4

        SlotF5,     // Paksa Slot F5

        SlotF6      // Paksa Slot F6

    }

    public enum MenuDisplayState {

        Normal,     // Tampilan Penuh

        Minimized,  // Tampilan Mini Bar

        Hidden      // Tersembunyi Total

    }

    public enum CombatTargetMode {

        AllNear,        // Semua Dino Dekat

        AggressiveOnly, // Musuh Agresif Saja

        LockedOnly      // Dino yang Sedang Dikunci (Target Lock)

    }

    public class SlotRequirementInfo {

        public string Name = "";

        public int Required = 0;

        public int Available = 0;

        public int Missing = 0;

        public ResourceFilterMode SuggestedFilter = ResourceFilterMode.All;

        public GatherTargetMode SuggestedTarget = GatherTargetMode.Auto;

        public string SuggestedKeyword = "";

        // Instruksi & Atribut Resmi Durango In-Game

        public int RequiredLevel = 0;

        public string MaterialName = "";        // Contoh: "Batu", "Kayu"

        public string AttributeNames = "";      // Contoh: "Potongan Besar, Potongan, Potongan Kecil", "Bilah Pisau"

        public string HowToObtain = "";         // Contoh: "• Kumpulkan Kerikil dari Kerikil." atau "• Produksi Mata Pisau Batu."

        public string SourceNodeName = "";      // Nama generator / node dunia resmi (contoh: "Kerikil", "Pohon")

        public string SourceItemName = "";      // Nama item hasil panen resmi (contoh: "Kerikil", "Cabang")

        public string SourceCollectibleId = "";

        public string SourceGeneratorId = "";

        public string SourceRecipeId = "";

        // Hierarki & Sub-Resep (Nested Crafting)

        public bool IsCraftable = false;

        public Crafting.Recipe SubRecipe = null;

        public List<SlotRequirementInfo> SubMaterials = new List<SlotRequirementInfo>();

        public bool IsSubRequirement = false;

        public string ParentName = "";

    }

    public enum EspCategory {

        RareDino,

        NormalDino,

        DinoCorpse,

        Crater,

        WaterSource,

        RareOre

    }

    public class EspTargetInfo {

        public string Name;

        public string CategoryLabel;

        public EspCategory Category;

        public Vector3 WorldPos;

        public float DistanceMeters;

        public Color TagColor;

        public int Level;

        public float HpRatio;

        public bool IsAlive;

        public bool IsLootable;

        public GameObject TargetObj;

    }

    public class AutoGatherController : MonoBehaviour {

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

        public KeyCode CombatToggleKey = KeyCode.F2;

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

        private string _learnedActionName = "";

        private string _learnedGeneratorId = "";

        private string _learnedNodeBaseName = "";

        private string _learnedNodeDisplayName = "";

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

        public bool EnableFastCrossbowReload = true; // Percepat animasi ngokang/reload Crossbow (2.0x) & pertahankan MoveLock saat reload agar tidak mencolok

        public float CrossbowReloadSpeed = 2.0f; // Multiplier kecepatan reload crossbow (2.0x)

        public bool EnableRecoveryCut = true;

        public bool EnableAntiStagger = true;

        public bool EnableAutoAimlock = true;

        public bool EnableDinoNoclip = true;

        public bool DinoNoclipOnlyInCombat = false;

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

        #region SETTINGS PERSISTENCE (AUTO-SAVE & LOAD)

        private static string _cachedSettingsFilePath = null;

        public static string GetSettingsFilePath() {

            if (!string.IsNullOrEmpty(_cachedSettingsFilePath)) {

                return _cachedSettingsFilePath;

            }

            try {

                string dir = null;

                try {

                    if (!string.IsNullOrEmpty(Application.dataPath)) {

                        dir = Path.GetDirectoryName(Application.dataPath);

                    }

                } catch {}

                if (string.IsNullOrEmpty(dir)) {

                    dir = AppDomain.CurrentDomain.BaseDirectory;

                }

                if (string.IsNullOrEmpty(dir)) {

                    dir = Directory.GetCurrentDirectory();

                }

                _cachedSettingsFilePath = Path.Combine(dir, "AutoGatherSettings.ini");

                return _cachedSettingsFilePath;

            } catch {

                return "AutoGatherSettings.ini";

            }

        }

        public void RequestSaveSettings() {

            SaveSettings(); // Simpan seketika agar tidak ada data hilang saat keluar game

        }

        public void SaveSettings() {

            if (Instance != null && Instance != this) return;

            try {

                string filePath = GetSettingsFilePath();

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("# ==================================================");

                sb.AppendLine("# XD LANDS / DURANGO - AUTOGATHER MOD SETTINGS");

                sb.AppendLine("# File ini menyimpan konfigurasi mod secara otomatis.");

                sb.AppendLine("# Terakhir disimpan: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                sb.AppendLine("# ==================================================");

                sb.AppendLine();

                sb.AppendLine("[General]");

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "WindowPosX={0:F0}", _windowPos.x));

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "WindowPosY={0:F0}", _windowPos.y));

                sb.AppendLine("MenuState=" + MenuState.ToString());

                sb.AppendLine("ActiveTab=" + ActiveTab.ToString());

                sb.AppendLine("ToggleKey=" + ToggleKey.ToString());

                sb.AppendLine("IsEnabled=" + IsEnabled.ToString().ToLower());

                sb.AppendLine();

                sb.AppendLine("[Gathering]");

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "ScanRadiusMeters={0:F0}", ScanRadiusMeters));

                sb.AppendLine("CurrentFilter=" + CurrentFilter.ToString());

                sb.AppendLine("TargetMode=" + TargetMode.ToString());

                sb.AppendLine("CustomKeyword=" + (CustomKeyword ?? ""));

                sb.AppendLine("HasLearnedTarget=" + _hasLearnedTarget.ToString().ToLower());

                sb.AppendLine("LearnedActionName=" + (_learnedActionName ?? ""));

                sb.AppendLine("LearnedGeneratorId=" + (_learnedGeneratorId ?? ""));

                sb.AppendLine("LearnedNodeBaseName=" + (_learnedNodeBaseName ?? ""));

                sb.AppendLine("LearnedNodeDisplayName=" + (_learnedNodeDisplayName ?? ""));

                sb.AppendLine("LearnedFilter=" + _learnedFilter.ToString());

                sb.AppendLine();

                sb.AppendLine("[CraftPlanner]");

                sb.AppendLine("IsCraftPlanActive=" + IsCraftPlanActive.ToString().ToLower());

                sb.AppendLine("PlanRecipeId=" + (PlanRecipeId ?? ""));

                sb.AppendLine("PlanQuantity=" + PlanQuantity.ToString());

                sb.AppendLine("AutoCraftWhenReady=" + AutoCraftWhenReady.ToString().ToLower());

                sb.AppendLine("AutoEatWhenCrafting=" + AutoEatWhenCrafting.ToString().ToLower());

                sb.AppendLine();

            sb.AppendLine("[Combat]");

                sb.AppendLine("EnableHoldToAttack=" + EnableHoldToAttack.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "HoldAttackInterval={0:F2}", HoldAttackInterval));

                sb.AppendLine("EnableRecoveryCut=" + EnableRecoveryCut.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "RecoveryCutDelay={0:F2}", RecoveryCutDelay));

                sb.AppendLine("EnableAntiStagger=" + EnableAntiStagger.ToString().ToLower());

                sb.AppendLine("EnableAutoAimlock=" + EnableAutoAimlock.ToString().ToLower());

                sb.AppendLine("EnableMobileAttack=" + EnableMobileAttack.ToString().ToLower());

                sb.AppendLine("EnableFastSniperStance=" + EnableFastSniperStance.ToString().ToLower());

                sb.AppendLine("EnableFastCrossbowReload=" + EnableFastCrossbowReload.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "CrossbowReloadSpeed={0:F1}", CrossbowReloadSpeed));

                sb.AppendLine("EnableDinoNoclip=" + EnableDinoNoclip.ToString().ToLower());

                sb.AppendLine("DinoNoclipOnlyInCombat=" + DinoNoclipOnlyInCombat.ToString().ToLower());

                sb.AppendLine("EnableStealthAntiAggro=" + EnableStealthAntiAggro.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MoveSpeedMultiplier={0:F2}", MoveSpeedMultiplier));

                sb.AppendLine("LockDefaultRunAnim=" + LockDefaultRunAnim.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MountSpeedMultiplier={0:F2}", MountSpeedMultiplier));

                sb.AppendLine("UnlockAllArchipelagoIslands=" + UnlockAllArchipelagoIslands.ToString().ToLower());

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                _lastSavedFeedback = "Tersimpan (" + DateTime.Now.ToString("HH:mm:ss") + ")";

                _lastSavedFeedbackTime = Time.time;

                _settingsDirty = false;

                Debug.Log("[AutoGather] Pengaturan mod berhasil disimpan ke: " + filePath);

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] Gagal menyimpan pengaturan: " + ex.Message);

            }

        }

        public void LoadSettings() {

            try {

                string filePath = GetSettingsFilePath();

                if (!File.Exists(filePath)) {

                    Debug.Log("[AutoGather] File pengaturan " + filePath + " belum ada, membuat file dengan konfigurasi default.");

                    SaveSettings();

                    return;

                }

                string[] lines = File.ReadAllLines(filePath);

                for (int i = 0; i < lines.Length; i++) {

                    string line = lines[i].Trim();

                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;

                    int eqIdx = line.IndexOf('=');

                    if (eqIdx <= 0) continue;

                    string key = line.Substring(0, eqIdx).Trim();

                    string val = line.Substring(eqIdx + 1).Trim();

                    try {

                        switch (key) {

                            case "WindowPosX":

                                float wx;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out wx)) _windowPos.x = wx;

                                break;

                            case "WindowPosY":

                                float wy;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out wy)) _windowPos.y = wy;

                                break;

                            case "MenuState":

                                try {

                                    MenuDisplayState ms = (MenuDisplayState)Enum.Parse(typeof(MenuDisplayState), val, true);

                                    if (ms == MenuDisplayState.Hidden) {

                                        MenuState = MenuDisplayState.Normal; // Selalu tampilkan menu saat baru buka game agar user tidak mengira mod hilang/reset

                                    } else {

                                        MenuState = ms;

                                    }

                                } catch {

                                    MenuState = MenuDisplayState.Normal;

                                }

                                break;

                            case "ActiveTab":

                                int at;

                                if (int.TryParse(val, out at)) ActiveTab = at;

                                break;

                            case "ToggleKey":

                                try { ToggleKey = (KeyCode)Enum.Parse(typeof(KeyCode), val, true); } catch {}

                                break;

                            case "IsEnabled":

                                bool ie;

                                if (bool.TryParse(val, out ie)) IsEnabled = ie;

                                break;

                            case "ScanRadiusMeters":

                                float rad;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out rad)) ScanRadiusMeters = rad;

                                break;

                            case "CurrentFilter":

                                try { CurrentFilter = (ResourceFilterMode)Enum.Parse(typeof(ResourceFilterMode), val, true); } catch {}

                                break;

                            case "TargetMode":

                                try { TargetMode = (GatherTargetMode)Enum.Parse(typeof(GatherTargetMode), val, true); } catch {}

                                break;

                            case "CustomKeyword":

                                CustomKeyword = val;

                                break;

                            case "HasLearnedTarget":

                                bool hlt;

                                if (bool.TryParse(val, out hlt)) _hasLearnedTarget = hlt;

                                break;

                            case "LearnedActionName":

                                _learnedActionName = val;

                                break;

                            case "LearnedGeneratorId":

                                _learnedGeneratorId = val;

                                break;

                            case "LearnedNodeBaseName":

                                _learnedNodeBaseName = val;

                                break;

                            case "LearnedNodeDisplayName":

                                _learnedNodeDisplayName = val;

                                break;

                            case "LearnedFilter":

                                try { _learnedFilter = (ResourceFilterMode)Enum.Parse(typeof(ResourceFilterMode), val, true); } catch {}

                                break;

                            case "IsCraftPlanActive":

                                bool icpa;

                                if (bool.TryParse(val, out icpa)) IsCraftPlanActive = icpa;

                                break;

                            case "PlanRecipeId":

                                PlanRecipeId = val;

                                break;

                            case "PlanQuantity":

                                int pq;

                                if (int.TryParse(val, out pq)) PlanQuantity = pq;

                                break;

                            case "AutoCraftWhenReady":

                                bool acwr;

                                if (bool.TryParse(val, out acwr)) AutoCraftWhenReady = acwr;

                                break;

                            case "AutoEatWhenCrafting":

                                bool aewc;

                                if (bool.TryParse(val, out aewc)) AutoEatWhenCrafting = aewc;

                                break;

                            case "EnableHoldToAttack":

                                bool ehta;

                                if (bool.TryParse(val, out ehta)) EnableHoldToAttack = ehta;

                                break;

                            case "HoldAttackInterval":

                                float hai;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out hai)) HoldAttackInterval = Mathf.Clamp(hai, 0.05f, 0.5f);

                                break;

                            case "EnableRecoveryCut":

                                bool erc;

                                if (bool.TryParse(val, out erc)) EnableRecoveryCut = erc;

                                break;

                            case "RecoveryCutDelay":

                                float rcd;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out rcd)) RecoveryCutDelay = rcd;

                                break;

                            case "EnableAntiStagger":

                                bool eas;

                                if (bool.TryParse(val, out eas)) EnableAntiStagger = eas;

                                break;

                            case "EnableAutoAimlock":

                                bool eaa;

                                if (bool.TryParse(val, out eaa)) EnableAutoAimlock = eaa;

                                break;

                            case "EnableMobileAttack":

                                bool ema;

                                if (bool.TryParse(val, out ema)) EnableMobileAttack = ema;

                                break;

                            case "EnableFastSniperStance":

                                bool efss;

                                if (bool.TryParse(val, out efss)) EnableFastSniperStance = efss;

                                break;

                            case "EnableFastCrossbowReload":

                                bool efcr;

                                if (bool.TryParse(val, out efcr)) EnableFastCrossbowReload = efcr;

                                break;

                            case "CrossbowReloadSpeed":

                                float crs;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out crs)) CrossbowReloadSpeed = crs;

                                break;

                            case "EnableDinoNoclip":

                                bool edn;

                                if (bool.TryParse(val, out edn)) EnableDinoNoclip = edn;

                                break;

                            case "DinoNoclipOnlyInCombat":

                                bool dnoic;

                                if (bool.TryParse(val, out dnoic)) DinoNoclipOnlyInCombat = dnoic;

                                break;

                            case "EnableAutoConsumeHunt":

                                bool each;

                                if (bool.TryParse(val, out each)) EnableAutoConsumeHunt = each;

                                break;

                            case "EnableAutoDisengageToEat":

                                bool eadte;

                                if (bool.TryParse(val, out eadte)) EnableAutoDisengageToEat = eadte;

                                break;

                            case "EnableEmergencyAutoDisengage":

                                bool eead;

                                if (bool.TryParse(val, out eead)) EnableEmergencyAutoDisengage = eead;

                                break;

                            case "AutoConsumeStaminaThreshold":

                                float acst;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out acst)) AutoConsumeStaminaThreshold = acst;

                                break;

                            case "AutoConsumeMinStaminaAbsolute":

                                float acmsa;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out acmsa)) AutoConsumeMinStaminaAbsolute = acmsa;

                                break;

                            case "AutoConsumeHealthThreshold":

                                float acht;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out acht)) AutoConsumeHealthThreshold = acht;

                                break;

                            case "AutoConsumeHuntMinFoodEnergy":

                                float achmfe;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out achmfe)) AutoConsumeHuntMinFoodEnergy = achmfe;

                                break;

                            case "EnableStealthAntiAggro":

                                bool esaa;

                                if (bool.TryParse(val, out esaa)) EnableStealthAntiAggro = esaa;

                                break;

                            case "MoveSpeedMultiplier":

                                float msm;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out msm)) MoveSpeedMultiplier = msm;

                                break;

                            case "LockDefaultRunAnim":

                                bool ldra;

                                if (bool.TryParse(val, out ldra)) {

                                    LockDefaultRunAnim = ldra;

                                    ApplyRunAnimationLock(LockDefaultRunAnim);

                                }

                                break;

                            case "MountSpeedMultiplier":

                                float mountSm;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out mountSm)) MountSpeedMultiplier = mountSm;

                                break;

                            case "UnlockAllArchipelagoIslands":

                                bool uai;

                                if (bool.TryParse(val, out uai)) UnlockAllArchipelagoIslands = uai;

                                break;


                        }

                    } catch {}

                }

                // Terapkan efek setting yang baru dimuat

                try {

                    ApplyRunAnimationLock(LockDefaultRunAnim);

                    UpdateMoveSpeedMultiplier();

                    SyncArchipelagoUnlocker();

                    if (IsEnabled) {

                        _nextScanTime = 0f;

                        if (_hasLearnedTarget) {

                            _statusText = string.Format("Auto-Panen Aktif: Berburu '{0}' ({1})!", _learnedActionName, _learnedNodeDisplayName);

                        } else {

                            _statusText = "<color=#f1c40f><b>Menunggu Anda Memanen 1x...</b></color> (Dekati & panen objek di game)";

                        }

                    } else {

                        _statusText = string.Format("Standby (Tekan [{0}] untuk Aktifkan)", ToggleKey);

                    }

                } catch {}

                Debug.Log("[AutoGather] Pengaturan mod berhasil dimuat dari: " + filePath);

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] Gagal memuat pengaturan: " + ex.Message);

            }

        }

        #endregion

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

                // Hotkey toggle Bot Gather (Default: F1)

                if (Input.GetKeyDown(ToggleKey) && !_isGathering) {

                    if (!Durango.UI.InteractionMenuListWidgetBase.IsShow || ToggleKey != KeyCode.F1) {

                        ToggleMod();

                    }

                }

                // Hotkey toggle Bot Combat (Default: F2)

                if (Input.GetKeyDown(CombatToggleKey)) {

                    ToggleCombatMod();

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

        // ================= FITUR TEMPUR & KECEPATAN GERAK =================

        private static FieldInfo _pcModPlayerSpeedField = null;
        private static FieldInfo _pcModMountSpeedField = null;
        private static FieldInfo _baseMoveSpeedField = null;
        private static FieldInfo _lmuDisableSniperCrouchField = null;

        private void UpdateMoveSpeedMultiplier() {
            try {
                if (_pcModPlayerSpeedField == null) {
                    _pcModPlayerSpeedField = typeof(PlayerController).GetField("ModPlayerSpeed", BindingFlags.Public | BindingFlags.Static);
                }
                if (_pcModMountSpeedField == null) {
                    _pcModMountSpeedField = typeof(PlayerController).GetField("ModMountSpeed", BindingFlags.Public | BindingFlags.Static);
                }

                PlayerController pc = GetPlayerController();

                // Deteksi Stance Sniper (ranged_keep_distance)
                bool isSniper = false;
                try {
                    string curStance = XDLands.Stance.XdStance.Current;
                    if (string.Equals(curStance, "ranged_keep_distance", StringComparison.OrdinalIgnoreCase)) {
                        isSniper = true;
                    }
                } catch {}

                // Validasi Tempur (In-Battle): Karakter hanya merunduk dan terkena penalti BattleSpeed saat dalam pertempuran!
                bool isInCombat = false;
                try {
                    if (CombatSystem.HasInstance()) {
                        CombatSystem cs = CombatSystem.Instance();
                        if (cs != null && cs.CombatMode) {
                            isInCombat = true;
                        }
                    }
                } catch {}

                bool applyFastSniper = EnableFastSniperStance && isSniper && isInCombat;

                // Hitung multiplier kecepatan gerak player
                float targetPlayerSpeed = MoveSpeedMultiplier;
                if (applyFastSniper) {
                    float speedRatio = 1.5f; // Penyesuaian user: 1.5x (+50%), tidak terlalu cepat
                    targetPlayerSpeed = MoveSpeedMultiplier * speedRatio;
                }

                if (_pcModPlayerSpeedField != null) {
                    _pcModPlayerSpeedField.SetValue(null, targetPlayerSpeed);
                }
                if (_pcModMountSpeedField != null) {
                    _pcModMountSpeedField.SetValue(null, MountSpeedMultiplier);
                }

                // Sinkronkan static field DisableSniperCrouch ke LocalMotionUpdater (hanya aktif saat in-battle)
                if (_lmuDisableSniperCrouchField == null) {
                    _lmuDisableSniperCrouchField = typeof(LocalMotionUpdater).GetField("DisableSniperCrouch", BindingFlags.Public | BindingFlags.Static);
                }
                if (_lmuDisableSniperCrouchField != null) {
                    try {
                        _lmuDisableSniperCrouchField.SetValue(null, applyFastSniper);
                    } catch {}
                }

                // Reset CheatMoveSpeedMultiply ke 1.0f agar tidak menduplikasi perkalian di engine
                if (pc != null && Mathf.Abs(pc.CheatMoveSpeedMultiply - 1.0f) > 0.001f) {
                    pc.CheatMoveSpeedMultiply = 1.0f;
                }

                if (pc != null) {
                    bool isRiding = false;
                    try {
                        PlayerBehavior player = PlayerBehavior.LocalPlayer;
                        if (player != null && player.IsRiding) isRiding = true;
                    } catch {}

                    // Kunci Animasi Lari Normal (*_Run) agar tidak berubah jadi sprint (*_BattleRun)

                    if (LockDefaultRunAnim != _runAnimationLocked || (LockDefaultRunAnim && _originalRunClips.Count == 0)) {

                        ApplyRunAnimationLock(LockDefaultRunAnim);

                    }

                    // Sinkronkan static field LockDefaultRunAnim di Assembly-CSharp.dll (LocalMotionUpdater)

                    if (_lmuLockRunAnimField == null) {

                        _lmuLockRunAnimField = typeof(LocalMotionUpdater).GetField("LockDefaultRunAnim", BindingFlags.Public | BindingFlags.Static);

                    }

                    if (_lmuLockRunAnimField != null) {

                        try {

                            _lmuLockRunAnimField.SetValue(null, LockDefaultRunAnim);

                        } catch {}

                    }

                    // Sanitize _currentMotionClip pada PlayerController.MotionUpdater jika LockDefaultRunAnim aktif

                    if (LockDefaultRunAnim) {

                        try {

                            LocalMotionUpdater lmu = PlayerController.MotionUpdater;

                            if (lmu != null) {

                                if (_lmuCurrentMotionClipField == null) {

                                    _lmuCurrentMotionClipField = typeof(LocalMotionUpdater).GetField("_currentMotionClip", BindingFlags.NonPublic | BindingFlags.Instance);

                                }

                                if (_lmuCurrentMotionClipField != null) {

                                    string curClip = _lmuCurrentMotionClipField.GetValue(lmu) as string;

                                    if (!string.IsNullOrEmpty(curClip) && (curClip.Contains("BattleRun") || curClip.Contains("Walk_Bush"))) {

                                        string normalClip = curClip.Replace("BattleRun", "Run");

                                        if (normalClip.Contains("Walk_Bush")) {

                                            if (normalClip.StartsWith("Onehand_")) normalClip = "Onehand_Run";

                                            else if (normalClip.StartsWith("Twohand_")) normalClip = "Twohand_Run";

                                            else if (normalClip.StartsWith("Chainsaw_")) normalClip = "Chainsaw_Run";

                                            else normalClip = "Barehand_Run";

                                        }

                                        _lmuCurrentMotionClipField.SetValue(lmu, normalClip);

                                        if (_lmuMotionUpdatedField == null) {

                                            _lmuMotionUpdatedField = typeof(LocalMotionUpdater).GetField("_motionUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

                                        }

                                        if (_lmuMotionUpdatedField != null) {

                                            _lmuMotionUpdatedField.SetValue(lmu, true);

                                        }

                                    }

                                }

                            }

                        } catch {}

                    }

                    // Sanitize _currentMotionClip pada LocalMotionUpdater untuk Fast Sniper Stance (hanya saat in-battle)
                    if (applyFastSniper) {
                        try {
                            LocalMotionUpdater lmu = PlayerController.MotionUpdater;
                            if (lmu != null) {
                                if (_lmuCurrentMotionClipField == null) {
                                    _lmuCurrentMotionClipField = typeof(LocalMotionUpdater).GetField("_currentMotionClip", BindingFlags.NonPublic | BindingFlags.Instance);
                                }
                                if (_lmuCurrentMotionClipField != null) {
                                    string curClip = _lmuCurrentMotionClipField.GetValue(lmu) as string;
                                    if (!string.IsNullOrEmpty(curClip)) {
                                        string fastClip = curClip;
                                        if (curClip.IndexOf("Aim_Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            curClip.IndexOf("Bow_Walk", StringComparison.OrdinalIgnoreCase) >= 0) {
                                            fastClip = "Bow_BattleRun";
                                        } else if (curClip.IndexOf("Crossbow_Walk", StringComparison.OrdinalIgnoreCase) >= 0) {
                                            fastClip = "Crossbow_BattleRun";
                                        } else if (curClip.IndexOf("Bow_AimStand", StringComparison.OrdinalIgnoreCase) >= 0) {
                                            fastClip = "Bow_BattleStand";
                                        } else if (curClip.IndexOf("Crossbow_AimStand", StringComparison.OrdinalIgnoreCase) >= 0) {
                                            fastClip = "Crossbow_BattleStand";
                                        }

                                        if (fastClip != curClip) {
                                            _lmuCurrentMotionClipField.SetValue(lmu, fastClip);
                                            if (_lmuMotionUpdatedField == null) {
                                                _lmuMotionUpdatedField = typeof(LocalMotionUpdater).GetField("_motionUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
                                            }
                                            if (_lmuMotionUpdatedField != null) {
                                                _lmuMotionUpdatedField.SetValue(lmu, true);
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {}
                    }

                    // Jika sedang menunggangi pet dan speed dinaikkan, pastikan rotate speed lincah

                    if (isRiding && MountSpeedMultiplier > 1.0f) {

                        try {

                            PlayerBehavior player = PlayerBehavior.LocalPlayer;

                            if (player != null && player.Driver != null && player.Driver.Vehicle != null) {

                                VehicleBase veh = player.Driver.Vehicle;

                                FieldInfo rotField = typeof(VehicleBase).GetField("_rotateSpeed", BindingFlags.NonPublic | BindingFlags.Instance);

                                if (rotField != null) {

                                    float baseRot = (float)rotField.GetValue(veh);

                                    if (baseRot < 360f) {

                                        rotField.SetValue(veh, 360f);

                                    }

                                }

                            }

                        } catch {}

                    }

                }

            } catch {}

        }

        private void ApplyRunAnimationLock(bool enable) {

            try {

                PlayerAnimationClipManager mgr = null;

                try { mgr = Durango.Utils.Singleton<PlayerAnimationClipManager>.Instance(); } catch {}

                if (mgr == null) {

                    try { mgr = UnityEngine.Object.FindObjectOfType<PlayerAnimationClipManager>(); } catch {}

                }

                if (mgr != null) {

                    Durango.Player.Animation.PlayerAnimationStateInfo s = mgr.GetPlayerAnimationStateInfo("Run");

                    if (s != null && s.Clips != null) {

                        for (int c = 0; c < s.Clips.Count; c++) {

                            Durango.Player.Animation.PlayerAnimationStateClip clip = s.Clips[c];

                            if (clip == null || string.IsNullOrEmpty(clip.Clip)) continue;

                            if (!_originalRunClips.ContainsKey(clip)) {

                                _originalRunClips[clip] = clip.Clip;

                            }

                            if (enable) {

                                string orig = _originalRunClips[clip];

                                if (orig.Contains("BattleRun")) {

                                    clip.Clip = orig.Replace("BattleRun", "Run");

                                } else if (orig.Contains("Walk_Bush")) {

                                    if (orig.StartsWith("Onehand_")) clip.Clip = "Onehand_Run";

                                    else if (orig.StartsWith("Twohand_")) clip.Clip = "Twohand_Run";

                                    else if (orig.StartsWith("Chainsaw_")) clip.Clip = "Chainsaw_Run";

                                    else clip.Clip = "Barehand_Run";

                                }

                            } else {

                                clip.Clip = _originalRunClips[clip];

                            }

                        }

                    }

                }

                _runAnimationLocked = enable;

            } catch {}

        }

                private void HandleCombatDPSMaximizer() {

            try {

                // Sinkronkan Speed Multiplier (Player vs Dino) secara real-time setiap frame

                UpdateMoveSpeedMultiplier();

                CombatSystem cs = null;

                try { cs = GameSystem<CombatSystem>.Instance(); } catch {}

                if (cs == null) {

                    try { cs = UnityEngine.Object.FindObjectOfType<CombatSystem>(); } catch {}

                }

                bool inCombat = (cs != null) && cs.CombatMode;

                object ua = null;

                if (cs != null && _csUsingActionField != null) {

                    try { ua = _csUsingActionField.GetValue(cs); } catch {}

                }

                bool hasActiveAction = false;

                if (ua != null) {

                    try {

                        MethodInfo hasVal = ua.GetType().GetMethod("HasValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (hasVal != null) hasActiveAction = (bool)hasVal.Invoke(ua, null);

                    } catch {}

                }

                BattleAction curAct = null;

                if (ua != null && hasActiveAction) {

                    try {

                        if (_uaActionField == null) {

                            _uaActionField = typeof(Durango.Logic.Combat.UsingAction).GetField("_action", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        }

                        if (_uaActionField != null) curAct = (BattleAction)_uaActionField.GetValue(ua);

                    } catch {}

                }

                // Deteksi status Crossbow Action & Reload
                bool isCrossbow = IsCrossbowAction(curAct);

                if (isCrossbow && curAct != null) {

                    if (curAct != _lastCrossbowAction) {

                        _lastCrossbowAction = curAct;

                        _crossbowActionStartTime = Time.time;

                        _originalCrossbowActionLength = 1.8f;

                        _crossbowDamageTime = 0.2f;

                        try {

                            if (curAct.Data != null) {

                                if (curAct.Data.Meta != null) {

                                    _originalCrossbowActionLength = curAct.Data.Meta.ActionLength;

                                }

                                if (curAct.Data.AttackInfo != null && curAct.Data.AttackInfo.Length > 0) {

                                    int lastIdx = curAct.Data.AttackInfo.Length - 1;

                                    _crossbowDamageTime = curAct.Data.AttackInfo[lastIdx].AttackTime;

                                }

                            }

                        } catch {}

                    }

                } else if (!hasActiveAction) {

                    _lastCrossbowAction = null;

                }

                // Cek apakah aksi Crossbow sedang aktif atau dalam fase reload
                bool isCrossbowBusy = EnableFastCrossbowReload && isCrossbow && hasActiveAction;

                bool isCrossbowReloading = isCrossbowBusy && (Time.time >= _crossbowActionStartTime + _crossbowDamageTime);

                // Sinkronisasi flag DisableAttackMoveLock ke UsingAction:
                // Jangan bypass MoveLock khusus saat Crossbow sedang aktif/reload agar animasi kokang berjalan stabil tanpa sliding!
                if (_uaDisableMoveLockField != null) {

                    try { _uaDisableMoveLockField.SetValue(null, EnableMobileAttack && !isCrossbowBusy); } catch {}

                }

                // 1. Mobile Attack: Kunci karakter saat reload Crossbow, atau lepas MoveLock untuk senjata lain
                InputSystem inputSys = null;

                try { inputSys = GameSystem<InputSystem>.Instance(); } catch {}

                if (inputSys == null) inputSys = UnityEngine.Object.FindObjectOfType<InputSystem>();

                if (isCrossbowBusy) {

                    if (inputSys != null && !inputSys.MoveLock) {

                        inputSys.MoveLock = true;

                    }

                } else if (EnableMobileAttack) {

                    if (inputSys != null && inputSys.MoveLock) {

                        inputSys.MoveLock = false;

                    }

                }

                // SINKRONISASI DisableMoveCancel:
                // HANYA aktif saat karakter sedang TANGKIS / GUARD atau sedang RELOAD CROSSBOW!
                bool isGuarding = false;

                if (curAct != null && IsGuardOrDefense(curAct)) {

                    isGuarding = true;

                }

                if (_csDisableMoveCancelField != null) {

                    try { _csDisableMoveCancelField.SetValue(null, isGuarding || isCrossbowBusy); } catch {}

                }

                // ACCELERATED CROSSBOW RELOAD: Percepat animasi reload / ngokang Crossbow (2.0x) & durasi action
                if (isCrossbowReloading && ua != null) {

                    float speedMult = Mathf.Max(1.0f, CrossbowReloadSpeed);

                    try {

                        PlayerBehavior lp = PlayerBehavior.LocalPlayer;

                        if (lp != null) {

                            string clipName = lp.CurrentAnimClipName;

                            if (string.IsNullOrEmpty(clipName) && curAct != null) {

                                if (curAct.Data != null && curAct.Data.Meta != null) clipName = curAct.Data.Meta.Motion;

                                if (string.IsNullOrEmpty(clipName)) clipName = curAct.Motion;

                            }

                            if (!string.IsNullOrEmpty(clipName) && clipName.IndexOf("Crossbow", StringComparison.OrdinalIgnoreCase) >= 0) {

                                if (lp.Anim != null) {

                                    AnimationState animState = lp.Anim[clipName];

                                    if (animState != null) {

                                        animState.speed = speedMult;

                                    }

                                }

                                if (_pEquipmentAnimField == null) {

                                    _pEquipmentAnimField = typeof(PlayerBehavior).GetField("_equipmentAnim", BindingFlags.NonPublic | BindingFlags.Instance);

                                }

                                if (_pEquipmentAnimField != null) {

                                    Animation eqAnim = _pEquipmentAnimField.GetValue(lp) as Animation;

                                    if (eqAnim != null) {

                                        AnimationState eqState = eqAnim[clipName];

                                        if (eqState != null) {

                                            eqState.speed = speedMult;

                                        }

                                    }

                                }

                            }

                        }

                    } catch {}

                    if (_uaStateFinishTimeField != null) {

                        try {

                            float normalReloadDuration = Mathf.Max(0.1f, _originalCrossbowActionLength - _crossbowDamageTime);

                            float acceleratedReloadDuration = normalReloadDuration / speedMult;

                            float targetFinishTime = _crossbowActionStartTime + _crossbowDamageTime + acceleratedReloadDuration;

                            float curFinishTime = (float)_uaStateFinishTimeField.GetValue(ua);

                            if (curFinishTime > targetFinishTime) {

                                _uaStateFinishTimeField.SetValue(ua, targetFinishTime);

                            }

                        } catch {}

                    }

                }

                // MOVING ATTACK: Cancel Animasi Serang Seketika dengan Berjalan (WASD / Gerak)

                if (EnableMobileAttack && cs != null && ua != null && hasActiveAction) {

                    bool isMovingInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || 

                                        Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||

                                        Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||

                                        Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||

                                        Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f || 

                                        Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.05f;

                    int uaState = 0;

                    try {

                        MethodInfo getState = ua.GetType().GetMethod("GetState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (getState != null) uaState = Convert.ToInt32(getState.Invoke(ua, null));

                    } catch {}

                    // State 3 = Using (Packet serangan UseBattleAction sudah terkirim ke server, damage dihitung)

                    // Jika pemain menekan tombol gerak -> BATALKAN JEDA ANIMASI & JALAN SEGERA!
                    // KECUALI AKSI PERTAHANAN / TANGKIS / GUARD atau RELOAD CROSSBOW: EFEK HARUS TETAP NYALA SELAMA DURASI!

                    if (isMovingInput && uaState == 3) {

                        if (curAct != null && (IsGuardOrDefense(curAct) || isCrossbow)) {

                            // JANGAN CLEAR TANGKIS ATAU RELOAD CROSSBOW! Karakter tetap di tempat, animasi reload berjalan tuntas tanpa sliding!

                        } else {

                            try {

                                MethodInfo clearM = ua.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                if (clearM != null) clearM.Invoke(ua, null);

                            } catch {}

                            try {

                                if (PlayerController.MotionUpdater != null) {

                                    PlayerController.MotionUpdater.ClearReservedMotions();

                                    PlayerController.MotionUpdater.RefreshMotion(null, true, true);

                                }

                            } catch {}

                        }

                        PlayerController pc = null;

                        try {

                            if (PlayerController.HasInstance()) pc = PlayerController.Instance();

                        } catch {}

                        if (pc == null) pc = UnityEngine.Object.FindObjectOfType<PlayerController>();

                        if (pc != null) {

                            if (_pcStopMoveField != null) {

                                try { _pcStopMoveField.SetValue(pc, false); } catch {}

                            }

                            if (_pcManualMoveAllowedField != null) {

                                try { _pcManualMoveAllowedField.SetValue(pc, true); } catch {}

                            }

                        }

                    }

                }

                // 2. Anti-Stagger / Anti-Stun: Kebal interupsi guncangan pukulan dino

                if (_dpDisableStaggerField != null) {

                    try {

                        _dpDisableStaggerField.SetValue(null, EnableAntiStagger);

                    } catch {}

                }

                if (EnableAntiStagger) {

                    try {

                        if (cs.DamagedProcesser != null) {

                            if (_dpControllLostUntilField != null && cs.DamagedProcesser.ControllLostUntil.HasValue) {

                                _dpControllLostUntilField.SetValue(cs.DamagedProcesser, null);

                            }

                            // Netralkan Blow & KnockBack pada antrian pesan damage lokal

                            if (_dpListField != null) {

                                PlayerBehavior localP = PlayerBehavior.LocalPlayer;

                                if (localP != null) {

                                    var list = _dpListField.GetValue(cs.DamagedProcesser) as LinkedList<Messages.Damaged>;

                                    if (list != null && list.Count > 0) {

                                        var node = list.First;

                                        while (node != null) {

                                            var d = node.Value;

                                            if (d.VictimId == localP.EntityId) {

                                                d.Damage.Effects = (Shared.Battle.DamageEffects)((int)d.Damage.Effects & ~6);

                                                node.Value = d;

                                            }

                                            node = node.Next;

                                        }

                                    }

                                }

                            }

                        }

                        // Hilangkan recoil / bone flinching fisik pada karakter saat terpukul

                        PlayerBehavior lp = PlayerBehavior.LocalPlayer;

                        if (lp != null && _pBoneFlinchField != null) {

                            object bfc = _pBoneFlinchField.GetValue(lp);

                            if (bfc != null) {

                                if (_bfcDurField != null) _bfcDurField.SetValue(bfc, 0f);

                                if (_bfcDegField != null) _bfcDegField.SetValue(bfc, 0f);

                            }

                        }

                    } catch {}

                }

                // 3. Fast Animation Cancel / Turbo ASPD (Recovery Cut)

                // Memangkas waktu pemulihan animasi agar ayunan senjata berikutnya siap seketika

                if (EnableRecoveryCut && ua != null && hasActiveAction) {

                    try {

                        if (_uaActionField == null) {

                            _uaActionField = typeof(Durango.Logic.Combat.UsingAction).GetField("_action", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        }

                        BattleAction curAction = _uaActionField != null ? (BattleAction)_uaActionField.GetValue(ua) : null;

                        // JANGAN cut animasi Roll / Evade / Dodge, dan JANGAN cut animasi reload Crossbow!

                        if (curAction == null || (!IsRollOrDodge(curAction) && !IsCrossbowAction(curAction))) {

                            if (_uaStateFinishTimeField != null) {

                                float finishTime = (float)_uaStateFinishTimeField.GetValue(ua);

                                if (finishTime > Time.time + RecoveryCutDelay) {

                                    _uaStateFinishTimeField.SetValue(ua, Time.time + RecoveryCutDelay);

                                }

                            }

                        }

                    } catch {}

                }

                // Target musuh aktif

                                // Dino Noclip (Tembus Halangan Tubuh Dino saat Reposisi)

                HandleDinoNoclip(inCombat || hasActiveAction);

            } catch {}

        }

        private void HandleDinoNoclip(bool inCombat) {

            try {

                bool shouldNoclip = EnableDinoNoclip && (!DinoNoclipOnlyInCombat || inCombat);

                if (_lupaNoclipField != null) {

                    _lupaNoclipField.SetValue(null, shouldNoclip);

                }

            } catch {}

        }

        private void Unused_HandleDinoNoclip_Legacy(bool inCombat) {

            try {

                bool shouldNoclip = EnableDinoNoclip && (!DinoNoclipOnlyInCombat || inCombat);

                PlayerController pc = GetPlayerController();

                if (pc == null) return;

                if (_pcMoveOperatorField == null) {

                    _pcMoveOperatorField = typeof(PlayerController).GetField("_localMoveOperator", BindingFlags.NonPublic | BindingFlags.Instance);

                }

                if (_pcMoveOperatorField == null) return;

                object lmo = _pcMoveOperatorField.GetValue(pc);

                if (lmo == null) return;

                if (_lmoUnitPushAreaField == null) {

                    _lmoUnitPushAreaField = lmo.GetType().GetField("_unitPushArea", BindingFlags.NonPublic | BindingFlags.Instance);

                }

                if (_lmoUnitPushAreaField == null) return;

                object pushArea = _lmoUnitPushAreaField.GetValue(lmo);

                if (pushArea == null) return;

                if (_pushUnitsField == null) {

                    _pushUnitsField = pushArea.GetType().GetField("_units", BindingFlags.NonPublic | BindingFlags.Instance);

                }

                if (_pushUnitsField == null) return;

                if (_lastPushAreaInstance != pushArea) {

                    _lastPushAreaInstance = pushArea;

                    _savedPushUnits = null;

                    _emptyPushUnits = null;

                }

                IDictionary currentDict = _pushUnitsField.GetValue(pushArea) as IDictionary;

                if (shouldNoclip) {

                    if (_savedPushUnits == null) {

                        if (currentDict != null) {

                            _savedPushUnits = currentDict;

                        }

                        _emptyPushUnits = Activator.CreateInstance(_pushUnitsField.FieldType) as IDictionary;

                        _pushUnitsField.SetValue(pushArea, _emptyPushUnits);

                    } else {

                        if (currentDict != _emptyPushUnits) {

                            _savedPushUnits = currentDict;

                            if (_emptyPushUnits == null) {

                                _emptyPushUnits = Activator.CreateInstance(_pushUnitsField.FieldType) as IDictionary;

                            }

                            _emptyPushUnits.Clear();

                            _pushUnitsField.SetValue(pushArea, _emptyPushUnits);

                        } else if (_emptyPushUnits != null && _emptyPushUnits.Count > 0) {

                            if (_savedPushUnits != null) {

                                foreach (DictionaryEntry kv in _emptyPushUnits) {

                                    _savedPushUnits[kv.Key] = kv.Value;

                                }

                            }

                            _emptyPushUnits.Clear();

                        }

                    }

                    // Netralkan juga tabrakan dengan bangkai hewan sekitar (corpse)

                    try {

                        PlayerBehavior player = PlayerBehavior.LocalPlayer;

                        if (player != null) {

                            Vector3 pPos = player.CurrentPosition;

                            if (pPos == Vector3.zero) pPos = player.transform.position;

                            AnimalBehavior[] animals = UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();

                            if (animals != null) {

                                for (int i = 0; i < animals.Length; i++) {

                                    AnimalBehavior ab = animals[i];

                                    if (ab != null && !ab.IsAlive && ab.gameObject != null) {

                                        float dist = Vector3.Distance(pPos, ab.transform.position);

                                        if (dist < 1500f) { // 15 meter

                                            Component[] comps = ab.GetComponentsInChildren<Component>();

                                            if (comps != null) {

                                                for (int c = 0; c < comps.Length; c++) {

                                                    if (comps[c] != null && comps[c].GetType().Name.Contains("Collider")) {

                                                        PropertyInfo pTrig = comps[c].GetType().GetProperty("isTrigger");

                                                        if (pTrig != null) {

                                                            bool isTrig = (bool)pTrig.GetValue(comps[c], null);

                                                            if (!isTrig) pTrig.SetValue(comps[c], true, null);

                                                        }

                                                    }

                                                }

                                            }

                                        }

                                    }

                                }

                            }

                        }

                    } catch {}

                } else {

                    if (_savedPushUnits != null) {

                        _pushUnitsField.SetValue(pushArea, _savedPushUnits);

                        _savedPushUnits = null;

                        _emptyPushUnits = null;

                    }

                }

            } catch {}

        }

        // ================= STEALTH & ANTI-AGGRO SYSTEM =================

        private void HandleStealthAntiAggro() {

            try {

                PlayerBehavior player = PlayerBehavior.LocalPlayer;

                if (player == null) return;

                // Pastikan IsBushWhacking tidak dipaksa aktif saat LockDefaultRunAnim aktif agar animasi lari normal tidak terganggu

                if (LockDefaultRunAnim && player.IsBushWhacking) {

                    player.IsBushWhacking = false;

                }

            } catch {}

        }

        // ================= ARCHIPELAGO ISLAND HELPER =================

        private void SyncArchipelagoUnlocker() {

            // Deprecated / safe no-op

        }

        public void RequestInstantArchipelagoClear() {

            try {

                Durango.Logic.ArchipelagoMissionSystem ams = null;

                try { ams = GameSystem<Durango.Logic.ArchipelagoMissionSystem>.Instance(); } catch {}

                if (ams == null) ams = UnityEngine.Object.FindObjectOfType<Durango.Logic.ArchipelagoMissionSystem>();

                if (ams != null) {

                    ams.RequestRegionClear();

                    _statusText = "<color=#2ecc71><b>[SUKSES]</b></color> RequestRegionClear terkirim ke server!";

                    Debug.Log("[AutoGather] Messages.RequestArchipelagoRegionClear berhasil dikirim!");

                } else {

                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> ArchipelagoMissionSystem belum aktif.";

                }

            } catch (Exception ex) {

                _statusText = "<color=#e74c3c><b>[ERROR]</b></color> " + ex.Message;

            }

        }

        public void DrawLavaFromShore() {

            try {

                var invSys = GameSystem<InventorySystem>.Instance();

                if (invSys == null || invSys.PlayerInventory == null) {

                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Inventory belum siap.";

                    return;

                }

                var bucket = invSys.PlayerInventory.Items.Find(i => i != null && !i.IsDestroyed() && i.HasTag("lava_container"));

                if (bucket == null) {

                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Tidak ada Ember Logam (Metal Bucket) di tas!";

                    return;

                }

                var isys = GameSystem<InteractionSystem>.Instance();

                if (isys == null) {

                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> InteractionSystem belum aktif.";

                    return;

                }

                Messages.DrawLava msg = new Messages.DrawLava();

                msg.ToolItemId = bucket.Id;

                isys.Draw<Messages.DrawLava>(msg);

                _statusText = string.Format("<color=#f39c12><b>[LAVA]</b></color> Menyendok lahar dengan {0}...", bucket.Name);

            } catch (Exception ex) {

                _statusText = "<color=#e74c3c><b>[ERROR]</b></color> " + ex.Message;

            }

        }

        public void TeleportToPort() {

            try {

                MapSystem ms = null;

                try { ms = GameSystem<MapSystem>.Instance(); } catch {}

                if (ms == null) ms = UnityEngine.Object.FindObjectOfType<MapSystem>();

                if (ms != null) {

                    ms.WarpToPort();

                    _statusText = "<color=#2ecc71><b>[PORT]</b></color> Melakukan teleport ke dermaga...";

                    return;

                }

                _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> MapSystem belum aktif.";

            } catch (Exception ex) {

                _statusText = "<color=#e74c3c><b>[ERROR]</b></color> " + ex.Message;

            }

        }

        private static FieldInfo _amsCollectionField = typeof(Durango.Logic.ArchipelagoMissionSystem).GetField("_collection", BindingFlags.NonPublic | BindingFlags.Instance);

        public Durango.Logic.ArchipelagoToDoCollection GetArchipelagoCollection() {

            try {

                Durango.Logic.ArchipelagoMissionSystem ams = null;

                try { ams = GameSystem<Durango.Logic.ArchipelagoMissionSystem>.Instance(); } catch {}

                if (ams == null) ams = UnityEngine.Object.FindObjectOfType<Durango.Logic.ArchipelagoMissionSystem>();

                if (ams != null && _amsCollectionField != null) {

                    return _amsCollectionField.GetValue(ams) as Durango.Logic.ArchipelagoToDoCollection;

                }

            } catch {}

            return null;

        }

        public void SendCheatCommand(string cmd) {

            try {

                var conn = Durango.Network.Connections.Frontend;

                if (conn != null) {

                    var cheat = new Messages.Cheat();

                    cheat._Cheat = cmd;

                    conn.Send<Messages.Cheat>(cheat, false, 0);

                    _statusText = "<color=#3498db><b>[CHEAT]</b></color> Mengirim: '" + cmd + "'";

                    Debug.Log("[AutoGather] Messages.Cheat terkirim: " + cmd);

                } else {

                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Koneksi belum siap.";

                }

            } catch (Exception ex) {

                _statusText = "<color=#e74c3c><b>[ERROR CHEAT]</b></color> " + ex.Message;

            }

        }

        private bool IsGuardOrDefense(BattleAction ba) {

            if (ba == null || ba.Data == null) return false;

            string id = (ba.Data.Id ?? "").ToLower();

            if (id.Contains("guard") || id.Contains("defense") || id.Contains("block") || id.Contains("parry") || id.Contains("tangkis")) return true;

            if (ba.Data.Meta != null) {

                if (ba.Data.Meta.BattleActionType == Shared.Battle.BattleActionType.Defense) return true;

                string motion = (ba.Data.Meta.Motion ?? "").ToLower();

                if (motion.Contains("guard") || motion.Contains("defense") || motion.Contains("block") || motion.Contains("parry")) return true;

                string icon = (ba.Data.Meta.Icon ?? "").ToLower();

                if (icon.Contains("guard") || icon.Contains("defense")) return true;

            }

            return false;

        }

        private bool IsCrossbowAction(BattleAction ba) {

            if (ba == null) return false;

            try {

                if (ba.Data != null) {

                    string id = (ba.Data.Id ?? "").ToLower();

                    if (id.Contains("crossbow")) return true;

                    if (ba.Data.Meta != null) {

                        string motion = (ba.Data.Meta.Motion ?? "").ToLower();

                        if (motion.Contains("crossbow_attack") || motion.Contains("crossbow_shoot")) return true;

                    }

                }

                string actMotion = (ba.Motion ?? "").ToLower();

                if (actMotion.Contains("crossbow_attack") || actMotion.Contains("crossbow_shoot")) return true;

            } catch {}

            return false;

        }

        private bool IsRollOrDodge(BattleAction ba) {

            if (ba == null || ba.Data == null) return false;

            string id = (ba.Data.Id ?? "").ToLower();

            if (id.Contains("dodge") || id.Contains("roll") || id.Contains("evade") || id.Contains("escape")) return true;

            if (ba.Data.Meta != null) {

                if (ba.Data.Meta.BattleActionType == Shared.Battle.BattleActionType.Dodge) return true;

                if (ba.Data.Meta.BattleActionType == Shared.Battle.BattleActionType.Defense) return true;

                if (ba.Data.Meta.Slot != null && ba.Data.Meta.Slot.Id == 0) return true;

                string motion = (ba.Data.Meta.Motion ?? "").ToLower();

                if (motion.Contains("dodge") || motion.Contains("roll") || motion.Contains("evade") || motion.Contains("escape")) return true;

                string icon = (ba.Data.Meta.Icon ?? "").ToLower();

                if (icon.Contains("evade") || icon.Contains("dodge")) return true;

            }

            return false;

        }

        private bool IsBasicAttack(BattleAction ba) {

            if (ba == null || ba.Data == null) return false;

            if (IsRollOrDodge(ba)) return false;

            // Cek BattleActionType (Melee atau Range)

            if (ba.Data.Meta != null) {

                if (ba.Data.Meta.BattleActionType == Shared.Battle.BattleActionType.Melee ||

                    ba.Data.Meta.BattleActionType == Shared.Battle.BattleActionType.Range) {

                    if (ba.Data.Meta.Slot != null && ba.Data.Meta.Slot.Id == 1) return true;

                    if (ba.Cooldown <= 0.001f) return true;

                }

            }

            string id = (ba.Data.Id ?? "").ToLower();

            if (id.Contains("_default_") || id.Contains("basic") || id.Contains("normal")) {

                return true;

            }

            if (ba.Cooldown <= 0.001f && ba.Data.AttackInfo != null && ba.Data.AttackInfo.Length > 0) {

                return true;

            }

            return false;

        }

        private BattleAction GetBasicAttackAction(CombatSystem cs) {

            if (cs == null) return null;

            BattleAction[] slots = cs.ActionSlots;

            if (slots != null && slots.Length > 1) {

                BattleAction slot1 = slots[1];

                if (slot1 != null && slot1.Data != null && !IsRollOrDodge(slot1) && IsBasicAttack(slot1)) {

                    return slot1;

                }

            }

            if (slots != null) {

                for (int i = 0; i < slots.Length; i++) {

                    BattleAction ba = slots[i];

                    if (ba == null || ba.Data == null) continue;

                    if (IsRollOrDodge(ba)) continue;

                    if (IsBasicAttack(ba)) return ba;

                }

            }

            return null;

        }

        private bool IsBasicAttackButtonHeld(CombatSystem cs, BattleAction basicAction) {

            if (cs == null || basicAction == null || basicAction.Data == null) return false;

            try {

                // JANGAN PERNAH izinkan KeyCode.Space di sini! Space adalah Roll/Evade.

                Durango.UI.BattleActionButtons babs = UnityEngine.Object.FindObjectOfType<Durango.UI.BattleActionButtons>();

                if (babs != null) {

                    // 1. Cek _pressedPlayerActions pada BattleActionButtons

                    if (_babsPressedField == null) {

                        _babsPressedField = typeof(Durango.UI.BattleActionButtons).GetField("_pressedPlayerActions", BindingFlags.NonPublic | BindingFlags.Instance);

                    }

                    if (_babsPressedField != null) {

                        var pressedList = _babsPressedField.GetValue(babs) as List<Durango.UI.BattleActionButton>;

                        if (pressedList != null && pressedList.Count > 0) {

                            for (int i = 0; i < pressedList.Count; i++) {

                                Durango.UI.BattleActionButton btn = pressedList[i];

                                if (btn != null) {

                                    if (btn.Id == basicAction.Data.Id) return true;

                                    if (cs.ActionSlots != null && cs.ActionSlots.Length > 1 && cs.ActionSlots[1] != null && cs.ActionSlots[1].Data != null && btn.Id == cs.ActionSlots[1].Data.Id) {

                                        return true;

                                    }

                                }

                            }

                        }

                    }

                    // 2. Cek BattleActionButton Slot 1 secara langsung

                    if (_babsContainerField == null) {

                        _babsContainerField = typeof(Durango.UI.BattleActionButtons).GetField("_actionButtons", BindingFlags.NonPublic | BindingFlags.Instance);

                    }

                    if (_babsContainerField != null) {

                        Durango.UI.BattleActionButtonContainer container = _babsContainerField.GetValue(babs) as Durango.UI.BattleActionButtonContainer;

                        if (container != null) {

                            Durango.UI.BattleActionButton btn = container.GetActionButton(1);

                            if (btn == null) btn = container.GetActionButton(basicAction.Data.Id);

                            if (btn != null) {

                                if (_babIsPressedField == null) {

                                    _babIsPressedField = typeof(Durango.UI.BattleActionButton).GetField("_isPressed", BindingFlags.NonPublic | BindingFlags.Instance);

                                }

                                if (_babIsPressedField != null && (bool)_babIsPressedField.GetValue(btn)) {

                                    return true;

                                }

                                // Cek UIKeyBinding jika tombol memiliki hotkey (kecuali Space)

                                UIKeyBinding kb = btn.GetComponentInChildren<UIKeyBinding>();

                                if (kb != null && kb.keyCode != KeyCode.None && kb.keyCode != KeyCode.Space) {

                                    if (Input.GetKey(kb.keyCode)) return true;

                                }

                            }

                        }

                    }

                }

                // 3. Cek apakah pemain sedang klik-tahan Mouse Kiri di atas tombol serang UI

                if (Input.GetMouseButton(0)) {

                    if (UICamera.currentTouch != null && UICamera.currentTouch.pressed != null) {

                        Durango.UI.BattleActionButton bab = UICamera.currentTouch.pressed.GetComponentInParent<Durango.UI.BattleActionButton>();

                        if (bab != null) {

                            if (bab.Id == basicAction.Data.Id) return true;

                            if (cs.ActionSlots != null && cs.ActionSlots.Length > 1 && cs.ActionSlots[1] != null && cs.ActionSlots[1].Data != null && bab.Id == cs.ActionSlots[1].Data.Id) {

                                return true;

                            }

                        }

                    }

                    if (UICamera.hoveredObject != null) {

                        Durango.UI.BattleActionButton bab = UICamera.hoveredObject.GetComponentInParent<Durango.UI.BattleActionButton>();

                        if (bab != null) {

                            if (bab.Id == basicAction.Data.Id) return true;

                            if (cs.ActionSlots != null && cs.ActionSlots.Length > 1 && cs.ActionSlots[1] != null && cs.ActionSlots[1].Data != null && bab.Id == cs.ActionSlots[1].Data.Id) {

                                return true;

                            }

                        }

                    }

                }

                // 4. Cek hotkey keyboard standar untuk Slot 1 (Angka 1)

                if (Input.GetKey(KeyCode.Alpha1)) return true;

            } catch {}

            return false;

        }

        private void TryHookAttackPacket() {

            if (_isAttackPacketHooked) return;

            try {

                Durango.Logic.Combat.UsingAction.OnAttackPacket -= OnAttackPacketHandler;

                Durango.Logic.Combat.UsingAction.OnAttackPacket += OnAttackPacketHandler;

                _isAttackPacketHooked = true;

            } catch {}

        }

        private void UnhookAttackPacket() {

            if (!_isAttackPacketHooked) return;

            try {

                Durango.Logic.Combat.UsingAction.OnAttackPacket -= OnAttackPacketHandler;

            } catch {}

            _isAttackPacketHooked = false;

        }

        private void OnAttackPacketHandler(Messages.UseBattleAction msg) {

            if (!EnableTripleStrike || _isBurstingTripleStrike) return;

            try {

                CombatSystem cs = null;

                try { cs = GameSystem<CombatSystem>.Instance(); } catch {}

                if (cs == null) cs = UnityEngine.Object.FindObjectOfType<CombatSystem>();

                if (cs == null) return;

                BattleAction ba = cs.GetBattleAction(msg.ActionId);

                if (ba == null || IsRollOrDodge(ba) || !IsBasicAttack(ba)) return;

                // Paket valid basic attack! Jalankan multi-strike packet burst!

                StartCoroutine(RoutineTripleStrikeBurst(msg, TripleStrikeCount));

            } catch {}

        }

        private IEnumerator RoutineTripleStrikeBurst(Messages.UseBattleAction originalMsg, int count) {

            _isBurstingTripleStrike = true;

            try {

                var frontend = Durango.Network.Connections.Frontend;

                if (frontend != null) {

                    int extraStrikes = Mathf.Clamp(count - 1, 1, 4);

                    for (int i = 0; i < extraStrikes; i++) {

                        yield return new WaitForSeconds(0.04f); // 40ms jeda micro antar paket damage

                        if (frontend == null) yield break;

                        // Pastikan target masih hidup sebelum mengirim burst

                        CombatSystem cs = null;

                        try { cs = GameSystem<CombatSystem>.Instance(); } catch {}

                        if (cs != null && cs.Target != null && cs.Target.Value != null) {

                            if (!cs.Target.Value.IsAlive) yield break;

                        }

                        Messages.UseBattleAction extra = new Messages.UseBattleAction();

                        extra.ActionId = originalMsg.ActionId;

                        extra.StartAt = frontend.GetPredictedServerTime();

                        extra.TargetEntityId = originalMsg.TargetEntityId;

                        extra.TargetTile = originalMsg.TargetTile;

                        frontend.Send<Messages.UseBattleAction>(extra, false, 0);

                    }

                }

            } finally {

                _isBurstingTripleStrike = false;

            }

        }

        

        private void OnAttackPacketSent(Messages.UseBattleAction msg) {

            try {

                if (!EnableTripleStrike || string.IsNullOrEmpty(msg.ActionId)) return;

                string id = msg.ActionId.ToLower();

                // Pastikan Roll / Evade / Dodge TIDAK PERNAH di-burst!

                if (id.Contains("dodge") || id.Contains("roll") || id.Contains("evade") || id.Contains("escape")) return;

                if (TripleStrikeHits > 1) {

                    StartCoroutine(RoutineSendExtraHits(msg.ActionId, msg.TargetEntityId, msg.TargetTile, TripleStrikeHits - 1));

                }

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather TripleStrike] Error in OnAttackPacketSent: " + ex);

            }

        }

        private IEnumerator RoutineSendExtraHits(string actionId, string targetId, Point2? targetTile, int extraHits) {

            for (int i = 0; i < extraHits; i++) {

                yield return new WaitForSeconds(TripleStrikeInterval);

                try {

                    if (Durango.Network.Connections.Frontend != null) {

                        Messages.UseBattleAction msg = new Messages.UseBattleAction();

                        msg.ActionId = actionId;

                        msg.StartAt = Durango.Network.Connections.Frontend.GetPredictedServerTime();

                        msg.TargetEntityId = targetId;

                        msg.TargetTile = targetTile;

                        Durango.Network.Connections.Frontend.Send(msg, false, 0);

                        _tripleStrikeHitCount++;

                        Debug.Log(string.Format("[AutoGather TripleStrike] Extra hit {0}/{1} terkirim: {2} -> target {3}", i + 1, extraHits, actionId, targetId ?? "none"));

                    }

                } catch {}

            }

        }

        // ================= RADAR & ESP SCANNER (DINO & KAWAH) =================

        private void UpdateEspScan() {

            try {

                PlayerBehavior player = PlayerBehavior.LocalPlayer;

                if (player == null) return;

                Vector3 playerPos = player.CurrentPosition;

                if (playerPos == Vector3.zero) playerPos = player.transform.position;

                List<EspTargetInfo> scanned = new List<EspTargetInfo>();

                HashSet<GameObject> seenObjs = new HashSet<GameObject>();

                // 1. Scan Dinosaurus & Hewan (AnimalManager & Scene)

                if (EspShowDinos || EspShowCorpses) {

                    List<AnimalBehavior> animalList = new List<AnimalBehavior>();

                    // a. Dari AnimalManager._animals

                    try {

                        AnimalManager am = null;

                        try { am = AnimalManager.Instance(); } catch {}

                        if (am == null) am = UnityEngine.Object.FindObjectOfType<AnimalManager>();

                        if (am != null) {

                            FieldInfo f = typeof(AnimalManager).GetField("_animals", BindingFlags.NonPublic | BindingFlags.Instance);

                            if (f != null) {

                                Dictionary<string, AnimalBehavior> d = f.GetValue(am) as Dictionary<string, AnimalBehavior>;

                                if (d != null) {

                                    foreach (KeyValuePair<string, AnimalBehavior> kvp in d) {

                                        if (kvp.Value != null) animalList.Add(kvp.Value);

                                    }

                                }

                            }

                        }

                    } catch {}

                    // b. Tambahkan hewan di scene aktif

                    try {

                        AnimalBehavior[] sceneAnimals = UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();

                        if (sceneAnimals != null) {

                            for (int i = 0; i < sceneAnimals.Length; i++) {

                                if (sceneAnimals[i] != null && !animalList.Contains(sceneAnimals[i])) {

                                    animalList.Add(sceneAnimals[i]);

                                }

                            }

                        }

                    } catch {}

                    for (int i = 0; i < animalList.Count; i++) {

                        AnimalBehavior ab = animalList[i];

                        if (ab == null || ab.gameObject == null) continue;

                        if (seenObjs.Contains(ab.gameObject)) continue;

                        seenObjs.Add(ab.gameObject);

                        Vector3 pos = ab.CurrentPosition;

                        if (pos == Vector3.zero) pos = ab.transform.position;

                        float distMeters = Vector3.Distance(playerPos, pos) / 100f; // 100 unit Durango = 1 meter

                        if (distMeters > EspMaxDistance) continue;

                        string aName = ab.GetName();

                        if (string.IsNullOrEmpty(aName)) aName = ab.gameObject.name;

                        int lv = ab.Level;

                        bool isAlive = ab.IsAlive;

                        bool isLootable = ab.IsLootable;

                        if (!isAlive) {

                            if (!EspShowCorpses) continue;

                            EspTargetInfo corpse = new EspTargetInfo();

                            corpse.Name = aName;

                            corpse.Category = EspCategory.DinoCorpse;

                            corpse.CategoryLabel = isLootable ? "[Bangkai - Siap Loot]" : "[Bangkai]";

                            corpse.TagColor = new Color(0.95f, 0.77f, 0.25f); // Emas / Amber

                            corpse.WorldPos = pos;

                            corpse.DistanceMeters = distMeters;

                            corpse.Level = lv;

                            corpse.HpRatio = 0f;

                            corpse.IsAlive = false;

                            corpse.IsLootable = isLootable;

                            corpse.TargetObj = ab.gameObject;

                            scanned.Add(corpse);

                        } else {

                            if (!EspShowDinos) continue;

                            string low = aName.ToLower();

                            bool isRare = low.Contains("tyranno") || low.Contains("tarbo") || low.Contains("raptor") || 

                                          low.Contains("smilodon") || low.Contains("allosaur") || low.Contains("carnotaur") || 

                                          low.Contains("spino") || low.Contains("cerato") || low.Contains("brachio") || 

                                          low.Contains("apato") || low.Contains("stego") || low.Contains("trico") || 

                                          low.Contains("ankylosaur") || low.Contains("mammoth") || low.Contains("saber") || 

                                          low.Contains("alpha") || low.Contains("boss") || low.Contains("king") || 

                                          low.Contains("rex") || lv >= 50;

                            if (EspShowRareOnly && !isRare) continue;

                            float hp = 1f;

                            try {

                                if (ab.Life != null) hp = ab.Life.Ratio();

                            } catch {}

                            EspTargetInfo dino = new EspTargetInfo();

                            dino.Name = aName;

                            dino.WorldPos = pos;

                            dino.DistanceMeters = distMeters;

                            dino.Level = lv;

                            dino.HpRatio = hp;

                            dino.IsAlive = true;

                            dino.IsLootable = false;

                            dino.TargetObj = ab.gameObject;

                            if (isRare) {

                                dino.Category = EspCategory.RareDino;

                                dino.CategoryLabel = "[Predator/Langka]";

                                dino.TagColor = new Color(1.0f, 0.28f, 0.35f); // Merah Neon

                            } else {

                                dino.Category = EspCategory.NormalDino;

                                dino.CategoryLabel = "[Dino]";

                                dino.TagColor = new Color(0.18f, 0.84f, 0.45f); // Hijau Neon

                            }

                            scanned.Add(dino);

                        }

                    }

                }

                // 2. Scan Kawah, Geyser, Mata Air & Natural Prefab di Scene Lokal (ImmovableBase & NaturalPrefabObject)

                if (EspShowCraters || EspShowWaterSources) {

                    ImmovableBase[] immovables = UnityEngine.Object.FindObjectsOfType<ImmovableBase>();

                    if (immovables != null) {

                        for (int i = 0; i < immovables.Length; i++) {

                            ImmovableBase imm = immovables[i];

                            if (imm == null || imm.gameObject == null) continue;

                            Vector3 pos = imm.InteractionPosition;

                            if (pos == Vector3.zero) pos = imm.transform.position;

                            float distMeters = Vector3.Distance(playerPos, pos) / 100f;

                            if (distMeters > EspMaxDistance) continue;

                            string raw = ((!string.IsNullOrEmpty(imm.GetName()) ? imm.GetName() : "") + " " + imm.gameObject.name).ToLower();

                            NaturalPrefabObject npo = imm as NaturalPrefabObject;

                            if (npo != null && !string.IsNullOrEmpty(npo.PoolName)) {

                                raw += " " + npo.PoolName.ToLower();

                            }

                            // Cek Kawah (Crater)

                            bool isCrater = raw.Contains("crater") || raw.Contains("kawah") || raw.Contains("caldera");

                            if (!isCrater) {

                                try {

                                    Yaml.Constants yamlConst = Yaml.Util.Singleton<Yaml.Constants>.Instance;

                                    if (yamlConst != null && yamlConst.NaturalComponents != null) {

                                        Yaml.NaturalComponentInfo nci;

                                        if (yamlConst.NaturalComponents.TryGetValue(imm.EntityType, out nci) && nci != null) {

                                            if (nci.POIType == Shared.System.PointOfInterest.Crater) {

                                                isCrater = true;

                                            }

                                        }

                                    }

                                } catch {}

                            }

                            if (isCrater) {

                                if (!EspShowCraters) continue;

                                EspTargetInfo crater = new EspTargetInfo();

                                crater.Name = !string.IsNullOrEmpty(imm.GetName()) ? imm.GetName() : "Kawah Vulkanik";

                                crater.Category = EspCategory.Crater;

                                crater.CategoryLabel = "[Kawah / Crater]";

                                crater.TagColor = new Color(1.0f, 0.60f, 0.0f); // Oranye Vulkanik

                                crater.WorldPos = pos;

                                crater.DistanceMeters = distMeters;

                                crater.IsAlive = false;

                                crater.TargetObj = imm.gameObject;

                                scanned.Add(crater);

                                continue;

                            }

                            // Cek Mata Air Panas & Sumber Air

                            if (EspShowWaterSources) {

                                bool isHotSpring = raw.Contains("geyser") || raw.Contains("hotspring") || raw.Contains("pemandian") || 

                                                   raw.Contains("belerang") || raw.Contains("sulfur") || raw.Contains("spring");

                                bool isWater = raw.Contains("water") || raw.Contains("well") || raw.Contains("sumur") || 

                                               raw.Contains("oasis") || raw.Contains("lake") || raw.Contains("danau") || 

                                               raw.Contains("sungai") || raw.Contains("river") || raw.Contains("mata air");

                                if (isHotSpring) {

                                    EspTargetInfo spring = new EspTargetInfo();

                                    spring.Name = !string.IsNullOrEmpty(imm.GetName()) ? imm.GetName() : "Mata Air Panas/Geyser";

                                    spring.Category = EspCategory.WaterSource;

                                    spring.CategoryLabel = "[Mata Air Panas/Geyser]";

                                    spring.TagColor = new Color(0.0f, 0.85f, 0.85f); // Cyan Panas

                                    spring.WorldPos = pos;

                                    spring.DistanceMeters = distMeters;

                                    spring.IsAlive = false;

                                    spring.TargetObj = imm.gameObject;

                                    scanned.Add(spring);

                                } else if (isWater) {

                                    EspTargetInfo water = new EspTargetInfo();

                                    water.Name = !string.IsNullOrEmpty(imm.GetName()) ? imm.GetName() : "Sumber Air Alami";

                                    water.Category = EspCategory.WaterSource;

                                    water.CategoryLabel = "[Sumber Air]";

                                    water.TagColor = new Color(0.33f, 0.63f, 1.0f); // Biru Air Segar

                                    water.WorldPos = pos;

                                    water.DistanceMeters = distMeters;

                                    water.IsAlive = false;

                                    water.TargetObj = imm.gameObject;

                                    scanned.Add(water);

                                }

                            }

                        }

                    }

                }

                // 3. Scan Seluruh Pulau via MapSystem (1 MAP PENUH: Kawah, Warphole, Crack)

                if (EspShowCraters || EspShowWaterSources) {

                    try {

                        MapSystem ms = null;

                        try { ms = GameSystem<MapSystem>.Instance(); } catch {}

                        if (ms == null) ms = UnityEngine.Object.FindObjectOfType<MapSystem>();

                        if (ms != null) {

                            // a. Cek dari _exploredPOIs (Messages.ExploredPOIs)

                            FieldInfo expField = typeof(MapSystem).GetField("_exploredPOIs", BindingFlags.NonPublic | BindingFlags.Instance);

                            if (expField != null) {

                                object expObj = expField.GetValue(ms);

                                if (expObj is Messages.ExploredPOIs) {

                                    Messages.ExploredPOIs exp = (Messages.ExploredPOIs)expObj;

                                    if (exp.POIs != null) {

                                        for (int p = 0; p < exp.POIs.Length; p++) {

                                            Messages.PointOfInterest poi = exp.POIs[p];

                                            Vector3 poiWorld = Durango.Terrain.Util.TilePositionToClientPosition(poi.Tile, true);

                                            float distMeters = Vector3.Distance(playerPos, poiWorld) / 100f;

                                            if (distMeters > EspMaxDistance) continue;

                                            bool dup = false;

                                            for (int s = 0; s < scanned.Count; s++) {

                                                if (Vector3.Distance(scanned[s].WorldPos, poiWorld) < 1500f) {

                                                    dup = true;

                                                    break;

                                                }

                                            }

                                            if (dup) continue;

                                            if (poi.Type == Shared.System.PointOfInterest.Crater) {

                                                if (!EspShowCraters) continue;

                                                EspTargetInfo crater = new EspTargetInfo();

                                                crater.Name = !string.IsNullOrEmpty(poi.Title) ? poi.Title : "Kawah Vulkanik (Peta)";

                                                crater.Category = EspCategory.Crater;

                                                crater.CategoryLabel = "[Kawah / Crater]";

                                                crater.TagColor = new Color(1.0f, 0.60f, 0.0f);

                                                crater.WorldPos = poiWorld;

                                                crater.DistanceMeters = distMeters;

                                                crater.IsAlive = false;

                                                scanned.Add(crater);

                                            } else if (poi.Type == Shared.System.PointOfInterest.Warphole || poi.Type == Shared.System.PointOfInterest.CargoWarphole) {

                                                EspTargetInfo warp = new EspTargetInfo();

                                                warp.Name = !string.IsNullOrEmpty(poi.Title) ? poi.Title : "Warphole Kubah";

                                                warp.Category = EspCategory.WaterSource;

                                                warp.CategoryLabel = "[Warphole]";

                                                warp.TagColor = new Color(0.65f, 0.35f, 0.95f);

                                                warp.WorldPos = poiWorld;

                                                warp.DistanceMeters = distMeters;

                                                warp.IsAlive = false;

                                                scanned.Add(warp);

                                            } else if (poi.Type == Shared.System.PointOfInterest.Crack || poi.Type == Shared.System.PointOfInterest.Rift) {

                                                EspTargetInfo crack = new EspTargetInfo();

                                                crack.Name = !string.IsNullOrEmpty(poi.Title) ? poi.Title : "Retakan Rift";

                                                crack.Category = EspCategory.Crater;

                                                crack.CategoryLabel = "[Rift/Retakan]";

                                                crack.TagColor = new Color(0.95f, 0.40f, 0.15f);

                                                crack.WorldPos = poiWorld;

                                                crack.DistanceMeters = distMeters;

                                                crack.IsAlive = false;

                                                scanned.Add(crack);

                                            }

                                        }

                                    }

                                }

                            }

                            // b. Cek dari _poiUpdater._exploreredPOIs

                            FieldInfo puField = typeof(MapSystem).GetField("_poiUpdater", BindingFlags.NonPublic | BindingFlags.Instance);

                            if (puField != null) {

                                object pu = puField.GetValue(ms);

                                if (pu != null) {

                                    FieldInfo dictField = pu.GetType().GetField("_exploreredPOIs", BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (dictField != null) {

                                        Dictionary<Point2, Shared.System.PointOfInterest> dict = dictField.GetValue(pu) as Dictionary<Point2, Shared.System.PointOfInterest>;

                                        if (dict != null) {

                                            foreach (KeyValuePair<Point2, Shared.System.PointOfInterest> kvp in dict) {

                                                Point2 tile = kvp.Key;

                                                Shared.System.PointOfInterest pType = kvp.Value;

                                                Vector3 poiWorld = Durango.Terrain.Util.TilePositionToClientPosition(tile, true);

                                                float distMeters = Vector3.Distance(playerPos, poiWorld) / 100f;

                                                if (distMeters > EspMaxDistance) continue;

                                                bool dup = false;

                                                for (int s = 0; s < scanned.Count; s++) {

                                                    if (Vector3.Distance(scanned[s].WorldPos, poiWorld) < 1500f) {

                                                        dup = true;

                                                        break;

                                                    }

                                                }

                                                if (dup) continue;

                                                if (pType == Shared.System.PointOfInterest.Crater) {

                                                    if (!EspShowCraters) continue;

                                                    EspTargetInfo crater = new EspTargetInfo();

                                                    crater.Name = "Kawah Vulkanik (Peta)";

                                                    crater.Category = EspCategory.Crater;

                                                    crater.CategoryLabel = "[Kawah / Crater]";

                                                    crater.TagColor = new Color(1.0f, 0.60f, 0.0f);

                                                    crater.WorldPos = poiWorld;

                                                    crater.DistanceMeters = distMeters;

                                                    crater.IsAlive = false;

                                                    scanned.Add(crater);

                                                } else if (pType == Shared.System.PointOfInterest.Warphole || pType == Shared.System.PointOfInterest.CargoWarphole) {

                                                    EspTargetInfo warp = new EspTargetInfo();

                                                    warp.Name = "Warphole Kubah";

                                                    warp.Category = EspCategory.WaterSource;

                                                    warp.CategoryLabel = "[Warphole]";

                                                    warp.TagColor = new Color(0.65f, 0.35f, 0.95f);

                                                    warp.WorldPos = poiWorld;

                                                    warp.DistanceMeters = distMeters;

                                                    warp.IsAlive = false;

                                                    scanned.Add(warp);

                                                } else if (pType == Shared.System.PointOfInterest.Crack || pType == Shared.System.PointOfInterest.Rift) {

                                                    EspTargetInfo crack = new EspTargetInfo();

                                                    crack.Name = "Retakan Rift";

                                                    crack.Category = EspCategory.Crater;

                                                    crack.CategoryLabel = "[Rift/Retakan]";

                                                    crack.TagColor = new Color(0.95f, 0.40f, 0.15f);

                                                    crack.WorldPos = poiWorld;

                                                    crack.DistanceMeters = distMeters;

                                                    crack.IsAlive = false;

                                                    scanned.Add(crack);

                                                }

                                            }

                                        }

                                    }

                                }

                            }

                        }

                    } catch (Exception mex) {

                        Debug.LogWarning("[AutoGather] MapSystem POI scan error: " + mex);

                    }

                }

                // Urutkan objek dari yang paling dekat

                scanned.Sort(delegate(EspTargetInfo a, EspTargetInfo b) {

                    return a.DistanceMeters.CompareTo(b.DistanceMeters);

                });

                _espTargets = scanned;

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] UpdateEspScan error: " + ex);

            }

        }

        private void DrawInWorldEsp() {

            if (!EnableEsp) return;

            if (_espTargets == null || _espTargets.Count == 0) return;

            Camera cam = Camera.main;

            if (cam == null) cam = Camera.current;

            if (cam == null) {

                Camera[] cams = Camera.allCameras;

                if (cams != null && cams.Length > 0) cam = cams[0];

            }

            if (cam == null) return;

            PlayerBehavior player = PlayerBehavior.LocalPlayer;

            Vector3 playerPos = (player != null) ? player.CurrentPosition : cam.transform.position;

            if (playerPos == Vector3.zero && player != null) playerPos = player.transform.position;

            Vector3 camFwd = cam.transform.forward; camFwd.y = 0; camFwd.Normalize();

            Vector3 camRgt = cam.transform.right; camRgt.y = 0; camRgt.Normalize();

            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            int onScreenCount = 0;

            int edgeCount = 0;

            Color prevColor = GUI.color;

            for (int i = 0; i < _espTargets.Count; i++) {

                EspTargetInfo t = _espTargets[i];

                if (t == null) continue;

                // Cek filter

                if (t.Category == EspCategory.NormalDino && (!EspShowDinos || EspShowRareOnly)) continue;

                if (t.Category == EspCategory.RareDino && !EspShowDinos) continue;

                if (t.Category == EspCategory.DinoCorpse && !EspShowCorpses) continue;

                if (t.Category == EspCategory.Crater && !EspShowCraters) continue;

                if (t.Category == EspCategory.WaterSource && !EspShowWaterSources) continue;

                if (t.DistanceMeters > EspMaxDistance) continue;

                Vector3 worldPos = t.WorldPos;

                if (t.TargetObj != null) {

                    try {

                        worldPos = t.TargetObj.transform.position;

                    } catch {}

                }

                // Offset posisi tag sedikit di atas kepala target

                Vector3 headPos = worldPos + new Vector3(0, 1.5f, 0);

                Vector3 screenPoint = cam.WorldToScreenPoint(headPos);

                bool isOnScreen = (screenPoint.z > 0.1f) &&

                                  (screenPoint.x >= 25f && screenPoint.x <= Screen.width - 25f) &&

                                  (screenPoint.y >= 25f && screenPoint.y <= Screen.height - 25f);

                if (isOnScreen) {

                    if (onScreenCount >= 30) continue; // Batasi tag dalam layar

                    float sx = screenPoint.x;

                    float sy = Screen.height - screenPoint.y; // Inversi Y untuk GUI

                    float tagW = 145f;

                    float tagH = 34f;

                    Rect tagRect = new Rect(sx - (tagW / 2f), sy - (tagH / 2f), tagW, tagH);

                    // Gambar Snapline opsional dari bawah tengah layar

                    if (EspShowSnaplines) {

                        DrawSnapline(new Vector2(sx, sy + (tagH / 2f)), t.TagColor);

                    }

                    // Background gelap transparan

                    if (_whiteTex != null) {

                        GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

                        GUI.DrawTexture(tagRect, _whiteTex);

                        // Garis warna kategori di sisi atas

                        GUI.color = t.TagColor;

                        GUI.DrawTexture(new Rect(tagRect.x, tagRect.y, tagRect.width, 2.5f), _whiteTex);

                    }

                    // Label teks info

                    GUI.color = Color.white;

                    string distStr = t.DistanceMeters >= 1000f ? string.Format("{0:F1}km", t.DistanceMeters / 1000f) : string.Format("{0:F0}m", t.DistanceMeters);

                    string displayText = string.Format("<b>{0}</b>\n<size=9>{1}{2}</size>", 

                        t.Level > 0 ? (t.Name + " Lv." + t.Level) : t.Name,

                        distStr,

                        t.IsAlive ? string.Format(" | HP:{0:F0}%", t.HpRatio * 100f) : (!string.IsNullOrEmpty(t.CategoryLabel) ? (" | " + t.CategoryLabel) : "")

                    );

                    GUI.Label(tagRect, displayText, _espTagStyle ?? GUI.skin.label);

                    onScreenCount++;

                } else if (EnableEdgeRadar) {

                    if (edgeCount >= 20) continue; // Batasi panah radar tepi layar

                    // Top-Down Edge Radar Clamping

                    Vector3 delta = worldPos - playerPos;

                    float relX = Vector3.Dot(delta, camRgt);

                    float relY = Vector3.Dot(delta, camFwd);

                    Vector2 screenDir = new Vector2(relX, -relY).normalized;

                    if (screenDir == Vector2.zero) continue;

                    float marginX = 85f;

                    float marginY = 32f;

                    float halfW = (Screen.width / 2f) - marginX;

                    float halfH = (Screen.height / 2f) - marginY;

                    float scaleX = (screenDir.x != 0) ? (halfW / Mathf.Abs(screenDir.x)) : float.MaxValue;

                    float scaleY = (screenDir.y != 0) ? (halfH / Mathf.Abs(screenDir.y)) : float.MaxValue;

                    float scale = Mathf.Min(scaleX, scaleY);

                    Vector2 edgePos = screenCenter + (screenDir * scale);

                    // 8-Way Direction Arrow

                    float angle = Mathf.Atan2(relY, relX) * Mathf.Rad2Deg;

                    string arrow = "●";

                    if (angle >= 67.5f && angle < 112.5f) arrow = "▲";

                    else if (angle >= 22.5f && angle < 67.5f) arrow = "↗";

                    else if (angle >= -22.5f && angle < 22.5f) arrow = "▶";

                    else if (angle >= -67.5f && angle < -22.5f) arrow = "↘";

                    else if (angle >= -112.5f && angle < -67.5f) arrow = "▼";

                    else if (angle >= -157.5f && angle < -112.5f) arrow = "↙";

                    else if (angle >= 112.5f && angle < 157.5f) arrow = "↖";

                    else arrow = "◀";

                    float badgeW = 145f;

                    float badgeH = 24f;

                    Rect badgeRect = new Rect(edgePos.x - (badgeW / 2f), edgePos.y - (badgeH / 2f), badgeW, badgeH);

                    if (_whiteTex != null) {

                        GUI.color = new Color(0.06f, 0.06f, 0.10f, 0.88f);

                        GUI.DrawTexture(badgeRect, _whiteTex);

                        // Garis tepi warna indikator

                        GUI.color = t.TagColor;

                        GUI.DrawTexture(new Rect(badgeRect.x, badgeRect.y, 3f, badgeRect.height), _whiteTex);

                    }

                    GUI.color = Color.white;

                    string distBadgeStr = t.DistanceMeters >= 1000f ? string.Format("{0:F1}km", t.DistanceMeters / 1000f) : string.Format("{0:F0}m", t.DistanceMeters);

                    string shortName = t.Name;

                    if (shortName.Length > 11) shortName = shortName.Substring(0, 10) + "…";

                    string badgeText = string.Format("<color=#{0}>{1}</color> <b>{2}</b> <color=#f1c40f>{3}</color>",

                        ColorUtility.ToHtmlStringRGB(t.TagColor),

                        arrow,

                        t.Level > 0 ? (shortName + " " + t.Level) : shortName,

                        distBadgeStr

                    );

                    GUI.Label(badgeRect, badgeText, _espEdgeStyle ?? GUI.skin.label);

                    edgeCount++;

                }

            }

            GUI.color = prevColor;

        }

        private void DrawSnapline(Vector2 screenPos, Color color) {

            Vector2 bottomCenter = new Vector2(Screen.width / 2f, Screen.height);

            DrawLine(bottomCenter, screenPos, color, 1.5f);

        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float width) {

            if (_whiteTex == null) return;

            Color savedColor = GUI.color;

            Matrix4x4 savedMatrix = GUI.matrix;

            GUI.color = color;

            float angle = Vector2.Angle(end - start, Vector2.right);

            if (start.y > end.y) angle = -angle;

            GUIUtility.RotateAroundPivot(angle, start);

            GUI.DrawTexture(new Rect(start.x, start.y, (end - start).magnitude, width), _whiteTex);

            GUI.matrix = savedMatrix;

            GUI.color = savedColor;

        }

        private void NavigateToEspTarget(EspTargetInfo target) {

            if (target == null) return;

            try {

                PlayerController pc = GetPlayerController();

                LocalMoveNavigator nav = GetNavigator(pc);

                if (nav != null) {

                    _statusText = "Berjalan ke " + target.Name + "...";

                    if (target.TargetObj != null) {

                        try {

                            nav.MoveToTarget(target.TargetObj, null, 150f, true);

                            return;

                        } catch {}

                    }

                    nav.MoveToPosition(target.WorldPos, null, 150f, true);

                }

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] NavigateToEspTarget error: " + ex);

            }

        }

        // ================= CRAFT PLANNER LOGIC & HIERARKI =================

        public int CountItemsInInventory(Crafting.RecipeSlot slot) {

            if (slot == null) return 0;

            int count = 0;

            try {

                InventorySystem invSys = null;

                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}

                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys != null && invSys.PlayerItemList != null) {

                    List<Durango.Logic.Item.ItemData> items = invSys.PlayerItemList;

                    for (int i = 0; i < items.Count; i++) {

                        Durango.Logic.Item.ItemData it = items[i];

                        if (it == null) continue;

                        if (it.IsDestroyed()) continue;

                        // Periksa apakah item memenuhi syarat level slot resep

                        if (slot.RequiredLevel > 0 && it.Level < slot.RequiredLevel) {

                            continue;

                        }

                        bool match = false;

                        try {

                            match = slot.IsSuitableItem(it, false);

                        } catch {}

                        if (!match && !string.IsNullOrEmpty(slot.Name) && !string.IsNullOrEmpty(it.Name)) {

                            string iName = it.Name.Trim().ToLower();

                            string sName = slot.Name.Trim().ToLower();

                            if (iName == sName || IsItemCharacteristicMatch(sName, iName, it, slot)) {

                                match = true;

                            }

                        }

                        if (match) {

                            int itemCount = (it.ContentCount > 0) ? it.ContentCount : 1;

                            count += itemCount;

                        }

                    }

                }

            } catch {}

            return count;

        }

        // Helper untuk membedakan Tali Pengikat (Tag Kategori Karakteristik) vs Tali Murni (Item Olahan)

        private bool IsBindingTagSlot(Crafting.RecipeSlot slot) {

            if (slot == null) return false;

            string sName = (!string.IsNullOrEmpty(slot.Name) ? slot.Name : "").Trim().ToLower();

            string sId = (!string.IsNullOrEmpty(slot.Id) ? slot.Id : "").Trim().ToLower();

            // 1. Cek dari nama atau ID yang secara spesifik menyebut "pengikat", "binding", atau "ikat"

            if (sName.Contains("pengikat") || sName.Contains("binding") || sName.Contains("ikat")) {

                return true;

            }

            if (sId.Contains("pengikat") || sId.Contains("binding") || sId.Contains("ikat")) {

                return true;

            }

            // 2. Cek dari AllowedTags jika ada tag kategori binding

            if (slot.AllowedTags != null) {

                try {

                    string[] tags = slot.AllowedTags.GetIdArray();

                    if (tags != null) {

                        for (int i = 0; i < tags.Length; i++) {

                            if (tags[i] != null) {

                                string t = tags[i].ToLower();

                                if (t.Contains("binding") || t.Contains("pengikat") || t.Contains("ikat")) {

                                    return true;

                                }

                            }

                        }

                    }

                } catch {}

            }

            return false;

        }

        private bool IsPureRopeSlot(Crafting.RecipeSlot slot) {

            if (slot == null) return false;

            string sName = (!string.IsNullOrEmpty(slot.Name) ? slot.Name : "").Trim().ToLower();

            string sId = (!string.IsNullOrEmpty(slot.Id) ? slot.Id : "").Trim().ToLower();

            // Jika mengandung "pengikat" atau "binding", itu adalah slot tag kategori Tali Pengikat, BUKAN Tali murni!

            if (IsBindingTagSlot(slot)) return false;

            // Cek nama Tali murni: "tali", "tali rami", "rope", "craft_rope", dll.

            if (sName == "tali" || sName.StartsWith("tali ") || sName.EndsWith(" tali") || sName == "rope" || sName.StartsWith("rope ") || sName.EndsWith(" rope")) {

                return true;

            }

            if (sId == "tali" || sId == "rope" || sId.StartsWith("rope_") || sId.StartsWith("tali_") || sId.EndsWith("_rope") || sId.EndsWith("_tali")) {

                return true;

            }

            return false;

        }

        private bool IsItemCharacteristicMatch(string slotName, string itemName, Durango.Logic.Item.ItemData it, Crafting.RecipeSlot slot) {

            // Syarat level: item di bawah level slot TIDAK BISA digunakan untuk craft

            if (slot != null && slot.RequiredLevel > 0 && it != null && it.Level < slot.RequiredLevel) {

                return false;

            }

            // Kesetaraan Karakteristik Item Durango:

            // 1. Tongkat / Stick / Pegangan -> Cabang, Ranting, Dahan, Tongkat (JANGAN samakan dengan "batang"!)

            // Di Durango, "batang tanaman" adalah tali pengikat, sedangkan "batang pohon" adalah kayu/log tebang.

            if (slotName.Contains("tongkat") || slotName.Contains("stick") || slotName.Contains("pegangan") || slotName.Contains("handle")) {

                if (itemName.Contains("cabang") || itemName.Contains("ranting") || itemName.Contains("dahan") || 

                    itemName.Contains("tongkat")) {

                    return true;

                }

            }

            // 2. Pemisahan Tali Murni (Crafted Rope) vs Tali Pengikat (Binding Tag):

            bool isBindingSlot = (slot != null && IsBindingTagSlot(slot)) || 

                                 slotName.Contains("pengikat") || slotName.Contains("binding") || slotName.Contains("ikat");

            bool isPureTaliSlot = (slot != null && IsPureRopeSlot(slot)) || 

                                  ((slotName == "tali" || slotName.StartsWith("tali ") || slotName == "rope") && !isBindingSlot);

            if (isPureTaliSlot) {

                // KEBUTUHAN TALI MURNI (seperti Kit Perbaikan):

                // HANYA cocok jika item di tas benar-benar Tali (Rope)!

                // Buluh, rumput, batang tanaman, akar TIDAK BISA menggantikan Tali murni!

                if (itemName.Contains("tali") || itemName.Contains("rope")) {

                    return true;

                }

            } else if (isBindingSlot) {

                // KEBUTUHAN TALI PENGIKAT (Kapak Batu, Tuas, Pisau Batu):

                // Boleh dipenuhi oleh bahan mentah di alam (Buluh, Batang Tanaman, Akar, Alang, Serat, Rumput) maupun Tali olahan!

                if (itemName.Contains("tali") || itemName.Contains("buluh") || itemName.Contains("akar") || 

                    itemName.Contains("batang tanaman") || itemName.Contains("alang") || itemName.Contains("serat") || 

                    itemName.Contains("rumput") || itemName.Contains("rope")) {

                    return true;

                }

            }

            // 3. Batu Tajam / Penghalus / Pisau Batu -> Batu, Kerikil, Serpihan Batu, Flint

            if (slotName.Contains("tajam") || slotName.Contains("penghalus") || slotName.Contains("serpihan")) {

                if (itemName.Contains("batu") || itemName.Contains("kerikil") || itemName.Contains("serpihan") || 

                    itemName.Contains("flint")) {

                    return true;

                }

            }

            // 4. Bahan Bakar / Mudah Terbakar -> Ranting, Cabang, Daun, Kayu, Batang

            if (slotName.Contains("bakar") || slotName.Contains("terbakar") || slotName.Contains("fuel")) {

                if (itemName.Contains("ranting") || itemName.Contains("cabang") || itemName.Contains("daun") || 

                    itemName.Contains("kayu") || itemName.Contains("batang")) {

                    return true;

                }

            }

            // 5. Cek tag langsung dari item data jika ada

            if (it != null && it.Tags != null && slot != null && slot.AllowedTags != null) {

                try {

                    string[] allowedTagIds = slot.AllowedTags.GetIdArray();

                    if (allowedTagIds != null && allowedTagIds.Length > 0) {

                        for (int t = 0; t < it.Tags.Count; t++) {

                            if (it.Tags[t] != null && !string.IsNullOrEmpty(it.Tags[t].Id)) {

                                string tid = it.Tags[t].Id.ToLower();

                                for (int a = 0; a < allowedTagIds.Length; a++) {

                                    if (string.Equals(tid, allowedTagIds[a], StringComparison.OrdinalIgnoreCase)) {

                                        return true;

                                    }

                                }

                            }

                        }

                    }

                } catch {}

            }

            return false;

        }

        private bool IsNaturalResourceSlot(Crafting.RecipeSlot slot) {

            if (slot == null) return false;

            // PENTING: Tali murni (Rope) adalah item olahan kerajinan (Craftable), BUKAN bahan mentah alam!

            // Jangan pernah anggap Tali murni sebagai natural resource agar dicari sub-resepnya!

            if (IsPureRopeSlot(slot)) {

                return false;

            }

            // 1. Cek dari SlotSourceInfos apakah memiliki sumber Collect di alam liar

            try {

                if (slot.SlotSourceInfos != null && slot.SlotSourceInfos.Length > 0) {

                    for (int s = 0; s < slot.SlotSourceInfos.Length; s++) {

                        Yaml.SlotSourceInfo info = slot.SlotSourceInfos[s];

                        if (info != null) {

                            if (info.type == Shared.Item.SourceDescription.Collect || 

                                info.type == Shared.Item.SourceDescription.CollectAndCraft ||

                                !string.IsNullOrEmpty(info.collectible_id) ||

                                !string.IsNullOrEmpty(info.generator_id)) {

                                return true;

                            }

                        }

                    }

                }

            } catch {}

            // 2. Cek gabungan nama, ID, dan allowed tags

            string combined = "";

            if (!string.IsNullOrEmpty(slot.Name)) combined += slot.Name.ToLower() + " ";

            if (!string.IsNullOrEmpty(slot.Id)) combined += slot.Id.ToLower() + " ";

            if (slot.AllowedTags != null) {

                try {

                    string[] ids = slot.AllowedTags.GetIdArray();

                    if (ids != null) combined += string.Join(" ", ids).ToLower() + " ";

                    string tName = slot.AllowedTags.GetName();

                    if (!string.IsNullOrEmpty(tName)) combined += tName.ToLower() + " ";

                } catch {}

            }

            if (slot.SlotSourceInfos != null) {

                for (int si = 0; si < slot.SlotSourceInfos.Length; si++) {

                    Yaml.SlotSourceInfo sInfo = slot.SlotSourceInfos[si];

                    if (sInfo == null) continue;

                    if (sInfo.type == Shared.Item.SourceDescription.Collect || 

                        sInfo.type == Shared.Item.SourceDescription.CollectAndCraft) {

                        string gid = (sInfo.generator_id ?? "").ToLower();

                        string cid = (sInfo.collectible_id ?? "").ToLower();

                        string gName = GetGeneratorNameSafe(sInfo.generator_id).ToLower();

                        string cName = GetCollectibleNameSafe(sInfo.collectible_id).ToLower();

                        combined += " " + gid + " " + cid + " " + gName + " " + cName;

                    }

                }

            }

            // Tongkat / Cabang / Ranting / Batang / Dahan

            if (combined.Contains("tongkat") || combined.Contains("cabang") || combined.Contains("ranting") || 

                combined.Contains("branch") || combined.Contains("stick") || combined.Contains("twig") || 

                combined.Contains("dahan") || combined.Contains("batang") || combined.Contains("pegangan")) {

                return true;

            }

            // Batu / Kerikil / Serpihan / Flint / Mineral

            if (combined.Contains("batu") || combined.Contains("stone") || combined.Contains("rock") || 

                combined.Contains("kerikil") || combined.Contains("pebble") || combined.Contains("flint") || 

                combined.Contains("mineral") || combined.Contains("serpihan")) {

                return true;

            }

            // Daun / Pelepah / Foliage

            if (combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("pelepah") || 

                combined.Contains("frond") || combined.Contains("foliage")) {

                return true;

            }

            // Tali Pengikat (Binding) / Buluh / Alang / Rumput / Akar / Serat

            bool isBinding = IsBindingTagSlot(slot);

            if (isBinding || combined.Contains("pengikat") || combined.Contains("binding") || 

                combined.Contains("buluh") || combined.Contains("alang") || combined.Contains("rumput") || 

                combined.Contains("akar") || combined.Contains("serat") || combined.Contains("plant") || 

                combined.Contains("tanaman") || combined.Contains("flax") || combined.Contains("rami")) {

                return true;

            }

            // Kulit Kayu

            if (combined.Contains("kulit kayu") || combined.Contains("bark")) {

                return true;

            }

            return false;

        }

        private Crafting.Recipe FindRecipeForSlot(Crafting.RecipeSlot slot, RecipeSystem rs, string parentRecipeId) {

            if (slot == null) return null;

            string cacheKey = (slot.Id ?? "") + "_" + (slot.Name ?? "") + "_" + (parentRecipeId ?? "");

            Crafting.Recipe cached;

            if (_subRecipeCache.TryGetValue(cacheKey, out cached)) {

                return cached;

            }

            if (rs == null) {

                try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

                if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

            }

            if (rs == null || rs.RecipeContainer == null) return null;

            // 1. Cek dari SlotSourceInfos jika ada recipe_id langsung

            try {

                if (slot.SlotSourceInfos != null && slot.SlotSourceInfos.Length > 0) {

                    for (int s = 0; s < slot.SlotSourceInfos.Length; s++) {

                        Yaml.SlotSourceInfo info = slot.SlotSourceInfos[s];

                        if (info != null) {

                            if (!string.IsNullOrEmpty(info.recipe_id) && info.recipe_id != parentRecipeId) {

                                Crafting.Recipe r = rs.GetRecipe(info.recipe_id);

                                if (r != null) { _subRecipeCache[cacheKey] = r; return r; }

                            }

                            if (!string.IsNullOrEmpty(info.recipe_id2) && info.recipe_id2 != parentRecipeId) {

                                Crafting.Recipe r = rs.GetRecipe(info.recipe_id2);

                                if (r != null) { _subRecipeCache[cacheKey] = r; return r; }

                            }

                        }

                    }

                }

            } catch {}

            // 2. Cek apakah ada recipe dengan ID persis sama dengan slot.Id

            if (!string.IsNullOrEmpty(slot.Id) && slot.Id != parentRecipeId) {

                try {

                    Crafting.Recipe r = rs.GetRecipe(slot.Id);

                    if (r != null) { _subRecipeCache[cacheKey] = r; return r; }

                } catch {}

            }

            // 3. Search di RecipeContainer berdasarkan kemiripan nama / ID

            string targetName = !string.IsNullOrEmpty(slot.Name) ? slot.Name.Trim().ToLower() : "";

            string targetId = !string.IsNullOrEmpty(slot.Id) ? slot.Id.Trim().ToLower() : "";

            if (string.IsNullOrEmpty(targetName) && string.IsNullOrEmpty(targetId)) {

                _subRecipeCache[cacheKey] = null;

                return null;

            }

            Crafting.Recipe bestMatch = null;

            try {

                rs.RecipeContainer.EnumerateRecipes(delegate(Crafting.Recipe r) {

                    if (r == null || r.Id == parentRecipeId) return;

                    string rName = !string.IsNullOrEmpty(r.Name) ? r.Name.Trim().ToLower() : "";

                    string rId = !string.IsNullOrEmpty(r.Id) ? r.Id.Trim().ToLower() : "";

                    // Exact match name / id

                    if (rName == targetName || rId == targetName || (!string.IsNullOrEmpty(targetId) && (rId == targetId || rName == targetId))) {

                        bestMatch = r;

                        return;

                    }

                    // Tali / Rope special pairing match

                    if (bestMatch == null && (targetName == "tali" || targetName.Contains("tali") || targetId.Contains("rope") || targetId.Contains("tali"))) {

                        if (rName == "tali" || rName.StartsWith("tali ") || rId == "rope" || rId == "tali" || rId.StartsWith("rope_") || rId.StartsWith("tali_")) {

                            bestMatch = r;

                            return;

                        }

                    }

                    if (bestMatch == null && rName.Length >= 4 && targetName.Length >= 4) {

                        if (rName.Contains(targetName) || targetName.Contains(rName)) {

                            bestMatch = r;

                        }

                    }

                });

            } catch {}

            _subRecipeCache[cacheKey] = bestMatch;

            return bestMatch;

        }

        public bool AreSubMaterialsReady(SlotRequirementInfo info) {

            if (info == null || !info.IsCraftable || info.SubMaterials == null || info.SubMaterials.Count == 0) return false;

            for (int i = 0; i < info.SubMaterials.Count; i++) {

                if (info.SubMaterials[i].Missing > 0) return false;

            }

            return true;

        }

        public List<SlotRequirementInfo> GetAllMissingGatherables() {

            List<SlotRequirementInfo> list = new List<SlotRequirementInfo>();

            for (int i = 0; i < _planSlotInfos.Count; i++) {

                SlotRequirementInfo slot = _planSlotInfos[i];

                if (slot == null) continue;

                if (slot.Missing <= 0) continue; // Top-level item sudah ada di inventory!

                if (slot.IsCraftable && slot.SubMaterials != null && slot.SubMaterials.Count > 0) {

                    // Item ini dibuat dari sub-material: ambil sub-material mentah yang masih kurang

                    for (int s = 0; s < slot.SubMaterials.Count; s++) {

                        SlotRequirementInfo sub = slot.SubMaterials[s];

                        if (sub != null && sub.Missing > 0) {

                            list.Add(sub);

                        }

                    }

                } else {

                    // Item ini bahan mentah langsung (kayu, batu, dll.)

                    list.Add(slot);

                }

            }

            return list;

        }

        private void TryHookInventoryEvent() {

            if (_isInventoryHooked) return;

            try {

                InventorySystem invSys = null;

                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}

                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys != null) {

                    invSys.PlayerInventoryUpdated += OnPlayerInventoryUpdated;

                    _isInventoryHooked = true;

                }

            } catch {}

        }

        private void UnhookInventoryEvent() {

            if (!_isInventoryHooked) return;

            try {

                InventorySystem invSys = null;

                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}

                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys != null) {

                    invSys.PlayerInventoryUpdated -= OnPlayerInventoryUpdated;

                }

            } catch {}

            _isInventoryHooked = false;

        }

        private void OnPlayerInventoryUpdated() {

            if (PlanRecipe != null || IsCraftPlanActive) {

                UpdatePlanRequirements();

            }

        }

        public void ForceCloseInteractionMenu() {

            try {

                Durango.UI.InteractionMenuListWidget_PC wheel = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidget_PC>();

                if (wheel != null) {

                    try { wheel.CloseMenus(); } catch {}

                    try { wheel.Hide(); } catch {}

                    try { wheel.ClearSubMenus(); } catch {}

                    try { wheel.transform.localScale = Vector3.zero; } catch {}

                }

            } catch {}

            try {

                Durango.UI.InteractionMenuListWidgetBase wheelBase = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidgetBase>();

                if (wheelBase != null) {

                    try { wheelBase.CloseMenus(); } catch {}

                    try { wheelBase.Hide(); } catch {}

                    try { wheelBase.ClearSubMenus(); } catch {}

                    try { wheelBase.transform.localScale = Vector3.zero; } catch {}

                }

            } catch {}

            try {

                InteractionSystem isys = null;

                try { isys = GameSystem<InteractionSystem>.Instance(); } catch {}

                if (isys == null) isys = UnityEngine.Object.FindObjectOfType<InteractionSystem>();

                if (isys != null) {

                    isys.SetInteractionTarget(null);

                }

            } catch {}

        }

        public bool IsGatheringActive() {

            try {

                GatheringSystem gs = GameSystem<GatheringSystem>.Instance();

                if (gs != null && gs.IsGathering) return true;

            } catch {}

            try {

                TimerSystem ts = GameSystem<TimerSystem>.Instance();

                if (ts != null && ts.HasTimerExceptPostProcess()) return true;

            } catch {}

            try {

                PlayerBehavior player = PlayerBehavior.LocalPlayer;

                if (player != null && !string.IsNullOrEmpty(player.CurrentAnimClipName)) {

                    string clip = player.CurrentAnimClipName.ToLower();

                    if (clip.Contains("gather") || clip.Contains("chop") || clip.Contains("mine") || 

                        clip.Contains("pick") || clip.Contains("harvest") || clip.Contains("action")) {

                        return true;

                    }

                }

            } catch {}

            return false;

        }

        private void StopBotForCraftReady(string msg) {

            IsEnabled = false;

            IsCraftPlanActive = false;

            _currentTargetObj = null;

            _isGathering = false;

            _statusText = msg;

            ForceCloseInteractionMenu();

            try {

                PlayerController controller = GetPlayerController();

                LocalMoveNavigator nav = GetNavigator(controller);

                if (controller != null) controller.StopMove();

                if (nav != null) nav.Stop();

            } catch {}

        }

        private static MethodInfo _sswGetRecipeName = null;

        private static MethodInfo _sswGetCollectibleName = null;

        private static MethodInfo _sswGetGeneratorName = null;

        private static MethodInfo _sswGetPrototypeName = null;

        private static void InitSlotSourceReflection() {

            if (_sswGetRecipeName != null) return;

            try {

                Type sswType = typeof(Durango.UI.Popup.SlotSourceWidget);

                _sswGetRecipeName = sswType.GetMethod("GetRecipeName", BindingFlags.NonPublic | BindingFlags.Static);

                _sswGetCollectibleName = sswType.GetMethod("GetCollectibleName", BindingFlags.NonPublic | BindingFlags.Static);

                _sswGetGeneratorName = sswType.GetMethod("GetGeneratorName", BindingFlags.NonPublic | BindingFlags.Static);

                _sswGetPrototypeName = sswType.GetMethod("GetPrototypeName", BindingFlags.NonPublic | BindingFlags.Static);

            } catch {}

        }

        private static string CleanRichText(string input) {

            if (string.IsNullOrEmpty(input)) return "";

            string res = input;

            while (res.Contains("<ref")) {

                int start = res.IndexOf("<ref");

                int end = res.IndexOf(">", start);

                if (end >= 0) {

                    res = res.Remove(start, end - start + 1);

                } else {

                    break;

                }

            }

            res = res.Replace("</ref>", "");

            res = res.Replace("<em>", "").Replace("</em>", "");

            res = res.Replace("<b>", "").Replace("</b>", "");

            return res.Trim();

        }

        private static string GetCollectibleNameSafe(string id) {

            if (string.IsNullOrEmpty(id)) return "";

            InitSlotSourceReflection();

            try {

                if (_sswGetCollectibleName != null) {

                    object res = _sswGetCollectibleName.Invoke(null, new object[] { id });

                    if (res != null) return res.ToString();

                }

            } catch {}

            return id;

        }

        private static string GetGeneratorNameSafe(string id) {

            if (string.IsNullOrEmpty(id)) return "";

            InitSlotSourceReflection();

            try {

                if (_sswGetGeneratorName != null) {

                    object res = _sswGetGeneratorName.Invoke(null, new object[] { id });

                    if (res != null) return res.ToString();

                }

            } catch {}

            return id;

        }

        private static string GetRecipeNameSafe(string id) {

            if (string.IsNullOrEmpty(id)) return "";

            InitSlotSourceReflection();

            try {

                if (_sswGetRecipeName != null) {

                    object res = _sswGetRecipeName.Invoke(null, new object[] { id });

                    if (res != null) return res.ToString();

                }

            } catch {}

            return id;

        }

        private static string GetPrototypeNameSafe(string id, int level) {

            if (string.IsNullOrEmpty(id)) return "";

            InitSlotSourceReflection();

            try {

                if (_sswGetPrototypeName != null) {

                    object res = _sswGetPrototypeName.Invoke(null, new object[] { id, level });

                    if (res != null) return res.ToString();

                }

            } catch {}

            return id;

        }

        private static void ParseSlotSourceInstructions(Crafting.RecipeSlot slot, SlotRequirementInfo info) {

            if (slot == null || info == null || slot.SlotSourceInfos == null || slot.SlotSourceInfos.Length == 0) return;

            List<string> obtainList = new List<string>();

            for (int idx = 0; idx < slot.SlotSourceInfos.Length; idx++) {

                Yaml.SlotSourceInfo src = slot.SlotSourceInfos[idx];

                if (src == null) continue;

                try {

                    switch (src.type) {

                        case Shared.Item.SourceDescription.Collect: {

                            string cName = GetCollectibleNameSafe(src.collectible_id);

                            string gName = GetGeneratorNameSafe(src.generator_id);

                            if (!string.IsNullOrEmpty(cName) || !string.IsNullOrEmpty(gName)) {

                                string formatted = "";

                                try {

                                    formatted = L10N.T._("<em>{0}</em>?? <em>{1}</em>{1:-?} ?????.", new object[] { cName, gName });

                                    formatted = CleanRichText(formatted);

                                } catch {}

                                if (string.IsNullOrEmpty(formatted)) {

                                    formatted = string.Format("Kumpulkan {0} dari {1}.", cName, gName);

                                }

                                obtainList.Add("• " + formatted);

                                if (string.IsNullOrEmpty(info.SourceNodeName)) info.SourceNodeName = gName;

                                if (string.IsNullOrEmpty(info.SourceItemName)) info.SourceItemName = cName;

                                if (string.IsNullOrEmpty(info.SourceCollectibleId)) info.SourceCollectibleId = src.collectible_id;

                                if (string.IsNullOrEmpty(info.SourceGeneratorId)) info.SourceGeneratorId = src.generator_id;

                            }

                            break;

                        }

                        case Shared.Item.SourceDescription.Craft: {

                            string rName = GetRecipeNameSafe(src.recipe_id);

                            string rName2 = !string.IsNullOrEmpty(src.recipe_id2) ? GetRecipeNameSafe(src.recipe_id2) : null;

                            if (!string.IsNullOrEmpty(rName)) {

                                string formatted = "";

                                if (!string.IsNullOrEmpty(rName2)) {

                                    try {

                                        formatted = L10N.T._("<ref>ui://Recipe/Crafting/{1},{0}</ref> ???? <ref>ui://Recipe/Crafting/{3},{2}</ref>{2:-?} ?????.", new object[] { rName, src.recipe_id, rName2, src.recipe_id2 });

                                        formatted = CleanRichText(formatted);

                                    } catch {}

                                    if (string.IsNullOrEmpty(formatted)) formatted = string.Format("Produksi {0} atau {1}.", rName, rName2);

                                } else {

                                    try {

                                        formatted = L10N.T._("<ref>ui://Recipe/Crafting/{1},{0}</ref>{0:-??} ?????.", new object[] { rName, src.recipe_id });

                                        formatted = CleanRichText(formatted);

                                    } catch {}

                                    if (string.IsNullOrEmpty(formatted)) formatted = string.Format("Produksi {0}.", rName);

                                }

                                obtainList.Add("• " + formatted);

                                if (string.IsNullOrEmpty(info.SourceRecipeId)) info.SourceRecipeId = src.recipe_id;

                            }

                            break;

                        }

                        case Shared.Item.SourceDescription.CollectAndCraft: {

                            string cName = GetCollectibleNameSafe(src.collectible_id);

                            string gName = GetGeneratorNameSafe(src.generator_id);

                            string rName = GetRecipeNameSafe(src.recipe_id);

                            string formatted = "";

                            try {

                                formatted = L10N.T._("<em>{0}</em>?? <em>{1}</em>{1:-?} ???? <ref>ui://Recipe/Crafting/{3},{2}</ref>{2:-?} ?????.", new object[] { cName, gName, rName, src.recipe_id });

                                formatted = CleanRichText(formatted);

                            } catch {}

                            if (string.IsNullOrEmpty(formatted)) formatted = string.Format("Kumpulkan {0} dari {1} atau Produksi {2}.", cName, gName, rName);

                            obtainList.Add("• " + formatted);

                            if (string.IsNullOrEmpty(info.SourceNodeName)) info.SourceNodeName = gName;

                            if (string.IsNullOrEmpty(info.SourceItemName)) info.SourceItemName = cName;

                            if (string.IsNullOrEmpty(info.SourceCollectibleId)) info.SourceCollectibleId = src.collectible_id;

                            if (string.IsNullOrEmpty(info.SourceGeneratorId)) info.SourceGeneratorId = src.generator_id;

                            if (string.IsNullOrEmpty(info.SourceRecipeId)) info.SourceRecipeId = src.recipe_id;

                            break;

                        }

                        case Shared.Item.SourceDescription.BoxCollect: {

                            string cName = GetCollectibleNameSafe(src.collectible_id);

                            string pName = GetPrototypeNameSafe(src.prototype_id, slot.RequiredLevel);

                            string formatted = "";

                            try {

                                formatted = L10N.T._("<em>{0}</em>?? <em>{1}</em>{1:-?} ?????.", new object[] { cName, pName });

                                formatted = CleanRichText(formatted);

                            } catch {}

                            if (string.IsNullOrEmpty(formatted)) formatted = string.Format("Kumpulkan {0} dari {1}.", cName, pName);

                            obtainList.Add("• " + formatted);

                            if (string.IsNullOrEmpty(info.SourceNodeName)) info.SourceNodeName = pName;

                            if (string.IsNullOrEmpty(info.SourceItemName)) info.SourceItemName = cName;

                            break;

                        }

                        case Shared.Item.SourceDescription.Text: {

                            if (src.text != null) {

                                string t = (string)src.text;

                                if (!string.IsNullOrEmpty(t)) obtainList.Add("• " + CleanRichText(t));

                            }

                            break;

                        }

                    }

                } catch {}

            }

            if (obtainList.Count > 0) {

                info.HowToObtain = string.Join("\n", obtainList.ToArray());

            }

        }

        public bool UpdatePlanRequirements() {

            if (PlanRecipe == null) {

                _planStatusSummary = "Pilih resep untuk memulai plan";

                _planSlotInfos.Clear();

                _currentHuntingSlot = null;

                _allMaterialsReady = false;

                return false;

            }

            RecipeSystem rs = null;

            try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

            if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

            bool hasTool = false;

            List<int> rsSlotCounts = new List<int>();

            try {

                if (rs != null && PlanRecipe != null) {

                    // Gunakan quantity 1 persis seperti in-game UI RecipeInfoWidget.ShowRecipeDetailInfo

                    // agar mendapatkan hitungan bahan inventory yang 100% identik dan authoritative!

                    RecipeSystem.HasMaterials(PlanRecipe, out hasTool, rsSlotCounts, 1);

                }

            } catch {}

            _planSlotInfos.Clear();

            if (PlanRecipe.Slots != null) {

                for (int i = 0; i < PlanRecipe.Slots.Length; i++) {

                    Crafting.RecipeSlot slot = PlanRecipe.Slots[i];

                    if (slot == null) continue;

                    SlotRequirementInfo info = new SlotRequirementInfo();

                    info.Name = !string.IsNullOrEmpty(slot.Name) ? slot.Name : ("Bahan " + (i + 1));

                    info.Required = slot.Count * PlanQuantity;

                    info.RequiredLevel = slot.RequiredLevel;

                    try {

                        if (slot.AllowedMaterials != null) {

                            info.MaterialName = Durango.Logic.Item.Util.LocalizedTagRequiredMsg(slot.AllowedMaterials, false);

                        }

                    } catch {}

                    try {

                        if (slot.AllowedTags != null) {

                            info.AttributeNames = Durango.Logic.Item.Util.LocalizedTagRequiredMsg(slot.AllowedTags, false);

                        }

                    } catch {}

                    ParseSlotSourceInstructions(slot, info);

                    // 1. Ambil hitungan resmi dari RecipeSystem Durango (sesuai UI in-game), fallback ke CountItemsInInventory jika perlu

                    int rsCount = (rsSlotCounts != null && i < rsSlotCounts.Count) ? rsSlotCounts[i] : -1;

                    if (rsCount >= 0) {

                        info.Available = rsCount;

                    } else {

                        info.Available = CountItemsInInventory(slot);

                    }

                    info.Missing = Mathf.Max(0, info.Required - info.Available);

                    // Suggested target untuk slot ini jika dicari langsung di alam liar

                    ResourceFilterMode f;

                    GatherTargetMode tm;

                    string kw;

                    DetermineGatherTargetFromSlot(slot, out f, out tm, out kw);

                    info.SuggestedFilter = f;

                    info.SuggestedTarget = tm;

                    info.SuggestedKeyword = kw;

                    // 2. HIRARKI: Cek apakah bahan ini bisa langsung dipanen di alam liar (seperti Cabang/Tongkat, Batu, Daun, Buluh)

                    // Jika BISA dipanen di alam liar, PRIORITASKAN panen langsung di alam, JANGAN paksa player untuk membuat sub-recipe!

                    bool isNaturalResource = IsNaturalResourceSlot(slot);

                    if (info.Missing > 0 && !isNaturalResource) {

                        Crafting.Recipe subRecipe = FindRecipeForSlot(slot, rs, PlanRecipe.Id);

                        if (subRecipe != null && subRecipe.Slots != null && subRecipe.Slots.Length > 0) {

                            info.IsCraftable = true;

                            info.SubRecipe = subRecipe;

                            int craftsNeeded = Mathf.CeilToInt((float)info.Missing / Mathf.Max(1, subRecipe.Count));

                            List<int> subRsCounts = new List<int>();

                            bool subHasTool = false;

                            try {

                                if (rs != null && subRecipe != null) {

                                    RecipeSystem.HasMaterials(subRecipe, out subHasTool, subRsCounts, 1);

                                }

                            } catch {}

                            for (int s = 0; s < subRecipe.Slots.Length; s++) {

                                Crafting.RecipeSlot subSlot = subRecipe.Slots[s];

                                if (subSlot == null) continue;

                                SlotRequirementInfo subInfo = new SlotRequirementInfo();

                                subInfo.IsSubRequirement = true;

                                subInfo.ParentName = info.Name;

                                subInfo.Name = !string.IsNullOrEmpty(subSlot.Name) ? subSlot.Name : ("Sub " + (s + 1));

                                subInfo.Required = subSlot.Count * craftsNeeded;

                                subInfo.RequiredLevel = subSlot.RequiredLevel;

                                try {

                                    if (subSlot.AllowedMaterials != null) {

                                        subInfo.MaterialName = Durango.Logic.Item.Util.LocalizedTagRequiredMsg(subSlot.AllowedMaterials, false);

                                    }

                                } catch {}

                                try {

                                    if (subSlot.AllowedTags != null) {

                                        subInfo.AttributeNames = Durango.Logic.Item.Util.LocalizedTagRequiredMsg(subSlot.AllowedTags, false);

                                    }

                                } catch {}

                                ParseSlotSourceInstructions(subSlot, subInfo);

                                int subCount = (subRsCounts != null && s < subRsCounts.Count) ? subRsCounts[s] : -1;

                                if (subCount >= 0) {

                                    subInfo.Available = subCount;

                                } else {

                                    subInfo.Available = CountItemsInInventory(subSlot);

                                }

                                subInfo.Missing = Mathf.Max(0, subInfo.Required - subInfo.Available);

                                ResourceFilterMode subF;

                                GatherTargetMode subTm;

                                string subKw;

                                DetermineGatherTargetFromSlot(subSlot, out subF, out subTm, out subKw);

                                subInfo.SuggestedFilter = subF;

                                subInfo.SuggestedTarget = subTm;

                                subInfo.SuggestedKeyword = subKw;

                                info.SubMaterials.Add(subInfo);

                            }

                        }

                    }

                    _planSlotInfos.Add(info);

                }

            }

            // Evaluasi status keseluruhan

            List<SlotRequirementInfo> missingGatherables = GetAllMissingGatherables();

            SlotRequirementInfo firstGatherable = (missingGatherables.Count > 0) ? missingGatherables[0] : null;

            bool allTopLevelInInventory = (_planSlotInfos.Count > 0);

            for (int i = 0; i < _planSlotInfos.Count; i++) {

                if (_planSlotInfos[i].Missing > 0) {

                    allTopLevelInInventory = false;

                    break;

                }

            }

            if (allTopLevelInInventory) {

                // Semua item level atas sudah ada di inventory!

                _allMaterialsReady = true;

                _currentHuntingSlot = null;

                _planStatusSummary = string.Format("<color=#2ecc71><b>BAHAN LENGKAP ({0} x{1})! Siap Crafting!</b></color>", PlanRecipe.Name, PlanQuantity);

                if (IsCraftPlanActive || (ActiveTab == 1 && IsEnabled)) {

                    StopBotForCraftReady(string.Format("Bahan Lengkap! Siap Crafting {0} x{1}!", PlanRecipe.Name, PlanQuantity));

                    if (AutoCraftWhenReady) {

                        OpenCraftUIInGame(PlanRecipeId);

                    }

                }

                return true;

            } else if (missingGatherables.Count == 0) {

                // Semua bahan mentah sudah lengkap, tapi ada komponen craftable yang belum dibuat oleh player!

                _allMaterialsReady = false;

                _currentHuntingSlot = null;

                string readySubNames = "";

                for (int i = 0; i < _planSlotInfos.Count; i++) {

                    SlotRequirementInfo sl = _planSlotInfos[i];

                    if (sl.Missing > 0 && sl.IsCraftable && AreSubMaterialsReady(sl)) {

                        readySubNames += (readySubNames.Length > 0 ? ", " : "") + sl.Name;

                    }

                }

                _planStatusSummary = string.Format("<color=#3498db><b>Bahan Baku Lengkap! Buat: {0} terlebih dahulu</b></color>", readySubNames);

                if (IsCraftPlanActive || (ActiveTab == 1 && IsEnabled)) {

                    StopBotForCraftReady(string.Format("Bahan Mentah Lengkap! Silakan craft {0} dulu!", readySubNames));

                }

                return false;

            } else {

                // Masih ada bahan mentah yang kurang di alam liar!

                _allMaterialsReady = false;

                if (_currentHuntingSlot == null || _currentHuntingSlot.Missing <= 0) {

                    _currentHuntingSlot = firstGatherable;

                }

                List<string> missingNames = new List<string>();

                for (int m = 0; m < missingGatherables.Count && m < 3; m++) {

                    missingNames.Add(string.Format("{0} ({1})", missingGatherables[m].Name, missingGatherables[m].Missing));

                }

                string missingStr = string.Join(", ", missingNames.ToArray());

                if (missingGatherables.Count > 3) missingStr += "...";

                _planStatusSummary = string.Format("Kurang: <color=#f1c40f><b>{0}</b></color> (Mencari node terdekat)", missingStr);

                if (IsCraftPlanActive && IsEnabled && _currentHuntingSlot != null) {

                    CurrentFilter = _currentHuntingSlot.SuggestedFilter;

                    TargetMode = _currentHuntingSlot.SuggestedTarget;

                    CustomKeyword = _currentHuntingSlot.SuggestedKeyword;

                }

                return false;

            }

        }

        private void DetermineGatherTargetFromSlot(Crafting.RecipeSlot slot, out ResourceFilterMode filter, out GatherTargetMode targetMode, out string keyword) {

            filter = ResourceFilterMode.All;

            targetMode = GatherTargetMode.Auto;

            keyword = "";

            if (slot == null) return;

            string combined = "";

            if (!string.IsNullOrEmpty(slot.Name)) combined += slot.Name.ToLower() + " ";

            if (!string.IsNullOrEmpty(slot.Id)) combined += slot.Id.ToLower() + " ";

            if (slot.AllowedTags != null) {

                try {

                    string[] ids = slot.AllowedTags.GetIdArray();

                    if (ids != null) combined += string.Join(" ", ids).ToLower() + " ";

                    string tName = slot.AllowedTags.GetName();

                    if (!string.IsNullOrEmpty(tName)) combined += tName.ToLower() + " ";

                } catch {}

            }

            // 1. Tongkat / Cabang / Ranting / Dahan / Pegangan (Wood / Bush)

            // Sesuai karakteristik Durango: Kebutuhan Tongkat dapat langsung dipenuhi oleh Cabang atau Ranting!

            if (combined.Contains("tongkat") || combined.Contains("cabang") || combined.Contains("ranting") || 

                combined.Contains("branch") || combined.Contains("twig") || combined.Contains("stick") || 

                combined.Contains("dahan") || combined.Contains("pegangan") || combined.Contains("handle")) {

                filter = ResourceFilterMode.Wood;

                targetMode = GatherTargetMode.Ranting;

                return;

            }

            // 2. Batu / Mineral / Flint / Tambang / Serpihan Batu / Batu Tajam

            if (combined.Contains("batu") || combined.Contains("stone") || combined.Contains("rock") || 

                combined.Contains("flint") || combined.Contains("mineral") || combined.Contains("ore") || 

                combined.Contains("kerikil") || combined.Contains("krikil") || combined.Contains("pebble") || 

                combined.Contains("gravel") || combined.Contains("cobble") || combined.Contains("clay") || 

                combined.Contains("tanah liat") || combined.Contains("iron") || combined.Contains("besi") || 

                combined.Contains("bongkahan") || combined.Contains("crag") || combined.Contains("boulder") ||

                combined.Contains("serpihan") || combined.Contains("tajam") || combined.Contains("penghalus")) {

                filter = ResourceFilterMode.Stone;

                targetMode = GatherTargetMode.Batu;

                return;

            }

            // 3. Daun / Palm Frond / Foliage / Pelepah

            if (combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("leaves") || 

                combined.Contains("frond") || combined.Contains("foliage") || combined.Contains("pelepah")) {

                filter = ResourceFilterMode.Bush;

                targetMode = GatherTargetMode.Daun;

                return;

            }

            // 4. Tali Pengikat (Binding) / Rumput / Reed / Flax / Tanaman / Alang / Serat / Buluh / Akar

            // Sesuai karakteristik Durango: Batang tanaman / buluh memiliki karakteristik "Tali Pengikat"!

            bool isBinding = IsBindingTagSlot(slot);

            bool isPureRope = IsPureRopeSlot(slot);

            if (!isPureRope && (isBinding || combined.Contains("pengikat") || combined.Contains("buluh") || 

                combined.Contains("rumput") || combined.Contains("grass") || combined.Contains("reed") || 

                combined.Contains("flax") || combined.Contains("tanaman") || combined.Contains("plant") || 

                combined.Contains("herb") || combined.Contains("serat") || combined.Contains("fiber") || 

                combined.Contains("alang") || combined.Contains("rami") || combined.Contains("akar") || 

                combined.Contains("ikat") || combined.Contains("cord"))) {

                filter = ResourceFilterMode.Plant;

                targetMode = GatherTargetMode.Rumput;

                return;

            }

            // 5. Batang Pohon / Kayu Besar / Log / Tebang / Timber

            if (combined.Contains("batang") || combined.Contains("kayu") || combined.Contains("log") || 

                combined.Contains("wood") || combined.Contains("timber") || combined.Contains("tebang")) {

                filter = ResourceFilterMode.Wood;

                targetMode = GatherTargetMode.Batang;

                return;

            }

            // 6. Daging / Bangkai / Sayat

            if (combined.Contains("daging") || combined.Contains("meat") || combined.Contains("sayat") || combined.Contains("carve")) {

                filter = ResourceFilterMode.Corpse;

                targetMode = GatherTargetMode.Daging;

                return;

            }

            // 7. Kulit Kayu / Kulit Hewan

            if (combined.Contains("bark") || combined.Contains("kulit kayu")) {

                filter = ResourceFilterMode.Wood;

                targetMode = GatherTargetMode.Kulit;

                return;

            }

            if (combined.Contains("leather") || combined.Contains("hide") || combined.Contains("kulit")) {

                filter = ResourceFilterMode.Corpse;

                targetMode = GatherTargetMode.Kulit;

                return;

            }

            // 8. Buah / Berry / Harvest

            if (combined.Contains("buah") || combined.Contains("berry") || combined.Contains("fruit")) {

                filter = ResourceFilterMode.Bush;

                targetMode = GatherTargetMode.Buah;

                return;

            }

            // Fallback: cari kata kunci spesifik nama slot

            if (!string.IsNullOrEmpty(slot.Name)) {

                filter = ResourceFilterMode.All;

                targetMode = GatherTargetMode.Rumput;

                keyword = slot.Name.Trim().ToLower();

            }

        }

        private bool DoesWidgetMatchSlotRequirement(Durango.UI.InteractionMenuWidget_PC widget, SlotRequirementInfo slotInfo) {

            if (widget == null || slotInfo == null) return false;

            string text = GetWidgetFullText(widget);

            if (string.IsNullOrEmpty(text)) return false;

            // 0. Match langsung berdasarkan nama item hasil panen resmi Durango:

            if (!string.IsNullOrEmpty(slotInfo.SourceItemName)) {

                string si = slotInfo.SourceItemName.Trim().ToLower();

                if (text.Contains(si)) return true;

            }

            // 1. Cek berdasarkan karakteristik Tongkat / Cabang / Ranting

            string sName = (!string.IsNullOrEmpty(slotInfo.Name) ? slotInfo.Name : "").ToLower();

            bool isTongkatReq = (slotInfo.SuggestedTarget == GatherTargetMode.Ranting) || 

                                sName.Contains("tongkat") || sName.Contains("stick") || sName.Contains("cabang") || 

                                sName.Contains("ranting") || sName.Contains("pegangan") || sName.Contains("handle");

            if (isTongkatReq) {

                if (text.Contains("cabang") || text.Contains("ranting") || text.Contains("dahan") || 

                    text.Contains("branch") || text.Contains("twig") || text.Contains("stick") || 

                    text.Contains("tongkat") || text.Contains("pegangan")) {

                    return true;

                }

            }

            // 2. Cek karakteristik Tali Murni vs Tali Pengikat

            bool isBindingReq = sName.Contains("pengikat") || sName.Contains("binding") || sName.Contains("ikat");

            bool isPureTaliReq = (sName == "tali" || sName.StartsWith("tali ") || sName == "rope") && !isBindingReq;

            if (isPureTaliReq) {

                // Jika mencari item Tali murni (item jadi): hanya cocok jika widget secara eksplisit menyebut tali/rope

                if (text.Contains("tali") || text.Contains("rope")) {

                    return true;

                }

            } else if (isBindingReq || sName.Contains("buluh") || sName.Contains("serat") || sName.Contains("alang") || sName.Contains("akar")) {

                // Tali Pengikat (Binding) / Buluh / Akar / Serat:

                // Cocok dengan semua bahan pengikat alam (buluh, batang, akar, alang, serat, rumput, tali)

                if (text.Contains("buluh") || text.Contains("akar") || text.Contains("batang") || 

                    text.Contains("alang") || text.Contains("serat") || text.Contains("rumput") || 

                    text.Contains("tali") || text.Contains("ikat") || text.Contains("fiber")) {

                    return true;

                }

            }

            // 3. Cek karakteristik Kerikil / Pebble (PENTING: Jangan cocok dengan Batu Besar / Bongkahan!)

            bool isKerikilReq = sName.Contains("kerikil") || sName.Contains("pebble");

            if (isKerikilReq) {

                if (text.Contains("kerikil") || text.Contains("pebble") || text.Contains("serpihan") || 

                    text.Contains("flint") || text.Contains("tajam") || text.Contains("batu kecil") || text.Contains("batu halus")) {

                    return true;

                }

                return false;

            }

            // 4. Cek karakteristik Batu Tajam / Penghalus / Serpihan

            bool isBatuTajamReq = sName.Contains("tajam") || sName.Contains("penghalus") || sName.Contains("serpihan") || sName.Contains("blade");

            if (isBatuTajamReq) {

                if (text.Contains("batu") || text.Contains("kerikil") || text.Contains("serpihan") || 

                    text.Contains("tajam") || text.Contains("flint") || text.Contains("rock") || text.Contains("stone")) {

                    return true;

                }

            }

            // 5. Cek karakteristik Batang Kayu / Log (PENTING: Jangan cocok dengan cabang, ranting, atau daun!)

            bool isBatangReq = (slotInfo.SuggestedTarget == GatherTargetMode.Batang) || 

                               ((sName.Contains("batang") || sName.Contains("log") || sName.Contains("trunk") || sName.Contains("kayu besar")) && 

                                !sName.Contains("tanaman") && !sName.Contains("buluh") && !sName.Contains("tali"));

            if (isBatangReq) {

                if (text.Contains("cabang") || text.Contains("branch") || text.Contains("ranting") || 

                    text.Contains("twig") || text.Contains("daun") || text.Contains("leaf") || 

                    text.Contains("kulit") || text.Contains("bark") || text.Contains("buah")) {

                    return false;

                }

                if (text.Contains("batang") || text.Contains("tebang") || text.Contains("log") || 

                    text.Contains("trunk") || text.Contains("chop") || text.Contains("kayu") || text.Contains("timber")) {

                    return true;

                }

            }

            // 6. Cek mode target spesifik

            switch (slotInfo.SuggestedTarget) {

                case GatherTargetMode.Ranting:

                    return text.Contains("ranting") || text.Contains("branch") || text.Contains("twig") || 

                           text.Contains("stick") || text.Contains("cabang") || text.Contains("dahan") || 

                           text.Contains("tongkat");

                case GatherTargetMode.Daun:

                    return text.Contains("daun") || text.Contains("leaf") || text.Contains("leaves") || 

                           text.Contains("frond") || text.Contains("pelepah") || text.Contains("foliage");

                case GatherTargetMode.Batang:

                    if (text.Contains("cabang") || text.Contains("branch") || text.Contains("ranting") || 

                        text.Contains("twig") || text.Contains("daun") || text.Contains("leaf") || 

                        text.Contains("kulit") || text.Contains("bark")) return false;

                    return text.Contains("batang") || text.Contains("log") || text.Contains("trunk") || 

                           text.Contains("tebang") || text.Contains("chop") || text.Contains("timber") || text.Contains("kayu");

                case GatherTargetMode.Kulit:

                    return text.Contains("kulit") || text.Contains("bark") || text.Contains("kupas") || text.Contains("hide") || text.Contains("leather");

                case GatherTargetMode.Batu:

                    return text.Contains("batu") || text.Contains("stone") || text.Contains("rock") || 

                           text.Contains("mineral") || text.Contains("ore") || text.Contains("tambang") || 

                           text.Contains("kerikil") || text.Contains("pebble") || text.Contains("boulder") || 

                           text.Contains("flint") || text.Contains("serpihan") || text.Contains("tajam");

                case GatherTargetMode.Rumput:

                    return text.Contains("rumput") || text.Contains("grass") || text.Contains("reed") || 

                           text.Contains("flax") || text.Contains("tanaman") || text.Contains("plant") || 

                           text.Contains("herb") || text.Contains("serat") || text.Contains("fiber") || 

                           text.Contains("alang") || text.Contains("rami") || text.Contains("cabut") ||

                           text.Contains("buluh") || text.Contains("akar") || text.Contains("batang") || text.Contains("tali");

                case GatherTargetMode.Daging:

                    return text.Contains("daging") || text.Contains("meat") || text.Contains("sayat") || text.Contains("carve") || text.Contains("butcher");

                case GatherTargetMode.Buah:

                    return text.Contains("buah") || text.Contains("berry") || text.Contains("fruit");

            }

            // 5. Cek keyword khusus

            if (!string.IsNullOrEmpty(slotInfo.SuggestedKeyword) && text.Contains(slotInfo.SuggestedKeyword.ToLower())) {

                return true;

            }

            // 6. Cek nama slot langsung jika ada kecocokan kata

            if (!string.IsNullOrEmpty(slotInfo.Name)) {

                if (text.Contains(sName)) return true;

                string[] parts = sName.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

                for (int p = 0; p < parts.Length; p++) {

                    if (parts[p].Length >= 4 && text.Contains(parts[p])) {

                        return true;

                    }

                }

            }

            return false;

        }

        public void OpenCraftUIInGame(string recipeId) {

            try {

                Durango.UI.RecipeSelectorGroup rsg = UnityEngine.Object.FindObjectOfType<Durango.UI.RecipeSelectorGroup>();

                if (rsg != null && !string.IsNullOrEmpty(recipeId)) {

                    rsg.QuickOpenCraftingUI(RecipeSystem.RecipeType.Crafting, recipeId);

                }

            } catch {}

        }

        public int GetMaxCraftableCount(Crafting.Recipe recipe) {

            if (recipe == null || recipe.Slots == null || recipe.Slots.Length == 0) return 0;

            

            try {

                RecipeSystem rs = null;

                try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

                if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

                if (rs != null) {

                    bool hasTool;

                    List<int> rsSlotCounts = new List<int>();

                    RecipeSystem.HasMaterials(recipe, out hasTool, rsSlotCounts, 1);

                    if (rsSlotCounts != null && rsSlotCounts.Count == recipe.Slots.Length) {

                        int maxCanCraft = int.MaxValue;

                        for (int i = 0; i < recipe.Slots.Length; i++) {

                            Crafting.RecipeSlot slot = recipe.Slots[i];

                            if (slot == null) continue;

                            int req = Mathf.Max(1, slot.Count);

                            int avail = rsSlotCounts[i];

                            int can = avail / req;

                            if (can < maxCanCraft) maxCanCraft = can;

                        }

                        if (maxCanCraft != int.MaxValue) return Mathf.Max(0, maxCanCraft);

                    }

                }

            } catch {}

            int minPossible = int.MaxValue;

            for (int i = 0; i < recipe.Slots.Length; i++) {

                Crafting.RecipeSlot slot = recipe.Slots[i];

                if (slot == null) continue;

                int req = Mathf.Max(1, slot.Count);

                int avail = CountItemsInInventory(slot);

                int can = avail / req;

                if (can < minPossible) minPossible = can;

            }

            return (minPossible != int.MaxValue) ? Mathf.Max(0, minPossible) : 0;

        }

        public void StartDirectCraft(Crafting.Recipe recipe, int quantity) {

            if (recipe == null) return;

            if (quantity <= 0) quantity = 1;

            try {

                CraftSystem cs = null;

                try { cs = GameSystem<CraftSystem>.Instance(); } catch {}

                if (cs == null) cs = UnityEngine.Object.FindObjectOfType<CraftSystem>();

                if (cs == null) {

                    _statusText = "CraftSystem tidak ditemukan!";

                    return;

                }

                InventorySystem isys = null;

                try { isys = GameSystem<InventorySystem>.Instance(); } catch {}

                if (isys == null) isys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                CraftSlotContainer container = cs.SlotContainer;

                if (container == null) {

                    _statusText = "CraftSlotContainer tidak siap!";

                    return;

                }

                Durango.Logic.Item.Inventory playerInv = (isys != null) ? isys.PlayerInventory : null;

                container.Set(recipe, null, playerInv, null);

                container.SetQuantity(quantity);

                container.QuickFill();

                try { global::UIBase.CloseAllUI(); } catch {}

                cs.Craft();

                _statusText = string.Format("Memulai craft {0} x{1}!", recipe.Name, quantity);

            } catch (Exception ex) {

                _statusText = "Gagal memulai craft: " + ex.Message;

            }

        }

        private void CheckAndUnlockInGameCraftLimit() {

            try {

                Durango.UI.ExpectResultWidget erw = UnityEngine.Object.FindObjectOfType<Durango.UI.ExpectResultWidget>();

                if (erw != null && erw.gameObject.activeInHierarchy) {

                    if (_erwQuantitySelectorField != null && _erwCraftField != null) {

                        Durango.UI.Control.IntSelector selector = _erwQuantitySelectorField.GetValue(erw) as Durango.UI.Control.IntSelector;

                        CraftSlotContainer craft = _erwCraftField.GetValue(erw) as CraftSlotContainer;

                        if (selector != null && craft != null && craft.Recipe != null) {

                            int maxCraftable = GetMaxCraftableCount(craft.Recipe);

                            int desiredMax = Mathf.Max(999, Mathf.Max(15, maxCraftable));

                            if (selector.Max < desiredMax) {

                                selector.Max = desiredMax;

                            }

                            if (_hookedIntSelector != selector) {

                                if (_hookedIntSelector != null) {

                                    try { _hookedIntSelector.ValueChanged -= OnInGameCraftQuantityChanged; } catch {}

                                }

                                _hookedIntSelector = selector;

                                _hookedExpectResultWidget = erw;

                                _hookedIntSelector.ValueChanged += OnInGameCraftQuantityChanged;

                            }

                        }

                    }

                } else {

                    if (_hookedIntSelector != null) {

                        try { _hookedIntSelector.ValueChanged -= OnInGameCraftQuantityChanged; } catch {}

                        _hookedIntSelector = null;

                        _hookedExpectResultWidget = null;

                    }

                }

            } catch {}

        }

        private void OnInGameCraftQuantityChanged() {

            try {

                if (_hookedIntSelector != null && _hookedExpectResultWidget != null && _erwCraftField != null) {

                    CraftSlotContainer craft = _erwCraftField.GetValue(_hookedExpectResultWidget) as CraftSlotContainer;

                    if (craft != null) {

                        craft.SetQuantity(_hookedIntSelector.Value);

                        craft.QuickFill();

                        _hookedExpectResultWidget.Refresh();

                    }

                }

            } catch {}

        }

        // ================= SISTEM AUTO-KONSUMSI MAKANAN BER-ENERGI =================

        public float GetItemFoodEnergy(Durango.Logic.Item.ItemData item) {

            if (item == null || item.IsDestroyed()) return 0f;

            float maxEnergy = 0f;

            // 1. Coba GetFloatAttribute("food", "energy") atau GetFloatAttribute("energy")

            try {

                float e1 = item.GetFloatAttribute("food", "energy");

                if (e1 > maxEnergy) maxEnergy = e1;

            } catch {}

            try {

                float e2 = item.GetFloatAttribute("energy");

                if (e2 > maxEnergy) maxEnergy = e2;

            } catch {}

            // 2. Periksa langsung dictionary Nums pada _performances item

            try {

                if (item.Performances != null) {

                    for (int i = 0; i < item.Performances.Count; i++) {

                        Messages.Performance p = item.Performances[i];

                        if (p.Nums != null) {

                            float nVal;

                            if (p.Nums.TryGetValue("energy", out nVal) && nVal > maxEnergy) {

                                maxEnergy = nVal;

                            }

                            if (p.Nums.TryGetValue("energy_stored", out nVal) && nVal > maxEnergy) {

                                maxEnergy = nVal;

                            }

                        }

                    }

                }

            } catch {}

            return maxEnergy;

        }

        public bool HasEatableAttribute(Durango.Logic.Item.ItemData item) {

            if (item == null || item.IsDestroyed()) return false;

            // 1. Cek tag resmi Durango: "eatable", "drinkable", "eat_food", "edible"

            try {

                if (item.HasTag("eatable") || item.HasTag("drinkable") || item.HasTag("eat_food") || item.HasTag("edible")) {

                    return true;

                }

            } catch {}

            try {

                if (item.Tags != null) {

                    for (int i = 0; i < item.Tags.Count; i++) {

                        if (item.Tags[i] != null && !string.IsNullOrEmpty(item.Tags[i].Id)) {

                            string tid = item.Tags[i].Id.ToLower();

                            if (tid == "eatable" || tid == "drinkable" || tid == "eat_food" || tid == "edible" || tid == "tag_property_eatable") {

                                return true;

                            }

                        }

                    }

                }

            } catch {}

            // 2. Cek PerformanceData("food") resmi game

            try {

                Nullable<Messages.Performance> perf = item.GetPerformanceData("food");

                if (perf.HasValue) return true;

            } catch {}

            // 3. Cek Prototype Category ("food", "cooking", "dish")

            try {

                if (item.Prototype != null && !string.IsNullOrEmpty(item.Prototype.Category)) {

                    string cat = item.Prototype.Category.ToLower();

                    if (cat == "food" || cat == "cooking" || cat == "dish" || cat == "drink") {

                        return true;

                    }

                }

            } catch {}

            return false;

        }

        public bool IsEdibleFood(Durango.Logic.Item.ItemData item) {

            return IsEdibleFood(item, 0f);

        }

        public bool IsEdibleFood(Durango.Logic.Item.ItemData item, float minEnergy) {

            if (item == null || item.IsDestroyed()) return false;

            // 1. Wajib memiliki atribut bisa dimakan (eatable / food performance)

            if (!HasEatableAttribute(item)) return false;

            // 2. Wajib menghasilkan energi minimal jika diminta (misal >= 120 untuk kombat)

            if (minEnergy > 0f) {

                float energy = GetItemFoodEnergy(item);

                if (energy < minEnergy) return false;

            }

            return true;

        }

        public bool TryEatFoodFromInventory() {

            try {

                InventorySystem invSys = null;

                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}

                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys == null || invSys.PlayerItemList == null) return false;

                List<Durango.Logic.Item.ItemData> items = invSys.PlayerItemList;

                Durango.Logic.Item.ItemData foodToEat = null;

                float bestEnergy = 0f;

                for (int i = 0; i < items.Count; i++) {

                    Durango.Logic.Item.ItemData it = items[i];

                    if (it != null && IsEdibleFood(it, 0f)) {

                        foodToEat = it;

                        bestEnergy = GetItemFoodEnergy(it);

                        break;

                    }

                }

                if (foodToEat != null) {

                    // accept = true agar otomatis override konfirmasi buff makanan di server

                    invSys.UseItem(foodToEat, true, null);

                    _lastEatenFoodName = foodToEat.Name;

                    _lastEatTime = Time.time;

                    string energyStr = (bestEnergy > 0f) ? string.Format(" (+{0:F0} Energi)", bestEnergy) : "";

                    _statusText = string.Format("<color=#2ecc71>Konsumsi Otomatis: {0}{1} (Energi Dipulihkan!)</color>", foodToEat.Name, energyStr);

                    Debug.Log(string.Format("[AutoGather] Auto-Eat: Mengonsumsi {0} untuk memulihkan energi!", foodToEat.Name));

                    return true;

                }

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGather] TryEatFoodFromInventory error: " + ex);

            }

            return false;

        }

        public bool IsCraftingInProgress() {

            try {

                CraftSystem cs = null;

                try { cs = GameSystem<CraftSystem>.Instance(); } catch {}

                if (cs == null) cs = UnityEngine.Object.FindObjectOfType<CraftSystem>();

                if (cs != null) {

                    if (_csCraftQueueField != null) {

                        ICollection q = _csCraftQueueField.GetValue(cs) as ICollection;

                        if (q != null && q.Count > 0) return true;

                    }

                    if (cs.CraftingTimer != null && cs.CraftingTimer.Timer != null && !cs.CraftingTimer.Timer.IsStop) {

                        return true;

                    }

                }

            } catch {}

            try {

                PlayerBehavior player = PlayerBehavior.LocalPlayer;

                if (player != null && !string.IsNullOrEmpty(player.CurrentAnimClipName)) {

                    string clip = player.CurrentAnimClipName.ToLower();

                    if (clip.Contains("craft") || clip.Contains("make")) return true;

                }

            } catch {}

            return false;

        }

        private void CheckAndHandleCraftingEnergy() {

            if (!AutoEatWhenCrafting) return;

            // 1. Tangani Pop-up Warning Energi Habis (LowEnergyWarning / ConfirmPopup) jika muncul di layar

            try {

                Durango.UI.Popup.ConfirmPopup cp = UnityEngine.Object.FindObjectOfType<Durango.UI.Popup.ConfirmPopup>();

                if (cp != null && cp.gameObject.activeInHierarchy && cp.IsVisible) {

                    string popupTxt = "";

                    if (_confirmPopupTextLabelField != null) {

                        UILabel lbl = _confirmPopupTextLabelField.GetValue(cp) as UILabel;

                        if (lbl != null && !string.IsNullOrEmpty(lbl.text)) {

                            popupTxt = lbl.text.ToLower();

                        }

                    }

                    // Deteksi jika popup tersebut adalah peringatan energi/fatigue

                    if (popupTxt.Contains("에너지") || popupTxt.Contains("energy") || popupTxt.Contains("energi") || popupTxt.Contains("건강")) {

                        // Konsumsi makanan langsung dari tas

                        TryEatFoodFromInventory();

                        // Eksekusi tombol 0 (실행 / Continue) agar antrean craft berjalan terus tanpa putus

                        if (_confirmPopupActionsField != null) {

                            List<Action> actions = _confirmPopupActionsField.GetValue(cp) as List<Action>;

                            if (actions != null && actions.Count > 0 && actions[0] != null) {

                                actions[0].Invoke();

                            }

                        }

                        try { cp.Hide(); } catch {}

                        _lastEatTime = Time.time;

                        return;

                    }

                }

            } catch {}

            // 2. Preventif Auto-Eat: Cek Stamina secara berkala saat crafting berlangsung

            if (Time.time < _nextAutoEatCheckTime) return;

            _nextAutoEatCheckTime = Time.time + 0.35f;

            if (IsCraftingInProgress()) {

                PlayerBehavior player = PlayerBehavior.LocalPlayer;

                if (player != null && player.Stamina != null) {

                    float curStamina = player.Stamina.Get();

                    float maxStamina = Mathf.Max(1f, player.Stamina.Max());

                    float ratio = curStamina / maxStamina;

                    // Jika stamina menipis (<= 25 unit atau <= 25%) dan ada jeda minimal 2.0s sejak makan terakhir

                    if ((curStamina <= 25f || ratio <= 0.25f) && (Time.time - _lastEatTime > 2.0f)) {

                        TryEatFoodFromInventory();

                    }

                }

            }

        }

        public void SyncRecipeFromGame() {

            RecipeSystem rs = null;

            try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

            if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

            string foundId = "";

            // 1. Cek dari RecipeSelectorGroup yang sedang dibuka

            try {

                Durango.UI.RecipeSelectorGroup rsg = UnityEngine.Object.FindObjectOfType<Durango.UI.RecipeSelectorGroup>();

                if (rsg != null && !string.IsNullOrEmpty(rsg.SelectedRecipeId)) {

                    foundId = rsg.SelectedRecipeId;

                }

            } catch {}

            // 2. Cek dari RecipeInfoWidget yang sedang terbuka di layar

            if (string.IsNullOrEmpty(foundId)) {

                try {

                    Durango.UI.RecipeInfoWidget riw = UnityEngine.Object.FindObjectOfType<Durango.UI.RecipeInfoWidget>();

                    if (riw != null && riw.gameObject.activeInHierarchy) {

                        FieldInfo idF = typeof(Durango.UI.RecipeInfoWidget).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (idF != null) {

                            string wId = idF.GetValue(riw) as string;

                            if (!string.IsNullOrEmpty(wId)) foundId = wId;

                        }

                    }

                } catch {}

            }

            // 3. Cek dari RecipeSystem TrackingTarget (resep yang di-pin pemain)

            if (string.IsNullOrEmpty(foundId) && rs != null && !string.IsNullOrEmpty(rs.TrackingTarget)) {

                foundId = rs.TrackingTarget;

            }

            if (!string.IsNullOrEmpty(foundId)) {

                SetPlanRecipeById(foundId);

            } else {

                _statusText = "Buka menu craft in-game atau pin resep terlebih dahulu!";

            }

        }

        public void SetPlanRecipeById(string recipeId) {

            RecipeSystem rs = null;

            try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

            if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

            if (rs == null) return;

            Crafting.Recipe r = rs.GetRecipe(recipeId);

            if (r != null) {

                PlanRecipe = r;

                PlanRecipeId = recipeId;

                _allMaterialsReady = false;

                UpdatePlanRequirements();

                _statusText = string.Format("Plan dipilih: {0} x{1}", r.Name, PlanQuantity);

                RequestSaveSettings();

            }

        }

        public void SearchRecipes(string keyword) {

            _searchResults.Clear();

            RecipeSystem rs = null;

            try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

            if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

            if (rs == null || rs.RecipeContainer == null) return;

            string kw = (keyword ?? "").Trim().ToLower();

            int count = 0;

            rs.RecipeContainer.EnumerateRecipes(delegate(Crafting.Recipe r) {

                if (r == null || count >= 30) return;

                string rName = (r.Name ?? "").ToLower();

                string rId = (r.Id ?? "").ToLower();

                if (string.IsNullOrEmpty(kw)) {

                    if (r.Available) {

                        _searchResults.Add(r);

                        count++;

                    }

                } else if (rName.Contains(kw) || rId.Contains(kw)) {

                    _searchResults.Add(r);

                    count++;

                }

            });

        }

        public void SetPlanPreset(string keyword) {

            RecipeSystem rs = null;

            try { rs = GameSystem<RecipeSystem>.Instance(); } catch {}

            if (rs == null) rs = UnityEngine.Object.FindObjectOfType<RecipeSystem>();

            if (rs == null || rs.RecipeContainer == null) return;

            string kw = keyword.ToLower();

            Crafting.Recipe found = null;

            rs.RecipeContainer.EnumerateRecipes(delegate(Crafting.Recipe r) {

                if (found != null || r == null) return;

                string rName = (r.Name ?? "").ToLower();

                string rId = (r.Id ?? "").ToLower();

                if (rName.Contains(kw) || rId.Contains(kw)) {

                    found = r;

                }

            });

            if (found != null) {

                PlanRecipe = found;

                PlanRecipeId = found.Id;

                _allMaterialsReady = false;

                UpdatePlanRequirements();

                _statusText = string.Format("Preset dipilih: {0}", found.Name);

                RequestSaveSettings();

            } else {

                _statusText = string.Format("Preset \"{0}\" tidak ditemukan", keyword);

            }

        }

        public void ToggleCraftPlanAutoLoot() {

            if (PlanRecipe == null) {

                _statusText = "Pilih resep terlebih dahulu sebelum memulai Auto-Loot!";

                return;

            }

            IsCraftPlanActive = !IsCraftPlanActive;

            if (IsCraftPlanActive) {

                UpdatePlanRequirements();

                if (!_allMaterialsReady && (GetAllMissingGatherables().Count > 0 || _currentHuntingSlot != null)) {

                    IsEnabled = true;

                    _nextScanTime = 0f;

                    _currentTargetObj = null;

                    _statusText = string.Format("Auto-Loot Plan Aktif! Berburu bahan {0}...", PlanRecipe.Name);

                } else {

                    IsCraftPlanActive = false;

                    IsEnabled = false;

                    _currentTargetObj = null;

                    _statusText = "Semua bahan untuk resep ini sudah lengkap!";

                }

            } else {

                IsEnabled = false;

                _currentTargetObj = null;

                _statusText = "Auto-Loot Plan Dinonaktifkan";

            }

            SaveSettings();

        }

        // ================= GATHERING ENGINE =================

        private bool MatchesFilter(GameObject go, string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            if (CurrentFilter == ResourceFilterMode.All) return true;

            string combined = ((name ?? "") + " " + (go != null ? go.name : "")).ToLower();

            bool isDeadBody = false;

            try {

                if (go != null && ObjectIdentifier.IsDeadBody(go)) isDeadBody = true;

            } catch {}

            if (CurrentFilter == ResourceFilterMode.Corpse) {

                return isDeadBody || combined.Contains("bangkai") || combined.Contains("corpse") || 

                       combined.Contains("dead") || combined.Contains("daging");

            }

            if (isDeadBody) return false;

            // 1. FILTER BATU / STONE / MINERAL

            if (CurrentFilter == ResourceFilterMode.Stone) {

                if (spriteInfo != null) {

                    if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Rock ||

                        spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Pebble) {

                        return true;

                    }

                }

                return combined.Contains("batu") || combined.Contains("rock") || combined.Contains("stone") || 

                       combined.Contains("mineral") || combined.Contains("kawah") || combined.Contains("flint") || 

                       combined.Contains("ore") || combined.Contains("tambang") || combined.Contains("boulder") ||

                       combined.Contains("crag") || combined.Contains("cliff") || combined.Contains("kerikil") ||

                       combined.Contains("pebble") || combined.Contains("iron") || combined.Contains("besi") ||

                       combined.Contains("tembaga") || combined.Contains("copper") || combined.Contains("clay") ||

                       combined.Contains("tanah liat") || combined.Contains("obsidian") || combined.Contains("kristal") ||

                       combined.Contains("crystal") || combined.Contains("limestone") || combined.Contains("basalt") ||

                       combined.Contains("granite") || combined.Contains("bongkahan");

            }

            // 2. FILTER KAYU / WOOD / TREE

            if (CurrentFilter == ResourceFilterMode.Wood) {

                if (combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub")) return false;

                if (spriteInfo != null) {

                    if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Tree) {

                        return true;

                    }

                }

                return combined.Contains("pohon") || combined.Contains("kayu") || combined.Contains("log") || 

                       combined.Contains("tree") || combined.Contains("branch") || combined.Contains("ranting") || 

                       combined.Contains("batang") || combined.Contains("tebang");

            }

            // 3. FILTER SEMAK / BUSH

            if (CurrentFilter == ResourceFilterMode.Bush) {

                if (spriteInfo != null) {

                    if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Shrub) {

                        return true;

                    }

                }

                return combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub") || 

                       combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("buah") || 

                       combined.Contains("berry");

            }

            // 4. FILTER RUMPUT / PLANT / GRASS

            if (CurrentFilter == ResourceFilterMode.Plant) {

                if (spriteInfo != null) {

                    if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Grass) {

                        return true;

                    }

                }

                return combined.Contains("rumput") || combined.Contains("grass") || combined.Contains("reed") || 

                       combined.Contains("tanaman") || combined.Contains("plant") || combined.Contains("herb") || 

                       combined.Contains("bunga") || combined.Contains("flower") || combined.Contains("alang") ||

                       combined.Contains("flax") || combined.Contains("rami");

            }

            return true;

        }

        private GameObject FindClosestTarget(PlayerBehavior player) {

            if ((IsCraftPlanActive || (ActiveTab == 1 && PlanRecipe != null)) && (_allMaterialsReady || _currentHuntingSlot == null)) {

                return null;

            }

            List<GameObject> candidates = new List<GameObject>();

            // 1. ImmovableBase (Batu, Tambang, Mineral, Pohon, Ranting, Bush)

            try {

                ImmovableBase[] immovables = UnityEngine.Object.FindObjectsOfType<ImmovableBase>();

                if (immovables != null) {

                    for (int i = 0; i < immovables.Length; i++) {

                        if (immovables[i] != null && immovables[i].gameObject.activeInHierarchy && !candidates.Contains(immovables[i].gameObject)) {

                            candidates.Add(immovables[i].gameObject);

                        }

                    }

                }

            } catch {}

            // 2. NaturalObject (NaturalPrefabObject, NaturalSpriteObject)

            try {

                NaturalObject[] natObjs = UnityEngine.Object.FindObjectsOfType<NaturalObject>();

                if (natObjs != null) {

                    for (int i = 0; i < natObjs.Length; i++) {

                        if (natObjs[i] != null && natObjs[i].gameObject.activeInHierarchy && !candidates.Contains(natObjs[i].gameObject)) {

                            candidates.Add(natObjs[i].gameObject);

                        }

                    }

                }

            } catch {}

            // 3. Artifact (Kawah, Struktur Alam, Peti)

            try {

                Artifact[] artifacts = UnityEngine.Object.FindObjectsOfType<Artifact>();

                if (artifacts != null) {

                    for (int i = 0; i < artifacts.Length; i++) {

                        if (artifacts[i] != null && artifacts[i].gameObject.activeInHierarchy && !candidates.Contains(artifacts[i].gameObject)) {

                            candidates.Add(artifacts[i].gameObject);

                        }

                    }

                }

            } catch {}

            // 4. InteractionHelperList ObjectBuffer via reflection (Semua objek interaktif di sekitar pemain)

            try {

                Durango.UI.InteractionHelperList helperList = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionHelperList>();

                if (helperList != null) {

                    FieldInfo obField = typeof(Durango.UI.InteractionHelperList).GetField("ObjectBuffer", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (obField != null) {

                        List<GameObject> obList = obField.GetValue(helperList) as List<GameObject>;

                        if (obList != null) {

                            for (int i = 0; i < obList.Count; i++) {

                                GameObject obj = obList[i];

                                if (obj != null && obj.activeInHierarchy && !candidates.Contains(obj)) {

                                    candidates.Add(obj);

                                }

                            }

                        }

                    }

                }

            } catch {}

            // 5. InteractionHelperLabel UI (Objek yang punya tombol interaksi aktif di layar)

            try {

                Durango.UI.InteractionHelperLabel[] labels = UnityEngine.Object.FindObjectsOfType<Durango.UI.InteractionHelperLabel>();

                if (labels != null) {

                    for (int i = 0; i < labels.Length; i++) {

                        if (labels[i] != null && labels[i].Target != null && labels[i].Target.activeInHierarchy && !candidates.Contains(labels[i].Target)) {

                            candidates.Add(labels[i].Target);

                        }

                    }

                }

            } catch {}

            // 6. InteractionSystem resmi game

            try {

                InteractionSystem.SearchPropObjects(candidates);

            } catch {}

            try {

                InteractionSystem.SearchMovableObjects(candidates);

            } catch {}

            _totalCandidatesFound = candidates.Count;

            GameObject absoluteClosest = null;

            float absoluteMinDist = 999999f;

            string absoluteClosestName = "-";

            Vector3 absoluteClosestPos = Vector3.zero;

            float absoluteClosestInteractDist = 220f;

            GameObject closestInRadius = null;

            float minRadiusDist = 999999f;

            string closestInRadiusName = "-";

            Vector3 closestInRadiusPos = Vector3.zero;

            float closestInRadiusInteractDist = 220f;

            float maxRadiusUnits = ScanRadiusMeters * 100f; // 1m = 100 unit Durango

            for (int i = 0; i < candidates.Count; i++) {

                GameObject go = candidates[i];

                if (go == null || !go.activeInHierarchy) continue;

                if (go == player.gameObject) continue;

                if (_recentlyGathered.ContainsKey(go)) continue;

                // Ambil posisi target

                Vector3 pos = go.transform.position;

                try {

                    Vector3 ipos = InteractionObject.GetInteractionPosition(go, false);

                    if (ipos != Vector3.zero) pos = ipos;

                } catch {}

                // Ambil nama objek & engine sprite info

                float iDist = 220f; // Default 2.2m

                string n = go.name;

                Durango.Terrain.BiomeSpriteInfo spriteInfo = null;

                try {

                    ImmovableBase imm = go.GetComponent<ImmovableBase>();

                    if (imm != null) {

                        try {

                            spriteInfo = Durango.Terrain.DataHelper.GetBiomeSpriteInfo((int)imm.EntityType);

                            if (spriteInfo != null && !string.IsNullOrEmpty(spriteInfo.Name)) {

                                n = spriteInfo.Name;

                            }

                        } catch {}

                        if (string.IsNullOrEmpty(n) || n == go.name) {

                            string immName = imm.GetName();

                            if (!string.IsNullOrEmpty(immName)) n = immName;

                        }

                        if (imm.InteractionDistance > 0) {

                            iDist = Mathf.Max(220f, imm.InteractionDistance + 60f);

                        }

                    }

                } catch {}

                n = CleanObjectName(n);

                // Cek filter resource yang dipilih user

                if (!MatchesFilter(go, n, spriteInfo)) continue;

                // Hitung jarak real-time

                float d = GetObjectDistance(player, go, pos);

                // Rekam objek terdekat mutlak

                if (d < absoluteMinDist) {

                    absoluteMinDist = d;

                    absoluteClosest = go;

                    absoluteClosestName = n;

                    absoluteClosestPos = pos;

                    absoluteClosestInteractDist = iDist;

                }

                // Rekam objek terdekat dalam radius

                if (d <= maxRadiusUnits && d < minRadiusDist) {

                    minRadiusDist = d;

                    closestInRadius = go;

                    closestInRadiusName = n;

                    closestInRadiusPos = pos;

                    closestInRadiusInteractDist = iDist;

                }

            }

            if (closestInRadius != null) {

                _targetName = closestInRadiusName;

                _currentTargetPos = closestInRadiusPos;

                _currentTargetInteractDist = closestInRadiusInteractDist;

                return closestInRadius;

            }

            // Jika tidak ada dalam radius, tampilkan objek terdekat yang sesuai filter

            if (absoluteClosest != null) {

                _statusText = string.Format("Terdekat: {0} ({1:F1}m) | Radius: {2:F0}m", absoluteClosestName, absoluteMinDist / 100f, ScanRadiusMeters);

            } else {

                _statusText = string.Format("Mencari ({0} node terdeteksi)", _totalCandidatesFound);

            }

            _targetName = "-";

            _targetDistance = 0f;

            _targetAvailableResources = "-";

            return null;

        }

        private string CleanObjectName(string raw) {

            if (string.IsNullOrEmpty(raw)) return "-";

            string res = raw.Replace("(Clone)", "").Trim();

            if (res.Length > 22) res = res.Substring(0, 22) + "..";

            return res;

        }

        private IEnumerator GatherRoutine(GameObject target, LocalMoveNavigator nav, PlayerController controller) {

            _isGathering = true;

            _statusText = "Mendekati & Buka Menu: " + _targetName;

            // Jika Craft Planner sudah lengkap sebelum interaksi dimulai -> langsung keluar

            if ((IsCraftPlanActive || (ActiveTab == 1 && PlanRecipe != null)) && _allMaterialsReady) {

                _currentTargetObj = null;

                IsEnabled = false;

                IsCraftPlanActive = false;

                _isGathering = false;

                yield break;

            }

            // 1. Berhenti berjalan dan hadap ke target (hilangkan momentum kecepatan karakter)

            if (controller != null) {

                try { controller.StopMove(); } catch {}

            }

            if (nav != null) {

                try { nav.Stop(); } catch {}

                if (target != null) {

                    try { nav.RotateToObject(target, true); } catch {}

                }

            }

            // Beri jeda 0.18 detik agar deselerasi fisik karakter mencapai 0 speed

            yield return new WaitForSeconds(0.18f);

            int gatherCountOnThisNode = 0;

            bool hadUsableSlots = false;

            // ================= LOOP PANEN PENUH PADA NODE INI =================

            // Terus panen di node ini sampai semua resource yang diinginkan habis!

            while (gatherCountOnThisNode < 30) {

                if (!IsEnabled) break;

                if (target == null || !target.activeInHierarchy) break;

                // Cek apakah bahan resep sudah lengkap (khusus Craft Plan)

                if (IsCraftPlanActive) {

                    UpdatePlanRequirements();

                    if (_allMaterialsReady) {

                        StopBotForCraftReady(string.Format("Bahan Lengkap! Siap Crafting {0} x{1}!", PlanRecipe != null ? PlanRecipe.Name : "", PlanQuantity));

                        ForceCloseInteractionMenu();

                        if (AutoCraftWhenReady) {

                            OpenCraftUIInGame(PlanRecipeId);

                        }

                        break;

                    }

                }

                // 2. Loop trigger interaksi (hingga 4x dengan jeda jika menu belum terbuka)

                Durango.UI.InteractionMenuListWidget_PC wheel = null;

                int attempt = 0;

                while (!Durango.UI.InteractionMenuListWidgetBase.IsShow && attempt < 4) {

                    attempt++;

                    _statusText = string.Format("Buka Menu {0} (Percobaan {1})...", _targetName, attempt);

                    if (controller != null) {

                        try { controller.StopMove(); } catch {}

                    }

                    TriggerInteraction(target);

                    float waitTimeout = Time.time + 0.65f;

                    while (Time.time < waitTimeout) {

                        wheel = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidget_PC>();

                        if (Durango.UI.InteractionMenuListWidgetBase.IsShow) break;

                        yield return new WaitForSeconds(0.04f);

                    }

                    if (Durango.UI.InteractionMenuListWidgetBase.IsShow) break;

                    yield return new WaitForSeconds(0.12f);

                }

                // Jika roda interaksi tidak terbuka:

                if (!Durango.UI.InteractionMenuListWidgetBase.IsShow) {

                    if (gatherCountOnThisNode == 0) {

                        // Fallback jika tidak muncul roda menu (interaksi instan / item lepas di tanah)

                        TriggerInteraction(target);

                        if (target != null) {

                            _recentlyGathered[target] = Time.time + 15f;

                        }

                        float fallbackTimeout = Time.time + 12f;

                        yield return new WaitForSeconds(0.6f);

                        while (Time.time < fallbackTimeout && IsGatheringActive()) {

                            yield return new WaitForSeconds(0.2f);

                        }

                        yield return new WaitForSeconds(0.5f);

                        if (IsCraftPlanActive) UpdatePlanRequirements();

                        ForceCloseInteractionMenu();

                    } else {

                        // Sudah pernah panen di node ini, dan sekarang node sudah tidak bisa dibuka lagi (habis / hancur)

                        _statusText = string.Format("Node {0} selesai dipanen sepenuhnya!", _targetName);

                    }

                    break;

                }

                yield return new WaitForSeconds(0.12f);

                // A. Deteksi SEMUA resource yang masih tersedia di roda menu

                // Lakukan polling singkat (maksimal 0.45s) jika slot UI belum selesai binding data

                List<string> availList = new List<string>();

                Dictionary<int, Durango.UI.InteractionMenuWidget_PC> activeSlots = new Dictionary<int, Durango.UI.InteractionMenuWidget_PC>();

                float scanTimeout = Time.time + 0.45f;

                while (Time.time < scanTimeout) {

                    activeSlots.Clear();

                    availList.Clear();

                    Durango.UI.InteractionMenuWidget_PC[] menuWidgets = UnityEngine.Object.FindObjectsOfType<Durango.UI.InteractionMenuWidget_PC>();

                    if (menuWidgets != null) {

                        for (int i = 0; i < menuWidgets.Length; i++) {

                            Durango.UI.InteractionMenuWidget_PC w = menuWidgets[i];

                            if (w != null && w.gameObject.activeInHierarchy && !w.Empty && w.Valid) {

                                // 1. Cek status disabled / warning dari widget UI in-game

                                if (w.Disabled || w.IsWarning()) continue;

                                // 2. Cek flag data disabled / access denied

                                if (w.Data.Disabled || w.Data.AccessDenied) continue;

                                // 3. Cek cooldown / timer pengisian ulang (recharging timer)

                                if (w.Data.Timer != null && !w.Data.Timer.IsStop && w.Data.Timer.Remain > 0.05f) continue;

                                // 4. Cek ketersediaan stok gathering (GatheringData.Amount <= 0)

                                if (w.Data.GatheringData != null) {

                                    if (!w.Data.GatheringData.IsAvailableForGathering() || w.Data.GatheringData.Amount <= 0) {

                                        continue;

                                    }

                                }

                                int slotIdx = w.Index;

                                if (slotIdx < 0 || slotIdx >= 6) slotIdx = i % 6;

                                string sName = "";

                                try {

                                    if (!string.IsNullOrEmpty(w.Data.Name)) sName = w.Data.Name;

                                    if (string.IsNullOrEmpty(sName) && w.Data.GatheringData != null && !string.IsNullOrEmpty(w.Data.GatheringData.Name)) {

                                        sName = w.Data.GatheringData.Name;

                                    }

                                    if (string.IsNullOrEmpty(sName)) {

                                        sName = GetWidgetLabelText(w);

                                    }

                                    if (string.IsNullOrEmpty(sName)) sName = "Item";

                                    int cnt = (w.Data.GatheringData != null) ? w.Data.GatheringData.Amount : w.Data.Count;

                                    if (!activeSlots.ContainsKey(slotIdx)) {

                                        activeSlots[slotIdx] = w;

                                        if (cnt > 0) {

                                            availList.Add(string.Format("F{0}: {1} ({2})", slotIdx + 1, sName, cnt));

                                        } else {

                                            availList.Add(string.Format("F{0}: {1}", slotIdx + 1, sName));

                                        }

                                    }

                                } catch {}

                            }

                        }

                    }

                    if (activeSlots.Count > 0) {

                        hadUsableSlots = true;

                        break;

                    }

                    yield return new WaitForSeconds(0.06f);

                }

                if (availList.Count > 0) {

                    _targetAvailableResources = string.Join(" | ", availList.ToArray());

                } else {

                    _targetAvailableResources = "Semua Resource Habis";

                }

                // B. Tentukan slot mana yang akan dieksekusi secara CERDAS

                int targetSlot = -1;

                Durango.UI.InteractionMenuWidget_PC chosenWidget = null;

                if (_hasLearnedTarget && !string.IsNullOrEmpty(_learnedActionName)) {

                    // ================= SMART LEARNED TARGET MODE =================

                    string learnedLow = _learnedActionName.ToLower().Trim();

                    string learnedGenLow = (!string.IsNullOrEmpty(_learnedGeneratorId)) ? _learnedGeneratorId.ToLower().Trim() : "";

                    foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                        Durango.UI.InteractionMenuWidget_PC w = kvp.Value;

                        string actText = GetSlotActionText(w);

                        string fullText = GetWidgetFullText(w);

                        // Proteksi daun liar jika target yang dipelajari bukan daun

                        bool isSlotDaun = actText.Contains("daun") || actText.Contains("leaf") || actText.Contains("leaves") || actText.Contains("pelepah");

                        bool learnedWantsDaun = learnedLow.Contains("daun") || learnedLow.Contains("leaf") || learnedLow.Contains("pelepah");

                        if (isSlotDaun && !learnedWantsDaun) continue;

                        // Proteksi tebang/hancurkan node (Anchor) jika yang dipelajari bukan tebang/batu

                        bool isSlotAnchor = IsAnchorSlot(w);

                        bool learnedWantsAnchor = learnedLow.Contains("tebang") || learnedLow.Contains("chop") || learnedLow.Contains("pecah") || learnedLow.Contains("quarry");

                        if (isSlotAnchor && !learnedWantsAnchor) continue;

                        bool genMatch = false;

                        if (!string.IsNullOrEmpty(learnedGenLow) && w.Data.GatheringData != null && !string.IsNullOrEmpty(w.Data.GatheringData.GeneratorId)) {

                            if (w.Data.GatheringData.GeneratorId.ToLower().Contains(learnedGenLow) || learnedGenLow.Contains(w.Data.GatheringData.GeneratorId.ToLower())) {

                                genMatch = true;

                            }

                        }

                        if (actText.Contains(learnedLow) || fullText.Contains(learnedLow) || (genMatch && actText.Contains(learnedLow))) {

                            targetSlot = kvp.Key;

                            chosenWidget = w;

                            break;

                        }

                        // Pencocokan kata kunci inti (misal: "buluh", "ranting", "batang", "batu")

                        string[] words = learnedLow.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

                        bool wordMatch = false;

                        for (int wIdx = 0; wIdx < words.Length; wIdx++) {

                            string wd = words[wIdx];

                            if (wd.Length > 2 && wd != "ambil" && wd != "petik" && wd != "cabut" && wd != "kupas") {

                                if (actText.Contains(wd) || fullText.Contains(wd)) {

                                    wordMatch = true;

                                    break;

                                }

                            }

                        }

                        if (wordMatch) {

                            targetSlot = kvp.Key;

                            chosenWidget = w;

                            break;

                        }

                    }

                    if (chosenWidget == null || targetSlot == -1) {

                        _statusText = string.Format("Resource '{0}' sudah habis di node ini!", _learnedActionName);

                        break;

                    }

                } else if (IsCraftPlanActive) {

                    // ================= CRAFT PLANNER MODE =================

                    List<SlotRequirementInfo> missingGatherables = GetAllMissingGatherables();

                    List<int> nonAnchorMatches = new List<int>();

                    List<int> anchorMatches = new List<int>();

                    Dictionary<int, SlotRequirementInfo> slotMatchReq = new Dictionary<int, SlotRequirementInfo>();

                    foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                        SlotRequirementInfo matchedReq = null;

                        if (_currentHuntingSlot != null && _currentHuntingSlot.Missing > 0 && DoesWidgetMatchSlotRequirement(kvp.Value, _currentHuntingSlot)) {

                            matchedReq = _currentHuntingSlot;

                        } else {

                            for (int m = 0; m < missingGatherables.Count; m++) {

                                SlotRequirementInfo req = missingGatherables[m];

                                if (req != null && req.Missing > 0 && DoesWidgetMatchSlotRequirement(kvp.Value, req)) {

                                    matchedReq = req;

                                    break;

                                }

                            }

                        }

                        if (matchedReq != null) {

                            slotMatchReq[kvp.Key] = matchedReq;

                            if (IsAnchorSlot(kvp.Value)) {

                                anchorMatches.Add(kvp.Key);

                            } else {

                                nonAnchorMatches.Add(kvp.Key);

                            }

                        }

                    }

                    // Prioritas 1: Ambil item sekunder (Non-Anchor) terlebih dahulu agar node tidak hancur sia-sia!

                    if (nonAnchorMatches.Count > 0) {

                        int chosenSlot = -1;

                        for (int na = 0; na < nonAnchorMatches.Count; na++) {

                            int sIdx = nonAnchorMatches[na];

                            if (_currentHuntingSlot != null && slotMatchReq[sIdx] == _currentHuntingSlot) {

                                chosenSlot = sIdx;

                                break;

                            }

                        }

                        if (chosenSlot == -1) chosenSlot = nonAnchorMatches[0];

                        targetSlot = chosenSlot;

                        chosenWidget = activeSlots[targetSlot];

                        _currentHuntingSlot = slotMatchReq[targetSlot];

                    }

                    // Prioritas 2: Jika SEMUA item sekunder di node ini sudah habis/tidak dibutuhkan, barulah ambil item jangkar!

                    else if (anchorMatches.Count > 0) {

                        int chosenSlot = -1;

                        for (int a = 0; a < anchorMatches.Count; a++) {

                            int sIdx = anchorMatches[a];

                            if (_currentHuntingSlot != null && slotMatchReq[sIdx] == _currentHuntingSlot) {

                                chosenSlot = sIdx;

                                break;

                            }

                        }

                        if (chosenSlot == -1) chosenSlot = anchorMatches[0];

                        targetSlot = chosenSlot;

                        chosenWidget = activeSlots[targetSlot];

                        _currentHuntingSlot = slotMatchReq[targetSlot];

                    }

                    // Fallback cerdas Planner: Jika hanya 1 slot di node target dan bukan tebang liar / petik daun liar

                    if (chosenWidget == null && activeSlots.Count == 1) {

                        foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                            bool isSingleAnchor = IsAnchorSlot(kvp.Value);

                            bool allowSingleAnchor = (_currentHuntingSlot != null && (_currentHuntingSlot.SuggestedTarget == GatherTargetMode.Batang || _currentHuntingSlot.SuggestedTarget == GatherTargetMode.Batu));

                            string sAct = GetSlotActionText(kvp.Value);

                            bool isSingleDaun = sAct.Contains("daun") || sAct.Contains("leaf") || sAct.Contains("leaves") || sAct.Contains("pelepah") || sAct.Contains("foliage");

                            bool allowSingleDaun = (_currentHuntingSlot != null && (_currentHuntingSlot.SuggestedTarget == GatherTargetMode.Daun || _currentHuntingSlot.Name.ToLower().Contains("daun")));

                            if ((!isSingleAnchor || allowSingleAnchor) && (!isSingleDaun || allowSingleDaun)) {

                                targetSlot = kvp.Key;

                                chosenWidget = kvp.Value;

                                break;

                            }

                        }

                    }

                    // Jika node ini tidak punya lagi bahan yang dibutuhkan di plan:

                    if (chosenWidget == null || targetSlot == -1) {

                        string huntName = (_currentHuntingSlot != null) ? _currentHuntingSlot.Name : "Bahan Plan";

                        _statusText = string.Format("Bahan plan ({0}) di node ini sudah selesai!", huntName);

                        break;

                    }

                } else {

                    // ================= PANEN MANUAL MODE =================

                    // 1. Prioritaskan aksi target yang dipelajari (Smart Learned Action) jika ada

                    if (_hasLearnedTarget && !string.IsNullOrEmpty(_learnedActionName) && string.IsNullOrEmpty((CustomKeyword ?? "").Trim())) {

                        string learnedLow = _learnedActionName.ToLower();

                        foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                            string actText = GetSlotActionText(kvp.Value);

                            string fullText = GetWidgetFullText(kvp.Value);

                            string slotGenId = "";

                            try {

                                if (kvp.Value.Data.GatheringData != null) {

                                    slotGenId = (kvp.Value.Data.GatheringData.GeneratorId ?? "").ToLower();

                                }

                            } catch {}

                            bool matchAct = actText.Contains(learnedLow) || learnedLow.Contains(actText);

                            bool matchGen = !string.IsNullOrEmpty(_learnedGeneratorId) && !string.IsNullOrEmpty(slotGenId) && slotGenId == _learnedGeneratorId.ToLower();

                            if (matchAct || matchGen) {

                                targetSlot = kvp.Key;

                                chosenWidget = kvp.Value;

                                break;

                            }

                        }

                    }

                    string customSearch = (CustomKeyword ?? "").Trim().ToLower();

                    if (!string.IsNullOrEmpty(customSearch)) {

                        string[] searchTerms = customSearch.Split(new char[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);

                        for (int t = 0; t < searchTerms.Length; t++) searchTerms[t] = searchTerms[t].Trim();

                        List<int> customNonAnchor = new List<int>();

                        List<int> customAnchor = new List<int>();

                        foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                            string actText = GetSlotActionText(kvp.Value);

                            string fullText = GetWidgetFullText(kvp.Value);

                            bool isDaunSlot = actText.Contains("daun") || actText.Contains("leaf") || actText.Contains("leaves") || actText.Contains("pelepah") || actText.Contains("foliage");

                            bool searchWantsDaun = customSearch.Contains("daun") || customSearch.Contains("leaf") || customSearch.Contains("pelepah");

                            if (isDaunSlot && !searchWantsDaun) {

                                continue; // TOLAK: Jangan pernah mencocokkan slot daun jika keyword pencarian tidak mencari daun!

                            }

                            bool matches = false;

                            for (int t = 0; t < searchTerms.Length; t++) {

                                string term = searchTerms[t];

                                if (!string.IsNullOrEmpty(term) && (actText.Contains(term) || fullText.Contains(term))) {

                                    matches = true;

                                    break;

                                }

                            }

                            if (matches) {

                                if (IsAnchorSlot(kvp.Value)) customAnchor.Add(kvp.Key);

                                else customNonAnchor.Add(kvp.Key);

                            }

                        }

                        bool isSecondarySearch = customSearch.Contains("ranting") || customSearch.Contains("cabang") || 

                                                 customSearch.Contains("daun") || customSearch.Contains("kulit") || 

                                                 customSearch.Contains("buah") || customSearch.Contains("paku") || 

                                                 customSearch.Contains("pasak") || customSearch.Contains("tongkat");

                        if (customNonAnchor.Count > 0) {

                            targetSlot = customNonAnchor[0];

                            chosenWidget = activeSlots[targetSlot];

                        } else if (customAnchor.Count > 0 && !isSecondarySearch) {

                            targetSlot = customAnchor[0];

                            chosenWidget = activeSlots[targetSlot];

                        }

                    }

                    if (chosenWidget == null) {

                        if (TargetMode == GatherTargetMode.Auto) {

                            List<int> autoNonAnchor = new List<int>();

                            List<int> autoAnchor = new List<int>();

                            for (int s = 0; s < 6; s++) {

                                if (activeSlots.ContainsKey(s)) {

                                    if (IsAnchorSlot(activeSlots[s])) autoAnchor.Add(s);

                                    else autoNonAnchor.Add(s);

                                }

                            }

                            if (autoNonAnchor.Count > 0) {

                                targetSlot = autoNonAnchor[0];

                                chosenWidget = activeSlots[targetSlot];

                            } else if (autoAnchor.Count > 0) {

                                targetSlot = autoAnchor[0];

                                chosenWidget = activeSlots[targetSlot];

                            }

                        } else if (TargetMode >= GatherTargetMode.SlotF1 && TargetMode <= GatherTargetMode.SlotF6) {

                            int reqSlot = (int)TargetMode - (int)GatherTargetMode.SlotF1;

                            if (activeSlots.ContainsKey(reqSlot)) {

                                targetSlot = reqSlot;

                                chosenWidget = activeSlots[reqSlot];

                            }

                        } else {

                            string[] keywords = GetKeywordsForMode(TargetMode);

                            List<int> modeNonAnchor = new List<int>();

                            List<int> modeAnchor = new List<int>();

                            foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                                string actText = GetSlotActionText(kvp.Value);

                                string fullText = GetWidgetFullText(kvp.Value);

                                bool isDaunSlot = actText.Contains("daun") || actText.Contains("leaf") || actText.Contains("leaves") || actText.Contains("pelepah") || actText.Contains("foliage");

                                if (isDaunSlot && TargetMode != GatherTargetMode.Daun && TargetMode != GatherTargetMode.Auto) {

                                    continue; // TOLAK: Jangan pernah mencocokkan slot daun jika mode bukan Daun atau Auto!

                                }

                                bool matches = false;

                                for (int k = 0; k < keywords.Length; k++) {

                                    if (actText.Contains(keywords[k]) || fullText.Contains(keywords[k])) {

                                        matches = true;

                                        break;

                                    }

                                }

                                if (matches) {

                                    if (IsAnchorSlot(kvp.Value)) modeAnchor.Add(kvp.Key);

                                    else modeNonAnchor.Add(kvp.Key);

                                }

                            }

                            bool allowAnchor = (TargetMode == GatherTargetMode.Batang || TargetMode == GatherTargetMode.Auto || TargetMode == GatherTargetMode.Batu);

                            if (modeNonAnchor.Count > 0) {

                                targetSlot = modeNonAnchor[0];

                                chosenWidget = activeSlots[targetSlot];

                            } else if (modeAnchor.Count > 0 && allowAnchor) {

                                targetSlot = modeAnchor[0];

                                chosenWidget = activeSlots[targetSlot];

                            }

                        }

                    }

                    // Fallback cerdas Manual: Jika hanya ada 1 slot aktif di node yang didekati pemain

                    if (chosenWidget == null && activeSlots.Count == 1) {

                        foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {

                            bool isSingleAnchor = IsAnchorSlot(kvp.Value);

                            bool allowSingleAnchor = (TargetMode == GatherTargetMode.Batang || TargetMode == GatherTargetMode.Auto || TargetMode == GatherTargetMode.Batu);

                            string sAct = GetSlotActionText(kvp.Value);

                            bool isSingleDaun = sAct.Contains("daun") || sAct.Contains("leaf") || sAct.Contains("leaves") || sAct.Contains("pelepah") || sAct.Contains("foliage");

                            bool allowSingleDaun = (TargetMode == GatherTargetMode.Daun || TargetMode == GatherTargetMode.Auto);

                            if ((!isSingleAnchor || allowSingleAnchor) && (!isSingleDaun || allowSingleDaun)) {

                                targetSlot = kvp.Key;

                                chosenWidget = kvp.Value;

                                break;

                            }

                        }

                    }

                    if (chosenWidget == null || targetSlot == -1) {

                        string targetDesc = (!string.IsNullOrEmpty(customSearch)) 

                            ? ("[" + customSearch + "]") 

                            : ("[" + TargetMode.ToString() + "]");

                        _statusText = string.Format("Resource {0} sudah habis di node ini!", targetDesc);

                        break;

                    }

                }

                // C. Eksekusi Panen Slot!

                gatherCountOnThisNode++;

                string actionName = "Slot F" + (targetSlot + 1);

                int remainingCount = 1;

                try {

                    if (!string.IsNullOrEmpty(chosenWidget.Data.Name)) actionName = chosenWidget.Data.Name;

                    remainingCount = chosenWidget.Data.Count;

                } catch {}

                _statusText = string.Format("Panen #{0}: {1} [F{2}] (Sisa di node: {3})...", 

                    gatherCountOnThisNode, actionName, targetSlot + 1, remainingCount);

                // Eksekusi via widget SetClick() langsung

                try { chosenWidget.SetClick(); } catch {}

                // Eksekusi via reflection ClickHotKey dengan Trigger.Up

                if (wheel != null && _clickHotKeyMethod != null) {

                    try {

                        _clickHotKeyMethod.Invoke(wheel, new object[] { targetSlot, Durango.Logic.InputSystem.Trigger.Up });

                    } catch {}

                }

                // Eksekusi via simulasi tombol keyboard fisik F1 - F6

                StartCoroutine(SimulatePressKeyRoutine((byte)(0x70 + targetSlot), (byte)(0x3B + targetSlot)));

                // Simpan waktu awal mulai memanen

                float gatherStartTime = Time.time;

                // Hitung durasi aksi secara dinamis dari data game Durango

                float actionDuration = 3.5f;

                try {

                    if (chosenWidget.Data.Duration > 0.5f) {

                        actionDuration = chosenWidget.Data.Duration;

                    } else if (chosenWidget.Data.GatheringData != null && chosenWidget.Data.GatheringData.Duration > 0.5f) {

                        actionDuration = chosenWidget.Data.GatheringData.Duration;

                    }

                } catch {}

                bool isAnchor = IsAnchorSlot(chosenWidget);

                if (isAnchor) {

                    actionDuration = Mathf.Max(actionDuration, 10.0f);

                }

                // Tunggu durasi aksi tuntas

                float minWaitTime = Mathf.Max(3.2f, actionDuration - 0.5f);

                float maxGatherTimeout = Time.time + actionDuration + 15.0f;

                while (Time.time < maxGatherTimeout) {

                    float elapsed = Time.time - gatherStartTime;

                    if (elapsed < minWaitTime) {

                        float remain = Mathf.Max(0f, actionDuration - elapsed);

                        _statusText = string.Format("Memanen #{0}: {1}... ({2:F0}s)", gatherCountOnThisNode, actionName, remain);

                        yield return new WaitForSeconds(0.2f);

                        continue;

                    }

                    if (IsGatheringActive()) {

                        _statusText = string.Format("Menyelesaikan loot #{0}: {1}...", gatherCountOnThisNode, actionName);

                        yield return new WaitForSeconds(0.2f);

                        continue;

                    }

                    break;

                }

                // Beri jeda agar item masuk ke inventory

                yield return new WaitForSeconds(0.8f);

                _statusText = string.Format("Selesai panen #{0}: {1}!", gatherCountOnThisNode, actionName);

                // Jeda singkat sebelum iterasi berikutnya pada node yang sama

                yield return new WaitForSeconds(0.35f);

            }

            // ================= SELESAI PADA NODE INI =================

            // Tutup menu dan blacklist node agar karakter berpindah ke node berikutnya

            if (target != null) {

                _recentlyGathered[target] = Time.time + 60f; // Blacklist 60s

            }

            _currentTargetObj = null;

            _targetAvailableResources = "-";

            ForceCloseInteractionMenu();

            _gatherCooldownUntil = Time.time + 0.35f;

            _isGathering = false;

        }

        private string GetWidgetFullText(Durango.UI.InteractionMenuWidget_PC w) {

            if (w == null) return "";

            string text = "";

            try {

                if (!string.IsNullOrEmpty(w.Data.Name)) text += " " + w.Data.Name;

                if (!string.IsNullOrEmpty(w.Data.Id)) text += " " + w.Data.Id;

                if (w.Data.GatheringData != null) {

                    if (!string.IsNullOrEmpty(w.Data.GatheringData.Name)) text += " " + w.Data.GatheringData.Name;

                    if (!string.IsNullOrEmpty(w.Data.GatheringData.GeneratorId)) text += " " + w.Data.GatheringData.GeneratorId;

                }

            } catch {}

            return text.ToLower();

        }

        private string[] GetKeywordsForMode(GatherTargetMode mode) {

            switch (mode) {

                case GatherTargetMode.Daun:

                    return new string[] { "daun", "leaf", "leaves", "frond", "pelepah", "foliage" };

                case GatherTargetMode.Ranting:

                    return new string[] { "ranting", "cabang", "branch", "twig", "stick", "dahan" };

                case GatherTargetMode.Batang:

                    return new string[] { "batang", "kayu", "log", "trunk", "wood", "tebang", "chop" };

                case GatherTargetMode.Batu:

                    return new string[] { "batu", "stone", "rock", "tambang", "mine", "ore", "kerikil", "pebble", "boulder", "flint" };

                case GatherTargetMode.Rumput:

                    return new string[] { "rumput", "grass", "reed", "flax", "tanaman", "plant", "herb", "serat", "fiber", "alang", "rami", "cabut", "petik" };

                case GatherTargetMode.Daging:

                    return new string[] { "daging", "meat", "sayat", "carve", "butcher" };

                case GatherTargetMode.Kulit:

                    return new string[] { "kulit", "bark", "hide", "leather", "kupas" };

                case GatherTargetMode.Buah:

                    return new string[] { "buah", "berry", "fruit", "petik" };

                default:

                    return new string[0];

            }

        }

        private void TriggerInteraction(GameObject target) {

            // 1. Cari InteractionHelperLabel aktif di UI

            try {

                Durango.UI.InteractionHelperLabel[] labels = UnityEngine.Object.FindObjectsOfType<Durango.UI.InteractionHelperLabel>();

                if (labels != null) {

                    for (int i = 0; i < labels.Length; i++) {

                        Durango.UI.InteractionHelperLabel lb = labels[i];

                        if (lb != null && lb.IsShow) {

                            bool isMatch = (lb.Target == target);

                            if (!isMatch && lb.Target != null && target != null) {

                                isMatch = (lb.Target.transform.root == target.transform.root);

                            }

                            if (isMatch) {

                                lb.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);

                                break;

                            }

                        }

                    }

                }

            } catch {}

            // 2. InteractionSystem in-engine

            try {

                InteractionSystem isys = GameSystem<InteractionSystem>.Instance();

                if (isys != null && target != null) {

                    isys.SetInteractionTarget(new InteractionObject(target));

                }

            } catch {}

            // 3. Tekan tombol fisik E (hold 80ms)

            StartCoroutine(SimulatePressKeyRoutine(VK_E, SCAN_E));

        }

        private IEnumerator SimulatePressKeyRoutine(byte vk, byte scan) {

            try { keybd_event(vk, scan, 0, UIntPtr.Zero); } catch {}

            yield return new WaitForSeconds(0.08f);

            try { keybd_event(vk, scan, KEYEVENTF_KEYUP, UIntPtr.Zero); } catch {}

        }

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

        private void TryHookInteractionMenu() {

            try {

                if (Durango.UI.InteractionMenuListWidgetBase.IsShow) {

                    Durango.UI.InteractionMenuListWidget_PC menuList = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidget_PC>();

                    if (menuList != null) {

                        GameObject targetGo = GetMenuTargetObject(menuList);

                        if (targetGo != null) _lastInteractedObject = targetGo;

                        if (_hookedMenuList != menuList) {

                            _hookedMenuList = menuList;

                            menuList.OnClickInteractionMenu -= OnPlayerMenuActionClicked;

                            menuList.OnClickInteractionMenu += OnPlayerMenuActionClicked;

                        }

                    }

                }

            } catch {}

        }

        private GameObject GetMenuTargetObject(Durango.UI.InteractionMenuListWidgetBase menuList) {

            if (menuList != null && _menuTargetField != null) {

                try {

                    object targetObj = _menuTargetField.GetValue(menuList);

                    if (targetObj is Component) return ((Component)targetObj).gameObject;

                    if (targetObj is GameObject) return (GameObject)targetObj;

                } catch {}

            }

            try {

                InteractionSystem isys = GameSystem<InteractionSystem>.Instance();

                if (isys != null) {

                    if (isys.Target != null && isys.Target.Target != null) return isys.Target.Target;

                    if (isys.LastInteractionTarget != null && isys.LastInteractionTarget.Target != null) return isys.LastInteractionTarget.Target;

                }

            } catch {}

            return null;

        }

        private static bool IsGenericEngineName(string name) {

            if (string.IsNullOrEmpty(name)) return true;

            string low = name.Trim().ToLower();

            return low == "naturalspriteobject" || 

                   low == "naturalprefabobject" || 

                   low == "immovablebase" || 

                   low == "gameobject" || 

                   low == "doodad" || 

                   low == "staticenv" || 

                   low == "prop" || 

                   low == "node alam" || 

                   low == "interactive" ||

                   low == "-" ||

                   low.StartsWith("immovable") ||

                   low.StartsWith("naturalsprite") ||

                   low.StartsWith("naturalprefab");

        }

        private ResourceFilterMode ClassifyResourceFilter(string actName, string genId, GameObject targetGo, InteractionData.GatheringData gatherData, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            // 1. Cek dari RequiredTools jika ada

            if (gatherData != null && gatherData.RequiredTools != null) {

                try {

                    if (gatherData.RequiredTools.ContainsKey("pickaxe")) return ResourceFilterMode.Stone;

                    if (gatherData.RequiredTools.ContainsKey("axe")) return ResourceFilterMode.Wood;

                } catch {}

            }

            // 2. Cek dari SpriteObjectType engine Durango

            if (spriteInfo != null) {

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Rock || 

                    spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Pebble) {

                    return ResourceFilterMode.Stone;

                }

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Tree) {

                    return ResourceFilterMode.Wood;

                }

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Shrub) {

                    return ResourceFilterMode.Bush;

                }

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Grass) {

                    return ResourceFilterMode.Plant;

                }

            }

            if (targetGo != null) {

                try {

                    NaturalSpriteObject nso = targetGo.GetComponent<NaturalSpriteObject>();

                    if (nso == null) nso = targetGo.GetComponentInParent<NaturalSpriteObject>();

                    if (nso == null) nso = targetGo.GetComponentInChildren<NaturalSpriteObject>();

                    if (nso != null && nso.Sprite != null) {

                        if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Rock ||

                            nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Pebble) {

                            return ResourceFilterMode.Stone;

                        }

                        if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Tree) {

                            return ResourceFilterMode.Wood;

                        }

                        if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Shrub) {

                            return ResourceFilterMode.Bush;

                        }

                        if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Grass) {

                            return ResourceFilterMode.Plant;

                        }

                    }

                } catch {}

            }

            // 3. Bangun string gabungan dari semua metadata yang ada (actName, genId, nama objek, pool name, imm name)

            string combined = string.Format("{0} {1} {2}", actName ?? "", genId ?? "", targetGo != null ? targetGo.name : "").ToLower();

            if (targetGo != null) {

                try {

                    ImmovableBase imm = targetGo.GetComponent<ImmovableBase>();

                    if (imm == null) imm = targetGo.GetComponentInParent<ImmovableBase>();

                    if (imm == null) imm = targetGo.GetComponentInChildren<ImmovableBase>();

                    if (imm != null) {

                        string iname = imm.GetName();

                        if (!string.IsNullOrEmpty(iname)) combined += " " + iname.ToLower();

                        NaturalPrefabObject npo = imm as NaturalPrefabObject;

                        if (npo != null && !string.IsNullOrEmpty(npo.PoolName)) combined += " " + npo.PoolName.ToLower();

                    }

                } catch {}

            }

            // Cek Batu (Stone)

            if (combined.Contains("batu") || combined.Contains("rock") || combined.Contains("stone") ||

                combined.Contains("gali") || combined.Contains("pecah") || combined.Contains("tambang") ||

                combined.Contains("mine") || combined.Contains("flint") || combined.Contains("ore") ||

                combined.Contains("bongkah") || combined.Contains("crag") || combined.Contains("kerikil") ||

                combined.Contains("krikil") || combined.Contains("pebble") || combined.Contains("cobble") ||

                combined.Contains("boulder") || combined.Contains("mineral") || combined.Contains("kawah") ||

                combined.Contains("tanah liat") || combined.Contains("clay") || combined.Contains("obsidian") ||

                combined.Contains("granit") || combined.Contains("granite") || combined.Contains("basalt") ||

                combined.Contains("limestone") || combined.Contains("besi") || combined.Contains("iron") ||

                combined.Contains("tembaga") || combined.Contains("copper")) {

                return ResourceFilterMode.Stone;

            }

            // Cek Kayu/Pohon (Wood)

            if (combined.Contains("pohon") || combined.Contains("kayu") || combined.Contains("wood") ||

                combined.Contains("tree") || combined.Contains("tebang") || combined.Contains("chop") ||

                combined.Contains("log") || combined.Contains("kulit kayu") || combined.Contains("kupas") ||

                combined.Contains("ranting") || combined.Contains("branch") || combined.Contains("batang") ||

                combined.Contains("gelondong") || combined.Contains("timber")) {

                return ResourceFilterMode.Wood;

            }

            // Cek Semak (Bush)

            if (combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub") ||

                combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("buah") ||

                combined.Contains("berry") || combined.Contains("fruit")) {

                return ResourceFilterMode.Bush;

            }

            // Cek Tanaman/Rumput (Plant)

            if (combined.Contains("rumput") || combined.Contains("grass") || combined.Contains("tanaman") ||

                combined.Contains("plant") || combined.Contains("buluh") || combined.Contains("reed") ||

                combined.Contains("petik") || combined.Contains("cabut") || combined.Contains("serat") ||

                combined.Contains("fiber") || combined.Contains("herbal") || combined.Contains("herb") ||

                combined.Contains("bunga") || combined.Contains("flower") || combined.Contains("alang") ||

                combined.Contains("flax") || combined.Contains("rami") || combined.Contains("akar") ||

                combined.Contains("root")) {

                return ResourceFilterMode.Plant;

            }

            // Cek Bangkai (Corpse)

            if (combined.Contains("bangkai") || combined.Contains("corpse") || combined.Contains("dead") ||

                combined.Contains("kuliti") || combined.Contains("daging") || combined.Contains("butcher") ||

                combined.Contains("carcass") || combined.Contains("meat")) {

                return ResourceFilterMode.Corpse;

            }

            return ResourceFilterMode.All;

        }

        private void OnPlayerMenuActionClicked(InteractionData.InteractionMenuData data) {

            if (_isGathering) return; // Jika bot yang sedang klik, abaikan

            string actName = "";

            string genId = "";

            if (data.GatheringData != null) {

                if (!string.IsNullOrEmpty(data.GatheringData.Name)) actName = data.GatheringData.Name;

                if (!string.IsNullOrEmpty(data.GatheringData.GeneratorId)) genId = data.GatheringData.GeneratorId;

            }

            if (string.IsNullOrEmpty(actName) && !string.IsNullOrEmpty(data.Name)) {

                actName = data.Name;

            }

            GameObject targetGo = GetMenuTargetObject(_hookedMenuList);

            if (targetGo == null) targetGo = _lastInteractedObject;

            if (!string.IsNullOrEmpty(actName) || targetGo != null) {

                LearnTargetFromAction(actName, genId, targetGo, data.GatheringData);

            }

        }

        private static FieldInfo _gsCurrentGatheringDataField = typeof(GatheringSystem).GetField("_currentGatheringData", BindingFlags.NonPublic | BindingFlags.Instance);

        private void CapturePlayerManualGathering() {

            try {

                GatheringSystem gs = GameSystem<GatheringSystem>.Instance();

                if (gs != null && gs.IsGathering && _gsCurrentGatheringDataField != null) {

                    InteractionData.GatheringData cur = _gsCurrentGatheringDataField.GetValue(gs) as InteractionData.GatheringData;

                    if (cur != null) {

                        string gName = cur.Name;

                        string genId = cur.GeneratorId;

                        if (!string.IsNullOrEmpty(gName) || !string.IsNullOrEmpty(genId)) {

                            if (_learnedActionName != gName || _learnedGeneratorId != genId) {

                                GameObject targetGo = _lastInteractedObject;

                                if (targetGo == null) {

                                    try {

                                        InteractionSystem isys = GameSystem<InteractionSystem>.Instance();

                                        if (isys != null) {

                                            if (isys.Target != null && isys.Target.Target != null) targetGo = isys.Target.Target;

                                            else if (isys.LastInteractionTarget != null && isys.LastInteractionTarget.Target != null) targetGo = isys.LastInteractionTarget.Target;

                                        }

                                    } catch {}

                                }

                                LearnTargetFromAction(gName, genId, targetGo, cur);

                            }

                        }

                    }

                }

            } catch {}

        }

        public void LearnTargetFromAction(string actName, string genId, GameObject targetGo) {

            LearnTargetFromAction(actName, genId, targetGo, null);

        }

        public void LearnTargetFromAction(string actName, string genId, GameObject targetGo, InteractionData.GatheringData gatherData) {

            if (string.IsNullOrEmpty(actName) && targetGo == null) return;

            if (!string.IsNullOrEmpty(actName)) {

                _learnedActionName = actName.Trim();

            }

            if (!string.IsNullOrEmpty(genId)) {

                _learnedGeneratorId = genId.Trim();

            }

            string cleanName = "";

            string displayName = "";

            Durango.Terrain.BiomeSpriteInfo spr = null;

            if (targetGo != null) {

                _lastInteractedObject = targetGo;

                cleanName = targetGo.name.Replace("(Clone)", "").Replace("(clone)", "").Trim();

                try {

                    ImmovableBase imm = targetGo.GetComponent<ImmovableBase>();

                    if (imm == null) imm = targetGo.GetComponentInParent<ImmovableBase>();

                    if (imm == null) imm = targetGo.GetComponentInChildren<ImmovableBase>();

                    if (imm != null) {

                        try {

                            spr = Durango.Terrain.DataHelper.GetBiomeSpriteInfo((int)imm.EntityType);

                        } catch {}

                        string immName = imm.GetName();

                        if (!string.IsNullOrEmpty(immName)) {

                            displayName = immName;

                        }

                        NaturalPrefabObject npo = imm as NaturalPrefabObject;

                        if (npo != null && !string.IsNullOrEmpty(npo.PoolName)) {

                            cleanName = npo.PoolName;

                            if (string.IsNullOrEmpty(displayName)) displayName = npo.PoolName;

                        }

                    }

                } catch {}

                if (string.IsNullOrEmpty(displayName)) {

                    displayName = CleanObjectName(cleanName);

                }

            }

            // Tentukan kategori filter dengan logika cerdas terpadu

            _learnedFilter = ClassifyResourceFilter(actName, genId, targetGo, gatherData, spr);

            CurrentFilter = _learnedFilter; // Sinkronkan filter aktif

            // Simpan base name jika bukan nama generik Unity engine

            if (!IsGenericEngineName(cleanName)) {

                _learnedNodeBaseName = cleanName;

            } else if (!string.IsNullOrEmpty(genId) && !IsGenericEngineName(genId)) {

                _learnedNodeBaseName = genId;

            } else {

                _learnedNodeBaseName = "";

            }

            if (string.IsNullOrEmpty(displayName) || IsGenericEngineName(displayName)) {

                if (!string.IsNullOrEmpty(_learnedActionName)) displayName = _learnedActionName;

                else if (!string.IsNullOrEmpty(_learnedGeneratorId)) displayName = _learnedGeneratorId;

                else displayName = string.Format("Node {0}", _learnedFilter);

            }

            _learnedNodeDisplayName = displayName;

            _hasLearnedTarget = true;

            _statusText = string.Format("Target Terkunci: '{0}' ({1} - Kategori: {2})!", _learnedActionName, _learnedNodeDisplayName, _learnedFilter);

            Debug.Log(string.Format("[AutoGather] Smart Learn: Aksi='{0}', GenId='{1}', Node='{2}', Kategori='{3}'", _learnedActionName, _learnedGeneratorId, _learnedNodeDisplayName, _learnedFilter));

            RequestSaveSettings();

        }

        public void ResetLearnedTarget() {

            _learnedActionName = "";

            _learnedGeneratorId = "";

            _learnedNodeBaseName = "";

            _learnedNodeDisplayName = "";

            _learnedFilter = ResourceFilterMode.All;

            CurrentFilter = ResourceFilterMode.All;

            _hasLearnedTarget = false;

            _currentTargetObj = null;

            _statusText = "Menunggu Anda memanen 1x untuk deteksi target...";

            RequestSaveSettings();

        }

        private bool MatchesFilterDirect(GameObject go, string name, Durango.Terrain.BiomeSpriteInfo spriteInfo, ResourceFilterMode filter) {

            if (filter == ResourceFilterMode.All) return true;

            string combined = ((name ?? "") + " " + (go != null ? go.name : "")).ToLower();

            if (filter == ResourceFilterMode.Corpse) {

                bool isDead = false;

                try { if (go != null && ObjectIdentifier.IsDeadBody(go)) isDead = true; } catch {}

                return isDead || combined.Contains("bangkai") || combined.Contains("corpse") || combined.Contains("dead");

            }

            if (spriteInfo == null && go != null) {

                try {

                    NaturalSpriteObject nso = go.GetComponent<NaturalSpriteObject>();

                    if (nso == null) nso = go.GetComponentInParent<NaturalSpriteObject>();

                    if (nso == null) nso = go.GetComponentInChildren<NaturalSpriteObject>();

                    if (nso != null && nso.Sprite != null) {

                        var st = nso.Sprite.SpriteObjectType;

                        if (filter == ResourceFilterMode.Stone && (st == Durango.Render.Sprite.SpriteObjectType.Rock || st == Durango.Render.Sprite.SpriteObjectType.Pebble)) return true;

                        if (filter == ResourceFilterMode.Wood && st == Durango.Render.Sprite.SpriteObjectType.Tree) return true;

                        if (filter == ResourceFilterMode.Bush && st == Durango.Render.Sprite.SpriteObjectType.Shrub) return true;

                        if (filter == ResourceFilterMode.Plant && st == Durango.Render.Sprite.SpriteObjectType.Grass) return true;

                    }

                } catch {}

            }

            if (filter == ResourceFilterMode.Wood) return IsWoodNode(combined, spriteInfo);

            if (filter == ResourceFilterMode.Stone) return IsStoneNode(combined, spriteInfo);

            if (filter == ResourceFilterMode.Bush) return IsBushNode(combined, spriteInfo);

            if (filter == ResourceFilterMode.Plant) return IsPlantNode(combined, spriteInfo);

            return true;

        }

        private bool DoesNodeMatchLearnedTarget(GameObject go, string rawName, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            if (go == null) return false;

            // 1. Kategori Filter Wajib: Node WAJIB lolos kategori filter yang dipelajari!

            if (_learnedFilter != ResourceFilterMode.All) {

                if (!MatchesFilterDirect(go, rawName, spriteInfo, _learnedFilter)) {

                    return false;

                }

            }

            string goName = (go.name ?? "").ToLower().Replace("(clone)", "").Trim();

            string combined = (goName + " " + (rawName ?? "")).ToLower();

            // 2. Cek kesesuaian Generator ID jika ada (sangat presisi)

            if (!string.IsNullOrEmpty(_learnedGeneratorId)) {

                string genLow = _learnedGeneratorId.ToLower();

                if (combined.Contains(genLow) || goName.Contains(genLow)) return true;

            }

            // 3. Cek kesesuaian base name prefab (hanya jika bukan nama generik Unity engine!)

            if (!string.IsNullOrEmpty(_learnedNodeBaseName) && !IsGenericEngineName(_learnedNodeBaseName)) {

                string baseLow = _learnedNodeBaseName.ToLower();

                if (goName.Contains(baseLow) || combined.Contains(baseLow)) return true;

            }

            // 4. Cek kata kunci aksi yang cocok dengan nama node (misal: "batu", "pohon", "semak", "ranting")

            if (!string.IsNullOrEmpty(_learnedActionName)) {

                string actLow = _learnedActionName.ToLower();

                string[] actWords = actLow.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

                for (int w = 0; w < actWords.Length; w++) {

                    string word = actWords[w];

                    if (word.Length > 2 && word != "ambil" && word != "petik" && word != "cabut" && word != "kupas" && word != "gali") {

                        if (combined.Contains(word)) return true;

                    }

                }

            }

            // 5. Jika lolos filter kategori spesifik (Stone / Wood / Bush / Plant), jadikan target yang valid!

            if (_learnedFilter != ResourceFilterMode.All) {

                return true;

            }

            return false;

        }

        public void ToggleMod() {

            IsEnabled = !IsEnabled;

            if (!IsEnabled) {

                _statusText = string.Format("Standby (Tekan [{0}] untuk Aktifkan)", ToggleKey);

                _currentTargetObj = null;

                _targetAvailableResources = "-";

                _isGathering = false;

                ForceCloseInteractionMenu();

                try {

                    PlayerController controller = GetPlayerController();

                    LocalMoveNavigator nav = GetNavigator(controller);

                    if (controller != null) controller.StopMove();

                    if (nav != null) nav.Stop();

                } catch {}

            } else {

                IsCraftPlanActive = false;

                _nextScanTime = 0f;

                _targetAvailableResources = "-";

                _currentTargetObj = null;

                if (_hasLearnedTarget) {

                    _statusText = string.Format("Auto-Panen Aktif: Berburu '{0}' ({1})!", _learnedActionName, _learnedNodeDisplayName);

                } else {

                    _statusText = "<color=#f1c40f><b>Menunggu Anda Memanen 1x...</b></color> (Dekati & panen objek di game)";

                }

            }

        }

        public void ToggleCombatMod() {

            IsCombatBotEnabled = !IsCombatBotEnabled;

            if (!IsCombatBotEnabled) {

                _combatStatusText = string.Format("Standby (Tekan [{0}] untuk Aktifkan Combat)", CombatToggleKey);

                _currentCombatTarget = null;

                _combatActionStatus = "-";

                _hasActiveAttackAlert = false;

                _pendingDodgeOnAlert = false;

                _isPlayerInsideHitbox = false;

                try {

                    PlayerController controller = GetPlayerController();

                    LocalMoveNavigator nav = GetNavigator(controller);

                    if (controller != null) controller.StopMove();

                    if (nav != null) nav.Stop();

                } catch {}

                if (CombatSystem.HasInstance()) {

                    try { CombatSystem.Instance().ClearTarget(); } catch {}

                }

            } else {

                // Matikan gather bot jika combat dinyalakan agar tidak saling berebut gerak

                IsEnabled = false;

                IsCraftPlanActive = false;

                _currentCombatTarget = null;

                _combatStatusText = "Mencari dino musuh...";

                _lastCombatScanTime = 0f;

            }

        }

        private void CycleCombatToggleKey() {

            if (CombatToggleKey == KeyCode.F2) CombatToggleKey = KeyCode.F3;

            else if (CombatToggleKey == KeyCode.F3) CombatToggleKey = KeyCode.F4;

            else if (CombatToggleKey == KeyCode.F4) CombatToggleKey = KeyCode.G;

            else if (CombatToggleKey == KeyCode.G) CombatToggleKey = KeyCode.R;

            else if (CombatToggleKey == KeyCode.R) CombatToggleKey = KeyCode.F2;

            else CombatToggleKey = KeyCode.F2;

            if (!IsCombatBotEnabled) {

                _combatStatusText = string.Format("Standby (Tekan [{0}] untuk Aktifkan Combat)", CombatToggleKey);

            }

        }

        private void TryHookCombatAlert() {

            if (_isCombatAlertHooked) return;

            try {

                if (CombatSystem.HasInstance()) {

                    CombatSystem cs = CombatSystem.Instance();

                    if (cs != null) {

                        cs.AttackAlerted += OnEnemyAttackAlerted;

                        _isCombatAlertHooked = true;

                        Debug.Log("[AutoGatherMod] Hook CombatSystem.AttackAlerted BERHASIL!");

                    }

                }

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGatherMod] Hook CombatAlert error: " + ex);

            }

        }

        private bool IsPlayerBusyActing(PlayerBehavior player) {

            if (player == null) return false;

            // 1. Cek status UsingAction pada CombatSystem

            if (CombatSystem.HasInstance()) {

                CombatSystem cs = CombatSystem.Instance();

                if (cs != null && cs.UsingAction != null && cs.UsingAction.HasValue()) {

                    UsingAction.State st = cs.UsingAction.GetState();

                    if (st != UsingAction.State.None) {

                        return true;

                    }

                }

            }

            // 2. Cek apakah masih dalam rentang waktu animasi serangan aktif

            if (Time.time < _attackAnimationFinishTime) {

                return true;

            }

            return false;

        }

        private bool IsPlayerInsideHitbox(Messages.AttackAlerted msg, Vector3 playerPos, DamageableEntity attacker, out string shapeDesc) {

            shapeDesc = "None";

            Vector3 center = Vector3.zero;

            if (msg.Center.HasValue) {

                center = msg.Center.Value.ToClientPosition();

            } else if (attacker != null) {

                center = attacker.GetCurrentPosition();

            } else {

                return false;

            }

            float yaw = msg.Yaw.HasValue ? msg.Yaw.Value : 0f;

            // 1. Hitbox Persegi Panjang (Oriented Rectangle / Forward Box)

            if (msg.RectSizeHalves.HasValue) {

                Vector2 halves = msg.RectSizeHalves.Value;

                float halfWidth = Mathf.Max(50f, halves.x);

                float halfLength = Mathf.Max(50f, halves.y);

                shapeDesc = string.Format("Rect({0:F1}x{1:F1}m, yaw:{2:F0})", (halfWidth * 2f) / 100f, (halfLength * 2f) / 100f, yaw);

                Vector3 toPlayer = playerPos - center;

                float rad = yaw * Mathf.Deg2Rad;

                Vector2 forward2D = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

                Vector2 right2D = new Vector2(forward2D.y, -forward2D.x);

                float dForward = toPlayer.x * forward2D.x + toPlayer.z * forward2D.y;

                float dLateral = toPlayer.x * right2D.x + toPlayer.z * right2D.y;

                return (Mathf.Abs(dForward) <= halfLength && Mathf.Abs(dLateral) <= halfWidth);

            }

            // 2. Hitbox Lingkaran / Busur (Arc / Circle)

            if (msg.Radius.HasValue && msg.Radius.Value > 0f) {

                float radius = msg.Radius.Value;

                Vector3 diff = playerPos - center;

                diff.y = 0f;

                float dist = diff.magnitude;

                if (dist > radius) {

                    shapeDesc = string.Format("Lingkaran({0:F1}m, Jarak:{1:F1}m)", radius / 100f, dist / 100f);

                    return false;

                }

                if (msg.Angles.HasValue) {

                    Vector2 angles = msg.Angles.Value;

                    float startAngle = Mathf.Repeat(yaw + angles.x, 360f);

                    float endAngle = Mathf.Repeat(yaw + angles.y, 360f);

                    float playerAngle = Mathf.Repeat(Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg, 360f);

                    shapeDesc = string.Format("Busur/Arc({0:F1}m, {1:F0}-{2:F0} deg)", radius / 100f, startAngle, endAngle);

                    if (startAngle <= endAngle) {

                        return (playerAngle >= startAngle && playerAngle <= endAngle);

                    } else {

                        return (playerAngle >= startAngle || playerAngle <= endAngle);

                    }

                } else {

                    shapeDesc = string.Format("Lingkaran({0:F1}m)", radius / 100f);

                    return true;

                }

            }

            return false;

        }

        private Vector3 CalculateSafeEscapePosition(Messages.AttackAlerted msg, Vector3 playerPos, DamageableEntity attacker, float safetyBuffer) {

            Vector3 center = Vector3.zero;

            if (msg.Center.HasValue) {

                center = msg.Center.Value.ToClientPosition();

            } else if (attacker != null) {

                center = attacker.GetCurrentPosition();

            } else {

                return playerPos;

            }

            float yaw = msg.Yaw.HasValue ? msg.Yaw.Value : 0f;

            // Hitbox Persegi Panjang: Keluar ke sisi samping (lateral) terdekat

            if (msg.RectSizeHalves.HasValue) {

                Vector2 halves = msg.RectSizeHalves.Value;

                float halfWidth = Mathf.Max(50f, halves.x);

                float halfLength = Mathf.Max(50f, halves.y);

                Vector3 toPlayer = playerPos - center;

                float rad = yaw * Mathf.Deg2Rad;

                Vector2 forward2D = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

                Vector2 right2D = new Vector2(forward2D.y, -forward2D.x);

                float dForward = toPlayer.x * forward2D.x + toPlayer.z * forward2D.y;

                float dLateral = toPlayer.x * right2D.x + toPlayer.z * right2D.y;

                float safeFwd = Mathf.Clamp(dForward, -halfLength, halfLength);

                float signLateral = (dLateral >= 0f) ? 1f : -1f;

                float safeLat = signLateral * (halfWidth + safetyBuffer);

                Vector3 safeExit = center + new Vector3(forward2D.x * safeFwd + right2D.x * safeLat, 0f, forward2D.y * safeFwd + right2D.y * safeLat);

                safeExit.y = playerPos.y;

                return safeExit;

            }

            // Hitbox Lingkaran / Busur: Keluar secara radial menjauhi pusat

            if (msg.Radius.HasValue && msg.Radius.Value > 0f) {

                float radius = msg.Radius.Value;

                Vector3 diff = playerPos - center;

                diff.y = 0f;

                if (diff.sqrMagnitude < 1f) {

                    float rad = yaw * Mathf.Deg2Rad;

                    diff = new Vector3(-Mathf.Sin(rad), 0f, -Mathf.Cos(rad));

                }

                Vector3 dir = diff.normalized;

                Vector3 safeExit = center + dir * (radius + safetyBuffer);

                safeExit.y = playerPos.y;

                return safeExit;

            }

            // Fallback aman jika shape tidak terdefinisi

            Vector3 away = playerPos - center;

            away.y = 0f;

            if (away.sqrMagnitude < 1f) away = -playerPos.normalized;

            away.Normalize();

            return playerPos + away * safetyBuffer;

        }

        private void ExecuteDodgeEscape(PlayerBehavior player, PlayerController controller, LocalMoveNavigator nav, float escapeYaw, Vector3 safePos) {

            try {

                // KRUSIAL: Dodge roll di Durango berguling maju searah hadap (Forward root motion)!

                // Putar hadap karakter ke arah jalur keluar sebelum UseBattleAction!

                if (player != null) {

                    player.TurnToYaw(escapeYaw, true);

                    player.transform.rotation = Quaternion.Euler(0f, escapeYaw, 0f);

                }

                if (controller != null) controller.TurnToYaw(escapeYaw, true);

                BattleAction dodge = GetDodgeAction();

                if (dodge != null && CombatSystem.HasInstance() && CombatSystem.Instance().IsUsableAction(dodge)) {

                    // DODGE READY: Langsung guling keluar dari hitbox!

                    CombatSystem.Instance().UseBattleAction(dodge.Data.Id);

                    _lastDodgeTime = Time.time;

                    float dodgeLen = (dodge.Data != null && dodge.Data.Meta != null && dodge.Data.Meta.ActionLength > 0.05f) 

                        ? dodge.Data.Meta.ActionLength 

                        : 0.7f;

                    _attackAnimationFinishTime = Time.time + dodgeLen;

                    _nextAttackAllowedTime = _attackAnimationFinishTime + 0.25f;

                    _combatActionStatus = string.Format("<color=#2ecc71>[DODGE ROLL] Guling keluar dari hitbox ({0})!</color>", _hitboxShapeInfo);

                    Debug.Log("[AutoGatherMod] Dodge Roll executed outward with yaw " + escapeYaw);

                } else {

                    // DODGE COOLDOWN / STAMINA KURANG: Lari keluar ke titik aman

                    _combatActionStatus = string.Format("<color=#e67e22>[DODGE CD / LARI] Menghindar lari keluar dari hitbox ({0})!</color>", _hitboxShapeInfo);

                    if (EvadeWhenCooldown && nav != null) {

                        nav.MoveToPosition(safePos, null, 30f, true);

                    }

                }

            } catch (Exception ex) {

                Debug.LogWarning("[AutoGatherMod] ExecuteDodgeEscape error: " + ex);

            }

        }

        private void OnEnemyAttackAlerted(Messages.AttackAlerted msg) {

            if (!IsCombatBotEnabled) return;

            PlayerBehavior player = PlayerBehavior.LocalPlayer;

            if (player == null) return;

            Vector3 pPos = player.CurrentPosition;

            if (pPos == Vector3.zero) pPos = player.transform.position;

            DamageableEntity attacker = null;

            if (CombatSystem.HasInstance()) {

                CombatSystem cs = CombatSystem.Instance();

                if (!string.IsNullOrEmpty(msg.EntityId) && cs.DamageableEntities != null) {

                    attacker = cs.DamageableEntities.Find(msg.EntityId);

                }

                if (attacker == null) attacker = _currentCombatTarget;

            }

            _lastAttackAlertTime = Time.time;

            _lastAttackAlertMsg = msg;

            _hasActiveAttackAlert = true;

            // Hitung durasi serangan dari event alert Durango secara AMAN!

            // msg.AttackTime adalah Unix timestamp server. Durango menghitung sisa waktu terhadap GetPredictedServerTime().

            float alertDuration = 2.0f;

            try {

                if (Durango.Network.Connections.Frontend != null) {

                    double sTime = Durango.Network.Connections.Frontend.GetPredictedServerTime();

                    if (msg.AttackTime > sTime) {

                        float remain = (float)(msg.AttackTime - sTime);

                        if (remain > 0.2f && remain < 5.0f) {

                            alertDuration = remain;

                        }

                    }

                }

            } catch {}

            // Batasi ketat durasi tunggu: minimal 1.5 detik, maksimal 3.5 detik (mencegah bug freeze 1.7 miliar detik)

            _alertClearTime = Time.time + Mathf.Clamp(alertDuration + 0.8f, 1.5f, 3.5f);

            PlayerController controller = GetPlayerController();

            LocalMoveNavigator nav = GetNavigator(controller);

            // 1. Cek apakah posisi player saat ini benar-benar ada di dalam hitbox serangan dino!

            _isPlayerInsideHitbox = IsPlayerInsideHitbox(msg, pPos, attacker, out _hitboxShapeInfo);

            if (!_isPlayerInsideHitbox) {

                // Posisi player sudah aman di luar hitbox! Segera stop gerak agar tidak berjalan masuk

                if (nav != null) nav.Stop();

                if (controller != null) controller.StopMove();

                _combatActionStatus = string.Format("<color=#2ecc71>[AMAN] Di luar hitbox ({0}), menunggu serangan musuh selesai...</color>", _hitboxShapeInfo);

                return;

            }

            // 2. Hitung titik keluar dan sudut rotasi paling aman (keluar dari kotak / lingkaran)

            _lastSafeEscapePos = CalculateSafeEscapePosition(msg, pPos, attacker, 180f); // 1.8m safety buffer

            Vector3 escapeVec = _lastSafeEscapePos - pPos;

            escapeVec.y = 0f;

            if (escapeVec.sqrMagnitude < 0.1f) {

                float dinoYaw = msg.Yaw.HasValue ? msg.Yaw.Value : 0f;

                float rad = dinoYaw * Mathf.Deg2Rad;

                escapeVec = new Vector3(-Mathf.Sin(rad), 0f, -Mathf.Cos(rad));

            }

            _pendingEscapeYaw = Mathf.Atan2(escapeVec.x, escapeVec.z) * Mathf.Rad2Deg;

            if (!AutoDodgeEnabled) return;

            // 3. Cek apakah player sedang terkunci dalam animasi serangan (Hanya 1 aksi dalam 1 saat!)

            if (IsPlayerBusyActing(player)) {

                // Tahan dan buffer perintah dodge! Segera dieksekusi saat animasi serangan selesai

                _pendingDodgeOnAlert = true;

                _combatActionStatus = string.Format("<color=#f39c12>[BUFFER DODGE] Terkunci animasi serang, segera Dodge ke luar hitbox ({0}) saat selesai!</color>", _hitboxShapeInfo);

                return;

            }

            // 4. Player tidak sibuk -> Langsung putar hadap ke jalur keluar dan Roll keluar dari hitbox!

            ExecuteDodgeEscape(player, controller, nav, _pendingEscapeYaw, _lastSafeEscapePos);

        }

        private BattleAction GetDodgeAction() {

            if (!CombatSystem.HasInstance()) return null;

            CombatSystem cs = CombatSystem.Instance();

            var actions = cs.GetCurrentBattleActions();

            if (actions != null) {

                foreach (var act in actions) {

                    if (act == null || act.Data == null) continue;

                    if (act.Data.Meta != null && act.Data.Meta.BattleActionType == BattleActionType.Dodge) return act;

                    if (act.Data.Id != null && (act.Data.Id.ToLower().Contains("dodge") || act.Data.Id.ToLower().Contains("roll") || act.Data.Id.ToLower().Contains("evade"))) return act;

                }

            }

            if (cs.ActionSlots != null) {

                foreach (var act in cs.ActionSlots) {

                    if (act == null || act.Data == null) continue;

                    if (act.Data.Meta != null && act.Data.Meta.BattleActionType == BattleActionType.Dodge) return act;

                    if (act.Data.Id != null && (act.Data.Id.ToLower().Contains("dodge") || act.Data.Id.ToLower().Contains("roll") || act.Data.Id.ToLower().Contains("evade"))) return act;

                }

            }

            return null;

        }

        private bool IsDodgeReady() {

            BattleAction d = GetDodgeAction();

            if (d == null) return false;

            if (!CombatSystem.HasInstance()) return false;

            return CombatSystem.Instance().IsUsableAction(d);

        }

        private BattleAction SelectBestAttackAction(CombatSystem cs, PlayerBehavior player) {

            if (cs == null) return null;

            float curStam = (player != null && player.Stamina != null) ? player.Stamina.Get() : 100f;

            BattleAction basicAttack = null;

            List<BattleAction> specialSkills = new List<BattleAction>();

            // 1. Prioritas Utama: Ambil dari ActionSlots yang terpasang di HUD skill bar pemain

            // Di Durango, slot index 0 = Basic Attack, slot index 1..N = Active Special Skills!

            BattleAction[] slots = cs.ActionSlots;

            if (slots != null && slots.Length > 0) {

                for (int i = 0; i < slots.Length; i++) {

                    BattleAction act = slots[i];

                    if (act == null || act.Data == null || act.Data.Meta == null) continue;

                    // Abaikan skill Dodge (khusus tombol Roll/Menghindar)

                    if (act.Data.Meta.BattleActionType == BattleActionType.Dodge) continue;

                    if (act.Data.Id != null && (act.Data.Id.ToLower().Contains("dodge") || act.Data.Id.ToLower().Contains("roll"))) continue;

                    float cd = (act.Data.Meta.Cooldown > 0f) ? act.Data.Meta.Cooldown : act.Cooldown;

                    if (i == 0 && cd <= 0.5f) {

                        if (basicAttack == null) basicAttack = act;

                    } else {

                        if (!specialSkills.Contains(act)) {

                            specialSkills.Add(act);

                        }

                    }

                }

            }

            // 2. Tambahan dari GetCurrentBattleActions() jika ada aksi serang yang belum masuk slot

            var allActions = cs.GetCurrentBattleActions();

            if (allActions != null) {

                foreach (var act in allActions) {

                    if (act == null || act.Data == null || act.Data.Meta == null) continue;

                    if (act.Data.Meta.BattleActionType == BattleActionType.Dodge) continue;

                    if (act.Data.Id != null && (act.Data.Id.ToLower().Contains("dodge") || act.Data.Id.ToLower().Contains("roll"))) continue;

                    int slotId = (act.Data.Meta.Slot != null) ? act.Data.Meta.Slot.Id : -1;

                    float cd = (act.Data.Meta.Cooldown > 0f) ? act.Data.Meta.Cooldown : act.Cooldown;

                    if (slotId == 0 && cd <= 0.5f) {

                        if (basicAttack == null) basicAttack = act;

                    } else if (slotId > 0 || cd > 0.5f) {

                        if (!specialSkills.Contains(act)) {

                            specialSkills.Add(act);

                        }

                    } else {

                        if (basicAttack == null) basicAttack = act;

                    }

                }

            }

            // PRIORITAS 1: ROTASI SKILL AKTIF (SPECIAL SKILLS)

            // Jalankan skill aktif yang sedang OFF-COOLDOWN / READY

            if (AutoCastSkills && specialSkills.Count > 0) {

                for (int i = 0; i < specialSkills.Count; i++) {

                    BattleAction skill = specialSkills[i];

                    if (skill == null || skill.Data == null) continue;

                    // Cek apakah skill ini usable di Durango CombatSystem (tidak CD, tidak prohibited)

                    if (!cs.IsUsableAction(skill)) {

                        continue;

                    }

                    float cost = skill.Stamina > 0 ? skill.Stamina : (skill.Data.Meta != null ? skill.Data.Meta.Stamina : 0f);

                    if (cost > 0f && curStam < cost) {

                        continue;

                    }

                    // SKILL AKTIF SIAP DIEKSEKUSI!

                    return skill;

                }

            }

            // PRIORITAS 2: Gunakan Basic Attack jika tidak ada skill aktif yang ready

            if (basicAttack != null && cs.IsUsableAction(basicAttack)) {

                return basicAttack;

            }

            // Fallback: Aksi serang apa pun dari special skills yang usable

            if (specialSkills.Count > 0) {

                for (int i = 0; i < specialSkills.Count; i++) {

                    if (cs.IsUsableAction(specialSkills[i])) return specialSkills[i];

                }

            }

            // Fallback: Aksi serang apa pun dari allActions yang usable

            if (allActions != null) {

                foreach (var act in allActions) {

                    if (act == null || act.Data == null || act.Data.Meta == null) continue;

                    if (act.Data.Meta.BattleActionType == BattleActionType.Dodge) continue;

                    if (cs.IsUsableAction(act)) return act;

                }

            }

            return basicAttack;

        }

        private DamageableEntity FindCombatTarget(CombatSystem cs, PlayerBehavior player, float maxDist) {

            Vector3 pPos = player.CurrentPosition;

            if (pPos == Vector3.zero) pPos = player.transform.position;

            // 1. Cek target yang sedang dikunci

            if (cs.Target != null && cs.Target.Value != null && cs.Target.Value.IsAlive) {

                return cs.Target.Value;

            }

            if (CombatTargetFilter == CombatTargetMode.LockedOnly) {

                return null;

            }

            // 2. Cek musuh dari DamageableEntities

            DamageableEntity closest = null;

            float closestDist = maxDist;

            if (cs.DamageableEntities != null) {

                var enemies = cs.DamageableEntities.GetEnemies();

                if (enemies != null) {

                    foreach (var enemy in enemies) {

                        if (enemy == null || !enemy.IsAlive) continue;

                        float d = Vector3.Distance(pPos, enemy.GetCurrentPosition());

                        if (d < closestDist) {

                            closestDist = d;

                            closest = enemy;

                        }

                    }

                }

            }

            if (closest != null || CombatTargetFilter == CombatTargetMode.AggressiveOnly) {

                return closest;

            }

            // 3. Mode AllNear: cari hewan/dino (AnimalBehavior) di sekitar jika belum agresif

            try {

                AnimalBehavior[] animals = UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();

                if (animals != null) {

                    AnimalBehavior closestAnimal = null;

                    for (int i = 0; i < animals.Length; i++) {

                        AnimalBehavior a = animals[i];

                        if (a == null || !a.IsAlive) continue;

                        float d = Vector3.Distance(pPos, a.CurrentPosition);

                        if (d < closestDist) {

                            closestDist = d;

                            closestAnimal = a;

                        }

                    }

                    if (closestAnimal != null && cs.DamageableEntities != null) {

                        closest = cs.DamageableEntities.Find(closestAnimal.EntityId);

                        if (closest == null) {

                            closest = cs.DamageableEntities.Get(closestAnimal.EntityId);

                        }

                    }

                }

            } catch {}

            return closest;

        }

        private void UpdateCombat() {

            if (!IsCombatBotEnabled) return;

            PlayerBehavior player = PlayerBehavior.LocalPlayer;

            if (player == null) {

                _combatStatusText = "Menunggu Karakter...";

                return;

            }

            PlayerController controller = GetPlayerController();

            if (controller == null) {

                _combatStatusText = "Controller tidak ditemukan";

                return;

            }

            LocalMoveNavigator nav = GetNavigator(controller);

            if (!CombatSystem.HasInstance()) {

                _combatStatusText = "CombatSystem tidak aktif";

                return;

            }

            CombatSystem cs = CombatSystem.Instance();

            // Clear expired attack alert (setelah swing/hitbox selesai seutuhnya)

            if (_hasActiveAttackAlert && Time.time >= _alertClearTime) {

                _hasActiveAttackAlert = false;

                _pendingDodgeOnAlert = false;

                _isPlayerInsideHitbox = false;

                _nextAttackAllowedTime = Mathf.Max(_nextAttackAllowedTime, Time.time + 0.2f);

            }

            Vector3 pPos = player.CurrentPosition;

            if (pPos == Vector3.zero) pPos = player.transform.position;

            // Prioritas 1: Eksekusi pending dodge saat animasi serangan baru saja selesai

            if (_pendingDodgeOnAlert && _hasActiveAttackAlert) {

                if (!IsPlayerBusyActing(player)) {

                    _pendingDodgeOnAlert = false;

                    ExecuteDodgeEscape(player, controller, nav, _pendingEscapeYaw, _lastSafeEscapePos);

                    return;

                } else {

                    _combatStatusText = "<color=#e74c3c>Menunggu animasi selesai untuk Dodge Roll keluar hitbox...</color>";

                    return;

                }

            }

            // Prioritas 2: Jika dino sedang telegraph serangan (hitbox aktif), fokus bertahan & keluar!

            if (_hasActiveAttackAlert) {

                _isPlayerInsideHitbox = IsPlayerInsideHitbox(_lastAttackAlertMsg, pPos, _currentCombatTarget, out _hitboxShapeInfo);

                if (_isPlayerInsideHitbox) {

                    _combatStatusText = string.Format("<color=#e74c3c>[AWAS! DALAM HITBOX: {0}] Menghindar keluar!</color>", _hitboxShapeInfo);

                    if (!IsPlayerBusyActing(player)) {

                        Vector3 safePos = CalculateSafeEscapePosition(_lastAttackAlertMsg, pPos, _currentCombatTarget, 180f);

                        Vector3 escVec = safePos - pPos;

                        escVec.y = 0f;

                        float escYaw = Mathf.Atan2(escVec.x, escVec.z) * Mathf.Rad2Deg;

                        ExecuteDodgeEscape(player, controller, nav, escYaw, safePos);

                    }

                    return;

                } else {

                    // SUDAH DI LUAR HITBOX!

                    // SANGAT PENTING: BERHENTI BERGERAK DAN TAHAN SERANGAN!

                    // Tunggu sampai hitbox & animasi swing dino selesai total sebelum maju/menyerang

                    if (nav != null) nav.Stop();

                    if (controller != null) controller.StopMove();

                    float sisaWaktu = Mathf.Max(0f, _alertClearTime - Time.time);

                    _combatStatusText = string.Format("<color=#2ecc71>[AMAN DI LUAR HITBOX: {0}] Menunggu serangan selesai ({1:F1}s)...</color>", _hitboxShapeInfo, sisaWaktu);

                    _combatActionStatus = string.Format("<color=#3498db>Menunggu hitbox & swing musuh selesai ({0:F1}s)...</color>", sisaWaktu);

                    return;

                }

            }

            // Stamina Management

            float curStamina = (player.Stamina != null) ? player.Stamina.Get() : 100f;

            // Target Validation / Refresh

            if (_currentCombatTarget == null || !_currentCombatTarget.IsAlive) {

                if (_currentCombatTarget != null && !_currentCombatTarget.IsAlive) {

                    _combatStatusText = string.Format("Dino {0} Dikalahkan!", _currentCombatTarget.GetName());

                    if (AutoHarvestKilledDino) {

                        CurrentFilter = ResourceFilterMode.Corpse;

                        _currentTargetObj = null;

                        _statusText = "Memanen bangkai dino hasil buruan...";

                    }

                }

                if (Time.time >= _lastCombatScanTime) {

                    _lastCombatScanTime = Time.time + 0.45f;

                    _currentCombatTarget = FindCombatTarget(cs, player, CombatScanRadiusMeters * 100f);

                }

                if (_currentCombatTarget == null) {

                    _combatStatusText = string.Format("Mencari dino musuh dalam radius {0:F0}m...", CombatScanRadiusMeters);

                    return;

                }

            }

            // Lock Target di engine

            if (cs.Target == null || cs.Target.Value != _currentCombatTarget) {

                cs.SelectTarget(_currentCombatTarget);

            }

            // Hitung jarak ke dino

            Vector3 targetPos = _currentCombatTarget.GetCurrentPosition();

            float dist = Vector3.Distance(pPos, targetPos);

            // Jangkauan serang (default 220 = 2.2m melee)

            float useRange = 220f;

            BattleAction previewAct = SelectBestAttackAction(cs, player);

            if (previewAct != null && previewAct.Data != null && previewAct.Data.Meta != null && previewAct.Data.Meta.UseRange > 0f) {

                useRange = Mathf.Max(200f, previewAct.Data.Meta.UseRange * 100f);

            }

            if (dist > useRange) {

                // Mendekati musuh (POSITIONING)

                _combatStatusText = string.Format("Mendekati {0} ({1:F1}m)", _currentCombatTarget.GetName(), dist / 100f);

                if (!IsPlayerBusyActing(player) && nav != null && Time.time >= _lastCombatMoveTime) {

                    _lastCombatMoveTime = Time.time + 0.25f;

                    nav.MoveToPosition(targetPos, null, useRange * 0.8f, true);

                }

            } else {

                // Di dalam jangkauan serang!

                _combatStatusText = string.Format("Bertarung dengan {0} ({1:F1}m)", _currentCombatTarget.GetName(), dist / 100f);

                // Hentikan lari saat sudah di jarak serang agar motion skill valid

                if (nav != null) nav.Stop();

                if (controller != null) controller.StopMove();

                // Hadapkan karakter langsung ke dino

                Vector3 toEnemy = targetPos - pPos;

                toEnemy.y = 0f;

                if (toEnemy.sqrMagnitude > 0.01f) {

                    float yawToEnemy = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;

                    player.TurnToYaw(yawToEnemy, true);

                    controller.TurnToYaw(yawToEnemy, true);

                }

                // Cek apakah player sedang sibuk dalam animasi aksi (1 aksi dalam 1 saat!)

                if (IsPlayerBusyActing(player)) {

                    return;

                }

                // Cek apakah jeda waktu antar-serangan sudah lewat

                if (Time.time < _nextAttackAllowedTime) {

                    return;

                }

                // Pilih skill serang terbaik (prioritaskan active special skills off-cd, fallback basic attack)

                BattleAction attackAct = SelectBestAttackAction(cs, player);

                if (attackAct != null) {

                    float actLen = 0.8f;

                    if (attackAct.Data != null && attackAct.Data.Meta != null && attackAct.Data.Meta.ActionLength > 0.05f) {

                        actLen = attackAct.Data.Meta.ActionLength;

                    }

                    // Kunci target dan eksekusi aksi di CombatSystem

                    try {

                        cs.SelectTarget(_currentCombatTarget);

                        cs.UseBattleAction(attackAct.Data.Id);

                    } catch (Exception ex) {

                        Debug.LogWarning("[AutoGatherMod] UseBattleAction failed: " + ex);

                    }

                    _lastAttackTime = Time.time;

                    _attackAnimationFinishTime = Time.time + actLen;

                    _nextAttackAllowedTime = _attackAnimationFinishTime + 0.2f;

                    string actName = (attackAct.Data.Meta != null && attackAct.Data.Meta.Name != null) 

                        ? attackAct.Data.Meta.Name.ToString() 

                        : attackAct.Data.Id;

                    _combatActionStatus = string.Format("Menyerang: <color=#2ecc71>{0}</color> ({1:F2}s)", actName, actLen);

                } else {

                    _combatActionStatus = string.Format("<color=#f39c12>Menunggu aksi siap (Stamina: {0:F0})</color>", curStamina);

                }

            }

        }

        void OnGUI() {

            try {

                InitGUIStyles();

                float oldScan = ScanRadiusMeters;

                ResourceFilterMode oldFilter = CurrentFilter;

                GatherTargetMode oldTargetMode = TargetMode;

                bool oldRecCut = EnableRecoveryCut;

                float oldRecDelay = RecoveryCutDelay;

                bool oldStagger = EnableAntiStagger;

                bool oldAim = EnableAutoAimlock;

                bool oldMob = EnableMobileAttack;

                bool oldSniper = EnableFastSniperStance;

                bool oldCrossbowReload = EnableFastCrossbowReload;

                float oldCrossbowSpeed = CrossbowReloadSpeed;

                bool oldNoclip = EnableDinoNoclip;

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

                        GUILayout.Label(string.Format("  Aksi Terkunci: <color=#2ecc71><size=13><b>{0}</b></size></color>", _learnedActionName), _labelStyle ?? GUI.skin.label);

                        GUILayout.Label(string.Format("  Node Target: <b>{0}</b> <color=#7f8c8d>({1})</color>", _learnedNodeDisplayName, _learnedNodeBaseName), _labelStyle ?? GUI.skin.label);

                        if (!string.IsNullOrEmpty(_learnedGeneratorId)) {

                            GUILayout.Label(string.Format("  Generator ID: <color=#95a5a6>{0}</color>", _learnedGeneratorId), _labelStyle ?? GUI.skin.label);

                        }

                        GUILayout.Label(string.Format("  Kategori Node: <color=#3498db><b>{0}</b></color>", _learnedFilter), _labelStyle ?? GUI.skin.label);

                        GUILayout.EndVertical();

                        GUILayout.Space(2);

                        GUILayout.BeginHorizontal();

                        if (GUILayout.Button("Deteksi Target Baru / Reset", _selectedBtnStyle ?? GUI.skin.button, GUILayout.Height(24), GUILayout.Width(220))) {

                            ResetLearnedTarget();

                        }

                        GUILayout.EndHorizontal();

                    } else {

                        GUILayout.BeginVertical(GUI.skin.box);

                        GUILayout.Label("  <color=#f1c40f><b>[Menunggu Anda Memanen 1x di Game...]</b></color>", _labelStyle ?? GUI.skin.label);

                        GUILayout.Label("  • Dekati pohon, batu, bambu, semak, atau tanaman apa pun,", _hintStyle ?? GUI.skin.label);

                        GUILayout.Label("  • Klik panen manual 1x pada bagian yang Anda inginkan,", _hintStyle ?? GUI.skin.label);

                        GUILayout.Label("  • Bot akan langsung mendeteksi aksi & jenis objek tersebut,", _hintStyle ?? GUI.skin.label);

                        GUILayout.Label("  • Lalu otomatis mencari dan memanen objek sejenis di sekitar!", _hintStyle ?? GUI.skin.label);

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

                GUILayout.Label("  <b>🌋 Pengumpul Lahar Aman (Lava Scoop dari Darat):</b>", _subHeaderStyle ?? GUI.skin.label);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("🌋 Ambil Lahar (Ember Logam)", _btnStyle ?? GUI.skin.button, GUILayout.Height(26))) {

                    DrawLavaFromShore();

                }

                GUILayout.EndHorizontal();

                GUILayout.Label("  <size=10><color=#95a5a6>• Berdiri di tanah kering tepi lahar, lalu klik tombol ini untuk menyendok lahar tanpa menginjak lahar panas.</color></size>", _hintStyle ?? GUI.skin.label);

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
                    string sniperTxt = EnableFastSniperStance ? "AKTIF [1.5x]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  <b>3b. Fast Sniper Stance (Bow/Xbow):</b> <color={0}><b>{1}</b></color>", sniperColor, sniperTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(250));
                    if (GUILayout.Button(EnableFastSniperStance ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(120))) {
                        EnableFastSniperStance = !EnableFastSniperStance;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Menghilangkan animasi merunduk lambat saat Sikap Sniper. Karakter berdiri & lari normal (1.5x).</color></size>", _hintStyle ?? GUI.skin.label);

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
                    GUILayout.Label("  <size=10><color=#95a5a6>• Mempercepat animasi ngokang Crossbow (2.0x). Moving attack dinonaktifkan khusus saat reload agar tampak wajar.</color></size>", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

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

                    // 7. PENGUMPUL LAHAR (LAVA SCOOP DARI TEPI DARAT)
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  <b>7. Ambil Lahar (Lava Scoop):</b>", _labelStyle ?? GUI.skin.label, GUILayout.Width(220));
                    if (GUILayout.Button("🌋 Ambil Lahar (Ember Logam)", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(180))) {
                        DrawLavaFromShore();
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  <size=10><color=#95a5a6>• Berdiri di tanah kering tepi lahar, klik ini untuk menyendok lahar tanpa menginjak lahar panas.</color></size>", _hintStyle ?? GUI.skin.label);

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

                    oldAim != EnableAutoAimlock ||

                    oldMob != EnableMobileAttack ||

                    oldSniper != EnableFastSniperStance ||

                    oldCrossbowReload != EnableFastCrossbowReload ||

                    Mathf.Abs(oldCrossbowSpeed - CrossbowReloadSpeed) > 0.01f ||

                    oldNoclip != EnableDinoNoclip ||

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

            } catch {}

        }

        void OnDestroy() {

            try {

                UnhookInventoryEvent();

                UnhookCombatEvent();

                if (_isCombatAlertHooked && CombatSystem.HasInstance()) {

                    CombatSystem cs = CombatSystem.Instance();

                    if (cs != null) {

                        cs.AttackAlerted -= OnEnemyAttackAlerted;

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

        private string GetWidgetLabelText(Durango.UI.InteractionMenuWidget_PC w) {

            if (w == null) return "";

            try {

                UILabel[] uils = w.GetComponentsInChildren<UILabel>(true);

                if (uils != null) {

                    for (int i = 0; i < uils.Length; i++) {

                        if (uils[i] != null && !string.IsNullOrEmpty(uils[i].text)) {

                            string t = uils[i].text.Trim();

                            if (!t.StartsWith("F") && t.Length > 1) return t;

                        }

                    }

                }

            } catch {}

            return "";

        }

        private string GetSlotActionText(Durango.UI.InteractionMenuWidget_PC w) {

            if (w == null) return "";

            string s = "";

            try {

                if (!string.IsNullOrEmpty(w.Data.Name)) s += " " + w.Data.Name;

                if (!string.IsNullOrEmpty(w.Data.Id)) s += " " + w.Data.Id;

                try {

                    s += " " + w.Data.Action.ToString();

                } catch {}

                if (string.IsNullOrEmpty(w.Data.Name) && w.Data.GatheringData != null && !string.IsNullOrEmpty(w.Data.GatheringData.Name)) {

                    s += " " + w.Data.GatheringData.Name;

                }

                string lbl = GetWidgetLabelText(w);

                if (!string.IsNullOrEmpty(lbl)) s += " " + lbl;

            } catch {}

            return s.ToLower();

        }

        private bool IsAnchorSlot(Durango.UI.InteractionMenuWidget_PC w) {

            if (w == null) return false;

            string act = GetSlotActionText(w);

            if (string.IsNullOrEmpty(act)) return false;

            if (act.Contains("cabang") || act.Contains("branch") || 

                act.Contains("ranting") || act.Contains("twig") || act.Contains("stick") || act.Contains("dahan") ||

                act.Contains("daun") || act.Contains("leaf") || act.Contains("leaves") || act.Contains("pelepah") || act.Contains("foliage") ||

                act.Contains("kerikil") || act.Contains("krikil") || act.Contains("pebble") || act.Contains("gravel") ||

                act.Contains("serpihan") || act.Contains("tajam") || act.Contains("flint") || act.Contains("shard") || act.Contains("chip") ||

                act.Contains("batu kecil") || act.Contains("batu halus") ||

                act.Contains("kulit") || act.Contains("bark") || act.Contains("kupas") ||

                act.Contains("buah") || act.Contains("berry") || act.Contains("biji") || act.Contains("seed") ||

                act.Contains("getah") || act.Contains("resin") || act.Contains("sap") ||

                act.Contains("bunga") || act.Contains("flower") ||

                act.Contains("rumput") || act.Contains("grass") || act.Contains("reed") || act.Contains("flax") || act.Contains("rami")) {

                return false;

            }

            if (act.Contains("batang") || act.Contains("log") || act.Contains("pohon") || act.Contains("tree") || act.Contains("tebang") ||

                act.Contains("batu") || act.Contains("rock") || act.Contains("stone") || act.Contains("tambang") || act.Contains("ore") ||

                act.Contains("boulder") || act.Contains("crag") || act.Contains("cliff") || act.Contains("bongkahan") ||

                act.Contains("mineral") || act.Contains("iron") || act.Contains("besi") || act.Contains("tembaga") || act.Contains("copper")) {

                return true;

            }

            return false;

        }

        private bool IsStoneNode(string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            if (spriteInfo != null) {

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Rock ||

                    spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Pebble) {

                    return true;

                }

            }

            string combined = (name ?? "").ToLower();

            return combined.Contains("batu") || combined.Contains("rock") || combined.Contains("stone") || 

                   combined.Contains("mineral") || combined.Contains("kawah") || combined.Contains("flint") || 

                   combined.Contains("ore") || combined.Contains("tambang") || combined.Contains("boulder") || 

                   combined.Contains("crag") || combined.Contains("cliff") || combined.Contains("kerikil") || 

                   combined.Contains("pebble") || combined.Contains("iron") || combined.Contains("besi") || 

                   combined.Contains("tembaga") || combined.Contains("copper") || combined.Contains("clay") || 

                   combined.Contains("tanah liat") || combined.Contains("obsidian") || combined.Contains("kristal") || 

                   combined.Contains("crystal") || combined.Contains("limestone") || combined.Contains("basalt") || 

                   combined.Contains("granite") || combined.Contains("bongkahan");

        }

        private bool IsStoneNode(string name) { return IsStoneNode(name, null); }

        private bool IsWoodNode(string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            string combined = (name ?? "").ToLower();

            if (combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub")) return false;

            if (spriteInfo != null) {

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Tree) {

                    return true;

                }

            }

            return combined.Contains("pohon") || combined.Contains("kayu") || combined.Contains("log") || 

                   combined.Contains("tree") || combined.Contains("branch") || combined.Contains("ranting") || 

                   combined.Contains("batang") || combined.Contains("tebang");

        }

        private bool IsWoodNode(string name) { return IsWoodNode(name, null); }

        private bool IsBushNode(string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            if (spriteInfo != null) {

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Shrub) {

                    return true;

                }

            }

            string combined = (name ?? "").ToLower();

            return combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub") || 

                   combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("buah") || 

                   combined.Contains("berry");

        }

        private bool IsBushNode(string name) { return IsBushNode(name, null); }

        private bool IsPlantNode(string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            if (spriteInfo != null) {

                if (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Grass) {

                    return true;

                }

            }

            string combined = (name ?? "").ToLower();

            return combined.Contains("rumput") || combined.Contains("grass") || combined.Contains("reed") || 

                   combined.Contains("tanaman") || combined.Contains("plant") || combined.Contains("herb") || 

                   combined.Contains("bunga") || combined.Contains("flower") || combined.Contains("alang") || 

                   combined.Contains("flax") || combined.Contains("rami");

        }

        private bool IsPlantNode(string name) { return IsPlantNode(name, null); }

        private void UnhookCombatEvent() {

            if (!_isCombatEventHooked) return;

            try {

                CombatSystem cs = null;

                try { cs = GameSystem<CombatSystem>.Instance(); } catch {}

                if (cs == null) cs = UnityEngine.Object.FindObjectOfType<CombatSystem>();

                if (cs != null) {

                    cs.ChangedCombatMode -= OnCombatModeChanged;

                }

            } catch {}

            _isCombatEventHooked = false;

        }

        private void OnCombatModeChanged(bool inCombat) {

            try {

                Debug.Log(string.Format("[AutoGather] Combat mode changed: {0}", inCombat));

            } catch {}

        }

    }

}

namespace Doorstop {
    public static class Entrypoint {
        public static void Start() {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string markPath = System.IO.Path.Combine(baseDir, @"XDLandsMark\XDLandsMark.dll");
                if (System.IO.File.Exists(markPath)) {
                    Assembly originalAsm = Assembly.LoadFrom(markPath);
                    Type origEntry = originalAsm.GetType("Doorstop.Entrypoint");
                    if (origEntry != null) {
                        MethodInfo startMethod = origEntry.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                        if (startMethod != null) {
                            startMethod.Invoke(null, null);
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Error memanggil original XDLandsMark: " + ex);
            }

            try {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) {
                    AutoGather.AutoGatherController.EnsureLoaded();
                };
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Gagal daftarkan sceneLoaded: " + ex);
            }

            try {
                AutoGather.AutoGatherController.EnsureLoaded();
            } catch {}
        }
    }
}
