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


    }
}
