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
                    float speedRatio = (FastSniperSpeedMultiplier > 0.1f) ? FastSniperSpeedMultiplier : 1.7f; // Penyesuaian user: 1.7x (+70%)
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

                int uaState = 0;

                if (ua != null && hasActiveAction) {

                    try {

                        if (_uaActionField == null) {

                            _uaActionField = typeof(Durango.Logic.Combat.UsingAction).GetField("_action", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        }

                        if (_uaActionField != null) curAct = (BattleAction)_uaActionField.GetValue(ua);

                    } catch {}

                    try {

                        MethodInfo getState = ua.GetType().GetMethod("GetState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (getState != null) uaState = Convert.ToInt32(getState.Invoke(ua, null));

                    } catch {}

                }

                // Deteksi status Crossbow Action
                bool isCrossbow = IsCrossbowAction(curAct);

                // Deteksi saat benar-benar memasuki State.Using (3): Serangan mulai dieksekusi di jangkauan
                if (isCrossbow && curAct != null && uaState == 3) {

                    if (!_crossbowState3Active || curAct != _lastCrossbowAction) {

                        _crossbowState3Active = true;

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

                } else if (uaState != 3) {

                    _crossbowState3Active = false;

                    if (!hasActiveAction) {

                        _lastCrossbowAction = null;

                    }

                }

                // Cek apakah SEDANG DALAM FASE RELOAD Crossbow:
                // Syarat: Sedang di State.Using (3), dan waktu sudah melewati damage_time (tembakan keluar).
                // SAAT MENGEJAR MUSUH (State.Prepare = 1), isCrossbowReloading adalah FALSE!
                bool isCrossbowReloading = EnableFastCrossbowReload && isCrossbow && _crossbowState3Active && (Time.time >= _crossbowActionStartTime + _crossbowDamageTime);

                // Sinkronisasi flag DisableAttackMoveLock ke UsingAction:
                // HANYA bypass MoveLock jika BUKAN sedang dalam fase reload Crossbow
                if (_uaDisableMoveLockField != null) {

                    try { _uaDisableMoveLockField.SetValue(null, EnableMobileAttack && !isCrossbowReloading); } catch {}

                }

                // 1. Mobile Attack: Kunci karakter hanya saat fase reload Crossbow, atau lepas MoveLock untuk senjata lain / saat mengejar
                InputSystem inputSys = null;

                try { inputSys = GameSystem<InputSystem>.Instance(); } catch {}

                if (inputSys == null) inputSys = UnityEngine.Object.FindObjectOfType<InputSystem>();

                if (isCrossbowReloading) {

                    if (inputSys != null && !inputSys.MoveLock) {

                        inputSys.MoveLock = true;

                    }

                } else if (EnableMobileAttack) {

                    if (inputSys != null && inputSys.MoveLock) {

                        inputSys.MoveLock = false;

                    }

                }

                // SINKRONISASI DisableMoveCancel:
                // HANYA aktif saat karakter sedang TANGKIS / GUARD atau saat SEDANG RELOAD CROSSBOW!
                // Saat karakter sedang mengejar dino di luar jangkauan (State 1), DisableMoveCancel WAJIB FALSE agar WASD bisa membatalkan pengejaran!
                bool isGuarding = false;

                if (curAct != null && IsGuardOrDefense(curAct)) {

                    isGuarding = true;

                }

                if (_csDisableMoveCancelField != null) {

                    try { _csDisableMoveCancelField.SetValue(null, isGuarding || isCrossbowReloading); } catch {}

                }

                // ACCELERATED CROSSBOW RELOAD: Percepat animasi reload / ngokang Crossbow (1.5x) & durasi action
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

                // MOVING ATTACK & MANUAL WASD CANCEL
                if (cs != null && ua != null && hasActiveAction) {

                    bool isMovingInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || 

                                        Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||

                                        Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||

                                        Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||

                                        Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f || 

                                        Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.05f;

                    if (isMovingInput) {

                        // Kasus 1: Sedang mengejar dino di luar jangkauan (State 1 Prepare)
                        // Jika pemain menekan WASD -> BATALKAN PENGEJARAN SEGERA & BERIKAN KONTROL MANUAL!
                        if (uaState == 1) {

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

                            PlayerController pc = GetPlayerController();

                            if (pc != null) {

                                if (_pcStopMoveField != null) {

                                    try { _pcStopMoveField.SetValue(pc, false); } catch {}

                                }

                                if (_pcManualMoveAllowedField != null) {

                                    try { _pcManualMoveAllowedField.SetValue(pc, true); } catch {}

                                }

                            }

                        }

                        // Kasus 2: Sedang dalam eksekusi animasi serangan (State 3 Using)
                        else if (uaState == 3 && EnableMobileAttack) {

                            if (curAct != null && (IsGuardOrDefense(curAct) || isCrossbowReloading)) {

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

                                PlayerController pc = GetPlayerController();

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

                if (EnableRecoveryCut && ua != null && hasActiveAction && uaState == 3) {

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

        // =========================================================================
        // MANUAL COUNTER ATTACK (SIKAP MELAWAN) ANIMATION CANCEL
        // Memungkinkan pemain membatalkan animasi serangan aktif (Pukul, Hajar, Stab)
        // secara manual saat menekan SPASI, dengan validasi ketat status kesiapan skill.
        // =========================================================================
        private static float _lastManualCounterTime = 0f;

        private void HandleManualCounterCancel() {
            try {
                if (!IsSpaceOrDodgeKeyPressed()) return;

                if (Time.unscaledTime - _lastManualCounterTime < 0.15f) return;

                if (!CombatSystem.HasInstance()) return;
                CombatSystem cs = CombatSystem.Instance();
                if (cs == null) return;

                // VALIDASI BATTLE MODE: Wajib sedang dalam mode bertarung (CombatMode), ATAU sedang diserang dino aktif
                // Jika tidak sedang bertarung dan tidak ada ancaman serangan dino, jangan picu counter attack!
                if (!cs.CombatMode && _currentImminentAlert == null) return;

                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null || !localPlayer.IsAlive || localPlayer.IsRiding) return;

                if (!IsOneHandWeapon(cs) || !IsCounterStance()) return;

                BattleAction spaceAction = GetSpaceAction(cs);
                if (spaceAction == null || spaceAction.Data == null) return;

                double serverTime = 0.0;
                try {
                    if (Durango.Network.Connections.Frontend != null) {
                        serverTime = Durango.Network.Connections.Frontend.GetPredictedServerTime();
                    }
                } catch {}
                if (serverTime <= 0.0) serverTime = (double)Time.time;

                // VALIDASI KESIAPAN SKILL: Jangan batalkan animasi & jangan timpa model jika skill sedang cooldown / stamina kurang!
                if (!IsCounterSkillReady(cs, spaceAction, serverTime, localPlayer)) return;

                _lastManualCounterTime = Time.unscaledTime;

                UsingAction ua = cs.UsingAction;
                bool hasActiveAction = ua != null && ua.HasValue();

                if (hasActiveAction) {
                    try { ua.Clear(); } catch {}
                    try { spaceAction.ProhibitedUntil = serverTime - 1.0; } catch {}
                    try {
                        if (PlayerController.MotionUpdater != null) {
                            PlayerController.MotionUpdater.ClearReservedMotions();
                        }
                        if (localPlayer.Anim != null) {
                            localPlayer.Anim.Stop();
                        }
                    } catch {}
                } else {
                    try { spaceAction.ProhibitedUntil = serverTime - 1.0; } catch {}
                }

                // Eksekusi aksi Counter Attack (Space Action)
                cs.UseBattleAction(spaceAction.Data.Id);

                // Langsung lompat ke State 2 (Execute) agar tidak delay rotasi / membeku
                try {
                    if (_uaStateField == null) {
                        _uaStateField = typeof(UsingAction).GetField("_state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    if (_uaStateField != null && cs.UsingAction != null) {
                        _uaStateField.SetValue(cs.UsingAction, 2);
                    }
                } catch {}

                // Timpa model 3D secara instan dengan animasi Counter Attack tanpa jeda "diam"
                ForcePlayCounterMotion(localPlayer, spaceAction);
            } catch (Exception ex) {
                Debug.LogWarning("[ManualCounter] HandleManualCounterCancel error: " + ex.Message);
            }
        }

        private static bool IsSpaceOrDodgeKeyPressed() {
            return Input.GetKeyDown(KeyCode.Space);
        }

        private static bool IsOneHandWeapon(CombatSystem cs) {
            try {
                string wc = XDLands.Stance.XdStance.WeaponClass;
                if (!string.IsNullOrEmpty(wc)) {
                    return wc.IndexOf("onehand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           wc.IndexOf("1hand", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (cs != null) {
                    var actions = cs.GetCurrentBattleActions();
                    if (actions != null) {
                        foreach (var act in actions) {
                            if (act != null && act.Data != null && !string.IsNullOrEmpty(act.Data.Id)) {
                                if (act.Data.Id.IndexOf("onehand", StringComparison.OrdinalIgnoreCase) >= 0)
                                    return true;
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }

        private static bool IsCounterStance() {
            try {
                string cur = XDLands.Stance.XdStance.Current;
                if (string.IsNullOrEmpty(cur)) return false;

                return cur.IndexOf("outfighting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       cur.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       cur.IndexOf("melawan", StringComparison.OrdinalIgnoreCase) >= 0;
            } catch {
                return false;
            }
        }

        private static BattleAction GetSpaceAction(CombatSystem cs) {
            if (cs == null) return null;

            if (cs.ActionSlots != null && cs.ActionSlots.Length > 0) {
                BattleAction slot0 = cs.ActionSlots[0];
                if (slot0 != null && slot0.Data != null) {
                    return slot0;
                }
            }

            var actions = cs.GetCurrentBattleActions();
            if (actions != null) {
                foreach (var act in actions) {
                    if (act == null || act.Data == null) continue;
                    if (!string.IsNullOrEmpty(act.Data.Id) &&
                        (act.Data.Id.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("roll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0)) {
                        return act;
                    }
                    if (act.Data.Meta != null && act.Data.Meta.BattleActionType == Shared.Battle.BattleActionType.Dodge) {
                        return act;
                    }
                }
            }

            return null;
        }

        private static bool IsCounterSkillReady(CombatSystem cs, BattleAction spaceAction, double serverTime, PlayerBehavior localPlayer) {
            try {
                if (cs == null || spaceAction == null || localPlayer == null) return false;

                // VALIDASI BATTLE MODE: Skill ready jika sedang tempur ATAU ada ancaman serangan dino aktif
                if (!cs.CombatMode && Instance != null && Instance._currentImminentAlert == null) return false;

                if (!localPlayer.IsAlive || localPlayer.IsRiding)
                    return false;

                if (cs.DamagedProcesser != null && cs.DamagedProcesser.ControllLostUntil.HasValue) {
                    if (Time.time < cs.DamagedProcesser.ControllLostUntil.Value)
                        return false;
                }

                try {
                    if (localPlayer.WaterDepthLevel != null && (int)localPlayer.WaterDepthLevel.Value >= 2)
                        return false;
                } catch {}

                // Validasi Cooldown
                if (serverTime < spaceAction.CooldownUntil && serverTime > spaceAction.CooldownSince) {
                    return false;
                }

                // Validasi Activated
                if (spaceAction.ActivatedUntil.HasValue && serverTime <= spaceAction.ActivatedUntil.Value) {
                    return false;
                }

                // Validasi Stamina
                if (spaceAction.Stamina > 0f) {
                    if (localPlayer.Stamina == null || localPlayer.Stamina.Get() < spaceAction.Stamina) {
                        return false;
                    }
                }

                return true;
            } catch {
                return false;
            }
        }

        private static void ForcePlayCounterMotion(PlayerBehavior localPlayer, BattleAction spaceAction) {
            try {
                if (localPlayer == null) return;

                string motion = spaceAction != null ? spaceAction.Motion : null;
                float rate = spaceAction != null && spaceAction.PlaybackRate > 0.01f ? spaceAction.PlaybackRate : 1.0f;

                if (PlayerController.MotionUpdater != null && !string.IsNullOrEmpty(motion)) {
                    try {
                        PlayerController.MotionUpdater.ClearReservedMotions();
                        PlayerController.MotionUpdater.Motion(motion, 0f, rate, false, false, null, default(ItemColor));
                    } catch {}
                }

                if (!string.IsNullOrEmpty(motion)) {
                    try {
                        localPlayer.PlayMotionForcely(motion, rate, true);
                    } catch {}
                }

                if (localPlayer.Anim != null) {
                    string prefix = localPlayer.MotionPrefix ?? "";
                    string clip1 = prefix + motion;
                    string clip2 = motion;

                    bool played = false;
                    if (!string.IsNullOrEmpty(clip1) && localPlayer.Anim[clip1] != null) {
                        localPlayer.Anim.CrossFade(clip1, 0.05f);
                        played = true;
                    } else if (!string.IsNullOrEmpty(clip2) && localPlayer.Anim[clip2] != null) {
                        localPlayer.Anim.CrossFade(clip2, 0.05f);
                        played = true;
                    }

                    if (!played) {
                        foreach (AnimationState st in localPlayer.Anim) {
                            if (st != null && !string.IsNullOrEmpty(st.name)) {
                                if (st.name.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    st.name.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0) {
                                    localPlayer.Anim.CrossFade(st.name, 0.05f);
                                    break;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning("[ManualCounter] ForcePlayCounterMotion error: " + ex.Message);
            }
        }

        
        // =========================================================================
        // AUTO OUT COMBAT SETELAH TARGET DINO MATI
        // Memastikan karakter langsung keluar dari mode combat (BattleView -> Normal)
        // dan melepas target segera setelah dino mati, serta mencegah karakter tertahan
        // di battle stance jika BattleModeLock atau server lag.
        // =========================================================================
        private static Durango.UI.CombatGroup _cachedCombatGroup = null;
        private static float _nextCombatGroupFindTime = 0f;
        private static DamageableEntity _lastMonitoredCombatTarget = null;
        private static float _lastAutoExitCombatTime = 0f;

        private Durango.UI.CombatGroup GetCombatGroup() {
            if (_cachedCombatGroup == null && Time.time >= _nextCombatGroupFindTime) {
                _nextCombatGroupFindTime = Time.time + 1.0f;
                try {
                    _cachedCombatGroup = UnityEngine.Object.FindObjectOfType<Durango.UI.CombatGroup>();
                } catch {}
            }
            return _cachedCombatGroup;
        }

        private void HandleAutoExitCombatOnTargetDead() {
            try {
                if (!AutoExitCombatOnKill) return;
                if (IsCombatBotEnabled) return; // Jika bot tempur aktif, biarkan bot mengelola targetnya sendiri

                if (!CombatSystem.HasInstance()) return;
                CombatSystem cs = CombatSystem.Instance();
                if (cs == null) return;

                DamageableEntity curTarget = (cs.Target != null) ? cs.Target.Value : null;

                // 1. Simpan target dino yang sedang aktif dilawan saat masih hidup
                if (curTarget != null && curTarget.IsAlive) {
                    _lastMonitoredCombatTarget = curTarget;
                }

                // 2. Evaluasi apakah target dino yang dilawan telah mati
                bool targetDied = false;
                if (curTarget != null && !curTarget.IsAlive) {
                    targetDied = true;
                } else if (_lastMonitoredCombatTarget != null) {
                    bool isStillAlive = false;
                    try {
                        isStillAlive = _lastMonitoredCombatTarget.IsAlive && _lastMonitoredCombatTarget.GetLife() != null && _lastMonitoredCombatTarget.GetLife().Get() > 0f;
                    } catch {
                        isStillAlive = false;
                    }
                    if (!isStillAlive) {
                        targetDied = true;
                    }
                }

                // 3. Evaluasi ancaman aktif (hanya musuh yang BENAR-BENAR sedang menyerang/menargetkan player)
                // Catatan: cs.DamageableEntities.GetEnemies() mendeteksi semua hewan netral/jinak dalam 20m, jadi wajib cek TargetId!
                bool hasActiveThreat = false;
                if (_activeAttackAlerts != null && _activeAttackAlerts.Count > 0) {
                    hasActiveThreat = true;
                }

                if (!hasActiveThreat && cs.DamageableEntities != null) {
                    string myEntityId = (PlayerBehavior.LocalPlayer != null) ? PlayerBehavior.LocalPlayer.EntityId : "";
                    var enemies = cs.DamageableEntities.GetEnemies();
                    if (enemies != null) {
                        foreach (var e in enemies) {
                            if (e != null && e.IsAlive && e != curTarget && e != _lastMonitoredCombatTarget) {
                                if (!string.IsNullOrEmpty(myEntityId) && e.TargetId == myEntityId) {
                                    hasActiveThreat = true;
                                    // Jika ada musuh lain yang sedang aktif menyerang player, alihkan target ke musuh tersebut
                                    if (targetDied) {
                                        cs.SelectTarget(e);
                                        _lastMonitoredCombatTarget = e;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // 4. Jika target mati dan TIDAK ada ancaman musuh lain yang menyerang player:
                // Eksekusi urutan keluar pertempuran secara tuntas (bersihkan target, reset cooldown, buka gembok, reset stance)
                if (targetDied && !hasActiveThreat) {
                    if (Time.unscaledTime - _lastAutoExitCombatTime > 0.3f) {
                        _lastAutoExitCombatTime = Time.unscaledTime;

                        // a. Lepas target reticle dari bangkai dino
                        try { cs.ClearTarget(); } catch {}
                        _lastMonitoredCombatTarget = null;

                        // b. Bypass leave-battle available cooldown
                        try {
                            if (cs.BattleLeaveAvailableAt != null) {
                                cs.BattleLeaveAvailableAt.Value = 0f;
                            }
                        } catch {}

                        // c. Kirim pesan ExitBattle ke server
                        try { CombatSystem.ExitBattle(); } catch {}

                        // d. Paksa CombatMode = false di client
                        try { cs.CombatMode = false; } catch {}

                        // e. Lepas gembok BattleModeLock agar UI dan stance tidak tertahan
                        Durango.UI.CombatGroup cg = GetCombatGroup();
                        if (cg != null) {
                            try {
                                if (cg.BattleModeLock != null && cg.BattleModeLock.Value) {
                                    cg.BattleModeLock.Value = false;
                                }
                            } catch {}
                            try {
                                PlayerPrefs.SetInt("BattleModeLock", 0);
                                PlayerPrefs.Save();
                            } catch {}

                            // f. Kembalikan kamera dan UI ke mode Normal
                            try {
                                if (cg.BattleView != Durango.UI.CombatGroup.BattleViewMode.Normal) {
                                    cg.SetBattleView(Durango.UI.CombatGroup.BattleViewMode.Normal);
                                }
                            } catch {}
                        }

                        // g. Reset animasi karakter ke Stand (sarungkan senjata)
                        try {
                            LocalMotionUpdater lmu = PlayerController.MotionUpdater;
                            if (lmu != null) {
                                if (lmu.IsBattleStand != null) {
                                    lmu.IsBattleStand.Value = false;
                                }
                                if (lmu.IsState("Prepare") || lmu.IsState("BattleStand")) {
                                    lmu.Motion("Stand", 0f, 1f, false, false, null, default(ItemColor));
                                }
                            }
                        } catch {}

                        Debug.Log("[AutoGather] Auto Out Combat: Target dino mati & tidak ada ancaman lain, keluar dari mode combat!");
                    }
                }
                // 5. Jika CombatMode sudah false (atau server sudah mengakhiri combat),
                // namun CombatGroup UI masih tertahan di mode Battle (akibat lag / gembok lock)
                else if (!cs.CombatMode && !hasActiveThreat) {
                    Durango.UI.CombatGroup cg = GetCombatGroup();
                    if (cg != null && cg.BattleView == Durango.UI.CombatGroup.BattleViewMode.Battle) {
                        if (Time.unscaledTime - _lastAutoExitCombatTime > 0.5f) {
                            _lastAutoExitCombatTime = Time.unscaledTime;
                            try {
                                if (cg.BattleModeLock != null && cg.BattleModeLock.Value) {
                                    cg.BattleModeLock.Value = false;
                                }
                                PlayerPrefs.SetInt("BattleModeLock", 0);
                                PlayerPrefs.Save();
                                cg.SetBattleView(Durango.UI.CombatGroup.BattleViewMode.Normal);
                                try { cs.ClearTarget(); } catch {}
                                _lastMonitoredCombatTarget = null;
                            } catch {}
                            try {
                                LocalMotionUpdater lmu = PlayerController.MotionUpdater;
                                if (lmu != null && lmu.IsBattleStand != null) {
                                    lmu.IsBattleStand.Value = false;
                                }
                            } catch {}
                        }
                    }
                }
            } catch {}
        }

        // =========================================================================
        // COUNTER ASSIST ENGINE (VISUAL TIMING ALERT)
        // Menghitung sisa waktu benturan pukulan dino dari AttackAlerted.AttackTime
        // terhadap GetPredictedServerTime(). Menampilkan peringatan visual berkedip
        // di tombol Counter (dan Banner HUD) saat serangan memasuki Golden Window (Fase 2).
        // =========================================================================
        private bool _isCounterSkillReady = false;

        private void RecordCounterAssistAlert(Messages.AttackAlerted msg) {
            try {
                lock (_activeAttackAlerts) {
                    string entId = msg.EntityId ?? "";
                    Vector3 newCenter = msg.Center.HasValue ? msg.Center.Value.ToClientPosition() : Vector3.zero;

                    IncomingAttackAlert matchingExisting = null;

                    if (!string.IsNullOrEmpty(entId)) {
                        for (int i = _activeAttackAlerts.Count - 1; i >= 0; i--) {
                            IncomingAttackAlert existing = _activeAttackAlerts[i];
                            if (existing.AttackerId == entId) {
                                // Jika ini dari siklus serangan lama (> 1.2 detik perbedaan mulai serangan), bersihkan yang lama
                                if (msg.EventAt > existing.EventAt + 1.2) {
                                    _activeAttackAlerts.RemoveAt(i);
                                    continue;
                                }

                                // Cek apakah ini hitbox duplikat persis yang sama (center dan attack time sama)
                                Vector3 exCenter = existing.Alert.Center.HasValue ? existing.Alert.Center.Value.ToClientPosition() : Vector3.zero;
                                bool sameCenter = (msg.Center.HasValue && existing.Alert.Center.HasValue) 
                                    ? (Vector3.Distance(newCenter, exCenter) < 40f) 
                                    : (!msg.Center.HasValue && !existing.Alert.Center.HasValue);

                                if (sameCenter && Math.Abs(existing.AttackTime - msg.AttackTime) < 0.15) {
                                    matchingExisting = existing;
                                    break;
                                }
                            }
                        }
                    }

                    if (matchingExisting != null) {
                        // Perbarui data alert yang sama
                        matchingExisting.Alert = msg;
                        matchingExisting.AttackTime = msg.AttackTime;
                        matchingExisting.EventAt = msg.EventAt;
                    } else {
                        // Tambahkan sebagai area merah / hitbox baru (MENDUKUNG 2 ATAU LEBIH AREA SEPERTI APATOSAURUS)
                        IncomingAttackAlert item = new IncomingAttackAlert();
                        item.Alert = msg;
                        item.AttackerId = entId;
                        item.AttackerName = "";
                        item.EventAt = msg.EventAt;
                        item.AttackTime = msg.AttackTime;
                        item.ReceivedLocalTime = Time.time;
                        item.SoundPlayed = false;
                        item.HasTelegraphHitbox = false;
                        item.IsPlayerInside = false;
                        item.DistanceToCenter = 9999f;
                        item.HitboxDesc = "";
                        _activeAttackAlerts.Add(item);
                    }
                }
            } catch {}
        }

        private bool IsAlertRelevantToPlayer(Messages.AttackAlerted msg, Vector3 playerPos, DamageableEntity attacker, string playerEntityId, CombatSystem cs, DamageableEntity combatTarget) {
            // Mode TARGET ONLY: Hanya deteksi serangan dari dino yang sedang jadi target
            if (CounterAssistTargetOnly) {
                if (combatTarget != null) {
                    // Pastikan attacker adalah dino target kita
                    bool isTargetAttacker = false;
                    if (attacker != null && attacker == combatTarget) isTargetAttacker = true;
                    else if (!string.IsNullOrEmpty(msg.EntityId) && msg.EntityId == combatTarget.GetEntityId()) isTargetAttacker = true;
                    else if (attacker != null && !string.IsNullOrEmpty(attacker.GetEntityId()) && attacker.GetEntityId() == combatTarget.GetEntityId()) isTargetAttacker = true;

                    if (!isTargetAttacker) {
                        return false; // Abaikan serangan dari dino lain di sekitar!
                    }
                    return true;
                } else {
                    // Jika player belum mengunci target tertentu, hanya terima serangan yang secara langsung mengincar player
                    if (attacker != null && !string.IsNullOrEmpty(attacker.TargetId) && !string.IsNullOrEmpty(playerEntityId)) {
                        if (attacker.TargetId == playerEntityId) return true;
                    }
                    string dummyHb;
                    if (IsPlayerInsideHitbox(msg, playerPos, attacker, out dummyHb)) {
                        return true;
                    }
                    return false;
                }
            }

            // Mode Universal (Semua Dino di Sekitar)
            if (attacker != null && !string.IsNullOrEmpty(attacker.TargetId) && !string.IsNullOrEmpty(playerEntityId)) {
                if (attacker.TargetId == playerEntityId) return true;
            }

            if (attacker != null) {
                if (cs != null && cs.Target != null && cs.Target.Value == attacker) return true;
                if (_currentCombatTarget == attacker) return true;
            }

            string dummy;
            if (IsPlayerInsideHitbox(msg, playerPos, attacker, out dummy)) {
                return true;
            }

            if (attacker != null) {
                float distToAttacker = Vector3.Distance(playerPos, attacker.GetCurrentPosition());
                if (distToAttacker <= 1800f) {
                    return true;
                }
            }

            if (msg.Center.HasValue) {
                Vector3 center = msg.Center.Value.ToClientPosition();
                float distToCenter = Vector3.Distance(playerPos, center);
                if (msg.Radius.HasValue && msg.Radius.Value > 0f) {
                    if (distToCenter <= (msg.Radius.Value + 300f)) return true;
                } else if (distToCenter <= 2000f) {
                    return true;
                }
            }

            if (attacker == null && !msg.Center.HasValue) {
                return true;
            }

            return false;
        }

        private void UpdateCounterAssistEngine() {
            if (!EnableCounterAssist) {
                _isCounterGoldenWindowActive = false;
                _currentImminentAlert = null;
                return;
            }

            try {
                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null || !localPlayer.IsAlive || localPlayer.IsRiding) {
                    _isCounterGoldenWindowActive = false;
                    _currentImminentAlert = null;
                    return;
                }

                CombatSystem cs = CombatSystem.HasInstance() ? CombatSystem.Instance() : null;

                double serverTime = 0.0;
                try {
                    if (Durango.Network.Connections.Frontend != null) {
                        serverTime = Durango.Network.Connections.Frontend.GetPredictedServerTime();
                    }
                } catch {}
                if (serverTime <= 0.0) serverTime = (double)Time.time;

                _isCounterStanceActiveFeedback = (Time.unscaledTime - _lastManualCounterTime < 0.6f);

                IncomingAttackAlert bestThreatAlert = null;
                float bestThreatTime = 999f;

                IncomingAttackAlert bestOutsideAlert = null;
                float bestOutsideTime = 999f;

                int totalHitboxes = 0;
                int threatCount = 0;

                Vector3 pPos = localPlayer.CurrentPosition;
                if (pPos == Vector3.zero) pPos = localPlayer.transform.position;
                string playerEntityId = localPlayer.EntityId;

                // Tentukan target tempur player saat ini
                DamageableEntity combatTarget = null;
                if (cs != null && cs.Target != null && cs.Target.Value != null && cs.Target.Value.IsAlive) {
                    combatTarget = cs.Target.Value;
                } else if (_currentCombatTarget != null && _currentCombatTarget.IsAlive) {
                    combatTarget = _currentCombatTarget;
                }

                lock (_activeAttackAlerts) {
                    for (int i = _activeAttackAlerts.Count - 1; i >= 0; i--) {
                        IncomingAttackAlert a = _activeAttackAlerts[i];

                        // Cek apakah dino penyerang sudah mati
                        DamageableEntity attacker = null;
                        if (!string.IsNullOrEmpty(a.AttackerId) && cs != null && cs.DamageableEntities != null) {
                            attacker = cs.DamageableEntities.Find(a.AttackerId);
                            if (attacker != null && !attacker.IsAlive) {
                                _activeAttackAlerts.RemoveAt(i);
                                continue;
                            }
                        }

                        float timeUntilHit = (float)(a.AttackTime - serverTime);

                        // Hapus alert jika serangan sudah mendarat lebih dari 0.35 detik atau sudah lewat 5 detik
                        if (timeUntilHit < -0.35f || (Time.time - a.ReceivedLocalTime) > 5.0f) {
                            _activeAttackAlerts.RemoveAt(i);
                            continue;
                        }

                        // Simpan nama penyerang jika tersedia
                        if (attacker != null && string.IsNullOrEmpty(a.AttackerName)) {
                            try {
                                a.AttackerName = attacker.GetName();
                            } catch {}
                        }

                        // Evaluasi apakah serangan relevan terhadap target yang dihadapi
                        if (IsAlertRelevantToPlayer(a.Alert, pPos, attacker, playerEntityId, cs, combatTarget)) {
                            totalHitboxes++;

                            // Evaluasi apakah serangan memiliki telegraph area merah formal
                            bool hasTelegraph = a.Alert.RectSizeHalves.HasValue || (a.Alert.Radius.HasValue && a.Alert.Radius.Value > 0f);
                            string shapeDesc = "None";
                            bool isInside = false;

                            if (hasTelegraph) {
                                isInside = IsPlayerInsideHitbox(a.Alert, pPos, attacker, out shapeDesc);
                            } else {
                                // Serangan biasa (tanpa telegraph area merah)
                                // Dianggap mengenai pemain jika dino menargetkan player dalam radius melee (<= 8 meter = 800f)
                                float distToAttacker = (attacker != null) ? Vector3.Distance(pPos, attacker.GetCurrentPosition()) : 9999f;
                                if (distToAttacker <= 800f || (attacker != null && attacker.TargetId == playerEntityId)) {
                                    isInside = true;
                                    shapeDesc = "Serangan Langsung";
                                }
                            }

                            a.HasTelegraphHitbox = hasTelegraph;
                            a.IsPlayerInside = isInside;
                            a.HitboxDesc = shapeDesc;

                            if (a.Alert.Center.HasValue) {
                                a.DistanceToCenter = Vector3.Distance(pPos, a.Alert.Center.Value.ToClientPosition());
                            }

                            // Klasifikasikan ancaman:
                            if (isInside) {
                                threatCount++;
                                if (timeUntilHit >= -0.05f && timeUntilHit < bestThreatTime) {
                                    bestThreatTime = timeUntilHit;
                                    bestThreatAlert = a;
                                }
                            } else {
                                if (timeUntilHit >= -0.05f && timeUntilHit < bestOutsideTime) {
                                    bestOutsideTime = timeUntilHit;
                                    bestOutsideAlert = a;
                                }
                            }
                        }
                    }
                }

                _totalActiveHitboxes = totalHitboxes;
                _threatHitboxesCount = threatCount;

                if (bestThreatAlert != null) {
                    // PEMAIN BERADA DI DALAM AREA MERAH (ANCAMAN LANGSUNG!)
                    _currentImminentAlert = bestThreatAlert;
                    _currentImminentTimeUntilHit = bestThreatTime;
                    _isPlayerInsideActiveHitbox = true;

                    // Golden window timing
                    bool inWindow = (bestThreatTime <= CounterAssistWindowMax && bestThreatTime >= -0.05f);

                    // Evaluasi kesiapan skill Counter vs Dodge
                    bool isOneHandCounter = IsOneHandWeapon(cs) && IsCounterStance();
                    BattleAction spaceAction = GetSpaceAction(cs);
                    bool isReady = false;
                    if (isOneHandCounter) {
                        isReady = IsCounterSkillReady(cs, spaceAction, serverTime, localPlayer);
                    } else if (spaceAction != null && cs != null) {
                        isReady = cs.IsUsableAction(spaceAction);
                    }

                    _isCounterSkillReady = isReady;
                    _isCounterGoldenWindowActive = inWindow;

                    if (inWindow && !bestThreatAlert.SoundPlayed) {
                        bestThreatAlert.SoundPlayed = true;
                        if (EnableCounterAssistSound) {
                            PlayCounterAssistSound();
                        }
                    }
                } else if (bestOutsideAlert != null) {
                    // DINO SEDANG MENYERANG, TETAPI PEMAIN BERADA DI LUAR SEMUA AREA MERAH (POSISI AMAN!)
                    _currentImminentAlert = bestOutsideAlert;
                    _currentImminentTimeUntilHit = bestOutsideTime;
                    _isPlayerInsideActiveHitbox = false;
                    _isCounterGoldenWindowActive = false; // JANGAN BUNYIKAN ALARM KELIRU UNTUK SERANGAN YANG MELESET!
                    _isCounterSkillReady = false;
                } else {
                    _currentImminentAlert = null;
                    _isCounterGoldenWindowActive = false;
                    _isCounterSkillReady = false;
                    _isPlayerInsideActiveHitbox = false;
                }
            } catch (Exception ex) {
                Debug.LogWarning("[CounterAssist] Update error: " + ex.Message);
            }
        }

        private void PlayCounterAssistSound() {
            try {
                UISound.PlayClick(UISound.ClickType.ButtonBattle);
            } catch {
                try {
                    SoundManager.PlayEvent("ui_click");
                } catch {}
            }
        }

        private static void DrawSolidRect(Rect rect, Color color) {
            if (_counterWhiteTexture == null) {
                _counterWhiteTexture = new Texture2D(1, 1);
                _counterWhiteTexture.SetPixel(0, 0, Color.white);
                _counterWhiteTexture.Apply();
            }
            Color oldCol = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _counterWhiteTexture);
            GUI.color = oldCol;
        }

        private static void DrawRectBorder(Rect rect, Color color, float thickness) {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y + thickness, thickness, Mathf.Max(0f, rect.height - thickness * 2f)), color);
            DrawSolidRect(new Rect(rect.x + rect.width - thickness, rect.y + thickness, thickness, Mathf.Max(0f, rect.height - thickness * 2f)), color);
        }

        private Rect GetCounterButtonScreenRect() {
            try {
                Transform btnTransform = null;
                Durango.UI.CombatGroup cg = GetCombatGroup();
                if (cg != null) {
                    btnTransform = cg.FindActionButton(0);
                }
                if (btnTransform == null) {
                    Durango.UI.BattleActionButtons actionButtons = UnityEngine.Object.FindObjectOfType<Durango.UI.BattleActionButtons>();
                    if (actionButtons != null) {
                        btnTransform = actionButtons.GetButton(0);
                    }
                }

                if (btnTransform != null && btnTransform.gameObject.activeInHierarchy) {
                    Camera uiCam = null;
                    UICamera uic = UICamera.FindCameraForLayer(btnTransform.gameObject.layer);
                    if (uic != null) uiCam = uic.cachedCamera;
                    if (uiCam == null) uiCam = UICamera.currentCamera;
                    if (uiCam == null) uiCam = UICamera.mainCamera;
                    if (uiCam == null) uiCam = Camera.main;

                    if (uiCam != null) {
                        UIWidget widget = btnTransform.GetComponent<UIWidget>();
                        if (widget == null) widget = btnTransform.GetComponentInChildren<UIWidget>();

                        if (widget != null) {
                            Vector3[] corners = widget.worldCorners;
                            if (corners != null && corners.Length == 4) {
                                Vector3 bl = uiCam.WorldToScreenPoint(corners[0]);
                                Vector3 tr = uiCam.WorldToScreenPoint(corners[2]);
                                float x = Mathf.Min(bl.x, tr.x);
                                float y = Screen.height - Mathf.Max(bl.y, tr.y);
                                float w = Mathf.Abs(tr.x - bl.x);
                                float h = Mathf.Abs(tr.y - bl.y);
                                if (w > 20f && h > 20f) {
                                    return new Rect(x, y, w, h);
                                }
                            }
                        }

                        Vector3 sp = uiCam.WorldToScreenPoint(btnTransform.position);
                        float btnSize = 85f;
                        return new Rect(sp.x - btnSize * 0.5f, Screen.height - sp.y - btnSize * 0.5f, btnSize, btnSize);
                    }
                }
            } catch {}

            float defSize = 80f;
            return new Rect(Screen.width - 230f, Screen.height - 105f, defSize, defSize);
        }

        private void RenderCounterAssistOverlay() {
            if (!EnableCounterAssist) return;

            // Jika tidak ada ancaman aktif dan tidak sedang guarding, tidak perlu render
            if (_currentImminentAlert == null && !_isCounterGoldenWindowActive && !_isCounterStanceActiveFeedback) return;

            try {
                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null || !localPlayer.IsAlive || localPlayer.IsRiding) return;

                CombatSystem cs = CombatSystem.HasInstance() ? CombatSystem.Instance() : null;

                bool isOneHandCounter = IsOneHandWeapon(cs) && IsCounterStance();
                string actionName = isOneHandCounter ? "COUNTER" : "DODGE";

                Rect btnRect = GetCounterButtonScreenRect();

                // 1. VISUAL DI ATAS / SEKITAR TOMBOL COUNTER (ACTION SLOT 0)
                if (_isCounterGoldenWindowActive) {
                    float pulse = Mathf.PingPong(Time.unscaledTime * 8f, 1f);

                    Color flashCol;
                    string hexCol;
                    string promptText;

                    if (_isCounterSkillReady) {
                        // KESIAPAN OK: Flash Emas / Cyan
                        flashCol = Color.Lerp(new Color(1f, 0.85f, 0.1f, 1f), new Color(0.1f, 1f, 1f, 1f), pulse);
                        hexCol = (pulse > 0.5f) ? "00ffff" : "f1c40f";
                        promptText = string.Format("⚡ [SPASI] {0}! ({1:F2}s) ⚡", actionName, Mathf.Max(0f, _currentImminentTimeUntilHit));
                    } else {
                        // SKILL COOLDOWN / KURANG STAMINA: Flash Oranye / Merah
                        flashCol = Color.Lerp(new Color(1f, 0.4f, 0.1f, 1f), new Color(1f, 0.15f, 0.15f, 1f), pulse);
                        hexCol = (pulse > 0.5f) ? "e67e22" : "e74c3c";
                        promptText = string.Format("⚠️ [SPASI] DODGE! (CD) ({0:F2}s) ⚠️", Mathf.Max(0f, _currentImminentTimeUntilHit));
                    }

                    DrawRectBorder(new Rect(btnRect.x - 4f - pulse * 3f, btnRect.y - 4f - pulse * 3f, btnRect.width + 8f + pulse * 6f, btnRect.height + 8f + pulse * 6f), flashCol, 3f);
                    DrawRectBorder(new Rect(btnRect.x - 8f, btnRect.y - 8f, btnRect.width + 16f, btnRect.height + 16f), new Color(flashCol.r, flashCol.g, flashCol.b, 0.35f), 2f);

                    DrawSolidRect(btnRect, new Color(flashCol.r, flashCol.g, flashCol.b, 0.22f));

                    float badgeW = btnRect.width + 60f;
                    float badgeH = 26f;
                    Rect badgeRect = new Rect(btnRect.x + (btnRect.width - badgeW) * 0.5f, btnRect.y - badgeH - 6f, badgeW, badgeH);
                    DrawSolidRect(badgeRect, new Color(0.08f, 0.10f, 0.15f, 0.94f));
                    DrawRectBorder(badgeRect, flashCol, 2f);
                    GUI.Label(badgeRect, string.Format("<b><color=#{0}>{1}</color></b>", hexCol, promptText), _counterBadgeStyle ?? GUI.skin.label);
                } else if (_isCounterStanceActiveFeedback) {
                    Color greenCol = new Color(0.18f, 0.85f, 0.44f, 0.95f);
                    DrawRectBorder(btnRect, greenCol, 3f);
                    DrawSolidRect(btnRect, new Color(greenCol.r, greenCol.g, greenCol.b, 0.20f));

                    float badgeW = btnRect.width + 50f;
                    float badgeH = 24f;
                    Rect badgeRect = new Rect(btnRect.x + (btnRect.width - badgeW) * 0.5f, btnRect.y - badgeH - 6f, badgeW, badgeH);
                    DrawSolidRect(badgeRect, new Color(0.08f, 0.14f, 0.10f, 0.94f));
                    DrawRectBorder(badgeRect, greenCol, 2f);
                    GUI.Label(badgeRect, "<b><color=#2ecc71>🛡️ SIKAP MELAWAN AKTIF</color></b>", _counterBadgeStyle ?? GUI.skin.label);
                } else if (_currentImminentAlert != null && _currentImminentTimeUntilHit > CounterAssistWindowMax && _currentImminentTimeUntilHit < 4.0f) {
                    Color amberCol = new Color(0.95f, 0.61f, 0.07f, 0.75f);
                    DrawRectBorder(btnRect, amberCol, 2f);

                    float badgeW = btnRect.width + 30f;
                    float badgeH = 22f;
                    Rect badgeRect = new Rect(btnRect.x + (btnRect.width - badgeW) * 0.5f, btnRect.y - badgeH - 5f, badgeW, badgeH);
                    DrawSolidRect(badgeRect, new Color(0.12f, 0.10f, 0.08f, 0.88f));
                    DrawRectBorder(badgeRect, amberCol, 1.5f);
                    GUI.Label(badgeRect, string.Format("<size=10><b><color=#f39c12>⌛ SIAGA ({0:F1}s)</color></b></size>", _currentImminentTimeUntilHit), _counterBadgeStyle ?? GUI.skin.label);
                }

                // 2. HUD ALERT BANNER (Ditempatkan pas di bawah karakter jika CounterAssistUnderCharacter aktif)
                if (EnableCounterAssistCenterHUD) {
                    float bannerW = 290f;
                    float bannerH = 42f;
                    float bannerX = (Screen.width - bannerW) * 0.5f;
                    float bannerY = Screen.height - 185f;

                    // Posisi dinamis pas di bawah karakter
                    if (CounterAssistUnderCharacter) {
                        Camera cam = Camera.main;
                        if (cam == null) cam = Camera.current;
                        if (cam == null) {
                            Camera[] cams = Camera.allCameras;
                            if (cams != null && cams.Length > 0) cam = cams[0];
                        }

                        if (cam != null) {
                            Vector3 feetWorldPos = localPlayer.transform.position;
                            if (feetWorldPos == Vector3.zero && localPlayer.CurrentPosition != Vector3.zero) {
                                feetWorldPos = localPlayer.CurrentPosition;
                            }

                            Vector3 sp = cam.WorldToScreenPoint(feetWorldPos);
                            if (sp.z > 0.1f) {
                                float sx = sp.x;
                                float sy = Screen.height - sp.y; // Inversi Y untuk GUI coordinate

                                bannerX = sx - bannerW * 0.5f;
                                bannerY = sy + 18f; // Pas di bawah kaki karakter!

                                // Batasi agar banner tidak terpotong di tepi layar
                                bannerX = Mathf.Clamp(bannerX, 10f, Screen.width - bannerW - 10f);
                                bannerY = Mathf.Clamp(bannerY, 10f, Screen.height - bannerH - 10f);
                            }
                        }
                    }

                    Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);

                    // Ambil nama dino penyerang/target jika ada
                    string targetDinoName = "Dinosaurus";
                    if (cs != null && cs.Target != null && cs.Target.Value != null) {
                        try {
                            string dn = cs.Target.Value.GetName();
                            if (!string.IsNullOrEmpty(dn)) targetDinoName = dn;
                        } catch {}
                    } else if (_currentCombatTarget != null) {
                        try {
                            string dn = _currentCombatTarget.GetName();
                            if (!string.IsNullOrEmpty(dn)) targetDinoName = dn;
                        } catch {}
                    } else if (_currentImminentAlert != null && !string.IsNullOrEmpty(_currentImminentAlert.AttackerName)) {
                        targetDinoName = _currentImminentAlert.AttackerName;
                    }

                    if (_isCounterGoldenWindowActive) {
                        float pulse = Mathf.PingPong(Time.unscaledTime * 8f, 1f);

                        Color flashCol;
                        string hexCol;
                        string titleText;
                        string subText;

                        string areaTag = (_totalActiveHitboxes > 1) ? string.Format(" [Area {0}/{1}]", _threatHitboxesCount, _totalActiveHitboxes) : "";

                        if (_isCounterSkillReady) {
                            flashCol = Color.Lerp(new Color(1f, 0.85f, 0.1f, 1f), new Color(0.1f, 1f, 1f, 1f), pulse);
                            hexCol = (pulse > 0.5f) ? "00ffff" : "f1c40f";
                            titleText = string.Format("⚡ [SPASI] {0}! ({1:F2}s){2} ⚡", actionName, Mathf.Max(0f, _currentImminentTimeUntilHit), areaTag);
                            subText = string.Format("Target: <b>{0}</b> (Posisi Anda dalam jangkauan!)", targetDinoName);
                        } else {
                            flashCol = Color.Lerp(new Color(1f, 0.4f, 0.1f, 1f), new Color(1f, 0.15f, 0.15f, 1f), pulse);
                            hexCol = (pulse > 0.5f) ? "e67e22" : "e74c3c";
                            titleText = string.Format("⚠️ [SPASI] DODGE ROLL! (CD) ({0:F2}s){1} ⚠️", Mathf.Max(0f, _currentImminentTimeUntilHit), areaTag);
                            subText = string.Format("Target: <b>{0}</b> (Segera menghindar!)", targetDinoName);
                        }

                        DrawSolidRect(bannerRect, new Color(0.08f, 0.10f, 0.14f, 0.94f));
                        DrawRectBorder(bannerRect, flashCol, 2.5f);

                        GUI.Label(new Rect(bannerX, bannerY + 2f, bannerW, 20f), string.Format("<b><color=#{0}>{1}</color></b>", hexCol, titleText), _counterHudTitleStyle ?? GUI.skin.label);
                        GUI.Label(new Rect(bannerX, bannerY + 20f, bannerW, 16f), string.Format("<size=10><color=#ecf0f1>{0}</color></size>", subText), _counterHudSubStyle ?? GUI.skin.label);

                        float progress = Mathf.Clamp01(1f - (_currentImminentTimeUntilHit / Mathf.Max(0.1f, CounterAssistWindowMax)));
                        DrawSolidRect(new Rect(bannerX + 2f, bannerY + bannerH - 4f, (bannerW - 4f) * progress, 3f), flashCol);
                    } else if (_isCounterStanceActiveFeedback) {
                        Color greenCol = new Color(0.18f, 0.85f, 0.44f, 0.95f);
                        DrawSolidRect(bannerRect, new Color(0.08f, 0.14f, 0.10f, 0.92f));
                        DrawRectBorder(bannerRect, greenCol, 2f);
                        GUI.Label(bannerRect, "<b><color=#2ecc71>🛡️ SIKAP MELAWAN AKTIF</color></b>", _counterHudCenterStyle ?? GUI.skin.label);
                    } else if (_currentImminentAlert != null && !_isPlayerInsideActiveHitbox) {
                        // POSISI AMAN (DI LUAR AREA MERAH DINO)
                        Color safeCol = new Color(0.18f, 0.85f, 0.44f, 0.90f);
                        DrawSolidRect(bannerRect, new Color(0.08f, 0.14f, 0.10f, 0.94f));
                        DrawRectBorder(bannerRect, safeCol, 2f);

                        string safeTitle = (_totalActiveHitboxes > 1) 
                            ? string.Format("🛡️ POSISI AMAN (DI LUAR {0} AREA MERAH)", _totalActiveHitboxes)
                            : "🛡️ POSISI AMAN (DI LUAR AREA SERANG)";

                        GUI.Label(new Rect(bannerX, bannerY + 3f, bannerW, 19f), string.Format("<b><color=#2ecc71>{0}</color></b>", safeTitle), _counterHudTitleStyle ?? GUI.skin.label);
                        GUI.Label(new Rect(bannerX, bannerY + 20f, bannerW, 16f), string.Format("<size=10><color=#bdc3c7>Serangan {0} akan meleset ({1:F1}s)</color></size>", targetDinoName, _currentImminentTimeUntilHit), _counterHudSubStyle ?? GUI.skin.label);

                        float totalSpan = 2.0f;
                        float progress = Mathf.Clamp01(1f - (_currentImminentTimeUntilHit / totalSpan));
                        DrawSolidRect(new Rect(bannerX + 2f, bannerY + bannerH - 4f, (bannerW - 4f) * progress, 3f), safeCol);
                    } else if (_currentImminentAlert != null && _currentImminentTimeUntilHit > CounterAssistWindowMax && _currentImminentTimeUntilHit < 4.0f) {
                        // FASE 1: PEMAIN DI DALAM AREA MERAH (SIAP-SIAP)
                        Color amberCol = new Color(0.95f, 0.61f, 0.07f, 0.80f);
                        DrawSolidRect(bannerRect, new Color(0.12f, 0.10f, 0.08f, 0.90f));
                        DrawRectBorder(bannerRect, amberCol, 1.5f);

                        string prepAreaTag = (_totalActiveHitboxes > 1) ? string.Format(" [Area {0}/{1}]", _threatHitboxesCount, _totalActiveHitboxes) : "";
                        GUI.Label(new Rect(bannerX, bannerY + 3f, bannerW, 19f), string.Format("<b><color=#f39c12>⌛ {0} BERSIAP ({1:F1}s){2}</color></b>", targetDinoName, _currentImminentTimeUntilHit, prepAreaTag), _counterHudTitleStyle ?? GUI.skin.label);
                        GUI.Label(new Rect(bannerX, bannerY + 20f, bannerW, 16f), "<size=10><color=#bdc3c7>⚠️ Posisi Anda di area serangan! Siap Counter...</color></size>", _counterHudSubStyle ?? GUI.skin.label);

                        float totalSpan = 2.0f;
                        float progress = Mathf.Clamp01(1f - (_currentImminentTimeUntilHit / totalSpan));
                        DrawSolidRect(new Rect(bannerX + 2f, bannerY + bannerH - 4f, (bannerW - 4f) * progress, 3f), amberCol);
                    }
                }
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
                _combatStatusText = "Auto Combat Aktif: Mencari musuh...";
                _lastCombatScanTime = 0f;
            }
        }

        private void CycleCombatToggleKey() {
            if (CombatToggleKey == KeyCode.F8) CombatToggleKey = KeyCode.F9;
            else if (CombatToggleKey == KeyCode.F9) CombatToggleKey = KeyCode.F10;
            else if (CombatToggleKey == KeyCode.F10) CombatToggleKey = KeyCode.G;
            else if (CombatToggleKey == KeyCode.G) CombatToggleKey = KeyCode.H;
            else if (CombatToggleKey == KeyCode.H) CombatToggleKey = KeyCode.F8;
            else CombatToggleKey = KeyCode.F8;

            if (!IsCombatBotEnabled) {

                _combatStatusText = string.Format("Standby (Tekan [{0}] untuk Aktifkan Combat)", CombatToggleKey);

            }

        }


        private CombatSystem _lastHookedCombatSystem = null;

        private void TryHookCombatAlert() {
            try {
                if (CombatSystem.HasInstance()) {
                    CombatSystem cs = CombatSystem.Instance();
                    if (cs != null && cs != _lastHookedCombatSystem) {
                        if (_lastHookedCombatSystem != null) {
                            try { _lastHookedCombatSystem.AttackAlerted -= OnEnemyAttackAlerted; } catch {}
                        }
                        cs.AttackAlerted += OnEnemyAttackAlerted;
                        _lastHookedCombatSystem = cs;
                        _isCombatAlertHooked = true;
                        Debug.Log("[AutoGatherMod] Hook CombatSystem.AttackAlerted BERHASIL untuk instance aktif!");
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
            RecordCounterAssistAlert(msg);

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

        private bool IsPetOrFriendly(DamageableEntity de) {
            if (de == null) return true;
            try {
                if (de is PetDamageableEntity) return true;
                string eid = de.GetEntityId();
                if (PetManager.HasInstance() && !string.IsNullOrEmpty(eid)) {
                    if (eid == PetManager.Instance().GetPlayerPetId()) return true;
                }
                GameObject go = de.GameObject;
                if (go != null) {
                    if (PlayerBehavior.LocalPlayer != null && go == PlayerBehavior.LocalPlayer.gameObject) return true;
                    if (go.GetComponent<PetAI>() != null || go.GetComponent<GrazingPetAI>() != null || go.GetComponent<VehiclePet>() != null) return true;
                }
                if (CombatSystem.HasInstance()) {
                    CombatSystem cs = CombatSystem.Instance();
                    if (cs != null && cs.DamageableEntities != null) {
                        var allies = cs.DamageableEntities.GetAllies();
                        if (allies != null) {
                            foreach (var ally in allies) {
                                if (ally != null && ally.GetEntityId() == eid) return true;
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }

        private bool IsAnimalPetOrFriendly(AnimalBehavior a) {
            if (a == null) return true;
            try {
                GameObject go = a.gameObject;
                if (go != null) {
                    if (PlayerBehavior.LocalPlayer != null && go == PlayerBehavior.LocalPlayer.gameObject) return true;
                    if (go.GetComponent<PetAI>() != null || go.GetComponent<GrazingPetAI>() != null || go.GetComponent<VehiclePet>() != null) return true;
                }
                string eid = a.EntityId;
                if (PetManager.HasInstance() && !string.IsNullOrEmpty(eid)) {
                    if (eid == PetManager.Instance().GetPlayerPetId()) return true;
                }
                if (CombatSystem.HasInstance()) {
                    CombatSystem cs = CombatSystem.Instance();
                    if (cs != null && cs.DamageableEntities != null) {
                        var allies = cs.DamageableEntities.GetAllies();
                        if (allies != null) {
                            foreach (var ally in allies) {
                                if (ally != null && ally.GetEntityId() == eid) return true;
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }

        private DamageableEntity FindCombatTarget(CombatSystem cs, PlayerBehavior player, float maxDist) {

            Vector3 pPos = player.CurrentPosition;

            if (pPos == Vector3.zero) pPos = player.transform.position;

            // 1. Cek target yang sedang dikunci (bukan pet/ally)
            if (cs.Target != null && cs.Target.Value != null && cs.Target.Value.IsAlive) {
                if (!IsPetOrFriendly(cs.Target.Value)) {
                    return cs.Target.Value;
                }
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
                        if (IsPetOrFriendly(enemy)) continue; // JANGAN SERANG PET / ALLY!

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
                        if (IsAnimalPetOrFriendly(a)) continue; // JANGAN SERANG PET / ALLY / MOUNT!

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

            if (_currentCombatTarget == null || !_currentCombatTarget.IsAlive || IsPetOrFriendly(_currentCombatTarget)) {

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
