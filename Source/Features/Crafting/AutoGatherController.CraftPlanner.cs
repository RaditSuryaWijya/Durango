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
                PlayerBehavior player = PlayerBehavior.LocalPlayer;
                if (player != null && !string.IsNullOrEmpty(player.CurrentAnimClipName)) {
                    string clip = player.CurrentAnimClipName.ToLower();
                    // Hanya cek animasi kerja fisik nyata (jangan cek 'action' karena idle stance sering mengandung kata action)
                    if (clip.Contains("gather") || clip.Contains("chop") || clip.Contains("mine") || 
                        clip.Contains("pick") || clip.Contains("harvest")) {
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


    }
}
