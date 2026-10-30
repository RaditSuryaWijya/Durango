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


        // =========================================================================
        // AUTO LOOT LOOP: LAHAR (LAVA) & AIR (WATER)
        // Memungkinkan pengambilan lahar dan air secara berulang (looping)
        // otomatis mengisi seluruh wadah/ember kosong di tas tanpa harus klik berkali-kali.
        // =========================================================================
        public bool IsLavaLoopActive = false;
        public bool IsWaterLoopActive = false;

        private int _lavaScoopSuccessCount = 0;
        private int _waterScoopSuccessCount = 0;
        private float _nextDrawAllowedTime = 0f;
        private int _consecutiveDrawFails = 0;
        private float _drawStartTime = 0f;
        private Vector3 _loopStartPos = Vector3.zero;
        private static FieldInfo _isysDrawTimerField = null;

        public bool IsDrawActive() {
            try {
                var isys = GameSystem<InteractionSystem>.Instance();
                if (isys != null) {
                    if (_isysDrawTimerField == null) {
                        _isysDrawTimerField = typeof(InteractionSystem).GetField("_drawTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    if (_isysDrawTimerField != null) {
                        var dt = _isysDrawTimerField.GetValue(isys) as Durango.Logic.Timer.PredictTimer;
                        if (dt != null && dt.Timer != null && !dt.Timer.IsStop) {
                            return true;
                        }
                    }
                }
            } catch {}

            try {
                var ts = GameSystem<TimerSystem>.Instance();
                if (ts != null && ts.HasTimerExceptPostProcess()) return true;
            } catch {}

            return false;
        }

        private static bool IsContainerNotFull(ItemData item) {
            if (item == null) return false;
            if (item.ContentCount == 0) return true;

            for (int i = 0; i < item.ContentCount; i++) {
                ItemData content = item.GetContent(i);
                if (content != null && content.Durability != null) {
                    float cur = content.Durability.Get();
                    float max = content.Durability.Max();
                    if (max > 0f && cur < max) {
                        return true;
                    }
                }
            }
            return false;
        }

        public ItemData FindEmptyLavaContainer() {
            try {
                var invSys = GameSystem<InventorySystem>.Instance();
                if (invSys == null || invSys.PlayerInventory == null || invSys.PlayerInventory.Items == null) return null;

                return invSys.PlayerInventory.Items.Find(i => i != null && !i.IsDestroyed() && i.HasTag("lava_container") && (i.ContentCount == 0 || IsContainerNotFull(i)));
            } catch {
                return null;
            }
        }

        public int CountEmptyLavaContainers() {
            try {
                var invSys = GameSystem<InventorySystem>.Instance();
                if (invSys == null || invSys.PlayerInventory == null || invSys.PlayerInventory.Items == null) return 0;
                int count = 0;
                foreach (var i in invSys.PlayerInventory.Items) {
                    if (i != null && !i.IsDestroyed() && i.HasTag("lava_container") && (i.ContentCount == 0 || IsContainerNotFull(i))) {
                        count++;
                    }
                }
                return count;
            } catch {
                return 0;
            }
        }

        public ItemData FindEmptyWaterContainer() {
            try {
                var invSys = GameSystem<InventorySystem>.Instance();
                if (invSys == null || invSys.PlayerInventory == null || invSys.PlayerInventory.Items == null) return null;

                List<string> validTags = new List<string>();
                try {
                    if (Yaml.Util.Singleton<Yaml.Constants>.Instance != null && 
                        Yaml.Util.Singleton<Yaml.Constants>.Instance.PutInContainerInfos != null &&
                        Yaml.Util.Singleton<Yaml.Constants>.Instance.PutInContainerInfos.ContainsKey("water")) {
                        var info = Yaml.Util.Singleton<Yaml.Constants>.Instance.PutInContainerInfos["water"];
                        if (info != null && info.Tags != null) {
                            foreach (var t in info.Tags) {
                                if (!string.IsNullOrEmpty(t) && !validTags.Contains(t)) validTags.Add(t);
                            }
                        }
                    }
                } catch {}

                string[] defaultTags = new string[] { "water_container", "liquid_container", "bottle", "waterskin", "bucket", "pot", "cup", "coconut_cup" };
                foreach (var dt in defaultTags) {
                    if (!validTags.Contains(dt)) validTags.Add(dt);
                }

                foreach (var item in invSys.PlayerInventory.Items) {
                    if (item == null || item.IsDestroyed()) continue;
                    foreach (var tag in validTags) {
                        if (item.HasTag(tag)) {
                            if (item.ContentCount == 0 || IsContainerNotFull(item)) {
                                return item;
                            }
                            break;
                        }
                    }
                }
            } catch {}
            return null;
        }

        public int CountEmptyWaterContainers() {
            try {
                var invSys = GameSystem<InventorySystem>.Instance();
                if (invSys == null || invSys.PlayerInventory == null || invSys.PlayerInventory.Items == null) return 0;

                List<string> validTags = new List<string>();
                try {
                    if (Yaml.Util.Singleton<Yaml.Constants>.Instance != null && 
                        Yaml.Util.Singleton<Yaml.Constants>.Instance.PutInContainerInfos != null &&
                        Yaml.Util.Singleton<Yaml.Constants>.Instance.PutInContainerInfos.ContainsKey("water")) {
                        var info = Yaml.Util.Singleton<Yaml.Constants>.Instance.PutInContainerInfos["water"];
                        if (info != null && info.Tags != null) {
                            foreach (var t in info.Tags) {
                                if (!string.IsNullOrEmpty(t) && !validTags.Contains(t)) validTags.Add(t);
                            }
                        }
                    }
                } catch {}

                string[] defaultTags = new string[] { "water_container", "liquid_container", "bottle", "waterskin", "bucket", "pot", "cup", "coconut_cup" };
                foreach (var dt in defaultTags) {
                    if (!validTags.Contains(dt)) validTags.Add(dt);
                }

                int count = 0;
                foreach (var item in invSys.PlayerInventory.Items) {
                    if (item == null || item.IsDestroyed()) continue;
                    foreach (var tag in validTags) {
                        if (item.HasTag(tag)) {
                            if (item.ContentCount == 0 || IsContainerNotFull(item)) {
                                count++;
                            }
                            break;
                        }
                    }
                }
                return count;
            } catch {
                return 0;
            }
        }

        public void StartLavaLoop() {
            var bucket = FindEmptyLavaContainer();
            if (bucket == null) {
                _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Tidak ada Ember Logam kosong di tas!";
                return;
            }
            IsWaterLoopActive = false;
            IsLavaLoopActive = true;
            _lavaScoopSuccessCount = 0;
            _consecutiveDrawFails = 0;
            _nextDrawAllowedTime = 0f;
            PlayerBehavior p = PlayerBehavior.LocalPlayer;
            if (p != null) _loopStartPos = p.CurrentPosition != Vector3.zero ? p.CurrentPosition : p.transform.position;
            _statusText = string.Format("<color=#f39c12><b>[LAVA LOOP]</b></color> Dimulai! Tersedia {0} wadah kosong.", CountEmptyLavaContainers());
        }

        public void StopLavaLoop() {
            StopLavaLoop(null);
        }

        public void StopLavaLoop(string reason) {
            IsLavaLoopActive = false;
            if (!string.IsNullOrEmpty(reason)) {
                _statusText = reason;
            } else {
                _statusText = string.Format("<color=#f39c12><b>[LAVA LOOP DIHENTIKAN]</b></color> Total terisi: {0} ember.", _lavaScoopSuccessCount);
            }
        }

        public void StartWaterLoop() {
            var container = FindEmptyWaterContainer();
            if (container == null) {
                _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Tidak ada wadah air (botol/kantung/panci/ember) kosong di tas!";
                return;
            }
            IsLavaLoopActive = false;
            IsWaterLoopActive = true;
            _waterScoopSuccessCount = 0;
            _consecutiveDrawFails = 0;
            _nextDrawAllowedTime = 0f;
            PlayerBehavior p = PlayerBehavior.LocalPlayer;
            if (p != null) _loopStartPos = p.CurrentPosition != Vector3.zero ? p.CurrentPosition : p.transform.position;
            _statusText = string.Format("<color=#3498db><b>[AIR LOOP]</b></color> Dimulai! Tersedia {0} wadah kosong.", CountEmptyWaterContainers());
        }

        public void StopWaterLoop() {
            StopWaterLoop(null);
        }

        public void StopWaterLoop(string reason) {
            IsWaterLoopActive = false;
            if (!string.IsNullOrEmpty(reason)) {
                _statusText = reason;
            } else {
                _statusText = string.Format("<color=#3498db><b>[AIR LOOP DIHENTIKAN]</b></color> Total terisi: {0} wadah.", _waterScoopSuccessCount);
            }
        }

        private void UpdateAutoLootLava() {
            if (!IsLavaLoopActive) return;

            PlayerBehavior player = PlayerBehavior.LocalPlayer;
            if (player == null || !player.IsAlive) {
                StopLavaLoop("<color=#e74c3c><b>[LAVA LOOP]</b></color> Karakter mati, loop dihentikan.");
                return;
            }

            if (CombatSystem.HasInstance() && CombatSystem.Instance().CombatMode) {
                StopLavaLoop("<color=#e74c3c><b>[LAVA LOOP]</b></color> Diserang musuh! Loop dihentikan demi keselamatan.");
                return;
            }

            Vector3 curPos = player.CurrentPosition != Vector3.zero ? player.CurrentPosition : player.transform.position;
            if (_loopStartPos != Vector3.zero && Vector3.Distance(curPos, _loopStartPos) > 4.5f) {
                StopLavaLoop("<color=#f39c12><b>[LAVA LOOP]</b></color> Karakter berpindah tempat, loop dihentikan otomatis.");
                return;
            }

            if (IsDrawActive() || IsGatheringActive()) {
                _drawStartTime = Time.time;
                return;
            }

            if (Time.time < _nextDrawAllowedTime) return;

            var bucket = FindEmptyLavaContainer();
            if (bucket == null) {
                StopLavaLoop(string.Format("<color=#2ecc71><b>[LAVA LOOP SELESAI]</b></color> Semua ember logam ({0} buah) sudah terisi penuh lahar!", _lavaScoopSuccessCount));
                return;
            }

            var isys = GameSystem<InteractionSystem>.Instance();
            if (isys == null) {
                StopLavaLoop("<color=#e74c3c><b>[GAGAL]</b></color> InteractionSystem tidak aktif.");
                return;
            }

            try {
                Messages.DrawLava msg = new Messages.DrawLava();
                msg.ToolItemId = bucket.Id;
                isys.Draw<Messages.DrawLava>(msg);

                _lavaScoopSuccessCount++;
                _nextDrawAllowedTime = Time.time + 1.2f;
                _drawStartTime = Time.time;
                _statusText = string.Format("<color=#f39c12><b>[LAVA LOOP #{0}]</b></color> Menyendok lahar ke {1} (Sisa kosong: {2})...", 
                    _lavaScoopSuccessCount, bucket.Name, CountEmptyLavaContainers());
            } catch (Exception ex) {
                _consecutiveDrawFails++;
                if (_consecutiveDrawFails >= 3) {
                    StopLavaLoop("<color=#e74c3c><b>[GAGAL]</b></color> Error saat menyendok lahar: " + ex.Message);
                } else {
                    _nextDrawAllowedTime = Time.time + 1.0f;
                }
            }
        }

        private void UpdateAutoLootWater() {
            if (!IsWaterLoopActive) return;

            PlayerBehavior player = PlayerBehavior.LocalPlayer;
            if (player == null || !player.IsAlive) {
                StopWaterLoop("<color=#e74c3c><b>[AIR LOOP]</b></color> Karakter mati, loop dihentikan.");
                return;
            }

            if (CombatSystem.HasInstance() && CombatSystem.Instance().CombatMode) {
                StopWaterLoop("<color=#e74c3c><b>[AIR LOOP]</b></color> Diserang musuh! Loop dihentikan demi keselamatan.");
                return;
            }

            Vector3 curPos = player.CurrentPosition != Vector3.zero ? player.CurrentPosition : player.transform.position;
            if (_loopStartPos != Vector3.zero && Vector3.Distance(curPos, _loopStartPos) > 4.5f) {
                StopWaterLoop("<color=#3498db><b>[AIR LOOP]</b></color> Karakter berpindah tempat, loop dihentikan otomatis.");
                return;
            }

            if (IsDrawActive() || IsGatheringActive()) {
                _drawStartTime = Time.time;
                return;
            }

            if (Time.time < _nextDrawAllowedTime) return;

            var container = FindEmptyWaterContainer();
            if (container == null) {
                StopWaterLoop(string.Format("<color=#2ecc71><b>[AIR LOOP SELESAI]</b></color> Semua wadah air ({0} buah) sudah terisi penuh!", _waterScoopSuccessCount));
                return;
            }

            var isys = GameSystem<InteractionSystem>.Instance();
            if (isys == null) {
                StopWaterLoop("<color=#e74c3c><b>[GAGAL]</b></color> InteractionSystem tidak aktif.");
                return;
            }

            try {
                Messages.DrawWater msg = new Messages.DrawWater();
                msg.ToolItemId = container.Id;
                isys.Draw<Messages.DrawWater>(msg);

                _waterScoopSuccessCount++;
                _nextDrawAllowedTime = Time.time + 1.2f;
                _drawStartTime = Time.time;
                _statusText = string.Format("<color=#3498db><b>[AIR LOOP #{0}]</b></color> Mengisi air ke {1} (Sisa kosong: {2})...", 
                    _waterScoopSuccessCount, container.Name, CountEmptyWaterContainers());
            } catch (Exception ex) {
                _consecutiveDrawFails++;
                if (_consecutiveDrawFails >= 3) {
                    StopWaterLoop("<color=#e74c3c><b>[GAGAL]</b></color> Error saat mengambil air: " + ex.Message);
                } else {
                    _nextDrawAllowedTime = Time.time + 1.0f;
                }
            }
        }

        public void DrawLavaFromShore() {
            try {
                var invSys = GameSystem<InventorySystem>.Instance();
                if (invSys == null || invSys.PlayerInventory == null) {
                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Inventory belum siap.";
                    return;
                }

                var bucket = FindEmptyLavaContainer();
                if (bucket == null) {
                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Tidak ada Ember Logam (Metal Bucket) kosong di tas!";
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

        public void DrawWaterFromShore() {
            try {
                var invSys = GameSystem<InventorySystem>.Instance();
                if (invSys == null || invSys.PlayerInventory == null) {
                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> Inventory belum siap.";
                    return;
                }

                var container = FindEmptyWaterContainer();
                var isys = GameSystem<InteractionSystem>.Instance();
                if (isys == null) {
                    _statusText = "<color=#e74c3c><b>[GAGAL]</b></color> InteractionSystem belum aktif.";
                    return;
                }

                Messages.DrawWater msg = new Messages.DrawWater();
                if (container != null) {
                    msg.ToolItemId = container.Id;
                    _statusText = string.Format("<color=#3498db><b>[AIR]</b></color> Mengisi air ke {0}...", container.Name);
                } else {
                    _statusText = "<color=#3498db><b>[AIR]</b></color> Mengambil air dengan tangan...";
                }
                isys.Draw<Messages.DrawWater>(msg);
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


    }
}
