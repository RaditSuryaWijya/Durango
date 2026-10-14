using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using Crafting;
using Durango.Logic.Combat;


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

    public class SlotRequirementInfo {
        public string Name = "";
        public int Required = 0;
        public int Available = 0;
        public int Missing = 0;
        public ResourceFilterMode SuggestedFilter = ResourceFilterMode.All;
        public GatherTargetMode SuggestedTarget = GatherTargetMode.Auto;
        public string SuggestedKeyword = "";

        // Hierarki & Sub-Resep (Nested Crafting)
        public bool IsCraftable = false;
        public Crafting.Recipe SubRecipe = null;
        public List<SlotRequirementInfo> SubMaterials = new List<SlotRequirementInfo>();
        public bool IsSubRequirement = false;
        public string ParentName = "";
    }

    public enum EspCategory {
        RareDino,       // Dino Langka / Predator / High Level
        NormalDino,     // Dino Biasa / Herbivora
        DinoCorpse,     // Bangkai Hewan Buruan / Siap Loot
        Crater,         // Kawah Vulkanik / Crater POI (NaturalPrefabObject)
        WaterSource,    // Mata Air Panas, Geyser, Belerang, Sumber Air
        RareOre         // Ores / Mineral Khusus
    }

    public class EspTargetInfo {
        public string Name = "";
        public string CategoryLabel = "";
        public EspCategory Category = EspCategory.NormalDino;
        public Vector3 WorldPos = Vector3.zero;
        public float DistanceMeters = 0f;
        public Color TagColor = Color.white;
        public int Level = 0;
        public float HpRatio = 1f;
        public bool IsAlive = true;
        public bool IsLootable = false;
        public GameObject TargetObj = null;
    }

    public class AutoGatherController : MonoBehaviour {
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

        // ===== CRAFT PLANNER SYSTEM =====
        public int ActiveTab = 0; // 0 = Manual Gather, 1 = Craft Planner
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
        public bool EnableMobileAttack = true;
        public bool EnableRecoveryCut = true;
        public bool EnableAntiStagger = true;
        public bool EnableAutoAimlock = true;
        public float RecoveryCutDelay = 0.02f; // 0.02s = Turbo, 0.08s = Fast, 0f = Off
        private static FieldInfo _csUsingActionField = typeof(CombatSystem).GetField("_usingAction", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _uaStateFinishTimeField = null;
        private static FieldInfo _uaStateField = null;
        private static FieldInfo _uaActionField = null;
        private static FieldInfo _dpControllLostUntilField = null;
        private static FieldInfo _dpDisableStaggerField = null;
        private static FieldInfo _dpListField = null;

        // ===== RADAR & ESP 3D SYSTEM =====
        public bool EnableEsp = true;
        public bool EnableEdgeRadar = true; // Radar Pinggiran Layar Top-Down (Off-Screen Directional Pointers)
        public bool EspShowRareOnly = false;
        public bool EspShowDinos = true;
        public bool EspShowCorpses = true;
        public bool EspShowCraters = true;
        public bool EspShowWaterSources = true;
        public bool EspShowSnaplines = false;
        public float EspMaxDistance = 99999f; // Default: 1 Map Penuh (Unlimited)!
        private float _nextEspScanTime = 0f;
        private List<EspTargetInfo> _espTargets = new List<EspTargetInfo>();
        private Vector2 _radarScrollPos = Vector2.zero;
        private Vector2 _combatScrollPos = Vector2.zero;
        private GUIStyle _espTagStyle = null;
        private GUIStyle _espEdgeStyle = null;
        private Texture2D _whiteTex = null;

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
                    if (gameObject.name == "AutoGatherHost") {
                        Destroy(gameObject);
                    } else {
                        Destroy(this);
                    }
                    return;
                }
                Instance = this;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
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
                        _dpListField = dpType.GetField("_list", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                } catch {}

                Debug.Log("[AutoGather] AutoGatherController Awake selesai!");
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Awake error: " + ex);
            }
        }

        void Update() {
            try {
                // Hotkey toggle tampilan menu (Ctrl + 3 + 1)
                CheckMenuToggleHotkey();

                // Pastikan hook event inventory aktif untuk update real-time
                TryHookInventoryEvent();

                // Hook Interaction Menu untuk menangkap klik manual pemain
                TryHookInteractionMenu();

                // Unlock batas craft 15 in-game di UI Durango
                CheckAndUnlockInGameCraftLimit();

                // Auto-konsumsi makanan jika energi habis saat crafting
                CheckAndHandleCraftingEnergy();

                // Kecepatan gerak karakter & tempur sambil jalan
                UpdateMoveSpeedMultiplier();
                HandleCombatDPSMaximizer();

                // Scan ESP / Radar Dinosaurus Langka & Kawah secara berkala (tiap 0.4s)
                if (EnableEsp || ActiveTab == 3) {
                    if (Time.time >= _nextEspScanTime) {
                        _nextEspScanTime = Time.time + 0.4f;
                        UpdateEspScan();
                    }
                }

                // Deteksi manual harvest pemain saat bot sedang standby / pemain panen mandiri
                if (!_isGathering) {
                    CapturePlayerManualGathering();
                }

                // Hotkey toggle Bot (Default: F1)
                if (Input.GetKeyDown(ToggleKey) && !_isGathering) {
                    if (!Durango.UI.InteractionMenuListWidgetBase.IsShow || ToggleKey != KeyCode.F1) {
                        ToggleMod();
                    }
                }

                // Update Craft Planner status secara berkala (real-time live inventory tracking)
                if (PlanRecipe != null || ActiveTab == 1) {
                    if (Time.time >= _nextPlanCheckTime) {
                        _nextPlanCheckTime = Time.time + 0.15f;
                        UpdatePlanRequirements();
                    }
                }

                if (!IsEnabled) return;

                // Jika bot aktif tapi belum ada target yang dipelajari, tunggu pemain memanen 1x
                if (!_hasLearnedTarget) {
                    _statusText = "<color=#f1c40f><b>Menunggu Anda Memanen 1x...</b></color> (Dekati & panen objek yang diinginkan)";
                    return;
                }

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
        private void UpdateMoveSpeedMultiplier() {
            try {
                PlayerController pc = GetPlayerController();
                if (pc != null) {
                    if (Mathf.Abs(pc.CheatMoveSpeedMultiply - MoveSpeedMultiplier) > 0.001f) {
                        pc.CheatMoveSpeedMultiply = MoveSpeedMultiplier;
                    }
                }
            } catch {}
        }

        private void HandleCombatDPSMaximizer() {
            try {
                CombatSystem cs = null;
                try { cs = GameSystem<CombatSystem>.Instance(); } catch {}
                if (cs == null) {
                    try { cs = UnityEngine.Object.FindObjectOfType<CombatSystem>(); } catch {}
                }
                if (cs == null) return;

                bool inCombat = cs.CombatMode;
                object ua = null;
                if (_csUsingActionField != null) {
                    try { ua = _csUsingActionField.GetValue(cs); } catch {}
                }

                bool hasActiveAction = false;
                if (ua != null) {
                    try {
                        MethodInfo hasVal = ua.GetType().GetMethod("HasValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (hasVal != null) hasActiveAction = (bool)hasVal.Invoke(ua, null);
                    } catch {}
                }

                // 1. Mobile Attack: Lepaskan MoveLock agar pemain bisa bergerak bebas saat menyerang
                if (EnableMobileAttack && (inCombat || hasActiveAction)) {
                    InputSystem inputSys = null;
                    try { inputSys = GameSystem<InputSystem>.Instance(); } catch {}
                    if (inputSys == null) inputSys = UnityEngine.Object.FindObjectOfType<InputSystem>();
                    if (inputSys != null && inputSys.MoveLock) {
                        inputSys.MoveLock = false;
                    }
                }

                // 2. Anti-Stagger / Anti-Stun: Kebal interupsi guncangan pukulan dino
                if (_dpDisableStaggerField != null) {
                    try {
                        _dpDisableStaggerField.SetValue(null, EnableAntiStagger);
                    } catch {}
                }

                if (EnableAntiStagger && cs.DamagedProcesser != null) {
                    try {
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
                    } catch {}
                }

                // 3. Fast Animation Cancel / Turbo ASPD (Recovery Cut)
                // Memangkas waktu pemulihan animasi agar ayunan senjata berikutnya siap seketika
                if (EnableRecoveryCut && ua != null && hasActiveAction) {
                    try {
                        if (_uaStateFinishTimeField != null) {
                            float finishTime = (float)_uaStateFinishTimeField.GetValue(ua);
                            if (finishTime > Time.time + RecoveryCutDelay) {
                                _uaStateFinishTimeField.SetValue(ua, Time.time + RecoveryCutDelay);
                            }
                        }
                    } catch {}
                }

                // Target musuh aktif
                DamageableEntity target = (cs.Target != null) ? cs.Target.Value : null;

                // 4. Auto-Aimlock: Mengunci orientasi karakter menghadap target musuh saat menyerang
                if (EnableAutoAimlock && target != null && target.IsAlive && (inCombat || hasActiveAction)) {
                    try {
                        PlayerController pc = GetPlayerController();
                        if (pc != null) {
                            Vector3 targetPos = target.GetCurrentPosition();
                            pc.RotateToPosition(targetPos, true);
                        }
                    } catch {}
                }
            } catch {}
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

            // 5. Kayu / Bahan Kayu -> Cabang, Ranting, Dahan, Tongkat, Batang
            if (slotName == "kayu" || slotName.Contains("bahan kayu") || slotName.Contains("wood")) {
                if (itemName.Contains("cabang") || itemName.Contains("ranting") || itemName.Contains("dahan") || 
                    itemName.Contains("tongkat") || itemName.Contains("batang") || itemName.Contains("kayu")) {
                    return true;
                }
            }

            // 6. Paku / Pasak -> Paku, Pasak, Nail, Peg
            if (slotName.Contains("paku") || slotName.Contains("pasak") || slotName.Contains("nail") || slotName.Contains("peg")) {
                if (itemName.Contains("paku") || itemName.Contains("pasak") || itemName.Contains("nail") || itemName.Contains("peg")) {
                    return true;
                }
            }

            // 7. Cek tag langsung dari item data jika ada
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
            if (IsPureRopeSlot(slot)) {
                return false;
            }

            // PENTING: Paku / Pasak (Nails / Pegs) adalah item olahan kerajinan (Craftable), BUKAN bahan mentah alam!
            // Jangan pernah anggap Paku sebagai natural resource agar dicarikan sub-resepnya di RecipeSystem!
            string sNameCheck = (!string.IsNullOrEmpty(slot.Name) ? slot.Name : "").Trim().ToLower();
            string sIdCheck = (!string.IsNullOrEmpty(slot.Id) ? slot.Id : "").Trim().ToLower();
            if (sNameCheck.Contains("paku") || sNameCheck.Contains("pasak") || sNameCheck.Contains("nail") || sNameCheck.Contains("peg") ||
                sIdCheck.Contains("paku") || sIdCheck.Contains("pasak") || sIdCheck.Contains("nail") || sIdCheck.Contains("peg")) {
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

            // Tongkat / Cabang / Ranting / Kayu / Batang / Dahan
            if (combined.Contains("tongkat") || combined.Contains("cabang") || combined.Contains("ranting") || 
                combined.Contains("branch") || combined.Contains("stick") || combined.Contains("twig") || 
                combined.Contains("dahan") || combined.Contains("batang") || combined.Contains("pegangan") ||
                combined.Contains("kayu") || combined.Contains("wood")) {
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

            // 1. Batang Pohon / Kayu Besar / Log / Balok / Timber / Tebang (HANYA JIKA EKSPLISIT)
            // Catatan penting: "batang tanaman" adalah tali pengikat (Rumput), BUKAN batang pohon!
            // Dan kebutuhan "kayu", "wood", "paku", "pasak", "tongkat", "ranting" BUKAN batang pohon!
            bool isExplicitBatang = (combined.Contains("batang pohon") || combined.Contains("batang kayu") || 
                                     combined.Contains("log") || combined.Contains("balok") || 
                                     combined.Contains("gelondongan") || combined.Contains("timber") || 
                                     combined.Contains("trunk") || combined.Contains("tebang")) && 
                                    !combined.Contains("tanaman") && !combined.Contains("buluh") && 
                                    !combined.Contains("paku") && !combined.Contains("pasak") && 
                                    !combined.Contains("ranting") && !combined.Contains("cabang");
            if (!isExplicitBatang && combined.Contains("batang") && 
                !combined.Contains("tanaman") && !combined.Contains("buluh") && !combined.Contains("tali") && 
                !combined.Contains("paku") && !combined.Contains("pasak") && !combined.Contains("ranting") && !combined.Contains("cabang")) {
                isExplicitBatang = true;
            }

            if (isExplicitBatang) {
                filter = ResourceFilterMode.Wood;
                targetMode = GatherTargetMode.Batang;
                return;
            }

            // 2. Tongkat / Cabang / Ranting / Dahan / Pegangan / Paku / Pasak / Kayu Umum (Wood / Bush)
            // Sesuai karakteristik Durango: Kebutuhan kayu umum, paku, pasak, dan tongkat
            // DAPAT DAN HARUS langsung dipenuhi oleh Cabang atau Ranting tanpa menebang pohon!
            if (combined.Contains("tongkat") || combined.Contains("cabang") || combined.Contains("ranting") || 
                combined.Contains("branch") || combined.Contains("twig") || combined.Contains("stick") || 
                combined.Contains("dahan") || combined.Contains("pegangan") || combined.Contains("handle") ||
                combined.Contains("paku") || combined.Contains("pasak") || combined.Contains("nail") || combined.Contains("peg") ||
                combined.Contains("kayu") || combined.Contains("wood")) {
                filter = ResourceFilterMode.Wood;
                targetMode = GatherTargetMode.Ranting;
                return;
            }

            // 3. Batu / Mineral / Flint / Tambang / Serpihan Batu / Batu Tajam
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

            // 4. Daun / Palm Frond / Foliage / Pelepah
            if (combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("leaves") || 
                combined.Contains("frond") || combined.Contains("foliage") || combined.Contains("pelepah")) {
                filter = ResourceFilterMode.Bush;
                targetMode = GatherTargetMode.Daun;
                return;
            }

            // 5. Tali Pengikat (Binding) / Rumput / Reed / Flax / Tanaman / Alang / Serat / Buluh / Akar
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
            string actText = GetSlotActionText(widget);
            string fullText = GetWidgetFullText(widget);
            if (string.IsNullOrEmpty(actText) && string.IsNullOrEmpty(fullText)) return false;

            string sName = (!string.IsNullOrEmpty(slotInfo.Name) ? slotInfo.Name : "").ToLower();

            // ================= PERLINDUNGAN MUTLAK DARI ANCHOR SLOT =================
            bool isAnchorWidget = IsAnchorSlot(widget);
            if (isAnchorWidget) {
                // A. Kebutuhan Batang Pohon / Log:
                bool requiresBatang = (slotInfo.SuggestedTarget == GatherTargetMode.Batang) || 
                                      ((sName.Contains("batang") || sName.Contains("log") || sName.Contains("balok") || sName.Contains("timber") || sName.Contains("trunk")) && 
                                       !sName.Contains("tanaman") && !sName.Contains("buluh") && !sName.Contains("tali") && 
                                       !sName.Contains("paku") && !sName.Contains("pasak") && !sName.Contains("ranting") && !sName.Contains("cabang"));

                // B. Kebutuhan Batu / Mineral:
                bool requiresBatu = (slotInfo.SuggestedTarget == GatherTargetMode.Batu) || 
                                    sName.Contains("batu") || sName.Contains("stone") || sName.Contains("mineral") || 
                                    sName.Contains("ore") || sName.Contains("bongkahan") || sName.Contains("quarry");

                // Jika aksi jangkar adalah Tebang Pohon / Batang:
                bool isTreeFell = actText.Contains("tebang") || actText.Contains("chop") || actText.Contains("fell") || 
                                  actText.Contains("batang") || actText.Contains("log") || actText.Contains("trunk");
                if (isTreeFell && !requiresBatang) {
                    return false; // TOLAK: Jangan tebang pohon jika hanya butuh ranting/paku/kayu biasa/daun!
                }

                // Jika aksi jangkar adalah Pecah Batu:
                bool isStoneBreak = actText.Contains("pecah") || actText.Contains("bongkahan") || actText.Contains("boulder") || actText.Contains("quarry");
                if (isStoneBreak && !requiresBatu) {
                    return false; // TOLAK: Jangan pecah batu jika tidak butuh batu!
                }
            }

            // ================= PERLINDUNGAN MUTLAK DARI DAUN =================
            // Jika aksi widget adalah Daun (Petik Daun / Ambil Daun):
            bool isWidgetDaun = actText.Contains("daun") || actText.Contains("leaf") || 
                                actText.Contains("leaves") || actText.Contains("pelepah") || 
                                actText.Contains("foliage") || actText.Contains("frond");

            bool requiresDaun = (slotInfo.SuggestedTarget == GatherTargetMode.Daun) || 
                                sName.Contains("daun") || sName.Contains("leaf") || 
                                sName.Contains("pelepah") || sName.Contains("foliage");

            if (isWidgetDaun && !requiresDaun) {
                return false; // TOLAK MUTLAK: Jangan ambil daun jika slot requirement TIDAK butuh daun!
            }

            // 1. Cek Tongkat / Cabang / Ranting / Paku / Pasak / Kayu Umum
            bool isWoodOrRantingReq = (slotInfo.SuggestedTarget == GatherTargetMode.Ranting) || 
                                      sName.Contains("tongkat") || sName.Contains("stick") || 
                                      sName.Contains("cabang") || sName.Contains("ranting") || 
                                      sName.Contains("pegangan") || sName.Contains("handle") ||
                                      sName.Contains("paku") || sName.Contains("pasak") || 
                                      sName.Contains("nail") || sName.Contains("peg") ||
                                      sName.Contains("kayu") || sName.Contains("wood");
            if (isWoodOrRantingReq) {
                if (actText.Contains("cabang") || actText.Contains("ranting") || actText.Contains("dahan") || 
                    actText.Contains("branch") || actText.Contains("twig") || actText.Contains("stick") || 
                    actText.Contains("tongkat") || actText.Contains("pegangan") ||
                    fullText.Contains("cabang") || fullText.Contains("ranting") || fullText.Contains("twig")) {
                    return true;
                }
            }

            // 2. Cek Batang Kayu / Log / Tebang (HANYA jika EKSPLISIT butuh Batang/Log/Balok)
            bool isBatangReq = (slotInfo.SuggestedTarget == GatherTargetMode.Batang) || 
                               ((sName.Contains("batang") || sName.Contains("log") || sName.Contains("trunk") || sName.Contains("balok") || sName.Contains("timber")) && 
                                !sName.Contains("tanaman") && !sName.Contains("buluh") && !sName.Contains("tali") && 
                                !sName.Contains("paku") && !sName.Contains("pasak") && !sName.Contains("ranting") && !sName.Contains("cabang"));
            if (isBatangReq) {
                if (actText.Contains("tebang") || actText.Contains("chop") || actText.Contains("fell") || 
                    actText.Contains("batang") || actText.Contains("log") || actText.Contains("trunk") || 
                    actText.Contains("timber") || actText.Contains("tumbang")) {
                    return true;
                }
            }

            // 3. Cek Kerikil / Pebble / Batu Kecil / Serpihan / Tajam
            bool isKerikilReq = sName.Contains("kerikil") || sName.Contains("pebble") || sName.Contains("krikil") ||
                                sName.Contains("tajam") || sName.Contains("penghalus") || sName.Contains("serpihan") || sName.Contains("blade");
            if (isKerikilReq) {
                if (actText.Contains("kerikil") || actText.Contains("pebble") || actText.Contains("krikil") ||
                    actText.Contains("serpihan") || actText.Contains("flint") || actText.Contains("tajam") || 
                    actText.Contains("batu kecil") || actText.Contains("batu halus")) {
                    return true;
                }
                if ((actText.Contains("batu") || actText.Contains("stone") || actText.Contains("rock")) &&
                    !actText.Contains("pecah") && !actText.Contains("besar") && !actText.Contains("bongkahan") && !actText.Contains("quarry")) {
                    return true;
                }
            }

            // 4. Cek Batu Umum / Mineral / Stone / Batu Besar / Bongkahan / Quarry / Pecahkan Batu
            bool isGeneralBatuReq = (slotInfo.SuggestedTarget == GatherTargetMode.Batu) || 
                                    sName.Contains("batu") || sName.Contains("stone") || sName.Contains("rock") || 
                                    sName.Contains("mineral") || sName.Contains("ore") || sName.Contains("tambang") ||
                                    sName.Contains("bongkahan") || sName.Contains("quarry") || sName.Contains("boulder");
            if (isGeneralBatuReq) {
                if (actText.Contains("batu") || actText.Contains("stone") || actText.Contains("rock") || 
                    actText.Contains("mineral") || actText.Contains("ore") || actText.Contains("tambang") || 
                    actText.Contains("kerikil") || actText.Contains("krikil") || actText.Contains("pebble") || 
                    actText.Contains("pecah") || actText.Contains("bongkahan") || actText.Contains("quarry") ||
                    actText.Contains("flint") || actText.Contains("serpihan") || actText.Contains("tajam") ||
                    fullText.Contains("batu") || fullText.Contains("stone") || fullText.Contains("rock") ||
                    fullText.Contains("kerikil") || fullText.Contains("pebble")) {
                    return true;
                }
            }

            // 5. Cek Tali Murni vs Tali Pengikat (Binding) / Rumput / Reed / Flax / Buluh / Serat / Akar / Batang Tanaman
            bool isBindingReq = sName.Contains("pengikat") || sName.Contains("binding") || sName.Contains("ikat");
            bool isPureTaliReq = (sName == "tali" || sName.StartsWith("tali ") || sName == "rope") && !isBindingReq;

            if (isPureTaliReq) {
                if (actText.Contains("tali") || actText.Contains("rope")) {
                    return true;
                }
            } else if (isBindingReq || sName.Contains("buluh") || sName.Contains("serat") || 
                       sName.Contains("alang") || sName.Contains("akar") || sName.Contains("tanaman") || 
                       slotInfo.SuggestedTarget == GatherTargetMode.Rumput) {
                if (!isWidgetDaun) {
                    if (actText.Contains("buluh") || actText.Contains("akar") || actText.Contains("alang") || 
                        actText.Contains("serat") || actText.Contains("rumput") || actText.Contains("tali") || 
                        actText.Contains("ikat") || actText.Contains("fiber") || actText.Contains("grass") || 
                        actText.Contains("reed") || actText.Contains("flax") || actText.Contains("tanaman") || 
                        actText.Contains("plant") || actText.Contains("herb") || actText.Contains("rami") ||
                        actText.Contains("cabut") || actText.Contains("batang")) {
                        return true;
                    }
                    if ((fullText.Contains("buluh") || fullText.Contains("reed") || fullText.Contains("serat") || fullText.Contains("rumput")) &&
                        !actText.Contains("tebang") && !actText.Contains("pecah")) {
                        return true;
                    }
                }
            }

            // 6. Cek Daun / Foliage / Pelepah
            bool isDaunReq = (slotInfo.SuggestedTarget == GatherTargetMode.Daun) || sName.Contains("daun") || sName.Contains("leaf") || sName.Contains("pelepah");
            if (isDaunReq) {
                if (actText.Contains("daun") || actText.Contains("leaf") || actText.Contains("leaves") || 
                    actText.Contains("pelepah") || actText.Contains("frond") || actText.Contains("foliage")) {
                    return true;
                }
            }

            // 7. Cek Mode Target Spesifik (Fallback)
            switch (slotInfo.SuggestedTarget) {
                case GatherTargetMode.Ranting:
                    return actText.Contains("ranting") || actText.Contains("branch") || actText.Contains("twig") || 
                           actText.Contains("stick") || actText.Contains("cabang") || actText.Contains("dahan") || 
                           actText.Contains("tongkat") || fullText.Contains("cabang") || fullText.Contains("ranting");
                case GatherTargetMode.Daun:
                    return actText.Contains("daun") || actText.Contains("leaf") || actText.Contains("leaves") || 
                           actText.Contains("frond") || actText.Contains("pelepah") || actText.Contains("foliage");
                case GatherTargetMode.Batang:
                    return actText.Contains("tebang") || actText.Contains("chop") || actText.Contains("batang") || 
                           actText.Contains("log") || actText.Contains("trunk") || actText.Contains("kayu") || actText.Contains("timber");
                case GatherTargetMode.Kulit:
                    return actText.Contains("kulit") || actText.Contains("bark") || actText.Contains("kupas") || actText.Contains("hide") || actText.Contains("leather");
                case GatherTargetMode.Batu:
                    return actText.Contains("batu") || actText.Contains("stone") || actText.Contains("rock") || 
                           actText.Contains("mineral") || actText.Contains("ore") || actText.Contains("tambang") || 
                           actText.Contains("kerikil") || actText.Contains("krikil") || actText.Contains("pebble") || 
                           actText.Contains("gravel") || actText.Contains("boulder") || actText.Contains("cobble") ||
                           actText.Contains("flint") || actText.Contains("serpihan") || actText.Contains("tajam") ||
                           actText.Contains("pecah") || fullText.Contains("batu") || fullText.Contains("stone") ||
                           fullText.Contains("kerikil") || fullText.Contains("krikil") || fullText.Contains("pebble");
                case GatherTargetMode.Rumput:
                    return !isWidgetDaun && (actText.Contains("rumput") || actText.Contains("grass") || actText.Contains("reed") || 
                           actText.Contains("flax") || actText.Contains("tanaman") || actText.Contains("plant") || 
                           actText.Contains("herb") || actText.Contains("serat") || actText.Contains("fiber") || 
                           actText.Contains("alang") || actText.Contains("rami") || actText.Contains("cabut") ||
                           actText.Contains("buluh") || actText.Contains("akar") || actText.Contains("batang") || actText.Contains("tali"));
                case GatherTargetMode.Daging:
                    return actText.Contains("daging") || actText.Contains("meat") || actText.Contains("sayat") || actText.Contains("carve") || actText.Contains("butcher");
                case GatherTargetMode.Buah:
                    return actText.Contains("buah") || actText.Contains("berry") || actText.Contains("fruit") || actText.Contains("petik");
            }

            // 8. Cek keyword khusus
            if (!string.IsNullOrEmpty(slotInfo.SuggestedKeyword)) {
                string kw = slotInfo.SuggestedKeyword.ToLower();
                if ((actText.Contains(kw) || fullText.Contains(kw)) && (!isWidgetDaun || requiresDaun)) return true;
            }

            // 9. Cek nama slot langsung jika ada kecocokan kata
            if (!string.IsNullOrEmpty(slotInfo.Name)) {
                if (isAnchorWidget) return false; // Jangan fallback match pada anchor widget jika bukan kebutuhan eksplisit
                if (isWidgetDaun && !requiresDaun) return false; // Jangan fallback match pada daun jika tidak butuh daun
                if (actText.Contains(sName) || fullText.Contains(sName)) return true;
                string[] parts = sName.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                for (int p = 0; p < parts.Length; p++) {
                    if (parts[p].Length >= 4 && (actText.Contains(parts[p]) || fullText.Contains(parts[p]))) {
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

        // ================= SISTEM AUTO-KONSUMSI MAKANAN SAAT CRAFTING =================
        public bool IsEdibleFood(Durango.Logic.Item.ItemData item) {
            if (item == null || item.IsDestroyed() || item.Locked) return false;
            try {
                if (item.HasTag("eatable") || item.HasTag("drinkable") || item.HasTag("food")) {
                    return true;
                }
            } catch {}

            try {
                if (item.Tags != null) {
                    for (int i = 0; i < item.Tags.Count; i++) {
                        if (item.Tags[i] != null && !string.IsNullOrEmpty(item.Tags[i].Id)) {
                            string tid = item.Tags[i].Id.ToLower();
                            if (tid == "eatable" || tid == "drinkable" || tid == "food" || tid == "edible" || tid == "beverage") {
                                return true;
                            }
                        }
                    }
                }
            } catch {}

            if (!string.IsNullOrEmpty(item.Name)) {
                string n = item.Name.ToLower();
                if (n.Contains("daging matang") || n.Contains("daging panggang") || n.Contains("roti") || 
                    n.Contains("sup") || n.Contains("teh") || n.Contains("jus") || n.Contains("sate") || 
                    n.Contains("buah") || n.Contains("berry") || n.Contains("rebus") || n.Contains("bakar") || 
                    n.Contains("susu") || n.Contains("kue") || n.Contains("makanan")) {
                    return true;
                }
            }
            return false;
        }

        public bool TryEatFoodFromInventory() {
            try {
                InventorySystem invSys = null;
                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}
                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();
                if (invSys == null || invSys.PlayerItemList == null) return false;

                List<Durango.Logic.Item.ItemData> items = invSys.PlayerItemList;
                Durango.Logic.Item.ItemData foodToEat = null;

                for (int i = 0; i < items.Count; i++) {
                    Durango.Logic.Item.ItemData it = items[i];
                    if (IsEdibleFood(it)) {
                        foodToEat = it;
                        break;
                    }
                }

                if (foodToEat != null) {
                    invSys.UseItem(foodToEat, false, null);
                    _lastEatenFoodName = foodToEat.Name;
                    _lastEatTime = Time.time;
                    _statusText = string.Format("<color=#2ecc71>Konsumsi Otomatis: {0} (Energi Dipulihkan!)</color>", foodToEat.Name);
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
        }

        // ================= GATHERING ENGINE & HIRARKI OBJEK =================
        private bool IsStoneNode(string combined, Durango.Terrain.BiomeSpriteInfo spriteInfo) {
            if (spriteInfo != null && (spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Rock ||
                                       spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Pebble)) return true;
            return combined.Contains("batu") || combined.Contains("rock") || combined.Contains("stone") || 
                   combined.Contains("mineral") || combined.Contains("kawah") || combined.Contains("flint") || 
                   combined.Contains("ore") || combined.Contains("tambang") || combined.Contains("boulder") || 
                   combined.Contains("crag") || combined.Contains("cliff") || combined.Contains("kerikil") || 
                   combined.Contains("krikil") || combined.Contains("pebble") || combined.Contains("gravel") || 
                   combined.Contains("cobble") || combined.Contains("tajam") || combined.Contains("serpihan") || 
                   combined.Contains("iron") || combined.Contains("besi") || combined.Contains("tembaga") || 
                   combined.Contains("copper") || combined.Contains("clay") || combined.Contains("tanah liat") || 
                   combined.Contains("obsidian") || combined.Contains("kristal") || combined.Contains("crystal") || 
                   combined.Contains("limestone") || combined.Contains("basalt") || combined.Contains("granite") || 
                   combined.Contains("bongkahan") || combined.Contains("shard");
        }

        private bool IsWoodNode(string combined, Durango.Terrain.BiomeSpriteInfo spriteInfo) {
            if (combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub")) return false;
            if (spriteInfo != null && spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Tree) return true;
            return combined.Contains("pohon") || combined.Contains("kayu") || combined.Contains("log") || 
                   combined.Contains("tree") || combined.Contains("branch") || combined.Contains("ranting") || 
                   combined.Contains("batang") || combined.Contains("tebang");
        }

        private bool IsBushNode(string combined, Durango.Terrain.BiomeSpriteInfo spriteInfo) {
            if (spriteInfo != null && spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Shrub) return true;
            return combined.Contains("semak") || combined.Contains("bush") || combined.Contains("shrub") || 
                   combined.Contains("daun") || combined.Contains("leaf") || combined.Contains("buah") || 
                   combined.Contains("berry");
        }

        private bool IsPlantNode(string combined, Durango.Terrain.BiomeSpriteInfo spriteInfo) {
            if (spriteInfo != null && spriteInfo.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Grass) return true;
            return combined.Contains("rumput") || combined.Contains("grass") || combined.Contains("reed") || 
                   combined.Contains("tanaman") || combined.Contains("plant") || combined.Contains("herb") || 
                   combined.Contains("bunga") || combined.Contains("flower") || combined.Contains("alang") || 
                   combined.Contains("flax") || combined.Contains("rami") || combined.Contains("buluh") || 
                   combined.Contains("akar") || combined.Contains("serat");
        }

        private bool MatchesFilter(GameObject go, string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {
            if (CurrentFilter == ResourceFilterMode.All) return true;
            string customSearch = (CustomKeyword ?? "").Trim().ToLower();
            if (!string.IsNullOrEmpty(customSearch)) return true;

            string combined = ((name ?? "") + " " + (go != null ? go.name : "")).ToLower();

            bool isDeadBody = false;
            try {
                if (go != null && ObjectIdentifier.IsDeadBody(go)) isDeadBody = true;
            } catch {}

            if (CurrentFilter == ResourceFilterMode.Corpse || TargetMode == GatherTargetMode.Daging) {
                return isDeadBody || combined.Contains("bangkai") || combined.Contains("corpse") || 
                       combined.Contains("dead") || combined.Contains("daging");
            }

            if (isDeadBody) return false;

            // HIRARKI OBJEK PENGHASIL TAG DI ALAM LIAR:
            // 1. Tag Ranting / Tongkat / Cabang: Bisa berasal dari Pohon (Tree) ATAU Semak (Bush)!
            if (TargetMode == GatherTargetMode.Ranting) {
                return IsWoodNode(combined, spriteInfo) || IsBushNode(combined, spriteInfo);
            }

            // 2. Tag Rumput / Tali Pengikat / Buluh / Akar: Bisa dari Tanaman (Plant/Buluh), Semak (Bush), atau Akar Pohon (Tree)
            if (TargetMode == GatherTargetMode.Rumput) {
                return IsPlantNode(combined, spriteInfo) || IsBushNode(combined, spriteInfo) || IsWoodNode(combined, spriteInfo);
            }

            // 3. Tag Daun / Pelepah: Bisa dari Semak (Bush) ATAU Pohon (Tree - kelapa/palem)
            if (TargetMode == GatherTargetMode.Daun) {
                return IsBushNode(combined, spriteInfo) || IsWoodNode(combined, spriteInfo);
            }

            // 4. Tag Batu / Batu Tajam: Bisa dari Batu Besar (Rock) maupun Kerikil di tanah (Pebble)
            if (TargetMode == GatherTargetMode.Batu) {
                return IsStoneNode(combined, spriteInfo);
            }

            // 5. Tag Batang Kayu Besar: Dari Pohon (Tree)
            if (TargetMode == GatherTargetMode.Batang) {
                return IsWoodNode(combined, spriteInfo);
            }

            // 6. Tag Buah: Dari Semak atau Pohon Buah
            if (TargetMode == GatherTargetMode.Buah) {
                return IsBushNode(combined, spriteInfo) || IsWoodNode(combined, spriteInfo);
            }

            // 7. Tag Kulit: Dari Pohon (kulit kayu)
            if (TargetMode == GatherTargetMode.Kulit) {
                return IsWoodNode(combined, spriteInfo);
            }

            // Fallback Filter Standar Tab 0 (Manual Filter):
            if (CurrentFilter == ResourceFilterMode.Stone) return IsStoneNode(combined, spriteInfo);
            if (CurrentFilter == ResourceFilterMode.Wood) return IsWoodNode(combined, spriteInfo);
            if (CurrentFilter == ResourceFilterMode.Bush) return IsBushNode(combined, spriteInfo);
            if (CurrentFilter == ResourceFilterMode.Plant) return IsPlantNode(combined, spriteInfo);

            return true;
        }

        private bool DoesNodeMatchRequirement(string combined, GameObject go, Durango.Terrain.BiomeSpriteInfo spriteInfo, SlotRequirementInfo req, bool isDeadBody) {
            if (req == null) return false;

            // 1. Corpse / Daging
            if (req.SuggestedTarget == GatherTargetMode.Daging || req.SuggestedFilter == ResourceFilterMode.Corpse) {
                return isDeadBody || combined.Contains("bangkai") || combined.Contains("corpse") || 
                       combined.Contains("dead") || combined.Contains("daging");
            }

            if (isDeadBody) return false;

            // 2. Hirarki Objek Penghasil Tag di Alam Liar:
            // Ranting / Tongkat / Cabang: Pohon (Tree) atau Semak (Bush)
            if (req.SuggestedTarget == GatherTargetMode.Ranting) {
                return IsWoodNode(combined, spriteInfo) || IsBushNode(combined, spriteInfo);
            }

            // Rumput / Tali Pengikat / Buluh / Akar: Tanaman (Plant), Semak (Bush), atau Pohon (Tree)
            if (req.SuggestedTarget == GatherTargetMode.Rumput) {
                return IsPlantNode(combined, spriteInfo) || IsBushNode(combined, spriteInfo) || IsWoodNode(combined, spriteInfo);
            }

            // Daun / Pelepah: Semak (Bush) atau Pohon (Tree)
            if (req.SuggestedTarget == GatherTargetMode.Daun) {
                return IsBushNode(combined, spriteInfo) || IsWoodNode(combined, spriteInfo);
            }

            // Batu / Batu Tajam / Mineral / Serpihan: Batu (Rock) atau Kerikil (Pebble)
            if (req.SuggestedTarget == GatherTargetMode.Batu) {
                return IsStoneNode(combined, spriteInfo);
            }

            // Batang Kayu Besar: Pohon (Tree)
            if (req.SuggestedTarget == GatherTargetMode.Batang) {
                return IsWoodNode(combined, spriteInfo);
            }

            // Buah: Semak atau Pohon
            if (req.SuggestedTarget == GatherTargetMode.Buah) {
                return IsBushNode(combined, spriteInfo) || IsWoodNode(combined, spriteInfo);
            }

            // Kulit: Pohon (kulit kayu)
            if (req.SuggestedTarget == GatherTargetMode.Kulit) {
                return IsWoodNode(combined, spriteInfo);
            }

            // 3. Fallback Filter Standar jika mode target Auto
            if (req.SuggestedFilter == ResourceFilterMode.Stone) return IsStoneNode(combined, spriteInfo);
            if (req.SuggestedFilter == ResourceFilterMode.Wood) return IsWoodNode(combined, spriteInfo);
            if (req.SuggestedFilter == ResourceFilterMode.Bush) return IsBushNode(combined, spriteInfo);
            if (req.SuggestedFilter == ResourceFilterMode.Plant) return IsPlantNode(combined, spriteInfo);

            // 4. Fallback Keyword & Nama
            if (!string.IsNullOrEmpty(req.SuggestedKeyword)) {
                if (combined.Contains(req.SuggestedKeyword.ToLower())) return true;
            }
            if (!string.IsNullOrEmpty(req.Name)) {
                string sName = req.Name.Trim().ToLower();
                if (combined.Contains(sName)) return true;
            }

            return false;
        }

        private SlotRequirementInfo FindMatchingMissingRequirement(GameObject go, string name, Durango.Terrain.BiomeSpriteInfo spriteInfo, List<SlotRequirementInfo> missingGatherables) {
            if (go == null || missingGatherables == null || missingGatherables.Count == 0) return null;
            string combined = ((name ?? "") + " " + go.name).ToLower();
            bool isDeadBody = false;
            try {
                if (ObjectIdentifier.IsDeadBody(go)) isDeadBody = true;
            } catch {}

            for (int i = 0; i < missingGatherables.Count; i++) {
                SlotRequirementInfo req = missingGatherables[i];
                if (req != null && req.Missing > 0) {
                    if (DoesNodeMatchRequirement(combined, go, spriteInfo, req, isDeadBody)) {
                        return req;
                    }
                }
            }
            return null;
        }

        private GameObject FindClosestTarget(PlayerBehavior player) {
            List<SlotRequirementInfo> missingPlanReqs = null;
            if (IsCraftPlanActive || (ActiveTab == 1 && PlanRecipe != null)) {
                missingPlanReqs = GetAllMissingGatherables();
                if (_allMaterialsReady || missingPlanReqs.Count == 0) {
                    return null;
                }
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
            Dictionary<GameObject, string> labelNames = new Dictionary<GameObject, string>();
            try {
                Durango.UI.InteractionHelperLabel[] labels = UnityEngine.Object.FindObjectsOfType<Durango.UI.InteractionHelperLabel>();
                if (labels != null) {
                    for (int i = 0; i < labels.Length; i++) {
                        if (labels[i] != null && labels[i].Target != null && labels[i].Target.activeInHierarchy) {
                            if (!candidates.Contains(labels[i].Target)) {
                                candidates.Add(labels[i].Target);
                            }
                            // Simpan nama label asli dari UI game (UILabel _nameLabel)
                            try {
                                FieldInfo fLbl = typeof(Durango.UI.InteractionHelperLabel).GetField("_nameLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (fLbl != null) {
                                    UILabel uil = fLbl.GetValue(labels[i]) as UILabel;
                                    if (uil != null && !string.IsNullOrEmpty(uil.text)) {
                                        labelNames[labels[i].Target] = uil.text.Trim();
                                    }
                                }
                            } catch {}
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
            SlotRequirementInfo absoluteClosestReq = null;

            GameObject closestInRadius = null;
            float minRadiusDist = 999999f;
            string closestInRadiusName = "-";
            Vector3 closestInRadiusPos = Vector3.zero;
            float closestInRadiusInteractDist = 220f;
            SlotRequirementInfo closestInRadiusReq = null;

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
                string rawName = go.name;
                Durango.Terrain.BiomeSpriteInfo spriteInfo = null;
                string uiLabelText = null;

                if (labelNames.ContainsKey(go)) {
                    uiLabelText = labelNames[go];
                }

                try {
                    ImmovableBase imm = go.GetComponent<ImmovableBase>();
                    if (imm == null) imm = go.GetComponentInParent<ImmovableBase>();
                    if (imm == null) imm = go.GetComponentInChildren<ImmovableBase>();
                    if (imm != null) {
                        try {
                            spriteInfo = Durango.Terrain.DataHelper.GetBiomeSpriteInfo((int)imm.EntityType);
                            if (spriteInfo != null && !string.IsNullOrEmpty(spriteInfo.Name)) {
                                rawName = spriteInfo.Name;
                            }
                        } catch {}

                        if (string.IsNullOrEmpty(rawName) || rawName == go.name) {
                            string immName = imm.GetName();
                            if (!string.IsNullOrEmpty(immName)) rawName = immName;
                        }

                        // Cek NaturalPrefabObject
                        try {
                            NaturalPrefabObject npo = imm as NaturalPrefabObject;
                            if (npo != null && !string.IsNullOrEmpty(npo.PoolName)) {
                                rawName += " " + npo.PoolName;
                            }
                        } catch {}

                        // Cek NaturalSpriteObject
                        try {
                            NaturalSpriteObject nso = imm as NaturalSpriteObject;
                            if (nso != null && nso.Sprite != null) {
                                if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Pebble) {
                                    rawName += " pebble kerikil stone batu";
                                } else if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Rock) {
                                    rawName += " rock batu stone";
                                } else if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Tree) {
                                    rawName += " tree pohon kayu wood log";
                                } else if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Shrub) {
                                    rawName += " shrub semak bush";
                                } else if (nso.Sprite.SpriteObjectType == Durango.Render.Sprite.SpriteObjectType.Grass) {
                                    rawName += " grass rumput plant tanaman";
                                }
                                if (nso.Sprite.GameObject != null) {
                                    rawName += " " + nso.Sprite.GameObject.name;
                                }
                            }
                        } catch {}

                        if (imm.InteractionDistance > 0) {
                            iDist = Mathf.Max(220f, imm.InteractionDistance + 60f);
                        }
                    }
                } catch {}

                if (!string.IsNullOrEmpty(uiLabelText)) {
                    rawName += " " + uiLabelText;
                }

                // Nama bersih HANYA untuk GUI display (jangan potong rawName sebelum pencocokan!)
                string displayName = CleanObjectName(!string.IsNullOrEmpty(uiLabelText) ? uiLabelText : rawName);

                // Cek filter resource: Pada Smart Mode, cocokkan dengan node target yang dipelajari
                SlotRequirementInfo matchedReq = null;
                if (_hasLearnedTarget) {
                    if (!DoesNodeMatchLearnedTarget(go, rawName, spriteInfo)) continue;
                } else if (IsCraftPlanActive) {
                    if (missingPlanReqs != null && missingPlanReqs.Count > 0) {
                        matchedReq = FindMatchingMissingRequirement(go, rawName, spriteInfo, missingPlanReqs);
                        if (matchedReq == null) continue;
                    } else {
                        continue;
                    }
                } else {
                    if (!MatchesFilter(go, rawName, spriteInfo)) continue;
                }

                // Hitung jarak real-time
                float d = GetObjectDistance(player, go, pos);

                // Rekam objek terdekat mutlak
                if (d < absoluteMinDist) {
                    absoluteMinDist = d;
                    absoluteClosest = go;
                    absoluteClosestName = displayName;
                    absoluteClosestPos = pos;
                    absoluteClosestInteractDist = iDist;
                    absoluteClosestReq = matchedReq;
                }

                // Rekam objek terdekat dalam radius
                if (d <= maxRadiusUnits && d < minRadiusDist) {
                    minRadiusDist = d;
                    closestInRadius = go;
                    closestInRadiusName = displayName;
                    closestInRadiusPos = pos;
                    closestInRadiusInteractDist = iDist;
                    closestInRadiusReq = matchedReq;
                }
            }

            if (closestInRadius != null) {
                _targetName = closestInRadiusName;
                _currentTargetPos = closestInRadiusPos;
                _currentTargetInteractDist = closestInRadiusInteractDist;

                // Sinkronkan hunting requirement dengan sumber daya TERDEKAT yang ditemukan!
                if (IsCraftPlanActive && closestInRadiusReq != null) {
                    _currentHuntingSlot = closestInRadiusReq;
                    CurrentFilter = closestInRadiusReq.SuggestedFilter;
                    TargetMode = closestInRadiusReq.SuggestedTarget;
                    CustomKeyword = closestInRadiusReq.SuggestedKeyword;
                }

                return closestInRadius;
            }

            // Jika tidak ada dalam radius, tampilkan objek terdekat yang sesuai filter
            if (absoluteClosest != null) {
                if (_hasLearnedTarget) {
                    _statusText = string.Format("Terdekat: {0} ({1:F1}m) untuk [{2}] | Radius: {3:F0}m", 
                        absoluteClosestName, absoluteMinDist / 100f, _learnedActionName, ScanRadiusMeters);
                } else if (IsCraftPlanActive && absoluteClosestReq != null) {
                    _statusText = string.Format("Terdekat: {0} ({1:F1}m) untuk [{2}] | Radius: {3:F0}m", 
                        absoluteClosestName, absoluteMinDist / 100f, absoluteClosestReq.Name, ScanRadiusMeters);
                } else {
                    _statusText = string.Format("Terdekat: {0} ({1:F1}m) | Radius: {2:F0}m", 
                        absoluteClosestName, absoluteMinDist / 100f, ScanRadiusMeters);
                }
            } else {
                _statusText = string.Format("Mencari node ({0} node terdeteksi)", _totalCandidatesFound);
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
                if (gatherCountOnThisNode > 0) {
                    _recentlyGathered[target] = Time.time + 60f; // Blacklist 60s jika sudah selesai panen
                } else if (!hadUsableSlots) {
                    _recentlyGathered[target] = Time.time + 45f; // Blacklist 45s jika semua resource di node ini habis/cooldown
                } else {
                    _recentlyGathered[target] = Time.time + 20f; // Jeda 20s jika resource di node ini tidak cocok dengan kebutuhan
                }
            }
            _currentTargetObj = null;
            _targetAvailableResources = "-";
            ForceCloseInteractionMenu();
            _gatherCooldownUntil = Time.time + 0.35f;
            _isGathering = false;
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

        private string GetWidgetFullText(Durango.UI.InteractionMenuWidget_PC w) {
            if (w == null) return "";
            string text = "";
            try {
                if (!string.IsNullOrEmpty(w.Data.Name)) text += " " + w.Data.Name;
                if (!string.IsNullOrEmpty(w.Data.Id)) text += " " + w.Data.Id;
                try {
                    text += " " + w.Data.Action.ToString();
                } catch {}
                if (w.Data.GatheringData != null) {
                    if (!string.IsNullOrEmpty(w.Data.GatheringData.Name)) text += " " + w.Data.GatheringData.Name;
                    if (!string.IsNullOrEmpty(w.Data.GatheringData.GeneratorId)) text += " " + w.Data.GatheringData.GeneratorId;
                }
                string lbl = GetWidgetLabelText(w);
                if (!string.IsNullOrEmpty(lbl)) text += " " + lbl;
            } catch {}
            return text.ToLower();
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

            // 1. Pengecualian MUTLAK (Pasti item sekunder / non-anchor / tanaman / kerikil):
            if (act.Contains("cabang") || act.Contains("branch") || 
                act.Contains("ranting") || act.Contains("twig") || act.Contains("stick") || act.Contains("dahan") ||
                act.Contains("daun") || act.Contains("leaf") || act.Contains("leaves") || act.Contains("pelepah") || act.Contains("foliage") ||
                act.Contains("kerikil") || act.Contains("krikil") || act.Contains("pebble") || act.Contains("gravel") ||
                act.Contains("serpihan") || act.Contains("tajam") || act.Contains("flint") || act.Contains("shard") || act.Contains("chip") ||
                act.Contains("batu kecil") || act.Contains("batu halus") ||
                act.Contains("kulit") || act.Contains("bark") || act.Contains("kupas") ||
                act.Contains("buah") || act.Contains("berry") || act.Contains("fruit") || act.Contains("petik") ||
                act.Contains("rumput") || act.Contains("grass") || act.Contains("tanaman") || act.Contains("plant") ||
                act.Contains("buluh") || act.Contains("reed") || act.Contains("flax") || act.Contains("serat") ||
                act.Contains("fiber") || act.Contains("alang") || act.Contains("rami") || act.Contains("akar") ||
                act.Contains("herb") || act.Contains("jamur") || act.Contains("mushroom") ||
                act.Contains("biji") || act.Contains("seed") || act.Contains("bunga") || act.Contains("flower") ||
                act.Contains("getah") || act.Contains("sap") || act.Contains("resin") ||
                act.Contains("bulu") || act.Contains("feather") || act.Contains("ambil")) {
                return false;
            }

            // 2. Kriteria Item Jangkar (Perusak fisik node):
            // Tebang pohon / potong batang:
            if (act.Contains("tebang") || act.Contains("chop") || act.Contains("fell") || act.Contains("tumbang") ||
                act.Contains("batang") || act.Contains("log") || act.Contains("trunk")) {
                return true;
            }

            // Pecahkan batu besar / tambang bongkahan batu:
            if (act.Contains("pecah") || act.Contains("bongkahan") || act.Contains("batu besar") || 
                act.Contains("quarry") || act.Contains("boulder") || act.Contains("crag")) {
                return true;
            }

            // Cabut tunggul pohon / stump:
            if (act.Contains("tunggul") || act.Contains("stump")) {
                return true;
            }

            return false;
        }

        private string[] GetKeywordsForMode(GatherTargetMode mode) {
            switch (mode) {
                case GatherTargetMode.Daun:
                    return new string[] { "daun", "leaf", "leaves", "frond", "pelepah", "foliage" };
                case GatherTargetMode.Ranting:
                    return new string[] { "ranting", "cabang", "branch", "twig", "stick", "dahan", "tongkat", "paku", "pasak" };
                case GatherTargetMode.Batang:
                    return new string[] { "tebang", "chop", "batang", "log", "trunk", "fell", "tumbang" };
                case GatherTargetMode.Batu:
                    return new string[] { "batu", "stone", "rock", "tambang", "ore", "kerikil", "krikil", "pebble", "gravel", "pecah", "boulder", "flint", "serpihan", "tajam", "cobble", "bongkahan", "quarry" };
                case GatherTargetMode.Rumput:
                    return new string[] { "rumput", "grass", "reed", "flax", "tanaman", "plant", "herb", "serat", "fiber", "alang", "rami", "cabut", "buluh", "akar", "tali" };
                case GatherTargetMode.Daging:
                    return new string[] { "daging", "meat", "sayat", "carve", "butcher" };
                case GatherTargetMode.Kulit:
                    return new string[] { "kulit", "bark", "hide", "leather", "kupas" };
                case GatherTargetMode.Buah:
                    return new string[] { "buah", "berry", "fruit" };
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
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (!ctrl) return;

                bool key3Down = Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                bool key1Down = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                bool key3Held = Input.GetKey(KeyCode.Alpha3) || Input.GetKey(KeyCode.Keypad3);
                bool key1Held = Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1);
                bool keyHDown = Input.GetKeyDown(KeyCode.H);

                float now = Time.time;
                if (key3Down) _lastCtrl3Time = now;
                if (key1Down) _lastCtrl1Time = now;

                bool triggered = false;

                // 1. Chord combo: Ctrl ditahan, 3 & 1 ditekan bersamaan
                if ((key3Held && key1Down) || (key1Held && key3Down)) {
                    triggered = true;
                }
                // 2. Sequential combo: Ctrl ditahan, tekan 3 lalu tekan 1 (atau sebaliknya dalam rentang 1.5 detik)
                else if (key1Down && (now - _lastCtrl3Time <= 1.5f)) {
                    triggered = true;
                } else if (key3Down && (now - _lastCtrl1Time <= 1.5f)) {
                    triggered = true;
                }
                // 3. Shortcut cepat alternatif: Ctrl + H
                else if (keyHDown) {
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

        public void CycleMenuState() {
            if (MenuState == MenuDisplayState.Normal) {
                MenuState = MenuDisplayState.Minimized;
            } else if (MenuState == MenuDisplayState.Minimized) {
                MenuState = MenuDisplayState.Hidden;
            } else {
                MenuState = MenuDisplayState.Normal;
            }
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

        void OnGUI() {
            try {
                InitGUIStyles();
                if (_boxStyle == null) return;

                // Render In-World 3D ESP Tags & Snaplines (Tetap aktif walau UI panel di-minimize/hidden)
                DrawInWorldEsp();

                // 1. STATE: HIDDEN
                if (MenuState == MenuDisplayState.Hidden) {
                    Rect pillRect = new Rect(20, 20, 195, 26);
                    GUI.Box(pillRect, GUIContent.none, _boxStyle);
                    GUILayout.BeginArea(pillRect);
                    GUILayout.BeginHorizontal();
                    string sTxt = IsEnabled ? "<color=#2ecc71>● ON</color>" : "<color=#e74c3c>● OFF</color>";
                    GUILayout.Label(string.Format("Bot {0} <size=9>[Ctrl+3+1]</size>", sTxt), _labelStyle ?? GUI.skin.label);
                    if (GUILayout.Button("Buka", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(45), GUILayout.Height(18))) {
                        MenuState = MenuDisplayState.Normal;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    return;
                }

                // 2. STATE: MINIMIZED
                if (MenuState == MenuDisplayState.Minimized) {
                    Rect miniRect = new Rect(20, 20, 390, 56);
                    GUI.Box(miniRect, GUIContent.none, _boxStyle);
                    GUILayout.BeginArea(miniRect);
                    GUILayout.Space(2);
                    GUILayout.BeginHorizontal();
                    string statusColor = IsEnabled ? "#2ecc71" : "#e74c3c";
                    string statusName = IsEnabled ? "AKTIF [ON]" : "OFF";
                    GUILayout.Label(string.Format("<b>[XD LANDS]</b> <color={0}>[{1}]</color> ({2})", statusColor, statusName, ToggleKey), _labelStyle ?? GUI.skin.label, GUILayout.Width(170));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("□", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {
                        MenuState = MenuDisplayState.Normal;
                    }
                    if (GUILayout.Button("X", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {
                        MenuState = MenuDisplayState.Hidden;
                    }
                    GUILayout.EndHorizontal();

                    string miniDetail = "";
                    if (_hasLearnedTarget) {
                        miniDetail = string.Format("Auto: <b>{0}</b> ({1:F1}m) | <size=10>{2}</size>", _learnedActionName, _targetDistance / 100f, _statusText);
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

                // 3. STATE: NORMAL
                Rect panelRect = new Rect(20, 20, 420, 440);
                GUI.Box(panelRect, GUIContent.none, _boxStyle);

                GUILayout.BeginArea(panelRect);
                GUILayout.Space(6);

                // Header dengan tombol minimize & close
                GUILayout.BeginHorizontal();
                if (_headerStyle != null) GUILayout.Label("  [XD LANDS] AUTO-GATHER BOT", _headerStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("-", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {
                    MenuState = MenuDisplayState.Minimized;
                }
                if (GUILayout.Button("X", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(24), GUILayout.Height(18))) {
                    MenuState = MenuDisplayState.Hidden;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);

                // Status Bar Utama
                string statusColorMain = IsEnabled ? "#2ecc71" : "#e74c3c";
                string statusNameMain = IsEnabled ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                if (_labelStyle != null) {
                    GUILayout.Label(string.Format("  Status: <color={0}><b>{1}</b></color> (Bot: [{2}] | Menu: [Ctrl+3+1] / [-] / [X])", statusColorMain, statusNameMain, ToggleKey), _labelStyle);
                    if (_hasLearnedTarget) {
                        GUILayout.Label(string.Format("  Target Terkunci: <color=#2ecc71><b>{0}</b></color> pada <b>{1}</b>", _learnedActionName, _learnedNodeDisplayName), _labelStyle);
                    } else if (IsCraftPlanActive && PlanRecipe != null) {
                        string huntInfo = (_currentHuntingSlot != null) 
                            ? ("Berburu: " + (!string.IsNullOrEmpty(_currentHuntingSlot.ParentName) ? (_currentHuntingSlot.Name + " (" + _currentHuntingSlot.ParentName + ")") : _currentHuntingSlot.Name)) 
                            : "Bahan Lengkap";
                        GUILayout.Label(string.Format("  Plan: <color=#f1c40f><b>{0} x{1}</b></color> ({2})", PlanRecipe.Name, PlanQuantity, huntInfo), _labelStyle);
                    } else {
                        GUILayout.Label("  Target: <color=#f1c40f><b>Belum Dipelajari</b></color> (Panen 1x di game untuk deteksi otomatis)", _labelStyle);
                    }

                    if (IsEnabled && _currentTargetObj != null) {
                        GUILayout.Label(string.Format("  Jarak: <b>{0:F1} m</b> | Node: <color=#f1c40f>{1}</color>", _targetDistance / 100f, _targetAvailableResources), _labelStyle);
                    }
                    GUILayout.Label(string.Format("  Info: <size=11>{0}</size>", _statusText), _labelStyle);
                }

                GUILayout.Space(4);

                // TAB SELECTOR: [Auto Panen] | [Craft Plan] | [Tempur] | [Radar ESP]
                GUILayout.BeginHorizontal();
                DrawTabButton("Auto Panen", 0, 97);
                string planTabLabel = (PlanRecipe != null) ? ("Plan:" + PlanRecipe.Name) : "Craft Plan";
                if (planTabLabel.Length > 12) planTabLabel = planTabLabel.Substring(0, 11) + "..";
                DrawTabButton(planTabLabel, 1, 97);
                DrawTabButton("Tempur", 2, 97);
                DrawTabButton("Radar ESP", 3, 97);
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
                } else if (ActiveTab == 1) {
                    // ================= TAB 1: CRAFT PLANNER =================
                    // 1. DETAIL TARGET RESEP & JUMLAH (Dukungan Crafting > 15)
                    int maxCanCraft = (PlanRecipe != null) ? GetMaxCraftableCount(PlanRecipe) : 0;
                    GUILayout.BeginHorizontal();
                    string recName = (PlanRecipe != null) ? PlanRecipe.Name : "<color=#e74c3c>Belum Dipilih</color>";
                    if (_labelStyle != null) GUILayout.Label(string.Format("  Resep: <b>{0}</b>", recName), _labelStyle, GUILayout.Width(170));

                    if (_smallBtnStyle != null) {
                        if (GUILayout.Button("-", _smallBtnStyle, GUILayout.Width(20), GUILayout.Height(20))) {
                            PlanQuantity = Mathf.Max(1, PlanQuantity - 1);
                            UpdatePlanRequirements();
                        }
                        GUILayout.Label(string.Format("<b>x{0}</b>", PlanQuantity), _labelStyle ?? GUI.skin.label, GUILayout.Width(32));
                        if (GUILayout.Button("+", _smallBtnStyle, GUILayout.Width(20), GUILayout.Height(20))) {
                            PlanQuantity = Mathf.Min(999, PlanQuantity + 1);
                            UpdatePlanRequirements();
                        }
                        if (GUILayout.Button("+5", _smallBtnStyle, GUILayout.Width(26), GUILayout.Height(20))) {
                            PlanQuantity = Mathf.Min(999, PlanQuantity + 5);
                            UpdatePlanRequirements();
                        }
                        if (GUILayout.Button("+15", _smallBtnStyle, GUILayout.Width(30), GUILayout.Height(20))) {
                            PlanQuantity = Mathf.Min(999, PlanQuantity + 15);
                            UpdatePlanRequirements();
                        }
                        if (GUILayout.Button("+50", _smallBtnStyle, GUILayout.Width(30), GUILayout.Height(20))) {
                            PlanQuantity = Mathf.Min(999, PlanQuantity + 50);
                            UpdatePlanRequirements();
                        }
                        if (GUILayout.Button("Max", _smallBtnStyle, GUILayout.Width(32), GUILayout.Height(20))) {
                            PlanQuantity = Mathf.Max(1, maxCanCraft);
                            UpdatePlanRequirements();
                        }
                    }
                    GUILayout.EndHorizontal();

                    // Status Bahan Lengkap & Kesanggupan Tas Real-Time
                    if (_labelStyle != null) {
                        string maxCol = (maxCanCraft > 0) ? "#2ecc71" : "#e74c3c";
                        GUILayout.Label(string.Format("  Status: {0} | Cukup Bahan Tas: <color={1}><b>x{2}</b></color>", _planStatusSummary, maxCol, maxCanCraft), _labelStyle);
                    }

                    // Tombol Eksekusi Langsung Multi-Crafting
                    if (PlanRecipe != null) {
                        GUILayout.BeginHorizontal();
                        bool canCraftPlan = (PlanQuantity <= maxCanCraft && maxCanCraft > 0);
                        string planCraftLabel = string.Format("Mulai Crafting (x{0})", PlanQuantity);
                        GUIStyle planBtnSt = canCraftPlan ? (_selectedBtnStyle ?? _smallBtnStyle) : (_btnStyle ?? GUI.skin.button);
                        if (GUILayout.Button(planCraftLabel, planBtnSt, GUILayout.Width(150), GUILayout.Height(22))) {
                            StartDirectCraft(PlanRecipe, PlanQuantity);
                        }

                        if (maxCanCraft > 0) {
                            string maxCraftLabel = string.Format("Craft Semua (x{0})", maxCanCraft);
                            if (GUILayout.Button(maxCraftLabel, _selectedBtnStyle ?? _smallBtnStyle, GUILayout.Width(130), GUILayout.Height(22))) {
                                StartDirectCraft(PlanRecipe, maxCanCraft);
                            }
                        }

                        if (GUILayout.Button("In-Game UI", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(65), GUILayout.Height(22))) {
                            OpenCraftUIInGame(PlanRecipeId);
                        }
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Reset", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(45), GUILayout.Height(22))) {
                            PlanRecipe = null;
                            PlanRecipeId = "";
                            _planSlotInfos.Clear();
                            _searchResults.Clear();
                            _planStatusSummary = "Pilih resep untuk memulai checklist";
                        }
                        GUILayout.EndHorizontal();
                    }

                    // 1b. AUTO-MAKAN ENERGI SAAT CRAFTING
                    GUILayout.BeginHorizontal();
                    string eatCol = AutoEatWhenCrafting ? "#2ecc71" : "#e74c3c";
                    string eatTxt = AutoEatWhenCrafting ? "● Auto-Makan [ON]" : "● Auto-Makan [OFF]";
                    string staminaInfo = "";
                    try {
                        PlayerBehavior p = PlayerBehavior.LocalPlayer;
                        if (p != null && p.Stamina != null) {
                            staminaInfo = string.Format(" | Energi: <b>{0:F0}/{1:F0}</b>", p.Stamina.Get(), p.Stamina.Max());
                        }
                    } catch {}
                    string lastEatMsg = !string.IsNullOrEmpty(_lastEatenFoodName) ? string.Format(" (Makan: {0})", _lastEatenFoodName) : "";
                    if (_labelStyle != null) {
                        GUILayout.Label(string.Format("  Auto-Makan Energi: <color={0}><b>{1}</b></color>{2}{3}", eatCol, eatTxt, staminaInfo, lastEatMsg), _labelStyle, GUILayout.Width(300));
                    }
                    if (GUILayout.Button(AutoEatWhenCrafting ? "Matikan" : "Nyalakan", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(65), GUILayout.Height(18))) {
                        AutoEatWhenCrafting = !AutoEatWhenCrafting;
                    }
                    GUILayout.EndHorizontal();

                    // 2. CHECKLIST MATERIAL SLOTS (HIERARKI & REALTIME)
                    if (_planSlotInfos.Count > 0) {
                        GUILayout.BeginHorizontal();
                        if (_subHeaderStyle != null) GUILayout.Label("  Checklist Bahan & Hierarki:", _subHeaderStyle);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Perbarui", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(65), GUILayout.Height(18))) {
                            UpdatePlanRequirements();
                        }
                        GUILayout.EndHorizontal();

                        _materialScrollPos = GUILayout.BeginScrollView(_materialScrollPos, GUILayout.Height(95));
                        for (int i = 0; i < _planSlotInfos.Count; i++) {
                            SlotRequirementInfo slot = _planSlotInfos[i];
                            if (slot == null) continue;

                            bool isReady = (slot.Missing == 0);
                            bool isHunting = (IsCraftPlanActive && _currentHuntingSlot == slot);

                            if (isReady) {
                                // Bahan utama sudah ada di inventory!
                                string line = string.Format("    <color=#2ecc71>[✓] <b>{0}</b>: {1}/{2} (Lengkap)</color>", 
                                    slot.Name, slot.Available, slot.Required);
                                GUILayout.Label(line, _labelStyle ?? GUI.skin.label);
                            } else {
                                // Bahan utama masih kurang
                                if (slot.IsCraftable && slot.SubRecipe != null) {
                                    bool subReady = AreSubMaterialsReady(slot);
                                    GUILayout.BeginHorizontal();
                                    string craftIcon = subReady ? "<color=#2ecc71>[⚙]</color>" : "<color=#e67e22>[✗]</color>";
                                    string craftStatus = subReady ? "<color=#2ecc71>(Bahan Lengkap! Siap Craft)</color>" : "<color=#e67e22>(Perlu Dibuat)</color>";
                                    string parentLine = string.Format("    {0} <b>{1}</b>: {2}/{3} {4}", craftIcon, slot.Name, slot.Available, slot.Required, craftStatus);
                                    GUILayout.Label(parentLine, _labelStyle ?? GUI.skin.label);
                                    
                                    if (subReady && _smallBtnStyle != null) {
                                        string subName = !string.IsNullOrEmpty(slot.SubRecipe.Name) ? slot.SubRecipe.Name : slot.Name;
                                        int subNeeded = Mathf.Max(1, slot.Missing);
                                        if (GUILayout.Button(string.Format("Craft x{0} {1}", subNeeded, subName), _selectedBtnStyle ?? _smallBtnStyle, GUILayout.Height(18))) {
                                            StartDirectCraft(slot.SubRecipe, subNeeded);
                                        }
                                    }
                                    GUILayout.EndHorizontal();

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

                                            string subLine = string.Format("        └ <color={0}>{1} {2}: {3}/{4}</color>{5}", 
                                                subCol, subMark, sub.Name, sub.Available, sub.Required, subHuntTag);
                                            GUILayout.Label(subLine, _labelStyle ?? GUI.skin.label);
                                        }
                                    }
                                } else {
                                    // Bahan mentah langsung (tanpa sub-resep)
                                    string huntingTag = isHunting ? " <color=#f1c40f><- [TERDEKAT]</color>" : "";
                                    string line = string.Format("    <color=#e67e22>[✗] <b>{0}</b>: {1}/{2}</color>{3}", 
                                        slot.Name, slot.Available, slot.Required, huntingTag);
                                    GUILayout.Label(line, _labelStyle ?? GUI.skin.label);
                                }
                            }
                        }
                        GUILayout.EndScrollView();
                    } else {
                        GUILayout.Space(2);
                        if (_hintStyle != null) GUILayout.Label("  Silakan pilih resep dari daftar atau klik [Ambil Dari Game]", _hintStyle);
                    }

                    if (PlanRecipe == null) {
                        GUILayout.Space(3);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Ambil Dari Menu Game", _btnStyle ?? GUI.skin.button, GUILayout.Width(250), GUILayout.Height(22))) {
                            SyncRecipeFromGame();
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
                    }

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
                    // ================= TAB 2: COMBAT & DPS MAXIMIZER =================
                    if (_subHeaderStyle != null) GUILayout.Label("  ⚔️ Pengaturan Tempur & DPS Maximizer (Hunt Protector):", _subHeaderStyle);

                    _combatScrollPos = GUILayout.BeginScrollView(_combatScrollPos, GUILayout.Height(280));

                    GUILayout.BeginVertical(GUI.skin.box);

                    // 1. Turbo ASPD / Fast Recovery Cut
                    GUILayout.BeginHorizontal();
                    string recColor = EnableRecoveryCut ? "#2ecc71" : "#e74c3c";
                    string recTxt = EnableRecoveryCut ? string.Format("AKTIF [{0:F2}s]", RecoveryCutDelay) : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  Turbo ASPD (Recovery Cut): <color={0}><b>{1}</b></color>", recColor, recTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(220));
                    if (GUILayout.Button(EnableRecoveryCut ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(130))) {
                        EnableRecoveryCut = !EnableRecoveryCut;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Jeda:", _labelStyle ?? GUI.skin.label, GUILayout.Width(45));
                    if (GUILayout.Button("Turbo (0.02s)", (EnableRecoveryCut && RecoveryCutDelay <= 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(110), GUILayout.Height(20))) {
                        EnableRecoveryCut = true;
                        RecoveryCutDelay = 0.02f;
                    }
                    if (GUILayout.Button("Fast (0.08s)", (EnableRecoveryCut && RecoveryCutDelay > 0.03f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Width(100), GUILayout.Height(20))) {
                        EnableRecoveryCut = true;
                        RecoveryCutDelay = 0.08f;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  • Memotong jeda animasi tebasan agar serangan berikutnya langsung siap instan.", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 2. Anti-Stagger / Anti-Stun (Kebal Interupsi Dino)
                    GUILayout.BeginHorizontal();
                    string stagCol = EnableAntiStagger ? "#2ecc71" : "#e74c3c";
                    string stagTxt = EnableAntiStagger ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  Anti-Stagger (Kebal Interupsi): <color={0}><b>{1}</b></color>", stagCol, stagTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(220));
                    if (GUILayout.Button(EnableAntiStagger ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(130))) {
                        EnableAntiStagger = !EnableAntiStagger;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  • Kebal guncangan & knockback dino. Pukulan monster tidak akan membatalkan skill atau menghentikan gerak.", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 3. Auto-Aimlock & Face Target
                    GUILayout.BeginHorizontal();
                    string aimCol = EnableAutoAimlock ? "#2ecc71" : "#e74c3c";
                    string aimTxt = EnableAutoAimlock ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  Auto-Aimlock (Hadap Musuh): <color={0}><b>{1}</b></color>", aimCol, aimTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(220));
                    if (GUILayout.Button(EnableAutoAimlock ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(130))) {
                        EnableAutoAimlock = !EnableAutoAimlock;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  • Mengunci arah hadap karakter tepat ke target dino, 100% serangan kena hitbox.", _hintStyle ?? GUI.skin.label);

                    GUILayout.Space(3);

                    // 4. Moving Attack
                    GUILayout.BeginHorizontal();
                    string mobColor = EnableMobileAttack ? "#2ecc71" : "#e74c3c";
                    string mobTxt = EnableMobileAttack ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  Moving Attack (Jalan Bebas): <color={0}><b>{1}</b></color>", mobColor, mobTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(220));
                    if (GUILayout.Button(EnableMobileAttack ? "Matikan" : "Aktifkan", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(130))) {
                        EnableMobileAttack = !EnableMobileAttack;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("  • Bebas bergerak (WASD / Klik) saat menebas tanpa membeku di tempat.", _hintStyle ?? GUI.skin.label);

                    GUILayout.EndVertical();

                    GUILayout.Space(3);

                    // 5. Pengaturan Kecepatan Gerak (Speed Multiplier)
                    if (_subHeaderStyle != null) GUILayout.Label("  Pengaturan Kecepatan Gerak (Speed Multiplier):", _subHeaderStyle);
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Format("  Kecepatan: <color=#2ecc71><size=13><b>{0:F2}x</b></size></color>", MoveSpeedMultiplier), _labelStyle ?? GUI.skin.label, GUILayout.Width(130));
                    if (GUILayout.Button("- 0.1x", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(46), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = Mathf.Max(0.5f, MoveSpeedMultiplier - 0.1f);
                    }
                    if (GUILayout.Button("+ 0.1x", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(46), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = Mathf.Min(3.0f, MoveSpeedMultiplier + 0.1f);
                    }
                    if (GUILayout.Button("1.0x Reset", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(66), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 1.0f;
                    }
                    if (GUILayout.Button("1.5x Cepat", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(66), GUILayout.Height(20))) {
                        MoveSpeedMultiplier = 1.5f;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("1.0x", (MoveSpeedMultiplier == 1.0f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20))) MoveSpeedMultiplier = 1.0f;
                    if (GUILayout.Button("1.2x", (Mathf.Abs(MoveSpeedMultiplier - 1.2f) < 0.01f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20))) MoveSpeedMultiplier = 1.2f;
                    if (GUILayout.Button("1.35x", (Mathf.Abs(MoveSpeedMultiplier - 1.35f) < 0.01f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20))) MoveSpeedMultiplier = 1.35f;
                    if (GUILayout.Button("1.5x", (Mathf.Abs(MoveSpeedMultiplier - 1.5f) < 0.01f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20))) MoveSpeedMultiplier = 1.5f;
                    if (GUILayout.Button("1.75x", (Mathf.Abs(MoveSpeedMultiplier - 1.75f) < 0.01f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20))) MoveSpeedMultiplier = 1.75f;
                    if (GUILayout.Button("2.0x", (Mathf.Abs(MoveSpeedMultiplier - 2.0f) < 0.01f) ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20))) MoveSpeedMultiplier = 2.0f;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                    GUILayout.EndScrollView();
                } else if (ActiveTab == 3) {
                    // ================= TAB 3: RADAR & ESP SCANNER =================
                    if (_subHeaderStyle != null) GUILayout.Label("  📡 Radar & ESP 3D Scanner (Dino Langka & Sumber Daya):", _subHeaderStyle);

                    GUILayout.BeginVertical(GUI.skin.box);
                    
                    // Master Switch & Snaplines & Edge Radar
                    GUILayout.BeginHorizontal();
                    string espColor = EnableEsp ? "#2ecc71" : "#e74c3c";
                    string espTxt = EnableEsp ? "AKTIF [ON]" : "NONAKTIF [OFF]";
                    GUILayout.Label(string.Format("  ESP 3D: <color={0}><b>{1}</b></color>", espColor, espTxt), _labelStyle ?? GUI.skin.label, GUILayout.Width(170));
                    if (GUILayout.Button(EnableEsp ? "Matikan ESP" : "Aktifkan ESP", _btnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(95))) {
                        EnableEsp = !EnableEsp;
                    }
                    if (GUILayout.Button(EnableEdgeRadar ? "Edge: ON" : "Edge: OFF", (EnableEdgeRadar ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(75))) {
                        EnableEdgeRadar = !EnableEdgeRadar;
                    }
                    if (GUILayout.Button(EspShowSnaplines ? "Line: ON" : "Line: OFF", (EspShowSnaplines ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(70))) {
                        EspShowSnaplines = !EspShowSnaplines;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);

                    // Kategori Filter Toggles (Baris 1: Dinosaurus & Bangkai)
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(EspShowRareOnly ? "[★ Langka Saja]" : "[Semua Dino]", (EspShowRareOnly ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(95), GUILayout.Height(20))) {
                        EspShowRareOnly = !EspShowRareOnly;
                    }
                    if (GUILayout.Button(EspShowCorpses ? "[✓ Bangkai]" : "[✗ Bangkai]", (EspShowCorpses ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(95), GUILayout.Height(20))) {
                        EspShowCorpses = !EspShowCorpses;
                    }
                    if (GUILayout.Button(EspShowCraters ? "[✓ Kawah]" : "[✗ Kawah]", (EspShowCraters ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(95), GUILayout.Height(20))) {
                        EspShowCraters = !EspShowCraters;
                    }
                    if (GUILayout.Button(EspShowWaterSources ? "[✓ Mata Air]" : "[✗ Mata Air]", (EspShowWaterSources ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(95), GUILayout.Height(20))) {
                        EspShowWaterSources = !EspShowWaterSources;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);

                    // Jarak Scan Presets
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Format("  Radius ({0}):", EspMaxDistance >= 90000f ? "1 Map Penuh" : string.Format("{0:F0}m", EspMaxDistance)), _labelStyle ?? GUI.skin.label, GUILayout.Width(110));
                    if (GUILayout.Button("150m", (Mathf.Abs(EspMaxDistance - 150f) < 1f ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(46), GUILayout.Height(18))) EspMaxDistance = 150f;
                    if (GUILayout.Button("500m", (Mathf.Abs(EspMaxDistance - 500f) < 1f ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(46), GUILayout.Height(18))) EspMaxDistance = 500f;
                    if (GUILayout.Button("1km", (Mathf.Abs(EspMaxDistance - 1000f) < 1f ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(44), GUILayout.Height(18))) EspMaxDistance = 1000f;
                    if (GUILayout.Button("2km", (Mathf.Abs(EspMaxDistance - 2000f) < 1f ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(44), GUILayout.Height(18))) EspMaxDistance = 2000f;
                    if (GUILayout.Button("1 Map", (EspMaxDistance >= 90000f ? _selectedBtnStyle : _smallBtnStyle) ?? GUI.skin.button, GUILayout.Width(52), GUILayout.Height(18))) EspMaxDistance = 99999f;
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);

                    // Tombol Buka Peta Penuh
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Jelajah / Buka Kabut Peta:", _labelStyle ?? GUI.skin.label, GUILayout.Width(170));
                    if (GUILayout.Button("🗺️ Buka Peta Penuh (Reveal Map)", _smallBtnStyle ?? GUI.skin.button, GUILayout.Height(20), GUILayout.Width(235))) {
                        try {
                            MapSystem ms = null;
                            try { ms = GameSystem<MapSystem>.Instance(); } catch {}
                            if (ms == null) ms = UnityEngine.Object.FindObjectOfType<MapSystem>();
                            if (ms != null) {
                                ms.PurchaseMap(string.Empty, 0f);
                                _statusText = "Membuka peta pulau...";
                                UpdateEspScan();
                            }
                        } catch (Exception ex) {
                            Debug.LogWarning("[AutoGather] PurchaseMap error: " + ex);
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();

                    GUILayout.Space(3);

                    // Target List Header
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Format("  <b>Daftar Radar ({0} Objek):</b>", _espTargets.Count), _labelStyle ?? GUI.skin.label);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Scan Ulang", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(80), GUILayout.Height(18))) {
                        UpdateEspScan();
                    }
                    GUILayout.EndHorizontal();

                    // Scroll Area untuk list objek radar
                    _radarScrollPos = GUILayout.BeginScrollView(_radarScrollPos, GUILayout.Height(150));
                    if (_espTargets.Count == 0) {
                        GUILayout.Label("  <color=#7f8c8d><i>Tidak ada objek terdeteksi dalam radius...</i></color>", _hintStyle ?? GUI.skin.label);
                    } else {
                        int drawnCount = 0;
                        for (int i = 0; i < _espTargets.Count; i++) {
                            EspTargetInfo item = _espTargets[i];
                            if (item == null) continue;

                            if (item.Category == EspCategory.NormalDino && EspShowRareOnly) continue;
                            if (item.Category == EspCategory.DinoCorpse && !EspShowCorpses) continue;
                            if (item.Category == EspCategory.Crater && !EspShowCraters) continue;
                            if (item.Category == EspCategory.WaterSource && !EspShowWaterSources) continue;

                            GUILayout.BeginHorizontal(GUI.skin.box);
                            
                            string catColorHex = ColorUtility.ToHtmlStringRGB(item.TagColor);
                            string title = string.Format("<color=#{0}><b>{1}</b></color> <b>{2}</b>", catColorHex, item.CategoryLabel, item.Level > 0 ? (item.Name + " Lv." + item.Level) : item.Name);
                            string distTxt = item.DistanceMeters >= 1000f ? string.Format("{0:F1}km", item.DistanceMeters / 1000f) : string.Format("{0:F0}m", item.DistanceMeters);
                            string sub = item.IsAlive 
                                ? string.Format("<size=9><color=#f1c40f>{0}</color> | HP: {1:F0}%</size>", distTxt, item.HpRatio * 100f)
                                : string.Format("<size=9><color=#f1c40f>{0}</color> | {1}</size>", distTxt, item.IsLootable ? "<color=#2ecc71>Bisa Di-loot</color>" : "Kosong");

                            GUILayout.BeginVertical();
                            GUILayout.Label(title, _labelStyle ?? GUI.skin.label);
                            GUILayout.Label(sub, _hintStyle ?? GUI.skin.label);
                            GUILayout.EndVertical();

                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Dekati", _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(58), GUILayout.Height(24))) {
                                NavigateToEspTarget(item);
                            }

                            GUILayout.EndHorizontal();
                            drawnCount++;
                            if (drawnCount >= 50) break;
                        }
                    }
                    GUILayout.EndScrollView();
                }

                GUILayout.FlexibleSpace();

                // 4. TOMBOL TOGGLE ON/OFF & PILIH HOTKEY
                GUILayout.BeginHorizontal();
                if (_btnStyle != null) {
                    string btnText = IsEnabled ? string.Format("Matikan Bot [{0}]", ToggleKey) : string.Format("Nyalakan Bot [{0}]", ToggleKey);
                    if (GUILayout.Button(btnText, _btnStyle, GUILayout.Height(26), GUILayout.Width(290))) {
                        ToggleMod();
                    }
                }
                if (GUILayout.Button("Key: [" + ToggleKey + "]", _smallBtnStyle ?? GUI.skin.button, GUILayout.Height(26), GUILayout.Width(110))) {
                    CycleToggleKey();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6);

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
            }
        }

        private void DrawPresetButton(string keyword, float width) {
            if (GUILayout.Button(keyword, _smallBtnStyle ?? GUI.skin.button, GUILayout.Width(width), GUILayout.Height(20))) {
                SetPlanPreset(keyword);
            }
        }

        private void DrawFilterButton(string label, ResourceFilterMode mode, float width) {
            bool isSelected = (CurrentFilter == mode);
            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;
            string display = isSelected ? ("[" + label + "]") : label;
            if (GUILayout.Button(display, st, GUILayout.Width(width), GUILayout.Height(20))) {
                CurrentFilter = mode;
                _currentTargetObj = null; // Reset target agar scan ulang sesuai filter baru
            }
        }

        private void DrawTargetModeButton(string label, GatherTargetMode mode, float width) {
            bool isSelected = (TargetMode == mode && string.IsNullOrEmpty(CustomKeyword.Trim()));
            GUIStyle st = isSelected ? _selectedBtnStyle : _smallBtnStyle;
            string display = isSelected ? ("[" + label + "]") : label;
            if (GUILayout.Button(display, st, GUILayout.Width(width), GUILayout.Height(20))) {
                TargetMode = mode;
                CustomKeyword = "";
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

                if (_whiteTex == null) {
                    _whiteTex = new Texture2D(1, 1);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                }

                if (_espTagStyle == null) {
                    _espTagStyle = new GUIStyle();
                    _espTagStyle.alignment = TextAnchor.MiddleCenter;
                    _espTagStyle.fontSize = 10;
                    _espTagStyle.richText = true;
                    _espTagStyle.normal.textColor = Color.white;
                }

                if (_espEdgeStyle == null) {
                    _espEdgeStyle = new GUIStyle();
                    _espEdgeStyle.alignment = TextAnchor.MiddleCenter;
                    _espEdgeStyle.fontSize = 10;
                    _espEdgeStyle.fontStyle = FontStyle.Bold;
                    _espEdgeStyle.richText = true;
                    _espEdgeStyle.normal.textColor = Color.white;
                }
            } catch {}
        }

        void OnDestroy() {
            try {
                UnhookInventoryEvent();
                if (_hookedIntSelector != null) {
                    try { _hookedIntSelector.ValueChanged -= OnInGameCraftQuantityChanged; } catch {}
                    _hookedIntSelector = null;
                }
                if (_bgTexture != null) {
                    Destroy(_bgTexture);
                }
                if (_whiteTex != null) {
                    Destroy(_whiteTex);
                    _whiteTex = null;
                }
                try {
                    PlayerController pc = GetPlayerController();
                    if (pc != null) {
                        pc.CheatMoveSpeedMultiply = 1.0f;
                    }
                } catch {}
            } catch {}
        }
    }
}
