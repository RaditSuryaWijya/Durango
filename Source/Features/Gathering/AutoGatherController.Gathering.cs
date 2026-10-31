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
        // ================= GATHERING ENGINE =================

        private bool MatchesFilter(GameObject go, string name, Durango.Terrain.BiomeSpriteInfo spriteInfo) {

            // JIKA TARGET PEMBELAJARAN AKTIF: Kunci ketat ke jenis node yang dipelajari (Pohon Akasia, dll)!

            if (_hasLearnedTarget) {

                return DoesNodeMatchLearnedTarget(go, name, spriteInfo);

            }

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

                if (_recentlyGathered.ContainsKey(go)) {
                    float expTime;
                    if (_recentlyGathered.TryGetValue(go, out expTime) && Time.time < expTime) {
                        continue;
                    }
                }

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

                // Tolak jika target berada tepat di dalam lahar atau bibir lahar (< 1.5 meter)!
                if (IsTargetInOrNearLava(pos, 1.5f)) continue;

                // Hitung jarak real-time
                float d = GetObjectDistance(player, go, pos);

                // Beri bobot penjelajahan maju (Island Sweep Bias) agar tidak bolak-balik ke belakang
                float candidateScore = d;

                // Jika target terpotong lahar:
                // Jangan langsung tolak jika jauh (karena navigasi anti-looping bisa memutari lahar),
                // namun jika sangat dekat (< 6 meter) dan terhalang langsung oleh lahar, tolak!
                Vector3 curPlayerPos = player.CurrentPosition;
                if (curPlayerPos == Vector3.zero) curPlayerPos = player.transform.position;
                if (IsPathBlockedByLava(curPlayerPos, pos)) {
                    if (d < 600f) continue;
                    candidateScore += 2500f; // Prioritaskan target di sisi daratan saat ini terlebih dahulu
                }

                // Beri penalti jika node berada di area yang baru saja dilewati (anti-looping)
                candidateScore += GetNavTrailPenalty(pos, 1500f);
                if (EnableIslandSweep && _islandSweepHeading != Vector3.zero) {
                    Vector3 toTarget = pos - player.CurrentPosition;
                    toTarget.y = 0;
                    if (toTarget.sqrMagnitude > 1.0f) {
                        float forwardDot = Vector3.Dot(_islandSweepHeading, toTarget.normalized);
                        if (forwardDot < -0.35f) {
                            candidateScore += 1800f; // Penalti jarak virtual jika berada 180 derajat di belakang arah jelajah
                        }
                    }
                }

                // Rekam objek terdekat mutlak

                if (d < absoluteMinDist) {

                    absoluteMinDist = d;

                    absoluteClosest = go;

                    absoluteClosestName = n;

                    absoluteClosestPos = pos;

                    absoluteClosestInteractDist = iDist;

                }

                // Rekam objek terdekat dalam radius (dengan preferensi arah maju jelajah pulau)

                if (d <= maxRadiusUnits && candidateScore < minRadiusDist) {

                    minRadiusDist = candidateScore;

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

            // Jika tidak ada dalam radius scan ketat, tapi ada node yang valid di pulau (hingga 150m): berjalan menuju node tersebut!
            if (absoluteClosest != null && absoluteMinDist <= Mathf.Max(maxRadiusUnits * 2.5f, 15000f)) {
                _targetName = absoluteClosestName;
                _currentTargetPos = absoluteClosestPos;
                _currentTargetInteractDist = absoluteClosestInteractDist;
                _statusText = string.Format("Mendekati {0} ({1:F1}m)...", absoluteClosestName, absoluteMinDist / 100f);
                return absoluteClosest;
            }

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

                private bool DoesWidgetMatchTargetItem(Durango.UI.InteractionMenuWidget_PC w, LearnedTargetItem targetItem) {
            if (w == null || targetItem == null) return false;
            if (w.Disabled || w.Empty || !w.Valid) return false;
            if (w.Data.Disabled || w.Data.AccessDenied) return false;

            // Pastikan kuantitas item yang tersisa di node masih > 0
            int cnt = (w.Data.GatheringData != null) ? w.Data.GatheringData.Amount : w.Data.Count;
            if (cnt <= 0 && w.Data.GatheringData != null) return false;

            string targetActLow = (targetItem.ActionName ?? "").ToLower().Trim();
            string targetGenLow = (targetItem.GeneratorId ?? "").ToLower().Trim();
            if (string.IsNullOrEmpty(targetActLow) && string.IsNullOrEmpty(targetGenLow)) return false;

            string actText = (GetSlotActionText(w) ?? "").ToLower().Trim();
            string fullText = (GetWidgetFullText(w) ?? "").ToLower().Trim();

            // Proteksi daun jika target bukan daun
            bool isSlotDaun = actText.Contains("daun") || actText.Contains("leaf") || actText.Contains("leaves") || actText.Contains("pelepah");
            bool targetWantsDaun = targetActLow.Contains("daun") || targetActLow.Contains("leaf") || targetActLow.Contains("pelepah");
            if (isSlotDaun && !targetWantsDaun) return false;

            // Proteksi tebang/hancurkan jika target bukan tebang/quarry
            bool isSlotAnchor = IsAnchorSlot(w);
            bool targetWantsAnchor = targetActLow.Contains("tebang") || targetActLow.Contains("chop") || targetActLow.Contains("pecah") || targetActLow.Contains("quarry");
            if (isSlotAnchor && !targetWantsAnchor) return false;

            // 1. GeneratorId matching
            if (!string.IsNullOrEmpty(targetGenLow) && w.Data.GatheringData != null && !string.IsNullOrEmpty(w.Data.GatheringData.GeneratorId)) {
                string wGenId = w.Data.GatheringData.GeneratorId.ToLower().Trim();
                if (wGenId == targetGenLow || wGenId.Contains(targetGenLow) || targetGenLow.Contains(wGenId)) {
                    return true;
                }
            }

            // 2. Exact or substring match on action text or full text
            if (!string.IsNullOrEmpty(targetActLow)) {
                if (actText.Contains(targetActLow) || fullText.Contains(targetActLow) || targetActLow.Contains(actText)) {
                    return true;
                }

                // 3. Keyword matching (misal: "buluh", "ranting", "batang", "batu", "buah", "kaki", "rusuk")
                string[] words = targetActLow.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                for (int wIdx = 0; wIdx < words.Length; wIdx++) {
                    string wd = words[wIdx];
                    if (wd.Length > 2 && wd != "ambil" && wd != "petik" && wd != "cabut" && wd != "kupas") {
                        if (actText.Contains(wd) || fullText.Contains(wd)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private IEnumerator GatherRoutine(GameObject target, LocalMoveNavigator nav, PlayerController controller) {

            _isGathering = true;

            _statusText = "Mendekati & Buka Menu: " + _targetName;

            if (_isFatigueRestActive || _isAutoEating) {
                _isGathering = false;
                yield break;
            }

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

                if (_isFatigueRestActive || _isAutoEating) break;

                PlayerBehavior pCheck = PlayerBehavior.LocalPlayer;
                if (pCheck != null) {
                    if (EnableAutoRestFatigue && pCheck.Fatigue != null && pCheck.Fatigue.Get() >= FatigueThreshold) {
                        ForceCloseInteractionMenu();
                        _isGathering = false;
                        _currentTargetObj = null;
                        StartCoroutine(FatigueRecoveryRoutine());
                        yield break;
                    }
                    if (EnableAutoEatOnHunger && pCheck.Stamina != null && pCheck.Stamina.Get() < AutoEatHungerThreshold) {
                        ForceCloseInteractionMenu();
                        _isGathering = false;
                        _currentTargetObj = null;
                        StartCoroutine(AutoEatRoutine());
                        yield break;
                    }
                }

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

                    float waitTimeout = Time.time + 1.2f;

                    while (Time.time < waitTimeout) {

                        wheel = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidget_PC>();

                        if (Durango.UI.InteractionMenuListWidgetBase.IsShow) break;

                        yield return new WaitForSeconds(0.05f);

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

                float scanTimeout = Time.time + 0.85f;

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

                                // 3. Lewati slot yang sudah habis (0 amount)!
                                int slotAmount = (w.Data.GatheringData != null) ? w.Data.GatheringData.Amount : w.Data.Count;
                                if (slotAmount <= 0 && w.Data.GatheringData != null) continue;

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

                if (_hasLearnedTarget) {

                    // ================= SMART LEARNED MULTI-TARGET SEQUENTIAL MODE =================
                    List<LearnedTargetItem> targetsToTry = _learnedTargetItems;
                    if ((targetsToTry == null || targetsToTry.Count == 0) && !string.IsNullOrEmpty(_learnedActionName)) {
                        targetsToTry = new List<LearnedTargetItem>();
                        targetsToTry.Add(new LearnedTargetItem(_learnedActionName, _learnedGeneratorId, ""));
                    }

                    if (targetsToTry != null && targetsToTry.Count > 0) {
                        for (int tIdx = 0; tIdx < targetsToTry.Count; tIdx++) {
                            LearnedTargetItem curTarget = targetsToTry[tIdx];
                            if (curTarget == null) continue;

                            foreach (KeyValuePair<int, Durango.UI.InteractionMenuWidget_PC> kvp in activeSlots) {
                                if (DoesWidgetMatchTargetItem(kvp.Value, curTarget)) {
                                    targetSlot = kvp.Key;
                                    chosenWidget = kvp.Value;
                                    break;
                                }
                            }

                            if (chosenWidget != null && targetSlot != -1) {
                                break; // Menemukan slot untuk target ini!
                            }
                        }
                    }

                    if (chosenWidget == null || targetSlot == -1) {
                        _statusText = string.Format("Semua target resource di node {0} sudah habis!", _learnedNodeDisplayName);
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

                // Eksekusi aksi slot in-engine secara langsung (TANPA simulasi F1-F6 fisik yang memicu hotkey mod)
                if (wheel != null && _clickHotKeyMethod != null) {
                    try {
                        _clickHotKeyMethod.Invoke(wheel, new object[] { targetSlot, Durango.Logic.InputSystem.Trigger.Up });
                    } catch {
                        try { chosenWidget.SetClick(); } catch {}
                    }
                } else {
                    try { chosenWidget.SetClick(); } catch {}
                }

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

                // ================= TUNGGU DURASI AKSI TUNTAS (RESPONSIF & CEPAT) =================
                float waitEndTime = Time.time + actionDuration;
                while (Time.time < waitEndTime) {
                    if (!IsEnabled) break;
                    float remain = Mathf.Max(0f, waitEndTime - Time.time);
                    _statusText = string.Format("Memanen #{0}: {1}... ({2:F1}s)", gatherCountOnThisNode, actionName, remain);
                    yield return new WaitForSeconds(0.12f);
                }

                // Grace period sangat singkat (maksimal 0.4 detik) jika animasi masih menyelesaikan transisi
                float graceTimeout = Time.time + 0.4f;
                while (Time.time < graceTimeout) {
                    if (!IsEnabled) break;
                    if (!IsGatheringActive()) break;
                    _statusText = string.Format("Menyelesaikan loot #{0}: {1}...", gatherCountOnThisNode, actionName);
                    yield return new WaitForSeconds(0.08f);
                }

                _statusText = string.Format("Selesai panen #{0}: {1}!", gatherCountOnThisNode, actionName);

                // Jeda kilat 0.18 detik agar inventory & roda aksi tersinkronisasi untuk pengambilan berikutnya
                yield return new WaitForSeconds(0.18f);

            }

            // ================= SELESAI PADA NODE INI =================

            // Tutup menu dan blacklist node agar karakter berpindah ke node berikutnya

            if (target != null) {

                _recentlyGathered[target] = Time.time + NodeBlacklistDuration; // Blacklist lama (default 8 menit) agar bot menjelajahi seluruh pulau

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
            if (target == null) return;

            try {
                PlayerController controller = GetPlayerController();
                if (controller != null) controller.StopMove();
            } catch {}

            // Buka interaksi langsung via InteractionSystem in-engine (bersih, tanpa spam 'E' yang menutup menu)
            try {
                InteractionSystem isys = GameSystem<InteractionSystem>.Instance();
                if (isys != null) {
                    isys.SetInteractionTarget(new InteractionObject(target));
                }
            } catch {}
        }

        private IEnumerator SimulatePressKeyRoutine(byte vk, byte scan) {

            try { keybd_event(vk, scan, 0, UIntPtr.Zero); } catch {}

            yield return new WaitForSeconds(0.08f);

            try { keybd_event(vk, scan, KEYEVENTF_KEYUP, UIntPtr.Zero); } catch {}

        }


        private static string CleanNodeDisplayName(string raw) {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw;
            // Hilangkan BBCode warna/tag seperti [f1c40f], [-], [b], [/b], [lv.45], dll
            while (true) {
                int start = s.IndexOf('[');
                if (start < 0) break;
                int end = s.IndexOf(']', start);
                if (end < 0) break;
                s = s.Remove(start, end - start + 1);
            }
            // Hilangkan newline dan penanda clone Unity
            s = s.Replace("\r", " ").Replace("\n", " ").Replace("(Clone)", "").Replace("(clone)", "").Trim();
            while (s.Contains("  ")) {
                s = s.Replace("  ", " ");
            }
            return s.Trim();
        }

        private void TryHookInteractionMenu() {

            try {

                if (Durango.UI.InteractionMenuListWidgetBase.IsShow) {

                    Durango.UI.InteractionMenuListWidget_PC menuList = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidget_PC>();

                    if (menuList != null) {

                        GameObject targetGo = GetMenuTargetObject(menuList);

                        if (targetGo != null) _lastInteractedObject = targetGo;

                        if (_hookedMenuList != menuList) {
                            if (_hookedMenuList != null) {
                                try { _hookedMenuList.OnClickInteractionMenu -= OnPlayerMenuActionClicked; } catch {}
                            }
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
                    if (targetObj != null) {
                        InteractionObject io = targetObj as InteractionObject;
                        if (io != null && io.Target != null) return io.Target;
                        if (targetObj is Component) return ((Component)targetObj).gameObject;
                        if (targetObj is GameObject) return (GameObject)targetObj;
                    }
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

        private static string GetMenuTargetName(Durango.UI.InteractionMenuListWidgetBase menuList) {
            if (menuList != null && _targetNameLabelField != null) {
                try {
                    UILabel lbl = _targetNameLabelField.GetValue(menuList) as UILabel;
                    if (lbl != null && !string.IsNullOrEmpty(lbl.text)) return CleanNodeDisplayName(lbl.text);
                } catch {}
            }
            return "";
        }

        private static bool IsGenericEngineName(string name) {
            if (string.IsNullOrEmpty(name)) return true;
            string low = name.Trim().ToLower();
            if (low.Length <= 2) return true;
            return low == "naturalobject" ||
                   low == "naturalspriteobject" || 
                   low == "naturalprefabobject" || 
                   low == "naturalcomponent" ||
                   low == "immovablebase" || 
                   low == "immovable" ||
                   low == "gameobject" || 
                   low == "doodad" || 
                   low == "staticenv" || 
                   low == "prop" || 
                   low == "props" ||
                   low == "node alam" || 
                   low == "interactive" ||
                   low == "interactionobject" ||
                   low == "pohon" ||
                   low == "tree" ||
                   low == "wood" ||
                   low == "kayu" ||
                   low == "batu" ||
                   low == "rock" ||
                   low == "stone" ||
                   low == "semak" ||
                   low == "bush" ||
                   low == "tanaman" ||
                   low == "plant" ||
                   low == "bunga" ||
                   low == "flower" ||
                   low == "-" ||
                   low.StartsWith("naturalobject") ||
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

                combined.Contains("gelondong") || combined.Contains("timber") || combined.Contains("abu") || combined.Contains("ash")) {

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
            string menuTargetName = "";
            int menuEntityType = -1;

            if (_hookedMenuList != null) {
                menuTargetName = GetMenuTargetName(_hookedMenuList);

                if (_menuTargetField != null) {
                    try {
                        InteractionObject io = _menuTargetField.GetValue(_hookedMenuList) as InteractionObject;
                        if (io != null) {
                            menuEntityType = io.EntityType;
                            if (io.Target != null && targetGo == null) targetGo = io.Target;
                        }
                    } catch {}
                }
            }

            // Fallback nama dan entity type dari InteractionSystem jika menu target belum terisi
            try {
                InteractionSystem isys = GameSystem<InteractionSystem>.Instance();
                if (isys != null) {
                    if (string.IsNullOrEmpty(menuTargetName)) {
                        if (!string.IsNullOrEmpty(isys.LastTouched.EntityName)) {
                            menuTargetName = CleanNodeDisplayName(isys.LastTouched.EntityName);
                        } else if (isys.MenuList != null && !string.IsNullOrEmpty(isys.MenuList.Name)) {
                            menuTargetName = CleanNodeDisplayName(isys.MenuList.Name);
                        }
                    }
                    if (menuEntityType <= 0) {
                        if (isys.Target != null && isys.Target.EntityType > 0) menuEntityType = isys.Target.EntityType;
                        else if (isys.LastInteractionTarget != null && isys.LastInteractionTarget.EntityType > 0) menuEntityType = isys.LastInteractionTarget.EntityType;
                    }
                    if (targetGo == null) {
                        if (isys.Target != null && isys.Target.Target != null) targetGo = isys.Target.Target;
                        else if (isys.LastInteractionTarget != null && isys.LastInteractionTarget.Target != null) targetGo = isys.LastInteractionTarget.Target;
                    }
                }
            } catch {}

            if (targetGo == null) targetGo = _lastInteractedObject;

            if (!string.IsNullOrEmpty(actName) || targetGo != null || !string.IsNullOrEmpty(menuTargetName)) {
                LearnTargetFromAction(actName, genId, targetGo, data.GatheringData, menuTargetName, menuEntityType);
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
                            GameObject targetGo = null;
                            int isysEntityType = -1;
                            string targetNodeName = "";

                            try {
                                InteractionSystem isys = GameSystem<InteractionSystem>.Instance();
                                if (isys != null) {
                                    if (isys.Target != null) {
                                        if (isys.Target.Target != null) targetGo = isys.Target.Target;
                                        isysEntityType = isys.Target.EntityType;
                                    }
                                    if (targetGo == null && isys.LastInteractionTarget != null) {
                                        if (isys.LastInteractionTarget.Target != null) targetGo = isys.LastInteractionTarget.Target;
                                        if (isysEntityType <= 0) isysEntityType = isys.LastInteractionTarget.EntityType;
                                    }
                                    if (!string.IsNullOrEmpty(isys.LastTouched.EntityName)) {
                                        targetNodeName = CleanNodeDisplayName(isys.LastTouched.EntityName);
                                    } else if (isys.MenuList != null && !string.IsNullOrEmpty(isys.MenuList.Name)) {
                                        targetNodeName = CleanNodeDisplayName(isys.MenuList.Name);
                                    }
                                }
                            } catch {}

                            if (targetGo == null && _hookedMenuList != null) {
                                targetGo = GetMenuTargetObject(_hookedMenuList);
                            }
                            if (string.IsNullOrEmpty(targetNodeName) && _hookedMenuList != null) {
                                targetNodeName = GetMenuTargetName(_hookedMenuList);
                            }

                            // Fallback jika tidak terdeteksi dari sumber aktif
                            if (targetGo == null) targetGo = _lastInteractedObject;

                            // Cek apakah aksi dilakukan pada node yang SAMA
                            bool isSameNode = false;
                            if (_hasLearnedTarget) {
                                if (targetGo != null) {
                                    isSameNode = DoesNodeMatchLearnedTarget(targetGo);
                                } else if (_learnedNodeEntityType > 0 && isysEntityType > 0) {
                                    isSameNode = (isysEntityType == _learnedNodeEntityType);
                                } else if (!string.IsNullOrEmpty(targetNodeName) && !string.IsNullOrEmpty(_learnedNodeDisplayName)) {
                                    isSameNode = string.Equals(targetNodeName, _learnedNodeDisplayName, StringComparison.OrdinalIgnoreCase);
                                }
                            }

                            bool alreadyHasItem = false;
                            if (isSameNode && _learnedTargetItems != null) {
                                for (int i = 0; i < _learnedTargetItems.Count; i++) {
                                    LearnedTargetItem item = _learnedTargetItems[i];
                                    if (item == null) continue;
                                    if (!string.IsNullOrEmpty(genId) && string.Equals(item.GeneratorId, genId, StringComparison.OrdinalIgnoreCase)) {
                                        alreadyHasItem = true;
                                        break;
                                    }
                                    if (!string.IsNullOrEmpty(gName) && string.Equals(item.ActionName, gName, StringComparison.OrdinalIgnoreCase)) {
                                        alreadyHasItem = true;
                                        break;
                                    }
                                }
                            }

                            if (!_hasLearnedTarget || !isSameNode || !alreadyHasItem) {
                                LearnTargetFromAction(gName, genId, targetGo, cur, targetNodeName, isysEntityType);
                            }
                        }
                    }
                }
            } catch {}
        }

        public void LearnTargetFromAction(string actName, string genId, GameObject targetGo) {
            LearnTargetFromAction(actName, genId, targetGo, null, "", -1);
        }

        public void LearnTargetFromAction(string actName, string genId, GameObject targetGo, InteractionData.GatheringData gatherData) {
            LearnTargetFromAction(actName, genId, targetGo, gatherData, "", -1);
        }

        public void LearnTargetFromAction(string actName, string genId, GameObject targetGo, InteractionData.GatheringData gatherData, string hintDisplayName, int hintEntityType) {
            if (string.IsNullOrEmpty(actName) && targetGo == null && string.IsNullOrEmpty(hintDisplayName)) return;

            string cleanName = "";
            string displayName = "";
            string poolName = "";
            int entityType = -1;
            Durango.Terrain.BiomeSpriteInfo spr = null;

            if (targetGo != null) {
                _lastInteractedObject = targetGo;
                cleanName = targetGo.name.Replace("(Clone)", "").Replace("(clone)", "").Trim();
                try {
                    ImmovableBase imm = targetGo.GetComponent<ImmovableBase>();
                    if (imm == null) imm = targetGo.GetComponentInParent<ImmovableBase>();
                    if (imm == null) imm = targetGo.GetComponentInChildren<ImmovableBase>();
                    if (imm != null) {
                        entityType = (int)imm.EntityType;
                        try {
                            spr = Durango.Terrain.DataHelper.GetBiomeSpriteInfo((int)imm.EntityType);
                        } catch {}
                        string immName = imm.GetName();
                        if (!string.IsNullOrEmpty(immName)) {
                            string cleanedImm = CleanNodeDisplayName(immName);
                            if (!IsGenericEngineName(cleanedImm)) displayName = cleanedImm;
                        }
                        NaturalPrefabObject npo = imm as NaturalPrefabObject;
                        if (npo != null && !string.IsNullOrEmpty(npo.PoolName)) {
                            poolName = npo.PoolName;
                            cleanName = npo.PoolName;
                            if (string.IsNullOrEmpty(displayName) || IsGenericEngineName(displayName)) displayName = npo.PoolName;
                        }
                    }
                } catch {}

                if (spr != null && !string.IsNullOrEmpty(spr.Name) && (string.IsNullOrEmpty(displayName) || IsGenericEngineName(displayName))) {
                    displayName = CleanNodeDisplayName(spr.Name);
                }

                if (string.IsNullOrEmpty(displayName) || IsGenericEngineName(displayName)) {
                    displayName = CleanObjectName(cleanName);
                }
            }

            if (entityType <= 0 && hintEntityType > 0) {
                entityType = hintEntityType;
            }

            // Prioritaskan hintDisplayName jika valid (nama resmi dari UI server)
            if (!string.IsNullOrEmpty(hintDisplayName)) {
                string cleanedHint = CleanNodeDisplayName(hintDisplayName);
                if (!string.IsNullOrEmpty(cleanedHint) && !IsGenericEngineName(cleanedHint)) {
                    displayName = cleanedHint;
                }
            }

            // Cek apakah aksi dilakukan pada node yang SAMA atau node yang BERBEDA
            bool isSameNode = false;
            if (_hasLearnedTarget) {
                if (targetGo != null) {
                    isSameNode = DoesNodeMatchLearnedTarget(targetGo);
                } else if (_learnedNodeEntityType > 0 && entityType > 0) {
                    isSameNode = (entityType == _learnedNodeEntityType);
                } else if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(_learnedNodeDisplayName)) {
                    isSameNode = string.Equals(displayName, _learnedNodeDisplayName, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Jika node BERBEDA atau baru pertama kali belajar: reset dan kunci node baru!
            if (!isSameNode) {
                if (_learnedTargetItems == null) _learnedTargetItems = new List<LearnedTargetItem>();
                else _learnedTargetItems.Clear();

                _learnedNodeEntityType = entityType;
                _learnedNodePoolName = poolName;
                _learnedNodeBaseName = (!IsGenericEngineName(cleanName)) ? cleanName : (!string.IsNullOrEmpty(genId) && !IsGenericEngineName(genId) ? genId : "");
                _learnedFilter = ClassifyResourceFilter(actName, genId, targetGo, gatherData, spr);
                CurrentFilter = _learnedFilter;

                if (string.IsNullOrEmpty(displayName) || IsGenericEngineName(displayName)) {
                    if (!string.IsNullOrEmpty(cleanName) && !IsGenericEngineName(cleanName)) displayName = cleanName;
                    else if (!string.IsNullOrEmpty(actName)) displayName = actName;
                    else displayName = string.Format("Node {0}", _learnedFilter);
                }
                _learnedNodeDisplayName = displayName;
                _lastInteractedObject = targetGo;
            }

            // Tambahkan item aksi ke _learnedTargetItems (secara dinamis, bisa lebih dari 1 item)
            string itemActName = (actName ?? "").Trim();
            string itemGenId = (genId ?? "").Trim();

            if (!string.IsNullOrEmpty(itemActName) || !string.IsNullOrEmpty(itemGenId)) {
                if (_learnedTargetItems == null) _learnedTargetItems = new List<LearnedTargetItem>();

                bool alreadyExists = false;
                for (int i = 0; i < _learnedTargetItems.Count; i++) {
                    LearnedTargetItem item = _learnedTargetItems[i];
                    if (item == null) continue;
                    if (!string.IsNullOrEmpty(itemGenId) && string.Equals(item.GeneratorId, itemGenId, StringComparison.OrdinalIgnoreCase)) {
                        alreadyExists = true;
                        break;
                    }
                    if (!string.IsNullOrEmpty(itemActName) && string.Equals(item.ActionName, itemActName, StringComparison.OrdinalIgnoreCase)) {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists) {
                    _learnedTargetItems.Add(new LearnedTargetItem(itemActName, itemGenId, ""));
                    Debug.Log(string.Format("[AutoGather] Ditambahkan Target Item #{0}: '{1}' (GenId: {2}) pada Node '{3}'", 
                        _learnedTargetItems.Count, itemActName, itemGenId, _learnedNodeDisplayName));
                }
            }

            // Sinkronkan _learnedActionName dan _learnedGeneratorId untuk kemudahan display/log
            List<string> actNames = new List<string>();
            for (int k = 0; k < _learnedTargetItems.Count; k++) {
                if (_learnedTargetItems[k] != null && !string.IsNullOrEmpty(_learnedTargetItems[k].ActionName)) {
                    actNames.Add(_learnedTargetItems[k].ActionName);
                }
            }
            _learnedActionName = string.Join(", ", actNames.ToArray());
            if (_learnedTargetItems.Count > 0 && _learnedTargetItems[0] != null) {
                _learnedGeneratorId = _learnedTargetItems[0].GeneratorId;
            }

            _hasLearnedTarget = true;
            _statusText = string.Format("Target Terkunci: {0} ({1} target item)", _learnedNodeDisplayName, _learnedTargetItems.Count);
            Debug.Log(string.Format("[AutoGather] Smart Learn Locked: Node='{0}', Target Items={1}", _learnedNodeDisplayName, _learnedTargetItems.Count));

            RequestSaveSettings();
        }

        public void ResetLearnedTarget() {
            if (_learnedTargetItems != null) _learnedTargetItems.Clear();
            _learnedActionName = "";
            _learnedGeneratorId = "";
            _learnedNodeBaseName = "";
            _learnedNodeDisplayName = "";
            _learnedNodePoolName = "";
            _learnedNodeEntityType = -1;
            _learnedFilter = ResourceFilterMode.All;
            CurrentFilter = ResourceFilterMode.All;
            _hasLearnedTarget = false;
            _lastInteractedObject = null;
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

                public bool DoesNodeMatchLearnedTarget(GameObject go) {
            if (go == null) return false;
            return DoesNodeMatchLearnedTarget(go, go.name, null);
        }

        private static bool IsGenericCategoryWord(string word) {
            if (string.IsNullOrEmpty(word)) return true;
            string w = word.Trim().ToLower();
            if (w.Length <= 2) return true;
            return w == "pohon" || w == "tree" || w == "trees" ||
                   w == "batu" || w == "rock" || w == "stone" || w == "mineral" ||
                   w == "kayu" || w == "wood" || w == "log" || w == "branch" || w == "ranting" || w == "batang" ||
                   w == "semak" || w == "bush" || w == "shrub" ||
                   w == "rumput" || w == "grass" || w == "plant" || w == "tanaman" ||
                   w == "daun" || w == "leaf" || w == "bunga" || w == "flower" ||
                   w == "alam" || w == "natural" || w == "node" || w == "obj" ||
                   w == "besar" || w == "kecil" || w == "sedang" || w == "small" || w == "large" || w == "medium" ||
                   w == "tumpukan" || w == "pile" || w == "doodad" || w == "prop";
        }

        private bool DoesNodeMatchLearnedTarget(GameObject go, string rawName, Durango.Terrain.BiomeSpriteInfo spriteInfo) {
            if (go == null) return false;

            int candEntityType = -1;
            string candPoolName = "";
            string candImmName = "";
            try {
                ImmovableBase imm = go.GetComponent<ImmovableBase>();
                if (imm == null) imm = go.GetComponentInParent<ImmovableBase>();
                if (imm == null) imm = go.GetComponentInChildren<ImmovableBase>();
                if (imm != null) {
                    candEntityType = (int)imm.EntityType;
                    try { candImmName = imm.GetName(); } catch {}
                    NaturalPrefabObject npo = imm as NaturalPrefabObject;
                    if (npo != null && !string.IsNullOrEmpty(npo.PoolName)) {
                        candPoolName = npo.PoolName;
                    }
                }
            } catch {}

            // =========================================================================
            // 1. HARD STRICT PRIMARY MATCH: EntityType
            // Jika target yang dipelajari punya EntityType valid DAN candidate punya EntityType valid:
            // KEDUANYA HARUS SAMA PERSIS! Tidak boleh jatuh ke substring matching jika EntityType beda!
            // (Contoh: Pohon Akasia != Pohon Palem != Pohon Apel)
            // =========================================================================
            if (_learnedNodeEntityType > 0 && candEntityType > 0) {
                return (candEntityType == _learnedNodeEntityType);
            }

            // =========================================================================
            // 2. STRICT 3D PREFAB POOL NAME MATCH (Misal "Tree_Acacia_01" vs "Tree_Palm_01")
            // =========================================================================
            if (!string.IsNullOrEmpty(_learnedNodePoolName) && !string.IsNullOrEmpty(candPoolName)) {
                if (string.Equals(candPoolName, _learnedNodePoolName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
                return false;
            }

            // =========================================================================
            // 3. DISTINCTIVE KEYWORD & DISPLAY NAME MATCH
            // Wajib mengandung kata kunci spesifik spesies/material (misal: "akasia", "palem", "tulang", "besi", "obsidian")
            // DILARANG mencocokkan kata generik seperti "pohon", "tree", "batu", "rock", "kayu", "semak"
            // =========================================================================
            if (!string.IsNullOrEmpty(_learnedNodeDisplayName) && !IsGenericEngineName(_learnedNodeDisplayName)) {
                string dispLow = _learnedNodeDisplayName.ToLower().Trim();
                string cleanRaw = (rawName ?? "").ToLower().Replace("(clone)", "").Trim();
                string immNameLow = (candImmName ?? "").ToLower().Replace("(clone)", "").Trim();
                string sprNameLow = (spriteInfo != null && !string.IsNullOrEmpty(spriteInfo.Name)) ? spriteInfo.Name.ToLower().Replace("(clone)", "").Trim() : "";

                // Exact match display name
                if (cleanRaw == dispLow || immNameLow == dispLow || sprNameLow == dispLow) {
                    return true;
                }

                // Ekstrak kata kunci unik/spesifik dari target yang dipelajari
                string[] dispWords = dispLow.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> uniqueKeywords = new List<string>();
                for (int dw = 0; dw < dispWords.Length; dw++) {
                    string w = dispWords[dw];
                    if (w.Length >= 3 && !IsGenericCategoryWord(w)) {
                        uniqueKeywords.Add(w);
                        // Tambahkan pasangan bilingual kata kunci umum
                        if (w == "akasia") uniqueKeywords.Add("acacia");
                        else if (w == "acacia") uniqueKeywords.Add("akasia");
                        else if (w == "abu") uniqueKeywords.Add("ash");
                        else if (w == "ash") uniqueKeywords.Add("abu");
                        else if (w == "cemara") uniqueKeywords.Add("pine");
                        else if (w == "pine") uniqueKeywords.Add("cemara");
                        else if (w == "apel") uniqueKeywords.Add("apple");
                        else if (w == "apple") uniqueKeywords.Add("apel");
                        else if (w == "palem") uniqueKeywords.Add("palm");
                        else if (w == "palm") uniqueKeywords.Add("palem");
                        else if (w == "kelapa") uniqueKeywords.Add("coconut");
                        else if (w == "bambu") uniqueKeywords.Add("bamboo");
                        else if (w == "besi") uniqueKeywords.Add("iron");
                        else if (w == "tembaga") uniqueKeywords.Add("copper");
                        else if (w == "tulang") uniqueKeywords.Add("bone");
                        else if (w == "arang") uniqueKeywords.Add("coal");
                        else if (w == "kristal") uniqueKeywords.Add("crystal");
                        else if (w == "timah") uniqueKeywords.Add("tin");
                        else if (w == "belerang") uniqueKeywords.Add("sulfur");
                    }
                }

                // Jika target punya kata kunci spesifik (misal "akasia", "besi", "tulang"):
                // Calon kandidat HARUS mengandung kata kunci tersebut!
                if (uniqueKeywords.Count > 0) {
                    for (int k = 0; k < uniqueKeywords.Count; k++) {
                        string kw = uniqueKeywords[k];
                        if (cleanRaw.Contains(kw) || immNameLow.Contains(kw) || sprNameLow.Contains(kw) || candPoolName.ToLower().Contains(kw)) {
                            return true;
                        }
                    }
                    return false;
                }
            }

            // =========================================================================
            // 4. PREFAB / BASE NAME MATCH (Hanya jika mengandung kata kunci spesifik non-generik)
            // =========================================================================
            if (!string.IsNullOrEmpty(_learnedNodeBaseName) && !IsGenericEngineName(_learnedNodeBaseName) && !IsGenericCategoryWord(_learnedNodeBaseName)) {
                string baseLow = _learnedNodeBaseName.ToLower().Trim();
                string goName = (go.name ?? "").ToLower().Replace("(clone)", "").Trim();
                if (goName == baseLow) return true;
                if (goName.Contains(baseLow) && baseLow.Length >= 6 && !IsGenericCategoryWord(baseLow)) {
                    return true;
                }
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
                _gatherCooldownUntil = 0f;
                ForceCloseInteractionMenu();
                StopAllCoroutines();
                try {
                    PlayerController controller = GetPlayerController();
                    LocalMoveNavigator nav = GetNavigator(controller);
                    if (controller != null) controller.StopMove();
                    if (nav != null) nav.Stop();
                } catch {}
            } else {
                IsCraftPlanActive = false;
                IsCombatBotEnabled = false; // Matikan Combat Bot agar tidak bentrok!
                _isGathering = false;
                _nextScanTime = 0f;
                _gatherCooldownUntil = 0f;
                _targetAvailableResources = "-";
                _currentTargetObj = null;
                if (_recentlyGathered != null) _recentlyGathered.Clear(); // Bersihkan blacklist agar langsung mencari node terdekat!
                if (_navVisitedTrail != null) _navVisitedTrail.Clear(); // Reset memori jejak
                StopAllCoroutines();

                if (_hasLearnedTarget) {
                    _statusText = string.Format("Auto-Panen Aktif: Berburu '{0}' ({1})!", _learnedActionName, _learnedNodeDisplayName);
                } else {
                    _statusText = "<color=#f1c40f><b>Menunggu Anda Memanen 1x...</b></color> (Dekati & panen objek di game)";
                }
            }
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


    }
}
