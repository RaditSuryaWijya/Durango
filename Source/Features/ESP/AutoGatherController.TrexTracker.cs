using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Durango.Logic.Combat;
using Durango.Logic.Explore;
using Durango.Logic.Map;
using Shared.Region;
using Shared.System;

namespace AutoGather {
    public partial class AutoGatherController {
        // =========================================================================
        // TYRANNOSAURUS REX & EPIC ISLAND TRACKER ENGINE
        // =========================================================================
        // Fitur pelacak T-Rex & Pulau Kawah Epik (Lv.60 Volcanic):
        // 1. Scan rute kepulauan (Archipelago) untuk mendeteksi Pulau Epik T-Rex
        //    yang sedang aktif di dunia, lengkap dengan hitung mundur sisa umur pulau.
        // 2. Radar kawah di pulau saat ini (menentukan arah mata angin & jarak ke kawah).
        // 3. Deteksi langsung entitas T-Rex hidup di sekitar kawah dengan HUD Alert & Audio.
        // =========================================================================

        public bool EnableTrexDetector = true;        // Master toggle fitur pelacak T-Rex
        public bool EnableTrexCraterRadar = true;     // Radar penunjuk arah sarang kawah di pulau
        public bool EnableTrexAudioAlarm = true;      // Audio sirene / alert saat T-Rex terlihat
        public bool EnableTrexHudBanner = true;       // Banner visual alert di layar

        public class EpicIslandEntry {
            public string ArchipelagoId = "";
            public string ArchipelagoName = "";
            public string RegionId = "";
            public string RegionName = "";
            public int Level = 60;
            public string BiomeName = "Volcanic";
            public double RemainingSeconds = 0.0;
            public System.DateTime ScanTime = System.DateTime.MinValue;
            public bool IsEpic = true;
        }

        public class TrexLiveTarget {
            public string Name = "Tyrannosaurus";
            public int Level = 60;
            public float HpRatio = 1f;
            public Vector3 WorldPos = Vector3.zero;
            public float DistanceMeters = 0f;
            public string CompassDirection = "";
            public bool IsAlive = true;
            public GameObject TargetObj = null;
        }

        public class CraterTarget {
            public string Title = "Kawah Sarang T-Rex";
            public Vector3 WorldPos = Vector3.zero;
            public float DistanceMeters = 0f;
            public string CompassDirection = "";
            public Point2 Tile = default(Point2);
            public bool IsExplored = false;
        }

        // State runtime pelacak
        private readonly List<EpicIslandEntry> _detectedEpicIslands = new List<EpicIslandEntry>();
        private readonly List<CraterTarget> _currentIslandCraters = new List<CraterTarget>();
        private CraterTarget _closestCrater = null;
        private TrexLiveTarget _activeLiveTrex = null;

        private float _lastTrexScanTime = 0f;
        private float _lastEpicScanTime = 0f;
        private float _lastTrexAlarmAudioTime = 0f;

        private string _currentIslandName = "";
        private string _currentIslandBiome = "";
        private int _currentIslandLevel = 0;
        private bool _isCurrentIslandVolcanic = false;
        private Vector2 _trexTabScrollPos = Vector2.zero;
        private string _trexStatusLog = "Menunggu pemindaian kepulauan...";

        // Cache reflection untuk ExploreSystem & MapSystem
        private static FieldInfo _fArchipelagoRoutes = null;
        private static FieldInfo _fExploredPOIs = null;
        private static FieldInfo _fPoiUpdater = null;
        private static FieldInfo _fExploredPOIsDict = null;

        // Waypoint navigasi kawah
        private Vector3 _trexWaypointPos = Vector3.zero;
        private bool _isTrexWaypointActive = false;

        // =========================================================================
        // ENGINE UPDATE (Dipanggil dari Update() setiap detik)
        // =========================================================================
        public void UpdateTrexTrackerEngine() {
            if (!EnableTrexDetector) return;

            // Throttle pemindaian setiap 1.0 detik agar tidak membebani frame rate
            if (Time.unscaledTime - _lastTrexScanTime < 1.0f) return;
            _lastTrexScanTime = Time.unscaledTime;

            try {
                PlayerBehavior player = PlayerBehavior.LocalPlayer;
                Vector3 pPos = Vector3.zero;
                if (player != null) {
                    pPos = player.CurrentPosition;
                    if (pPos == Vector3.zero) pPos = player.transform.position;
                }

                // 1. Periksa Status Pulau Saat Ini (GameManager.Region)
                UpdateCurrentIslandStatus();

                // 2. Scan Titik Kawah di Pulau Ini jika berada di Pulau Lava / Volcanic
                UpdateCurrentIslandCraters(pPos);

                // 3. Scan Entitas T-Rex Hidup di Sekitar Pemain
                UpdateLiveTrexScan(pPos);

                // 4. Auto-Scan Rute Kepulauan Setiap 35 detik (atau jika list masih kosong)
                if (Time.unscaledTime - _lastEpicScanTime > 35f || _detectedEpicIslands.Count == 0) {
                    ScanArchipelagoEpicIslands();
                }
            } catch (Exception ex) {
                _trexStatusLog = "Error TrexEngine: " + ex.Message;
            }
        }

        // =========================================================================
        // 1. STATUS PULAU SAAT INI
        // =========================================================================
        private void UpdateCurrentIslandStatus() {
            try {
                Durango.Logic.Explore.Region curReg = null;
                try { curReg = GameManager.Region; } catch {}

                if (curReg != null) {
                    _currentIslandName = curReg.Name ?? "Pulau Tanpa Nama";
                    _currentIslandLevel = curReg.Level;

                    string tmpl = (curReg.TemplateId ?? "").ToLower();
                    string rName = _currentIslandName.ToLower();

                    // Cek apakah pulau saat ini merupakan pulau kawah / lava / volcanic / level 55+
                    _isCurrentIslandVolcanic = tmpl.Contains("volcano") || tmpl.Contains("crater") ||
                                               tmpl.Contains("lava") || rName.Contains("kawah") ||
                                               rName.Contains("volcano") || rName.Contains("lava") ||
                                               _currentIslandLevel >= 55;

                    _currentIslandBiome = _isCurrentIslandVolcanic ? "Volcanic / Kawah Lava" : "Umum / Non-Lava";
                } else {
                    _currentIslandName = "Sedang Memuat / Tidak Diketahui";
                    _currentIslandBiome = "-";
                    _isCurrentIslandVolcanic = false;
                    _currentIslandLevel = 0;
                }
            } catch {}
        }

        // =========================================================================
        // 2. SCAN TITIK KAWAH (CRATER) DI PULAU SAAT INI
        // =========================================================================
        private void UpdateCurrentIslandCraters(Vector3 playerPos) {
            _currentIslandCraters.Clear();
            _closestCrater = null;

            try {
                MapSystem ms = null;
                try { ms = GameSystem<MapSystem>.Instance(); } catch {}
                if (ms == null) ms = UnityEngine.Object.FindObjectOfType<MapSystem>();
                if (ms == null) return;

                // a. Dari _exploredPOIs (Messages.ExploredPOIs)
                if (_fExploredPOIs == null) {
                    _fExploredPOIs = typeof(MapSystem).GetField("_exploredPOIs", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_fExploredPOIs != null) {
                    object expObj = _fExploredPOIs.GetValue(ms);
                    if (expObj is Messages.ExploredPOIs) {
                        Messages.ExploredPOIs exp = (Messages.ExploredPOIs)expObj;
                        if (exp.POIs != null) {
                            for (int i = 0; i < exp.POIs.Length; i++) {
                                Messages.PointOfInterest poi = exp.POIs[i];
                                if (poi.Type == Shared.System.PointOfInterest.Crater) {
                                    Vector3 wPos = Durango.Terrain.Util.TilePositionToClientPosition(poi.Tile, true);
                                    AddCraterTarget(poi.Title, wPos, poi.Tile, true, playerPos);
                                }
                            }
                        }
                    }
                }

                // b. Dari POIUpdater._exploreredPOIs (Dictionary<Point2, Shared.System.PointOfInterest>)
                if (_fPoiUpdater == null) {
                    _fPoiUpdater = typeof(MapSystem).GetField("_poiUpdater", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_fPoiUpdater != null) {
                    object poiUp = _fPoiUpdater.GetValue(ms);
                    if (poiUp != null) {
                        if (_fExploredPOIsDict == null) {
                            _fExploredPOIsDict = typeof(POIUpdater).GetField("_exploreredPOIs", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        if (_fExploredPOIsDict != null) {
                            Dictionary<Point2, Shared.System.PointOfInterest> dict = _fExploredPOIsDict.GetValue(poiUp) as Dictionary<Point2, Shared.System.PointOfInterest>;
                            if (dict != null) {
                                foreach (KeyValuePair<Point2, Shared.System.PointOfInterest> kvp in dict) {
                                    if (kvp.Value == Shared.System.PointOfInterest.Crater) {
                                        Vector3 wPos = Durango.Terrain.Util.TilePositionToClientPosition(kvp.Key, true);
                                        AddCraterTarget("Kawah Vulkanik (Sarang T-Rex)", wPos, kvp.Key, true, playerPos);
                                    }
                                }
                            }
                        }
                    }
                }

                // Tentukan kawah terdekat
                float minDst = 999999f;
                for (int i = 0; i < _currentIslandCraters.Count; i++) {
                    CraterTarget ct = _currentIslandCraters[i];
                    if (ct.DistanceMeters < minDst) {
                        minDst = ct.DistanceMeters;
                        _closestCrater = ct;
                    }
                }
            } catch {}
        }

        private void AddCraterTarget(string title, Vector3 worldPos, Point2 tile, bool isExplored, Vector3 playerPos) {
            // Hindari duplikasi kawah di titik yang sama (< 15 meter)
            for (int i = 0; i < _currentIslandCraters.Count; i++) {
                if (Vector3.Distance(_currentIslandCraters[i].WorldPos, worldPos) < 1500f) {
                    return;
                }
            }

            CraterTarget ct = new CraterTarget();
            ct.Title = !string.IsNullOrEmpty(title) ? title : "Kawah Sarang T-Rex";
            ct.WorldPos = worldPos;
            ct.Tile = tile;
            ct.IsExplored = isExplored;

            if (playerPos != Vector3.zero) {
                ct.DistanceMeters = Vector3.Distance(playerPos, worldPos) / 100f;
                ct.CompassDirection = CalculateCompass(playerPos, worldPos);
            }

            _currentIslandCraters.Add(ct);
        }

        // =========================================================================
        // 3. SCAN ENTITAS T-REX HIDUP SECARA LANGSUNG (LIVE SCAN)
        // =========================================================================
        private void UpdateLiveTrexScan(Vector3 playerPos) {
            try {
                AnimalBehavior foundTrex = null;
                List<AnimalBehavior> searchList = new List<AnimalBehavior>();

                // a. Dari AnimalManager._animals
                try {
                    AnimalManager am = null;
                    try { am = AnimalManager.Instance(); } catch {}
                    if (am == null) am = UnityEngine.Object.FindObjectOfType<AnimalManager>();

                    if (am != null) {
                        FieldInfo fAnimals = typeof(AnimalManager).GetField("_animals", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fAnimals != null) {
                            Dictionary<string, AnimalBehavior> dict = fAnimals.GetValue(am) as Dictionary<string, AnimalBehavior>;
                            if (dict != null) {
                                foreach (KeyValuePair<string, AnimalBehavior> kvp in dict) {
                                    if (kvp.Value != null) searchList.Add(kvp.Value);
                                }
                            }
                        }
                    }
                } catch {}

                // b. Dari scene aktif
                try {
                    AnimalBehavior[] sceneAnimals = UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();
                    if (sceneAnimals != null) {
                        for (int i = 0; i < sceneAnimals.Length; i++) {
                            if (sceneAnimals[i] != null && !searchList.Contains(sceneAnimals[i])) {
                                searchList.Add(sceneAnimals[i]);
                            }
                        }
                    }
                } catch {}

                // Cari T-Rex di antara hewan yang termuat
                float closestDist = 999999f;
                for (int i = 0; i < searchList.Count; i++) {
                    AnimalBehavior ab = searchList[i];
                    if (ab == null || !ab.IsAlive) continue;

                    string aName = ab.GetName();
                    if (string.IsNullOrEmpty(aName)) aName = ab.gameObject.name ?? "";
                    string low = aName.ToLower();

                    // Identifikasi nama spesies Tyrannosaurus
                    bool isTrex = low.Contains("tyranno") || low.Contains("t-rex") || low.Contains("t_rex") ||
                                  (low.Contains("rex") && !low.Contains("pachy"));

                    if (isTrex) {
                        Vector3 pos = ab.CurrentPosition;
                        if (pos == Vector3.zero) pos = ab.transform.position;
                        float dist = (playerPos != Vector3.zero) ? Vector3.Distance(playerPos, pos) / 100f : 0f;

                        if (dist < closestDist) {
                            closestDist = dist;
                            foundTrex = ab;
                        }
                    }
                }

                if (foundTrex != null) {
                    Vector3 pos = foundTrex.CurrentPosition;
                    if (pos == Vector3.zero) pos = foundTrex.transform.position;

                    float hp = 1f;
                    try {
                        if (foundTrex.Life != null) hp = foundTrex.Life.Ratio();
                    } catch {}

                    if (_activeLiveTrex == null) _activeLiveTrex = new TrexLiveTarget();
                    _activeLiveTrex.Name = !string.IsNullOrEmpty(foundTrex.GetName()) ? foundTrex.GetName() : "Tyrannosaurus Rex";
                    _activeLiveTrex.Level = foundTrex.Level;
                    _activeLiveTrex.HpRatio = hp;
                    _activeLiveTrex.WorldPos = pos;
                    _activeLiveTrex.DistanceMeters = (playerPos != Vector3.zero) ? Vector3.Distance(playerPos, pos) / 100f : 0f;
                    _activeLiveTrex.CompassDirection = CalculateCompass(playerPos, pos);
                    _activeLiveTrex.IsAlive = foundTrex.IsAlive;
                    _activeLiveTrex.TargetObj = foundTrex.gameObject;

                    // Bunyikan sirene / audio alert jika diaktifkan dan belum bunyi dalam 12 detik
                    if (EnableTrexAudioAlarm && (Time.unscaledTime - _lastTrexAlarmAudioTime > 12f)) {
                        _lastTrexAlarmAudioTime = Time.unscaledTime;
                        PlayTrexAlarmAudio();
                    }
                } else {
                    _activeLiveTrex = null;
                }
            } catch {}
        }

        // =========================================================================
        // 4. SCAN GLOBAL KEPULAUAN (ARCHIPELAGO EPIC SCANNER)
        // =========================================================================
        public void ScanArchipelagoEpicIslands() {
            try {
                _lastEpicScanTime = Time.unscaledTime;
                _detectedEpicIslands.Clear();

                ExploreSystem es = null;
                try { es = GameSystem<ExploreSystem>.Instance(); } catch {}
                if (es == null) es = UnityEngine.Object.FindObjectOfType<ExploreSystem>();

                if (es == null) {
                    _trexStatusLog = "ExploreSystem belum siap. Silakan buka Peta Dunia / Kepulauan [M] sekali di game!";
                    return;
                }

                if (_fArchipelagoRoutes == null) {
                    _fArchipelagoRoutes = typeof(ExploreSystem).GetField("_archipelagoRoutes", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_fArchipelagoRoutes == null) {
                    _trexStatusLog = "Field _archipelagoRoutes tidak ditemukan pada ExploreSystem.";
                    return;
                }

                Messages.ArchipelagoRoute[] routes = _fArchipelagoRoutes.GetValue(es) as Messages.ArchipelagoRoute[];
                if (routes == null || routes.Length == 0) {
                    _trexStatusLog = "Rute kepulauan kosong. Buka Peta Dunia / Kepulauan [M] agar rute terisi dari server!";
                    return;
                }

                double serverTime = 0.0;
                try {
                    if (Durango.Network.Connections.Frontend != null) {
                        serverTime = Durango.Network.Connections.Frontend.GetPredictedServerTime();
                    }
                } catch {}
                if (serverTime <= 0.0) serverTime = (double)Time.time;

                int foundCount = 0;
                for (int i = 0; i < routes.Length; i++) {
                    Messages.ArchipelagoRoute r = routes[i];
                    if (string.IsNullOrEmpty(r.ArchipelagoId)) continue;

                    // Filter rute yang merupakan Pulau Epik / Kawah Lv.60 Volcanic
                    bool isEpicIsland = r.IsEpic || (r.Level >= 55 && r.Biome == Shared.Region.Biome.Volcanic);
                    if (!isEpicIsland) continue;

                    EpicIslandEntry entry = new EpicIslandEntry();
                    entry.ArchipelagoId = r.ArchipelagoId ?? "";
                    entry.RegionId = r.EpicRegionId ?? "";
                    entry.Level = r.Level;
                    entry.BiomeName = r.Biome.ToString();
                    entry.IsEpic = r.IsEpic;
                    entry.ScanTime = System.DateTime.Now;

                    // Ambil nama kepulauan & batas waktu expired
                    try {
                        Messages.Archipelago archi = es.GetArchipelago(r.ArchipelagoId);
                        if (!string.IsNullOrEmpty(archi.Id) || !string.IsNullOrEmpty(archi.Name)) {
                            entry.ArchipelagoName = archi.Name ?? r.ArchipelagoId;
                            if (archi.ExpiresAt > serverTime) {
                                entry.RemainingSeconds = archi.ExpiresAt - serverTime;
                            }
                        }
                    } catch {}

                    // Ambil nama pulau / region
                    try {
                        if (!string.IsNullOrEmpty(r.EpicRegionId)) {
                            Durango.Logic.Explore.Region reg = es.GetRegion(r.EpicRegionId);
                            if (reg != null) {
                                entry.RegionName = reg.Name ?? r.EpicRegionId;
                                if (reg.RemaingTime > 0.0) {
                                    entry.RemainingSeconds = reg.RemaingTime;
                                }
                            }
                        }
                    } catch {}

                    if (string.IsNullOrEmpty(entry.RegionName)) {
                        entry.RegionName = string.Format("Pulau Kawah Epik (Lv.{0})", r.Level);
                    }
                    if (string.IsNullOrEmpty(entry.ArchipelagoName)) {
                        entry.ArchipelagoName = string.Format("Kepulauan {0}", r.ArchipelagoId);
                    }

                    _detectedEpicIslands.Add(entry);
                    foundCount++;
                }

                if (foundCount > 0) {
                    _trexStatusLog = string.Format("Ditemukan <b>{0} Pulau Epik T-Rex aktif</b> di kepulauan!", foundCount);
                } else {
                    _trexStatusLog = string.Format("Dipindai {0} rute: Saat ini belum ada Pulau Epik T-Rex yang aktif.", routes.Length);
                }
            } catch (Exception ex) {
                _trexStatusLog = "Gagal memindai kepulauan: " + ex.Message;
            }
        }

        // =========================================================================
        // HUD BANNER & COMPASS OVERLAY (OnGUI)
        // =========================================================================
        public void RenderTrexHUDOverlay() {
            if (!EnableTrexDetector) return;

            try {
                // 1. BANNER ALERT: T-REX HIDUP TERDETEKSI DI DEKAT PEMAIN!
                if (_activeLiveTrex != null && EnableTrexHudBanner) {
                    float pulse = Mathf.PingPong(Time.unscaledTime * 6f, 1f);
                    Color alertCol = Color.Lerp(new Color(1f, 0.15f, 0.15f, 0.95f), new Color(1f, 0.65f, 0.10f, 0.95f), pulse);

                    float bw = 380f;
                    float bh = 48f;
                    float bx = (Screen.width - bw) * 0.5f;
                    float by = 65f;
                    Rect bRect = new Rect(bx, by, bw, bh);

                    DrawSolidRect(bRect, new Color(0.12f, 0.04f, 0.04f, 0.94f));
                    DrawRectBorder(bRect, alertCol, 3f);

                    string title = string.Format("<b><color=#{0}>⚠️ TYRANNOSAURUS REX TERDETEKSI! ⚠️</color></b>", (pulse > 0.5f) ? "ff3838" : "f39c12");
                    string sub = string.Format("<size=11><color=#ecf0f1>Jarak: <b>{0:F0}m [{1}]</b> | Darah: <b>{2:P0}</b> | Level: <b>{3}</b></color></size>",
                        _activeLiveTrex.DistanceMeters, _activeLiveTrex.CompassDirection, _activeLiveTrex.HpRatio, _activeLiveTrex.Level);

                    GUI.Label(new Rect(bx, by + 3f, bw, 22f), title, _counterHudTitleStyle ?? GUI.skin.label);
                    GUI.Label(new Rect(bx, by + 24f, bw, 20f), sub, _counterHudSubStyle ?? GUI.skin.label);

                    // HP Bar T-Rex
                    float barW = (bw - 6f) * Mathf.Clamp01(_activeLiveTrex.HpRatio);
                    DrawSolidRect(new Rect(bx + 3f, by + bh - 5f, barW, 3f), new Color(0.9f, 0.2f, 0.2f, 0.95f));
                    return;
                }

                // 2. RADAR KOMPAS KAWAH (Saat berada di Pulau Volcanic/Lava)
                if (EnableTrexCraterRadar && _isCurrentIslandVolcanic && _closestCrater != null) {
                    float rw = 320f;
                    float rh = 34f;
                    float rx = (Screen.width - rw) * 0.5f;
                    float ry = 15f;
                    Rect rRect = new Rect(rx, ry, rw, rh);

                    Color craterCol = new Color(0.95f, 0.45f, 0.12f, 0.90f);
                    DrawSolidRect(rRect, new Color(0.12f, 0.08f, 0.05f, 0.90f));
                    DrawRectBorder(rRect, craterCol, 2f);

                    string radarTxt = string.Format("<b><color=#e67e22>🌋 SARANG T-REX (KAWAH): {0:F0}m [{1}]</color></b>",
                        _closestCrater.DistanceMeters, _closestCrater.CompassDirection);

                    GUI.Label(new Rect(rx, ry + 6f, rw, 22f), radarTxt, _counterHudCenterStyle ?? GUI.skin.label);
                }
            } catch {}
        }

        // =========================================================================
        // TAB GUI MOD (ActiveTab == 3)
        // =========================================================================
        public void DrawTrexTrackerTab() {
            _trexTabScrollPos = GUILayout.BeginScrollView(_trexTabScrollPos, GUILayout.Height(335));

            // ----- CARD 1: STATUS PULAU SAAT INI -----
            GUILayout.BeginVertical(_boxStyle ?? GUI.skin.box);
            GUILayout.Label("<b>1. Status Pulau Anda Saat Ini:</b>", _subHeaderStyle ?? GUI.skin.label);

            string islandBadgeColor = _isCurrentIslandVolcanic ? "#e67e22" : "#2ecc71";
            string habitatText = _isCurrentIslandVolcanic ? "<color=#e74c3c><b>[HABITAT T-REX - PULAU KAWAH LAVA]</b></color>" : "<color=#95a5a6>[Pulau Aman / Bukan Kawah]</color>";

            GUILayout.Label(string.Format("  Nama Pulau: <b><color={0}>{1}</color></b> (Lv.{2})", islandBadgeColor, _currentIslandName, _currentIslandLevel), _labelStyle ?? GUI.skin.label);
            GUILayout.Label(string.Format("  Tipe Biome: {0} | {1}", _currentIslandBiome, habitatText), _labelStyle ?? GUI.skin.label);

            if (_isCurrentIslandVolcanic) {
                GUILayout.Label(string.Format("  Jumlah Kawah Ditemukan: <b>{0} Kawah</b>", _currentIslandCraters.Count), _labelStyle ?? GUI.skin.label);
                if (_closestCrater != null) {
                    GUILayout.Label(string.Format("  Kawah Terdekat: <b><color=#f39c12>{0:F0} meter</color></b> Arah <b>{1}</b>",
                        _closestCrater.DistanceMeters, _closestCrater.CompassDirection), _labelStyle ?? GUI.skin.label);

                    GUILayout.BeginHorizontal();
                    string navBtnText = _isTrexWaypointActive ? "⏹️ Hentikan Arah ke Kawah" : "🎯 Arahkan Navigasi ke Kawah Ini";
                    if (GUILayout.Button(navBtnText, _smallBtnStyle ?? GUI.skin.button, GUILayout.Height(22), GUILayout.Width(220))) {
                        _isTrexWaypointActive = !_isTrexWaypointActive;
                        if (_isTrexWaypointActive) {
                            _trexWaypointPos = _closestCrater.WorldPos;
                            _islandSweepHeading = (_closestCrater.WorldPos - PlayerBehavior.LocalPlayer.CurrentPosition).normalized;
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            } else {
                GUILayout.Label("  <size=11><color=#bdc3c7>Anda sedang tidak di pulau lava. Gunakan daftar di bawah untuk mengecek pulau T-Rex aktif di dunia!</color></size>", _hintStyle ?? GUI.skin.label);
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // ----- CARD 2: RADAR T-REX DI PULAU SAAT INI (LIVE) -----
            GUILayout.BeginVertical(_boxStyle ?? GUI.skin.box);
            GUILayout.Label("<b>2. Radar T-Rex di Pulau Ini:</b>", _subHeaderStyle ?? GUI.skin.label);

            if (_activeLiveTrex != null) {
                GUILayout.Label(string.Format("  Status: <b><color=#e74c3c>⚠️ {0} TERDETEKSI HIDUP!</color></b>", _activeLiveTrex.Name), _labelStyle ?? GUI.skin.label);
                GUILayout.Label(string.Format("  Jarak: <b>{0:F0}m</b> | Arah Kompas: <b>{1}</b> | Level: <b>{2}</b>",
                    _activeLiveTrex.DistanceMeters, _activeLiveTrex.CompassDirection, _activeLiveTrex.Level), _labelStyle ?? GUI.skin.label);
                GUILayout.Label(string.Format("  Sisa Darah (HP): <b><color=#2ecc71>{0:P0}</color></b>", _activeLiveTrex.HpRatio), _labelStyle ?? GUI.skin.label);
            } else {
                string noTrexTxt = _isCurrentIslandVolcanic 
                    ? "  Status: <color=#2ecc71>Belum terlihat T-Rex dalam jangkauan radar kawah.</color>" 
                    : "  Status: <color=#95a5a6>Tidak ada T-Rex di pulau ini (Bukan habitat kawah).</color>";
                GUILayout.Label(noTrexTxt, _labelStyle ?? GUI.skin.label);
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // ----- CARD 3: PULAU EPIK T-REX AKTIF DI KEPULAUAN (GLOBAL SCAN) -----
            GUILayout.BeginVertical(_boxStyle ?? GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>3. Pulau Epik T-Rex Aktif di Dunia:</b>", _subHeaderStyle ?? GUI.skin.label, GUILayout.Width(250));
            if (GUILayout.Button("🔄 Pindai Sekarang", _smallBtnStyle ?? GUI.skin.button, GUILayout.Height(20), GUILayout.Width(130))) {
                ScanArchipelagoEpicIslands();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format("  Status Info: <size=11><color=#f1c40f>{0}</color></size>", _trexStatusLog), _hintStyle ?? GUI.skin.label);

            if (_detectedEpicIslands.Count > 0) {
                for (int i = 0; i < _detectedEpicIslands.Count; i++) {
                    EpicIslandEntry epi = _detectedEpicIslands[i];
                    GUILayout.BeginVertical(GUI.skin.box);
                    string countStr = FormatTimeCountdown(epi.RemainingSeconds);
                    GUILayout.Label(string.Format("  🌋 <b><color=#e67e22>{0}</color></b> [{1}]", epi.RegionName, epi.ArchipelagoName), _labelStyle ?? GUI.skin.label);
                    GUILayout.Label(string.Format("  Level: <b>Lv.{0}</b> | Biome: <b>{1}</b> | Sisa Umur: <b><color=#2ecc71>{2}</color></b>",
                        epi.Level, epi.BiomeName, countStr), _hintStyle ?? GUI.skin.label);
                    GUILayout.EndVertical();
                }
            } else {
                GUILayout.Label("  <color=#bdc3c7><i>Belum ada pulau epik yang terdeteksi aktif. Buka peta kepulauan di game [M] sekali lalu tekan 'Pindai Sekarang'!</i></color>", _hintStyle ?? GUI.skin.label);
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // ----- CARD 4: PENGATURAN RADAR & ALERT -----
            GUILayout.BeginVertical(_boxStyle ?? GUI.skin.box);
            GUILayout.Label("<b>4. Opsi & Alarm Pelacak:</b>", _subHeaderStyle ?? GUI.skin.label);

            GUILayout.BeginHorizontal();
            string radTxt = EnableTrexCraterRadar ? "🌋 Radar Kawah [ON]" : "🌋 Radar Kawah [OFF]";
            if (GUILayout.Button(radTxt, EnableTrexCraterRadar ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20), GUILayout.Width(130))) {
                EnableTrexCraterRadar = !EnableTrexCraterRadar;
            }

            string hudTxt = EnableTrexHudBanner ? "🖥️ Banner Alert [ON]" : "🖥️ Banner Alert [OFF]";
            if (GUILayout.Button(hudTxt, EnableTrexHudBanner ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20), GUILayout.Width(130))) {
                EnableTrexHudBanner = !EnableTrexHudBanner;
            }

            string sndTxt = EnableTrexAudioAlarm ? "🔊 Alarm Sirene [ON]" : "🔇 Alarm Sirene [OFF]";
            if (GUILayout.Button(sndTxt, EnableTrexAudioAlarm ? (_selectedBtnStyle ?? GUI.skin.button) : (_smallBtnStyle ?? GUI.skin.button), GUILayout.Height(20), GUILayout.Width(130))) {
                EnableTrexAudioAlarm = !EnableTrexAudioAlarm;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }

        // =========================================================================
        // HELPER MATEMATIKA KOMPAS & WAKTU
        // =========================================================================
        private static string CalculateCompass(Vector3 fromPos, Vector3 toPos) {
            Vector3 dir = toPos - fromPos;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f) return "Utara [N] ⬆";
            if (angle >= 22.5f && angle < 67.5f) return "Timur Laut [NE] ↗";
            if (angle >= 67.5f && angle < 112.5f) return "Timur [E] ➡";
            if (angle >= 112.5f && angle < 157.5f) return "Tenggara [SE] ↘";
            if (angle >= 157.5f && angle < 202.5f) return "Selatan [S] ⬇";
            if (angle >= 202.5f && angle < 247.5f) return "Barat Daya [SW] ↙";
            if (angle >= 247.5f && angle < 292.5f) return "Barat [W] ⬅";
            return "Barat Laut [NW] ↖";
        }

        private static string FormatTimeCountdown(double totalSeconds) {
            if (totalSeconds <= 0.0) return "Tenggelam / Kadaluarsa";
            int hours = (int)(totalSeconds / 3600.0);
            int minutes = (int)((totalSeconds % 3600.0) / 60.0);
            int secs = (int)(totalSeconds % 60.0);

            if (hours > 0) return string.Format("{0} jam {1} menit", hours, minutes);
            if (minutes > 0) return string.Format("{0} menit {1} detik", minutes, secs);
            return string.Format("{0} detik", secs);
        }

        private static void PlayTrexAlarmAudio() {
            try {
                UISound.PlayClick(UISound.ClickType.ButtonBattle);
            } catch {
                try {
                    SoundManager.PlayEvent("ui_click");
                } catch {}
            }
        }
    }
}
