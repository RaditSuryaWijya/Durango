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
                // 1. Check stance weapon class
                string wc = XdStance.WeaponClass;
                if (!string.IsNullOrEmpty(wc))
                {
                    if (wc.IndexOf("onehand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        wc.IndexOf("1hand", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                    return false;
                }

                // 2. Fallback: check active actions in combat system
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
                Debug.LogWarning("[AutoCounter] IsOneHandWeapon check error: " + ex.Message);
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

        public static BattleAction GetDodgeAction(CombatSystem cs)
        {
            if (cs == null) return null;

            if (cs.ActionSlots != null)
            {
                for (int i = 0; i < cs.ActionSlots.Length; i++)
                {
                    BattleAction act = cs.ActionSlots[i];
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

        public static BattleAction GetCounterAttackAction(CombatSystem cs, PlayerBehavior player)
        {
            if (cs == null) return null;

            BattleAction basicAttack = null;
            BattleAction specialSkill = null;

            // Prioritize HUD ActionSlots
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

            // Fallback: check all current actions
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

        public static void TryCounter(string attackerId, bool isPreEmptive)
        {
            try
            {
                if (!IsOneHandWeapon())
                    return;

                if (!IsCounterStance())
                    return;

                if (Time.unscaledTime - _lastCounterTime < 0.25f)
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

                // Turn to face attacker and select as target
                if (attacker != null && attacker.IsAlive)
                {
                    Vector3 diff = attacker.GetCurrentPosition() - localPlayer.CurrentPosition;
                    diff.y = 0f;
                    if (diff.sqrMagnitude > 0.01f)
                    {
                        float yaw = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                        localPlayer.TurnToYaw(yaw, true);
                    }
                    cs.SelectTarget(attacker);
                }

                // Strategy 1: Pre-emptive dodge roll (iframe) when enemy winds up attack
                if (isPreEmptive)
                {
                    BattleAction dodge = GetDodgeAction(cs);
                    if (dodge != null && cs.IsUsableAction(dodge))
                    {
                        _lastCounterTime = Time.unscaledTime;
                        cs.UseBattleAction(dodge.Data.Id);
                        Debug.Log("[AutoCounter] Auto-Counter Dodge executed: " + dodge.Data.Id);
                        try { UIManager.SystemMsg("XdStance", "Serangan Balik (Dodge)!", 2f); } catch { }
                        return;
                    }
                }

                // Strategy 2: Strike back with counterattack skill or combo
                BattleAction attack = GetCounterAttackAction(cs, localPlayer);
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
                    Debug.Log("[AutoCounter] Auto-Counter Strike executed: " + attack.Data.Id + " against " + (attackerId ?? "target"));
                    try { UIManager.SystemMsg("XdStance", "Serangan Balik!", 2f); } catch { }
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
                    TryCounter(attackerId, false);
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
                    float distSqr = (attacker.GetCurrentPosition() - localPlayer.CurrentPosition).sqrMagnitude;
                    // within 7 meters (threat range)
                    if (distSqr <= 49f)
                    {
                        Debug.Log(string.Format("[AutoCounter] OnAttackAlert: Attacker={0}, Dist={1:F1}m, OneHand={2}, Stance={3}",
                            attackerId, Mathf.Sqrt(distSqr), IsOneHandWeapon(), XdStance.Current));
                        TryCounter(attackerId, true);
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
