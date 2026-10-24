using System;
using System.Collections.Generic;
using UnityEngine;
using Durango.Logic.Combat;
using Shared.Battle;
using XDLands.Stance;

namespace XDLands.CombatMods
{
    public static class AutoCounter
    {
        private static float _lastCounterTime = 0f;

        public static bool IsOneHandWeapon()
        {
            try
            {
                string wc = XdStance.WeaponClass;
                if (!string.IsNullOrEmpty(wc))
                {
                    return wc.IndexOf("onehand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           wc.IndexOf("1hand", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (GameSystem<CombatSystem>.HasInstance())
                {
                    CombatSystem cs = GameSystem<CombatSystem>.Instance();
                    if (cs != null)
                    {
                        var actions = cs.GetCurrentBattleActions();
                        if (actions != null)
                        {
                            foreach (var act in actions)
                            {
                                if (act != null && act.Data != null && !string.IsNullOrEmpty(act.Data.Id))
                                {
                                    if (act.Data.Id.IndexOf("onehand", StringComparison.OrdinalIgnoreCase) >= 0)
                                        return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] IsOneHandWeapon error: " + ex.Message);
            }
            return false;
        }

        public static bool IsCounterStance()
        {
            try
            {
                string cur = XdStance.Current;
                if (string.IsNullOrEmpty(cur))
                    return false;

                // "melee_outfighting" = "Sikap Melawan" (Counter Stance)
                return cur.IndexOf("outfighting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       cur.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       cur.IndexOf("melawan", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static BattleAction GetSpaceAction(CombatSystem cs)
        {
            if (cs == null) return null;

            // 1. Slot 0 is the Space key action in Durango HUD (battle_dodge / onehand_dodge)
            if (cs.ActionSlots != null && cs.ActionSlots.Length > 0)
            {
                BattleAction slot0 = cs.ActionSlots[0];
                if (slot0 != null && slot0.Data != null)
                {
                    if (cs.IsUsableAction(slot0))
                        return slot0;
                }
            }

            // 2. Search all actions for Dodge / Roll / Evade
            var actions = cs.GetCurrentBattleActions();
            if (actions != null)
            {
                foreach (var act in actions)
                {
                    if (act == null || act.Data == null) continue;
                    if (act.Data.Meta != null && act.Data.Meta.BattleActionType == BattleActionType.Dodge)
                    {
                        if (cs.IsUsableAction(act)) return act;
                    }
                    if (!string.IsNullOrEmpty(act.Data.Id) &&
                        (act.Data.Id.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("roll", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        if (cs.IsUsableAction(act)) return act;
                    }
                }
            }

            return null;
        }

        public static BattleAction GetAttackAction(CombatSystem cs, PlayerBehavior player)
        {
            if (cs == null) return null;

            BattleAction basicAttack = null;
            BattleAction specialSkill = null;

            if (cs.ActionSlots != null && cs.ActionSlots.Length > 0)
            {
                for (int i = 0; i < cs.ActionSlots.Length; i++)
                {
                    BattleAction act = cs.ActionSlots[i];
                    if (act == null || act.Data == null || act.Data.Meta == null) continue;

                    if (act.Data.Meta.BattleActionType == BattleActionType.Dodge) continue;
                    if (!string.IsNullOrEmpty(act.Data.Id) &&
                        (act.Data.Id.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("roll", StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    if (cs.IsUsableAction(act))
                    {
                        if (i > 0 && specialSkill == null)
                        {
                            specialSkill = act;
                        }
                        else if (i == 0 && basicAttack == null)
                        {
                            basicAttack = act;
                        }
                    }
                }
            }

            if (specialSkill != null) return specialSkill;
            if (basicAttack != null) return basicAttack;

            var actions = cs.GetCurrentBattleActions();
            if (actions != null)
            {
                foreach (var act in actions)
                {
                    if (act == null || act.Data == null || act.Data.Meta == null) continue;
                    if (act.Data.Meta.BattleActionType == BattleActionType.Dodge) continue;
                    if (!string.IsNullOrEmpty(act.Data.Id) &&
                        (act.Data.Id.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("roll", StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    if (cs.IsUsableAction(act))
                    {
                        if (!string.IsNullOrEmpty(act.Data.Id) &&
                            (act.Data.Id.IndexOf("smash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             act.Data.Id.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             act.Data.Id.IndexOf("flurry", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            return act;
                        }
                        if (basicAttack == null) basicAttack = act;
                    }
                }
            }

            return basicAttack;
        }

        public static void TryCounter(string attackerId, string triggerReason)
        {
            try
            {
                if (!IsOneHandWeapon())
                    return;

                if (!IsCounterStance())
                    return;

                if (Time.unscaledTime - _lastCounterTime < 0.2f)
                    return;

                if (!GameSystem<CombatSystem>.HasInstance())
                    return;

                CombatSystem cs = GameSystem<CombatSystem>.Instance();
                if (cs == null)
                    return;

                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null)
                    return;

                DamageableEntity attacker = null;
                if (!string.IsNullOrEmpty(attackerId) && cs.DamageableEntities != null)
                {
                    bool found;
                    attacker = cs.DamageableEntities.Get(attackerId, out found);
                }

                // Face attacker and lock target
                if (attacker != null && attacker.IsAlive)
                {
                    Vector3 diff = attacker.GetCurrentPosition() - localPlayer.CurrentPosition;
                    diff.y = 0f;
                    if (diff.sqrMagnitude > 1f)
                    {
                        float yaw = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                        localPlayer.TurnToYaw(yaw, true);
                    }
                    cs.SelectTarget(attacker);
                }

                // PRIMARY ACTION: The SPACE KEY action (onehand_dodge / slot 0)
                // "serangan balik itu pake spasi"
                BattleAction spaceAction = GetSpaceAction(cs);
                if (spaceAction != null && cs.IsUsableAction(spaceAction))
                {
                    _lastCounterTime = Time.unscaledTime;
                    cs.UseBattleAction(spaceAction.Data.Id);
                    Debug.Log(string.Format("[AutoCounter] Serangan Balik (Space / {0}) executed! Trigger: {1}",
                        spaceAction.Data.Id, triggerReason));
                    try { UIManager.SystemMsg("XdStance", "Serangan Balik (Spasi)!", 2f); } catch { }
                    return;
                }

                // SECONDARY ACTION: If Space is on cooldown, retaliate with attack skill
                BattleAction attack = GetAttackAction(cs, localPlayer);
                if (attack != null && cs.IsUsableAction(attack))
                {
                    _lastCounterTime = Time.unscaledTime;
                    if (attacker != null && attacker.IsAlive)
                    {
                        cs.UseBattleAction(attack.Data.Id, attacker, true);
                    }
                    else
                    {
                        cs.UseBattleAction(attack.Data.Id);
                    }
                    Debug.Log(string.Format("[AutoCounter] Counter Follow-up ({0}) executed! Trigger: {1}",
                        attack.Data.Id, triggerReason));
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] TryCounter error: " + ex.Message);
            }
        }

        public static void OnDamaged(string attackerId, string victimId)
        {
            try
            {
                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null)
                    return;

                if (!string.IsNullOrEmpty(victimId) && string.Equals(victimId, localPlayer.EntityId, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log(string.Format("[AutoCounter] OnDamaged: Victim={0}, Attacker={1}, OneHand={2}, Stance={3}",
                        victimId, attackerId, IsOneHandWeapon(), XdStance.Current));
                    TryCounter(attackerId, "OnDamaged");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] OnDamaged error: " + ex.Message);
            }
        }

        public static void OnAttackAlert(string attackerId)
        {
            try
            {
                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null || string.IsNullOrEmpty(attackerId))
                    return;

                if (string.Equals(attackerId, localPlayer.EntityId, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!GameSystem<CombatSystem>.HasInstance())
                    return;

                CombatSystem cs = GameSystem<CombatSystem>.Instance();
                if (cs == null || cs.DamageableEntities == null)
                    return;

                bool found;
                DamageableEntity attacker = cs.DamageableEntities.Get(attackerId, out found);
                if (attacker != null && attacker.IsAlive)
                {
                    // 100 units = 1 meter in Durango. 800 units = 8 meters.
                    float dist = Vector3.Distance(attacker.GetCurrentPosition(), localPlayer.CurrentPosition);
                    if (dist <= 800f)
                    {
                        Debug.Log(string.Format("[AutoCounter] OnAttackAlert: Attacker={0}, Dist={1:F1}m, OneHand={2}, Stance={3}",
                            attackerId, dist / 100f, IsOneHandWeapon(), XdStance.Current));
                        TryCounter(attackerId, "OnAttackAlert");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] OnAttackAlert error: " + ex.Message);
            }
        }
    }
}
