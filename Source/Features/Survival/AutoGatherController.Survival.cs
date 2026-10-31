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



        // =========================================================================
        // FITUR AUTO-MAKAN (HUNGER & STAMINA MANAGEMENT)
        // Terpicu ketika kelaparan/stamina < 20. Karakter berhenti bergerak,
        // lalu memakan HANYA item yang diinisiasi di awal secara manual oleh user,
        // berurutan hingga stamina > 200.
        // =========================================================================

        private void TryHookUseItemEvent() {
            if (_isUseItemHooked) return;
            try {
                InventorySystem invSys = null;
                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}
                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys != null) {
                    invSys.UseItemStarted += OnPlayerUseItemStarted;
                    _isUseItemHooked = true;
                    Debug.Log("[AutoGather] Hook UseItemStarted berhasil dipasang!");
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] TryHookUseItemEvent error: " + ex);
            }
        }

        private void OnPlayerUseItemStarted(ItemData item, Messages.StartTimer timer) {
            try {
                if (item == null) return;
                // Jangan pelajari item jika mod sendiri yang sedang melakukan auto-makan
                if (_isAutoEating) return;

                // Validasi apakah item yang digunakan merupakan makanan/minuman
                if (IsEdibleFood(item, 0f) || HasEatableAttribute(item) || GetItemFoodEnergy(item) > 0f) {
                    LearnedFoodName = item.Name;
                    LearnedFoodPrototypeId = (!string.IsNullOrEmpty(item.PrototypeId)) ? item.PrototypeId : "";
                    _settingsDirty = true;
                    _statusText = string.Format("<color=#2ecc71><b>[MAKANAN DITETAPKAN]</b></color> Inisiasi makanan: <b>{0}</b>", LearnedFoodName);
                    Debug.Log(string.Format("[AutoGather] Makanan berhasil diinisiasi: Name='{0}', ProtoId='{1}'", LearnedFoodName, LearnedFoodPrototypeId));
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] OnPlayerUseItemStarted error: " + ex);
            }
        }

        public ItemData FindLearnedFoodInInventory() {
            try {
                InventorySystem invSys = null;
                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}
                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys == null || invSys.PlayerInventory == null || invSys.PlayerInventory.Items == null) return null;

                string targetProto = (LearnedFoodPrototypeId ?? "").ToLower().Trim();
                string targetName = (LearnedFoodName ?? "").ToLower().Trim();

                if (string.IsNullOrEmpty(targetProto) && string.IsNullOrEmpty(targetName)) return null;

                List<ItemData> items = invSys.PlayerInventory.Items;
                for (int i = 0; i < items.Count; i++) {
                    ItemData it = items[i];
                    if (it == null || it.IsDestroyed()) continue;

                    // 1. Prioritaskan kecocokan PrototypeId
                    if (!string.IsNullOrEmpty(targetProto) && !string.IsNullOrEmpty(it.PrototypeId)) {
                        if (it.PrototypeId.ToLower().Trim() == targetProto) {
                            return it;
                        }
                    }

                    // 2. Fallback kecocokan nama item
                    if (!string.IsNullOrEmpty(targetName) && !string.IsNullOrEmpty(it.Name)) {
                        if (it.Name.ToLower().Trim() == targetName) {
                            return it;
                        }
                    }
                }
            } catch {}
            return null;
        }

        public int CountLearnedFoodInInventory() {
            try {
                InventorySystem invSys = null;
                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}
                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys == null || invSys.PlayerInventory == null || invSys.PlayerInventory.Items == null) return 0;

                string targetProto = (LearnedFoodPrototypeId ?? "").ToLower().Trim();
                string targetName = (LearnedFoodName ?? "").ToLower().Trim();

                if (string.IsNullOrEmpty(targetProto) && string.IsNullOrEmpty(targetName)) return 0;

                int count = 0;
                List<ItemData> items = invSys.PlayerInventory.Items;
                for (int i = 0; i < items.Count; i++) {
                    ItemData it = items[i];
                    if (it == null || it.IsDestroyed()) continue;

                    bool match = false;
                    if (!string.IsNullOrEmpty(targetProto) && !string.IsNullOrEmpty(it.PrototypeId)) {
                        if (it.PrototypeId.ToLower().Trim() == targetProto) match = true;
                    }
                    if (!match && !string.IsNullOrEmpty(targetName) && !string.IsNullOrEmpty(it.Name)) {
                        if (it.Name.ToLower().Trim() == targetName) match = true;
                    }

                    if (match) {
                        count++;
                    }
                }
                return count;
            } catch {
                return 0;
            }
        }

        public IEnumerator AutoEatRoutine() {
            _isAutoEating = true;

            // Hentikan pergerakan karakter seketika
            PlayerController controller = GetPlayerController();
            if (controller != null) {
                try { controller.StopMove(); } catch {}
            }
            LocalMoveNavigator nav = (controller != null) ? GetNavigator(controller) : null;
            if (nav != null) {
                try { nav.Stop(); } catch {}
            }

            _statusText = "<color=#f39c12><b>[KELAPARAN < 20]</b></color> Menghentikan pergerakan karakter & bersiap makan...";
            yield return new WaitForSeconds(0.4f);

            int eatCount = 0;
            while (true) {
                PlayerBehavior player = PlayerBehavior.LocalPlayer;
                if (player == null || !player.IsAlive) break;

                float curStamina = (player.Stamina != null) ? player.Stamina.Get() : 0f;
                // Jika stamina sudah pulih di atas target (> 200) -> selesai makan
                if (curStamina >= AutoEatEnergyTarget) {
                    _statusText = string.Format("<color=#2ecc71><b>[KENYANG > {0:F0}]</b></color> Energi terisi {1:F0}! Melanjutkan Auto Gather...", AutoEatEnergyTarget, curStamina);
                    break;
                }

                // Cek apakah pemain sudah menginisiasi makanan di awal
                if (string.IsNullOrEmpty(LearnedFoodName) && string.IsNullOrEmpty(LearnedFoodPrototypeId)) {
                    _statusText = "<color=#e74c3c><b>[AUTO-MAKAN DITUNDA]</b></color> Belum ada makanan yang diinisiasi! Makan 1x secara manual dari tas.";
                    break;
                }

                // Cari HANYA item yang diinisiasi di awal di dalam tas!
                ItemData foodItem = FindLearnedFoodInInventory();
                if (foodItem == null) {
                    _statusText = string.Format("<color=#e74c3c><b>[MAKANAN HABIS]</b></color> Makanan inisiasi (<b>{0}</b>) habis di tas!", LearnedFoodName);
                    break;
                }

                // Pastikan karakter tetap diam sebelum makan
                if (controller != null) {
                    try { controller.StopMove(); } catch {}
                }

                InventorySystem invSys = null;
                try { invSys = GameSystem<InventorySystem>.Instance(); } catch {}
                if (invSys == null) invSys = UnityEngine.Object.FindObjectOfType<InventorySystem>();

                if (invSys != null) {
                    invSys.UseItem(foodItem, true, null);
                    _lastEatTime = Time.time;
                    eatCount++;
                    _statusText = string.Format("<color=#2ecc71><b>[MAKAN #{0}]</b></color> Memakan {1} (Energi: {2:F0} -> Target: > {3:F0})...", 
                        eatCount, foodItem.Name, curStamina, AutoEatEnergyTarget);
                }

                // Beri jeda 1.0 detik per item agar packet & animasi diproses server
                yield return new WaitForSeconds(1.0f);
            }

            _isAutoEating = false;
        }

        // =========================================================================
        // FITUR AUTO-ISTIRAHAT (FATIGUE MANAGEMENT)
        // Terpicu ketika kecapaian/fatigue >= 600. Bot menghentikan semua pekerjaan,
        // membuka maps -> menekan Titik Acuan (Warp ke Pulau Tamed/Home),
        // mendeteksi Ayunan di pulau tamed -> istirahat sampai fatigue < 5,
        // buka maps lagi -> tekan Kembali Menjelajah -> auto klik OK pada popup konfirmasi,
        // teleport ke pulau sebelumnya -> melanjutkan auto gather!
        // =========================================================================

        public static bool IsGameLoadingActive() {
            try {
                if (UIManager.IsLoadingCurtain) return true;
            } catch {}

            try {
                Durango.UI.LoadingCurtainGroup lcg = UnityEngine.Object.FindObjectOfType<Durango.UI.LoadingCurtainGroup>();
                if (Durango.UI.LoadingCurtainGroup.LoadingCount > 0 || (lcg != null && lcg.IsVisible)) return true;
            } catch {}

            try {
                PlayerBehavior p = PlayerBehavior.LocalPlayer;
                if (p == null || !p.IsLoaded) return true;
            } catch {}

            return false;
        }

        public GameObject FindNearestAyunan() {
            PlayerBehavior player = PlayerBehavior.LocalPlayer;
            if (player == null) return null;
            Vector3 myPos = player.CurrentPosition != Vector3.zero ? player.CurrentPosition : player.transform.position;

            string custom = (AyunanCustomKeyword ?? "").ToLower().Trim();
            string[] keywords = new string[] { "ayunan", "swing", "hammock", "buaian", "kursi", "bench", "daybed" };

            GameObject bestObj = null;
            float bestDist = 1000000f;

            // 1. Scan ImmovableBase / Artifact
            try {
                ImmovableBase[] immovables = UnityEngine.Object.FindObjectsOfType<ImmovableBase>();
                if (immovables != null) {
                    for (int i = 0; i < immovables.Length; i++) {
                        ImmovableBase imm = immovables[i];
                        if (imm == null || imm.gameObject == null || !imm.gameObject.activeInHierarchy) continue;

                        string goName = (imm.gameObject.name ?? "").ToLower();
                        string locName = "";
                        string bpId = "";

                        Artifact art = imm as Artifact;
                        if (art != null) {
                            try { locName = (art.LocalizedName ?? "").ToLower(); } catch {}
                            try { bpId = (art.BlueprintId ?? "").ToLower(); } catch {}
                        }

                        bool match = false;
                        if (!string.IsNullOrEmpty(custom)) {
                            if (goName.Contains(custom) || locName.Contains(custom) || bpId.Contains(custom)) match = true;
                        } else {
                            for (int k = 0; k < keywords.Length; k++) {
                                string kw = keywords[k];
                                if (goName.Contains(kw) || locName.Contains(kw) || bpId.Contains(kw)) {
                                    match = true;
                                    break;
                                }
                            }
                        }

                        // Fallback: Jika belum ada nama cocok dan bukan custom, cek interaction 407 (Rest)
                        if (!match && string.IsNullOrEmpty(custom) && art != null) {
                            try {
                                if ((int)art.InteractionType == 407 || (art.ContextIcon != null && art.ContextIcon.ToLower().Contains("rest"))) {
                                    match = true;
                                }
                            } catch {}
                        }

                        if (match) {
                            float dist = Vector3.Distance(myPos, imm.transform.position);
                            if (dist < bestDist) {
                                bestDist = dist;
                                bestObj = imm.gameObject;
                            }
                        }
                    }
                }
            } catch {}

            // 2. Scan GameObject umum jika belum ditemukan
            if (bestObj == null) {
                try {
                    GameObject[] allGo = UnityEngine.Object.FindObjectsOfType<GameObject>();
                    if (allGo != null) {
                        for (int i = 0; i < allGo.Length; i++) {
                            GameObject go = allGo[i];
                            if (go == null || !go.activeInHierarchy) continue;
                            string gname = go.name.ToLower();

                            bool match = false;
                            if (!string.IsNullOrEmpty(custom)) {
                                if (gname.Contains(custom)) match = true;
                            } else {
                                for (int k = 0; k < keywords.Length; k++) {
                                    if (gname.Contains(keywords[k])) { match = true; break; }
                                }
                            }

                            if (match) {
                                float dist = Vector3.Distance(myPos, go.transform.position);
                                if (dist < bestDist) {
                                    bestDist = dist;
                                    bestObj = go;
                                }
                            }
                        }
                    }
                } catch {}
            }

            return bestObj;
        }

        public void WarpToTitikAcuan() {
            try {
                // Buka UI peta dunia
                try { Durango.UI.MenuHelper.Open(Durango.Logic.MenuType.WorldMap, false); } catch {}

                // Panggil ReturnToHome (Warp ke Titik Acuan / Pulau Tamed)
                MapSystem ms = null;
                try { ms = GameSystem<MapSystem>.Instance(); } catch {}
                if (ms == null) ms = UnityEngine.Object.FindObjectOfType<MapSystem>();

                if (ms != null) {
                    ms.ReturnToHome();
                    Debug.Log("[AutoGather] MapSystem.ReturnToHome dieksekusi!");
                    return;
                }

                // Fallback: WorldMapGroup
                Durango.UI.WorldMapGroup wmg = UnityEngine.Object.FindObjectOfType<Durango.UI.WorldMapGroup>();
                if (wmg != null) {
                    MethodInfo m = typeof(Durango.UI.WorldMapGroup).GetMethod("ReturnToHome", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null) {
                        m.Invoke(wmg, null);
                        Debug.Log("[AutoGather] WorldMapGroup.ReturnToHome dieksekusi!");
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] WarpToTitikAcuan error: " + ex);
            }
        }

        public void TriggerKembaliMenjelajah() {
            try {
                // Buka UI peta dunia
                try { Durango.UI.MenuHelper.Open(Durango.Logic.MenuType.WorldMap, false); } catch {}

                // 1. Prioritaskan method WarpBack pada WorldMapGroup
                Durango.UI.WorldMapGroup wmg = UnityEngine.Object.FindObjectOfType<Durango.UI.WorldMapGroup>();
                if (wmg != null) {
                    MethodInfo m = typeof(Durango.UI.WorldMapGroup).GetMethod("WarpBack", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null) {
                        m.Invoke(wmg, null);
                        Debug.Log("[AutoGather] WorldMapGroup.WarpBack dieksekusi!");
                        return;
                    }
                }

                // 2. Fallback: Request cost via MapSystem
                MapSystem ms = null;
                try { ms = GameSystem<MapSystem>.Instance(); } catch {}
                if (ms == null) ms = UnityEngine.Object.FindObjectOfType<MapSystem>();

                if (ms != null) {
                    MapSystem.RequestWarpBackCost(delegate(long cost) {
                        Messages.Points pts = ms.Points;
                        if (pts.LastReturnPoint.HasValue) {
                            Messages.Region reg = pts.LastReturnPoint.Value.Region;
                            ms.WarpBack(reg);
                            Debug.Log("[AutoGather] MapSystem.WarpBack dieksekusi via callback!");
                        }
                    });
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] TriggerKembaliMenjelajah error: " + ex);
            }
        }

        public bool TryConfirmWarpBackPopup() {
            try {
                // 1. Cek Durango.UI.MessageBox (standar ShowPayConfirm dari WarpBack)
                Durango.UI.MessageBox mb = null;
                try { mb = UIManager.MessageBox; } catch {}
                if (mb == null) mb = UnityEngine.Object.FindObjectOfType<Durango.UI.MessageBox>();

                if (mb != null && mb.IsShow) {
                    // Coba panggil ClickOkButton
                    MethodInfo mClickOk = typeof(Durango.UI.MessageBox).GetMethod("ClickOkButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mClickOk != null) {
                        mClickOk.Invoke(mb, null);
                        Debug.Log("[AutoGather] MessageBox.ClickOkButton berhasil dieksekusi!");
                        return true;
                    }

                    // Fallback invoke delegate _onOkCancel
                    FieldInfo fOkCancel = typeof(Durango.UI.MessageBox).GetField("_onOkCancel", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fOkCancel != null) {
                        Action<bool> act = fOkCancel.GetValue(mb) as Action<bool>;
                        if (act != null) {
                            act.Invoke(true);
                            mb.Hide(true);
                            Debug.Log("[AutoGather] MessageBox._onOkCancel(true) berhasil dieksekusi!");
                            return true;
                        }
                    }

                    // Fallback invoke delegate _onOk
                    FieldInfo fOk = typeof(Durango.UI.MessageBox).GetField("_onOk", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fOk != null) {
                        Action act = fOk.GetValue(mb) as Action;
                        if (act != null) {
                            act.Invoke();
                            mb.Hide(true);
                            Debug.Log("[AutoGather] MessageBox._onOk() berhasil dieksekusi!");
                            return true;
                        }
                    }
                }

                // 2. Cek ConfirmPopup (fallback jika berupa ConfirmPopup)
                Durango.UI.Popup.ConfirmPopup cp = UnityEngine.Object.FindObjectOfType<Durango.UI.Popup.ConfirmPopup>();
                if (cp != null && cp.gameObject.activeInHierarchy && cp.IsVisible) {
                    if (_confirmPopupActionsField != null) {
                        List<Action> actions = _confirmPopupActionsField.GetValue(cp) as List<Action>;
                        if (actions != null && actions.Count > 0 && actions[0] != null) {
                            actions[0].Invoke();
                            try { cp.Hide(); } catch {}
                            Debug.Log("[AutoGather] ConfirmPopup aksi 0 dieksekusi!");
                            return true;
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] TryConfirmWarpBackPopup error: " + ex);
            }
            return false;
        }

        public bool TryClickRestMenuSlot() {
            try {
                Durango.UI.InteractionMenuListWidget_PC wheel = UnityEngine.Object.FindObjectOfType<Durango.UI.InteractionMenuListWidget_PC>();
                if (wheel != null && Durango.UI.InteractionMenuListWidgetBase.IsShow) {
                    FieldInfo fWidgets = typeof(Durango.UI.InteractionMenuListWidgetBase).GetField("_widgets", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fWidgets != null) {
                        Durango.UI.InteractionMenuWidget_PC[] widgets = fWidgets.GetValue(wheel) as Durango.UI.InteractionMenuWidget_PC[];
                        if (widgets != null) {
                            for (int i = 0; i < widgets.Length; i++) {
                                Durango.UI.InteractionMenuWidget_PC w = widgets[i];
                                if (w == null || w.Disabled || !w.Valid) continue;
                                string actTxt = (GetSlotActionText(w) ?? "").ToLower();
                                string fullTxt = (GetWidgetFullText(w) ?? "").ToLower();
                                if (actTxt.Contains("istirahat") || actTxt.Contains("rest") || actTxt.Contains("duduk") || actTxt.Contains("sit") ||
                                    fullTxt.Contains("istirahat") || fullTxt.Contains("rest") || fullTxt.Contains("duduk")) {
                                    w.SetClick();
                                    Debug.Log("[AutoGather] Roda interaksi slot istirahat berhasil diklik!");
                                    return true;
                                }
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }

        public IEnumerator FatigueRecoveryRoutine() {
            _isFatigueRestActive = true;
            _statusText = string.Format("<color=#e67e22><b>[KECAPAIAN >= {0:F0}]</b></color> Menghentikan panen & bersiap warp ke Titik Acuan...", FatigueThreshold);

            // 1. Hentikan pergerakan & batalkan interaksi berjalan
            PlayerController controller = GetPlayerController();
            if (controller != null) {
                try { controller.StopMove(); } catch {}
            }
            LocalMoveNavigator nav = (controller != null) ? GetNavigator(controller) : null;
            if (nav != null) {
                try { nav.Stop(); } catch {}
            }
            ForceCloseInteractionMenu();

            yield return new WaitForSeconds(0.5f);

            // 2. Buka maps lalu tekan ke titik acuan
            _statusText = "<color=#3498db><b>[WARP]</b></color> Membuka Map & Teleport ke Titik Acuan...";
            WarpToTitikAcuan();

            yield return new WaitForSeconds(1.5f);

            // 3. Tunggu sampai loading selesai dan karakter tiba di Pulau Tamed
            _statusText = "<color=#3498db><b>[LOADING]</b></color> Menunggu tiba di Pulau Tamed...";
            float waitArrivalTimeout = Time.time + 60f;
            while (Time.time < waitArrivalTimeout) {
                if (!IsGameLoadingActive()) {
                    PlayerBehavior p = PlayerBehavior.LocalPlayer;
                    if (p != null && p.IsLoaded) break;
                }
                yield return new WaitForSeconds(0.5f);
            }

            // Jeda 1.5 detik agar entity lingkungan (ayunan) ter-spawn di scene
            yield return new WaitForSeconds(1.5f);

            // 4. Deteksi objek Ayunan di sekitar pulau tamed
            _statusText = "<color=#f39c12><b>[MENCARI AYUNAN]</b></color> Memindai ayunan di sekitar titik acuan...";
            GameObject ayunan = null;
            float searchTimeout = Time.time + 10f;
            while (Time.time < searchTimeout && ayunan == null) {
                ayunan = FindNearestAyunan();
                if (ayunan != null) break;
                yield return new WaitForSeconds(0.5f);
            }

            if (ayunan == null) {
                _statusText = "<color=#e74c3c><b>[AYUNAN TIDAK DITEMUKAN]</b></color> Tidak ada ayunan terdeteksi. Silakan istirahat secara manual.";
                _isFatigueRestActive = false;
                yield break;
            }

            _statusText = string.Format("<color=#2ecc71><b>[AYUNAN DITEMUKAN]</b></color> Mendekati <b>{0}</b>...", ayunan.name);

            // 5. Bergerak mendekati Ayunan
            float approachTimeout = Time.time + 30f;
            while (ayunan != null && Time.time < approachTimeout) {
                PlayerBehavior p = PlayerBehavior.LocalPlayer;
                if (p == null) yield break;
                Vector3 curPos = p.CurrentPosition != Vector3.zero ? p.CurrentPosition : p.transform.position;
                Vector3 targetPos = ayunan.transform.position;
                float dist = Vector3.Distance(curPos, targetPos);

                // Jika sudah dalam jarak interaksi (<= 2.2 meter / 220 unit)
                if (dist <= 220f) {
                    if (controller != null) controller.StopMove();
                    if (nav != null) nav.Stop();
                    break;
                }

                if (controller != null && nav != null) {
                    nav.MoveToPosition(targetPos, null, 100f, true);
                }
                yield return new WaitForSeconds(0.2f);
            }

            if (controller != null) controller.StopMove();
            if (nav != null) nav.Stop();
            yield return new WaitForSeconds(0.3f);

            // 6. Lakukan aksi istirahat di Ayunan
            _statusText = "<color=#f39c12><b>[INTERAKSI]</b></color> Duduk & istirahat di ayunan...";
            Artifact art = ayunan.GetComponent<Artifact>();
            if (art == null) art = ayunan.GetComponentInParent<Artifact>();

            if (art != null) {
                try {
                    Messages.RestOn restMsg = new Messages.RestOn();
                    restMsg.EntityId = art.EntityId;
                    restMsg.Tile = art.WorldTile;
                    Durango.Network.Connections.Frontend.Send<Messages.RestOn>(restMsg, false, 0);
                } catch {}
            }

            TriggerInteraction(ayunan);
            yield return new WaitForSeconds(0.5f);
            TryClickRestMenuSlot();

            // 7. Loop istirahat sampai kecapaian < 5
            while (true) {
                PlayerBehavior p = PlayerBehavior.LocalPlayer;
                if (p == null) yield break;
                float curFatigue = (p.Fatigue != null) ? p.Fatigue.Get() : 0f;
                _statusText = string.Format("<color=#2ecc71><b>[ISTIRAHAT DI AYUNAN]</b></color> Fatigue: <b>{0:F0}</b> -> Target: < <b>{1:F0}</b>", curFatigue, FatigueRecoverTarget);

                if (curFatigue < FatigueRecoverTarget) {
                    break;
                }
                yield return new WaitForSeconds(1.0f);
            }

            _statusText = string.Format("<color=#2ecc71><b>[FATIGUE PULIH]</b></color> Fatigue < {0:F0}! Membuka map untuk Kembali Menjelajah...", FatigueRecoverTarget);
            yield return new WaitForSeconds(1.0f);

            // 8. Buka maps lagi dan tekan Kembali Menjelajah
            TriggerKembaliMenjelajah();

            // 9. Tunggu dan tekan OK otomatis pada popup konfirmasi
            float confirmTimeout = Time.time + 8f;
            bool confirmed = false;
            while (Time.time < confirmTimeout) {
                if (TryConfirmWarpBackPopup()) {
                    confirmed = true;
                    _statusText = "<color=#2ecc71><b>[KONFIRMASI OK]</b></color> Popup disetujui, bersiap teleport kembali...";
                    break;
                }
                yield return new WaitForSeconds(0.2f);
            }

            if (!confirmed) {
                TryConfirmWarpBackPopup();
            }

            // 10. Tunggu proses loading screen teleportasi kembali ke pulau asal
            yield return new WaitForSeconds(1.5f);
            _statusText = "<color=#3498db><b>[LOADING]</b></color> Menunggu tiba kembali di pulau sebelumnya...";
            float waitReturnTimeout = Time.time + 60f;
            while (Time.time < waitReturnTimeout) {
                if (!IsGameLoadingActive()) {
                    PlayerBehavior p = PlayerBehavior.LocalPlayer;
                    if (p != null && p.IsLoaded) break;
                }
                yield return new WaitForSeconds(0.5f);
            }

            yield return new WaitForSeconds(1.5f);
            _statusText = "<color=#2ecc71><b>[TIBA DI PULAU ASAL]</b></color> Stamina & kondisi prima. Melanjutkan Auto Gather!";
            _isFatigueRestActive = false;
        }

    }
}
