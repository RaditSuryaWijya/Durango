using System;
using System.Collections.Generic;
using UnityEngine;
using Durango.Logic.Combat;
using Shared.Battle;
using XDLands.Stance;

namespace XDLands.CombatMods
{
    public class AutoCounterRunner : MonoBehaviour
    {
        private static AutoCounterRunner _instance;

        public static void Ensure()
        {
            if (_instance == null)
            {
                try
                {
                    GameObject go = new GameObject("XDLands_AutoCounterRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AutoCounterRunner>();
                    Debug.Log("[AutoCounter] AutoCounterRunner initialized successfully.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AutoCounter] AutoCounterRunner.Ensure error: " + ex.Message);
                }
            }
        }

        private class QueuedAlert
        {
            public string AttackerId;
            public float TriggerTime; // Time.time when to execute counter
            public double AttackTime;
        }

        private static readonly List<QueuedAlert> _queue = new List<QueuedAlert>();

        public static void QueueCounter(string attackerId, float delay, double attackTime)
        {
            Ensure();
            float targetTime = Time.time + delay;
            for (int i = 0; i < _queue.Count; i++)
            {
                if (string.Equals(_queue[i].AttackerId, attackerId, StringComparison.OrdinalIgnoreCase))
                {
                    _queue[i].TriggerTime = targetTime;
                    _queue[i].AttackTime = attackTime;
                    return;
                }
            }
            _queue.Add(new QueuedAlert { AttackerId = attackerId, TriggerTime = targetTime, AttackTime = attackTime });
        }

        private void Update()
        {
            if (_queue.Count == 0) return;

            float now = Time.time;
            List<string> readyAttackers = null;

            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (now >= _queue[i].TriggerTime)
                {
                    if (readyAttackers == null) readyAttackers = new List<string>();
                    readyAttackers.Add(_queue[i].AttackerId);
                    _queue.RemoveAt(i);
                }
            }

            if (readyAttackers != null)
            {
                for (int i = 0; i < readyAttackers.Count; i++)
                {
                    AutoCounter.TryCounter(readyAttackers[i], "PerfectTimingAlert");
                }
            }
        }
    }

    public static class AutoCounter
    {
        private static float _lastCounterTime = 0f;
        public static float LeadTime = 0.22f; // Trigger counter 0.22s before attack impact

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

            // 1. Slot 0 is the Space key action in Durango HUD (battle_dodge / onehand_dodge / xd_counter)
            if (cs.ActionSlots != null && cs.ActionSlots.Length > 0)
            {
                BattleAction slot0 = cs.ActionSlots[0];
                if (slot0 != null && slot0.Data != null)
                {
                    return slot0;
                }
            }

            // 2. Search all actions for Counter / Dodge / Roll / Evade
            var actions = cs.GetCurrentBattleActions();
            if (actions != null)
            {
                foreach (var act in actions)
                {
                    if (act == null || act.Data == null) continue;
                    if (!string.IsNullOrEmpty(act.Data.Id) &&
                        (act.Data.Id.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("roll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         act.Data.Id.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return act;
                    }
                    if (act.Data.Meta != null && act.Data.Meta.BattleActionType == BattleActionType.Dodge)
                    {
                        return act;
                    }
                }
            }

            return null;
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

                if (attacker != null && attacker.IsAlive)
                {
                    float currentDist = Vector3.Distance(attacker.GetCurrentPosition(), localPlayer.CurrentPosition);
                    // Dino must be within plausible combat range (12 meters)
                    if (currentDist > 1200f)
                    {
                        Debug.Log(string.Format("[AutoCounter] Attacker is too far ({0:F1}m), skip counter.", currentDist / 100f));
                        return;
                    }

                    // Turn character towards attacker and lock target
                    Vector3 diff = attacker.GetCurrentPosition() - localPlayer.CurrentPosition;
                    diff.y = 0f;
                    if (diff.sqrMagnitude > 0.01f)
                    {
                        float yaw = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                        localPlayer.TurnToYaw(yaw, true);
                    }
                    cs.SelectTarget(attacker);
                }

                // Counterattack in Counter Stance (Sikap Melawan) is strictly the SPACE KEY action (xd_counter / onehand_dodge / slot 0)
                BattleAction spaceAction = GetSpaceAction(cs);
                if (spaceAction != null)
                {
                    if (cs.IsUsableAction(spaceAction))
                    {
                        _lastCounterTime = Time.unscaledTime;
                        cs.UseBattleAction(spaceAction.Data.Id);
                        Debug.Log(string.Format("[AutoCounter] Serangan Balik (Space / {0}) executed! Trigger: {1}",
                            spaceAction.Data.Id, triggerReason));
                        try { UIManager.SystemMsg("XdStance", "Serangan Balik (Spasi)!", 1.5f); } catch { }
                    }
                    else
                    {
                        Debug.Log(string.Format("[AutoCounter] Space action ({0}) is currently not usable.", spaceAction.Data.Id));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] TryCounter error: " + ex.Message);
            }
        }

        public static void OnAttackAlert(string attackerId, double attackTime, double eventAt)
        {
            try
            {
                if (!IsOneHandWeapon() || !IsCounterStance())
                    return;

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
                if (attacker == null || !attacker.IsAlive)
                    return;

                // 25 meters detection range for attack alert (charging dinos start from afar)
                float dist = Vector3.Distance(attacker.GetCurrentPosition(), localPlayer.CurrentPosition);
                if (dist > 2500f)
                    return;

                double serverTime = 0;
                try
                {
                    if (Durango.Network.Connections.Frontend != null)
                    {
                        serverTime = Durango.Network.Connections.Frontend.GetPredictedServerTime();
                    }
                }
                catch { }

                float timeUntilImpact = 0f;
                if (serverTime > 0 && attackTime > serverTime)
                {
                    timeUntilImpact = (float)(attackTime - serverTime);
                }
                else if (attackTime > eventAt && eventAt > 0)
                {
                    timeUntilImpact = (float)(attackTime - eventAt);
                }

                float delay = timeUntilImpact - LeadTime;

                Debug.Log(string.Format("[AutoCounter] OnAttackAlert: Attacker={0}, Dist={1:F1}m, ImpactIn={2:F2}s, Delay={3:F2}s",
                    attackerId, dist / 100f, timeUntilImpact, delay));

                if (delay <= 0.05f)
                {
                    // Attack is hitting right now (or within lead window), counter immediately!
                    TryCounter(attackerId, "ImmediateAlert");
                }
                else
                {
                    // Delay execution so the counter parry window is active right at impact time!
                    AutoCounterRunner.QueueCounter(attackerId, delay, attackTime);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] OnAttackAlert error: " + ex.Message);
            }
        }

        public static void OnAttackAlert(string attackerId)
        {
            OnAttackAlert(attackerId, 0.0, 0.0);
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
                    // Note: OnDamaged does not trigger TryCounter because damage was already applied.
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoCounter] OnDamaged error: " + ex.Message);
            }
        }
    }
}
