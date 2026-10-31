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
                sb.AppendLine("LearnedNodePoolName=" + (_learnedNodePoolName ?? ""));
                sb.AppendLine("LearnedNodeEntityType=" + _learnedNodeEntityType.ToString());
                sb.AppendLine("LearnedFilter=" + _learnedFilter.ToString());

                List<string> itemStrs = new List<string>();
                if (_learnedTargetItems != null) {
                    for (int ti = 0; ti < _learnedTargetItems.Count; ti++) {
                        LearnedTargetItem it = _learnedTargetItems[ti];
                        if (it == null) continue;
                        itemStrs.Add(string.Format("{0}:::{1}:::{2}", it.ActionName ?? "", it.GeneratorId ?? "", it.IconName ?? ""));
                    }
                }
                sb.AppendLine("LearnedTargetItems=" + string.Join("|||", itemStrs.ToArray()));

                sb.AppendLine();

                sb.AppendLine("[CraftPlanner]");

                sb.AppendLine("IsCraftPlanActive=" + IsCraftPlanActive.ToString().ToLower());

                sb.AppendLine("PlanRecipeId=" + (PlanRecipeId ?? ""));

                sb.AppendLine("PlanQuantity=" + PlanQuantity.ToString());

                sb.AppendLine("AutoCraftWhenReady=" + AutoCraftWhenReady.ToString().ToLower());

                sb.AppendLine("AutoEatWhenCrafting=" + AutoEatWhenCrafting.ToString().ToLower());

                sb.AppendLine();

                sb.AppendLine("[Survival]");
                sb.AppendLine("EnableAutoEatOnHunger=" + EnableAutoEatOnHunger.ToString().ToLower());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "AutoEatHungerThreshold={0:F0}", AutoEatHungerThreshold));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "AutoEatEnergyTarget={0:F0}", AutoEatEnergyTarget));
                sb.AppendLine("LearnedFoodName=" + (LearnedFoodName ?? ""));
                sb.AppendLine("LearnedFoodPrototypeId=" + (LearnedFoodPrototypeId ?? ""));
                sb.AppendLine("EnableAutoRestFatigue=" + EnableAutoRestFatigue.ToString().ToLower());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "FatigueThreshold={0:F0}", FatigueThreshold));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "FatigueRecoverTarget={0:F0}", FatigueRecoverTarget));
                sb.AppendLine("AyunanCustomKeyword=" + (AyunanCustomKeyword ?? ""));
                sb.AppendLine();

            sb.AppendLine("[Navigation]");
            sb.AppendLine("EnableIslandSweep=" + EnableIslandSweep.ToString().ToLower());
            sb.AppendLine("EnableObstacleDetour=" + EnableObstacleDetour.ToString().ToLower());
            sb.AppendLine("EnableLavaAvoidance=" + EnableLavaAvoidance.ToString().ToLower());
            sb.AppendLine("EnableDinoFlee=" + EnableDinoFlee.ToString().ToLower());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "DinoFleeLeashDistance={0:F0}", DinoFleeLeashDistance));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "NodeBlacklistDuration={0:F0}", NodeBlacklistDuration));
            sb.AppendLine();

            sb.AppendLine("[Combat]");

                sb.AppendLine("EnableHoldToAttack=" + EnableHoldToAttack.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "HoldAttackInterval={0:F2}", HoldAttackInterval));

                sb.AppendLine("EnableRecoveryCut=" + EnableRecoveryCut.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "RecoveryCutDelay={0:F2}", RecoveryCutDelay));

                sb.AppendLine("EnableAntiStagger=" + EnableAntiStagger.ToString().ToLower());

                sb.AppendLine("EnableMobileAttack=" + EnableMobileAttack.ToString().ToLower());

                sb.AppendLine("EnableFastSniperStance=" + EnableFastSniperStance.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "FastSniperSpeedMultiplier={0:F1}", FastSniperSpeedMultiplier));

                sb.AppendLine("EnableFastCrossbowReload=" + EnableFastCrossbowReload.ToString().ToLower());

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "CrossbowReloadSpeed={0:F1}", CrossbowReloadSpeed));

                sb.AppendLine("EnableDinoNoclip=" + EnableDinoNoclip.ToString().ToLower());

                sb.AppendLine("AutoExitCombatOnKill=" + AutoExitCombatOnKill.ToString().ToLower());
                sb.AppendLine("EnableCounterAssist=" + EnableCounterAssist.ToString().ToLower());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "CounterAssistWindowMax={0:F2}", CounterAssistWindowMax));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "CounterAssistWindowMin={0:F2}", CounterAssistWindowMin));
                sb.AppendLine("EnableCounterAssistSound=" + EnableCounterAssistSound.ToString().ToLower());
                sb.AppendLine("EnableCounterAssistCenterHUD=" + EnableCounterAssistCenterHUD.ToString().ToLower());
                sb.AppendLine("CounterAssistDodgeFallback=" + CounterAssistDodgeFallback.ToString().ToLower());
                sb.AppendLine("CounterAssistTargetOnly=" + CounterAssistTargetOnly.ToString().ToLower());
                sb.AppendLine("CounterAssistUnderCharacter=" + CounterAssistUnderCharacter.ToString().ToLower());

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
                                if (IsGenericEngineName(_learnedNodeBaseName)) _learnedNodeBaseName = "";

                                break;

                            case "LearnedNodeDisplayName":

                                _learnedNodeDisplayName = val;

                                break;

                            case "LearnedFilter":

                                try { _learnedFilter = (ResourceFilterMode)Enum.Parse(typeof(ResourceFilterMode), val, true); } catch {}

                                break;

                            case "LearnedNodePoolName":
                                _learnedNodePoolName = val;
                                break;

                            case "LearnedNodeEntityType":
                                int et;
                                if (int.TryParse(val, out et)) _learnedNodeEntityType = et;
                                break;

                            case "LearnedTargetItems":
                                if (_learnedTargetItems == null) _learnedTargetItems = new List<LearnedTargetItem>();
                                else _learnedTargetItems.Clear();
                                if (!string.IsNullOrEmpty(val)) {
                                    string[] rawItems = val.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                                    for (int lti = 0; lti < rawItems.Length; lti++) {
                                        string[] parts = rawItems[lti].Split(new string[] { ":::" }, StringSplitOptions.None);
                                        if (parts.Length >= 2) {
                                            string act = parts[0];
                                            string gen = parts[1];
                                            string icon = (parts.Length > 2) ? parts[2] : "";
                                            _learnedTargetItems.Add(new LearnedTargetItem(act, gen, icon));
                                        }
                                    }
                                }
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

                            case "EnableAutoEatOnHunger":
                                bool eaeh;
                                if (bool.TryParse(val, out eaeh)) EnableAutoEatOnHunger = eaeh;
                                break;

                            case "AutoEatHungerThreshold":
                                float aeht;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out aeht)) AutoEatHungerThreshold = aeht;
                                break;

                            case "AutoEatEnergyTarget":
                                float aeet;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out aeet)) AutoEatEnergyTarget = aeet;
                                break;

                            case "LearnedFoodName":
                                LearnedFoodName = val ?? "";
                                break;

                            case "LearnedFoodPrototypeId":
                                LearnedFoodPrototypeId = val ?? "";
                                break;

                            case "EnableAutoRestFatigue":
                                bool earf;
                                if (bool.TryParse(val, out earf)) EnableAutoRestFatigue = earf;
                                break;

                            case "FatigueThreshold":
                                float fth;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out fth)) FatigueThreshold = fth;
                                break;

                            case "FatigueRecoverTarget":
                                float frt;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out frt)) FatigueRecoverTarget = frt;
                                break;

                            case "AyunanCustomKeyword":
                                AyunanCustomKeyword = val ?? "";
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


                            case "EnableMobileAttack":

                                bool ema;

                                if (bool.TryParse(val, out ema)) EnableMobileAttack = ema;

                                break;

                            case "EnableFastSniperStance":

                                bool efss;

                                if (bool.TryParse(val, out efss)) EnableFastSniperStance = efss;

                                break;

                            case "FastSniperSpeedMultiplier":

                                float fssm;

                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out fssm)) FastSniperSpeedMultiplier = fssm;

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

                            case "AutoExitCombatOnKill":

                                bool aeck;

                                if (bool.TryParse(val, out aeck)) AutoExitCombatOnKill = aeck;

                                break;

                            case "EnableCounterAssist":
                                bool eca;
                                if (bool.TryParse(val, out eca)) EnableCounterAssist = eca;
                                break;

                            case "CounterAssistWindowMax":
                                float cawm;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out cawm)) CounterAssistWindowMax = cawm;
                                break;

                            case "CounterAssistWindowMin":
                                float cawmin;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out cawmin)) CounterAssistWindowMin = cawmin;
                                break;

                            case "EnableCounterAssistSound":
                                bool ecas;
                                if (bool.TryParse(val, out ecas)) EnableCounterAssistSound = ecas;
                                break;

                            case "EnableCounterAssistCenterHUD":
                                bool ecach;
                                if (bool.TryParse(val, out ecach)) EnableCounterAssistCenterHUD = ecach;
                                break;

                            case "CounterAssistDodgeFallback":
                                bool cadf;
                                if (bool.TryParse(val, out cadf)) CounterAssistDodgeFallback = cadf;
                                break;

                            case "CounterAssistTargetOnly":
                                bool cato;
                                if (bool.TryParse(val, out cato)) CounterAssistTargetOnly = cato;
                                break;

                            case "CounterAssistUnderCharacter":
                                bool cauc;
                                if (bool.TryParse(val, out cauc)) CounterAssistUnderCharacter = cauc;
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

                            case "EnableIslandSweep":
                                bool eis;
                                if (bool.TryParse(val, out eis)) EnableIslandSweep = eis;
                                break;

                            case "EnableObstacleDetour":
                                bool eod;
                                if (bool.TryParse(val, out eod)) EnableObstacleDetour = eod;
                                break;

                            case "EnableLavaAvoidance":
                                bool ela;
                                if (bool.TryParse(val, out ela)) EnableLavaAvoidance = ela;
                                break;

                            case "EnableDinoFlee":
                                bool edf;
                                if (bool.TryParse(val, out edf)) EnableDinoFlee = edf;
                                break;

                            case "DinoFleeLeashDistance":
                                float dfld;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out dfld)) DinoFleeLeashDistance = dfld;
                                break;

                            case "NodeBlacklistDuration":
                                float nbld;
                                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out nbld)) NodeBlacklistDuration = nbld;
                                break;


                        }

                    } catch {}

                }

                // Terapkan efek setting yang baru dimuat

                try {

                    ApplyRunAnimationLock(LockDefaultRunAnim);

                    UpdateMoveSpeedMultiplier();

                    SyncArchipelagoUnlocker();

                    if (_hasLearnedTarget && (_learnedTargetItems == null || _learnedTargetItems.Count == 0) && !string.IsNullOrEmpty(_learnedActionName)) {
                        if (_learnedTargetItems == null) _learnedTargetItems = new List<LearnedTargetItem>();
                        _learnedTargetItems.Add(new LearnedTargetItem(_learnedActionName, _learnedGeneratorId, ""));
                    }

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


    }
}
