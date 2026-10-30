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

    public class LearnedTargetItem {

        public string ActionName = "";

        public string GeneratorId = "";

        public string IconName = "";

        public LearnedTargetItem() {}

        public LearnedTargetItem(string actionName, string generatorId, string iconName) {

            this.ActionName = actionName;

            this.GeneratorId = generatorId;

            this.IconName = iconName;

        }

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

}
