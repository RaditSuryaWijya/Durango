using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Durango.Logic.Combat;
using Shared.Region;

namespace AutoGather {
    public partial class AutoGatherController {
        // =========================================================================
        // ISLAND SWEEP & HAZARD / AGGRO NAVIGATION ENGINE
        // =========================================================================

        public bool EnableIslandSweep = true;
        public bool EnableObstacleDetour = true;
        public bool EnableLavaAvoidance = true;
        public bool EnableDinoFlee = true;

        public float DinoFleeLeashDistance = 32f; // Jarak aman kabur keluar wilayah teritorial dino (meter)
        public float NodeBlacklistDuration = 480f; // 8 Menit blacklist node yang sudah dipanen

        // Island sweep heading & wander state
        private Vector3 _islandSweepHeading = Vector3.zero;
        private float _nextSweepChangeTime = 0f;

        // Obstacle & Lava Detour state
        private bool _isDetourActive = false;
        private Vector3 _detourDirection = Vector3.zero;
        private float _detourExpireTime = 0f;
        private float _detourTurnSign = 1f; // +1 = belok kanan, -1 = belok kiri
        private Vector3 _lastNavCheckPos = Vector3.zero;
        private float _lastNavCheckTime = 0f;

        // Dino Threat & Flee state
        private bool _isFleeingDino = false;
        private DamageableEntity _currentThreatEnemy = null;
        private float _threatEscapeUntil = 0f;

        // =========================================================================
        // 1. LAVA & HAZARD SAFETY SYSTEM (Terintegrasi World Pos, Tile Depth, & Biome)
        // =========================================================================

        public bool IsLavaAtPosition(Vector3 clientPos) {
            return IsLavaAtClientPosition(clientPos);
        }

        public bool IsLavaAtClientPosition(Vector3 clientPos) {
            if (!EnableLavaAvoidance) return false;
            try {
                Durango.Terrain.Terrain_PC terrain = Durango.Terrain.Terrain_PC.Instance();
                if (terrain != null) {
                    // 1. Cek Biome via World Position
                    Vector3 worldPos = Durango.Terrain.Util.ClientPositionToWorldPosition(clientPos);
                    Biome b = terrain.GetTileBiome(worldPos);

                    // Biome murni Lava
                    if (b == Biome.Lava) return true;

                    // 2. Cek Kedalaman Cairan (Tile Depth / Liquid Mesh)
                    // Di Durango: ubin lahar di pulau kawah sering ber-biome Volcanic dengan lapisan cairan (TileDepth > 0)
                    Vector2 tilePos = Durango.Terrain.Util.ClientPositionToTilePosition(clientPos);
                    float depth = terrain.GetTileDepth(tilePos);

                    if (depth > 0.015f) {
                        // Di pulau vulkanik/kawah, semua genangan cairan adalah lahar mendidih!
                        if (b == Biome.Volcanic || b == Biome.Lava || Durango.Terrain.Util.IsWater(b)) {
                            return true;
                        }
                        // Jika kedalaman di atas tanah (Foot, Waist, Swim, Deep)
                        if (Durango.Terrain.TerrainWater.GetWaterDepthLevel(depth) != Durango.Terrain.TerrainWater.WaterDepthLevel.Land) {
                            return true;
                        }
                    }

                    // 3. Validasi Sekunder via TilePosition
                    Point2 pt = new Point2(Mathf.FloorToInt(tilePos.x), Mathf.FloorToInt(tilePos.y));
                    Biome bTile = terrain.TilePositionToBiome(pt);
                    if (bTile == Biome.Lava) return true;
                    if (bTile == Biome.Volcanic && depth > 0.015f) return true;
                    if (Durango.Terrain.Util.IsWater(bTile) && depth > 0.015f) return true;
                }
            } catch {}
            return false;
        }

        public bool IsTargetInOrNearLava(Vector3 targetClientPos, float dangerRadiusMeters) {
            if (!EnableLavaAvoidance) return false;
            if (IsLavaAtClientPosition(targetClientPos)) return true;

            float rUnits = dangerRadiusMeters * 100f; // 1m = 100 unit Durango
            Vector3[] offsets = new Vector3[] {
                new Vector3(rUnits, 0, 0),
                new Vector3(-rUnits, 0, 0),
                new Vector3(0, 0, rUnits),
                new Vector3(0, 0, -rUnits),
                new Vector3(rUnits * 0.707f, 0, rUnits * 0.707f),
                new Vector3(-rUnits * 0.707f, 0, rUnits * 0.707f),
                new Vector3(rUnits * 0.707f, 0, -rUnits * 0.707f),
                new Vector3(-rUnits * 0.707f, 0, -rUnits * 0.707f)
            };

            for (int i = 0; i < offsets.Length; i++) {
                if (IsLavaAtClientPosition(targetClientPos + offsets[i])) {
                    return true;
                }
            }
            return false;
        }

        public bool IsPathBlockedByLava(Vector3 fromClientPos, Vector3 toClientPos) {
            if (!EnableLavaAvoidance) return false;

            float dist = Vector3.Distance(fromClientPos, toClientPos);
            if (dist < 60f) return false; // Jarak sangat dekat (< 0.6m)

            // Lakukan sampling ray rapat setiap 0.5 meter (50 unit Durango) sepanjang garis ke target
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / 50f), 2, 160);
            for (int i = 1; i <= steps; i++) {
                float t = (float)i / (float)steps;
                Vector3 samplePos = Vector3.Lerp(fromClientPos, toClientPos, t);
                if (IsLavaAtClientPosition(samplePos)) {
                    return true; // Terdeteksi ubin lahar di lintasan garis lurus menuju target!
                }
            }
            return false;
        }

        public bool HasLavaAhead(Vector3 curClientPos, Vector3 moveDir, float lookAheadMeters) {
            if (!EnableLavaAvoidance) return false;
            Vector3 dirFlat = new Vector3(moveDir.x, 0, moveDir.z);
            if (dirFlat.sqrMagnitude < 0.001f) return false;
            dirFlat.Normalize();

            // Cek pada jarak 0.6m, 1.2m, 2.0m, 3.0m, dan lookAheadMeters
            float[] sampleDists = new float[] { 60f, 120f, 200f, 300f, lookAheadMeters * 100f };
            for (int i = 0; i < sampleDists.Length; i++) {
                Vector3 p = curClientPos + (dirFlat * sampleDists[i]);
                if (IsLavaAtClientPosition(p)) return true;
            }
            return false;
        }

        // =========================================================================
        // 2. OBSTACLE & CLIFF / TERRAIN MASK SYSTEM
        // =========================================================================

        public bool IsObstacleAtPosition(Vector3 clientPos) {
            if (!EnableObstacleDetour) return false;
            try {
                Durango.Terrain.Terrain_PC terrain = Durango.Terrain.Terrain_PC.Instance();
                if (terrain != null) {
                    Vector3 worldPos = Durango.Terrain.Util.ClientPositionToWorldPosition(clientPos);
                    if (terrain.IsCollidableMasked(worldPos)) {
                        return true;
                    }
                }
            } catch {}
            return false;
        }

        public bool HasObstacleAhead(Vector3 curClientPos, Vector3 moveDir, float lookAheadMeters) {
            if (!EnableObstacleDetour) return false;
            Vector3 dirFlat = new Vector3(moveDir.x, 0, moveDir.z);
            if (dirFlat.sqrMagnitude < 0.001f) return false;
            dirFlat.Normalize();

            float[] sampleDists = new float[] { 100f, 200f, lookAheadMeters * 100f };
            for (int i = 0; i < sampleDists.Length; i++) {
                Vector3 p = curClientPos + (dirFlat * sampleDists[i]);
                if (IsObstacleAtPosition(p)) return true;
            }
            return false;
        }

        public Vector3 FindSafeDetourDirection(Vector3 curPos, Vector3 desiredDir, float probeMeters) {
            Vector3 flatDesired = new Vector3(desiredDir.x, 0, desiredDir.z);
            if (flatDesired.sqrMagnitude < 0.001f) flatDesired = Vector3.forward;
            else flatDesired.Normalize();

            float[] angles = new float[] { 45f, -45f, 75f, -75f, 90f, -90f, 120f, -120f, 150f, -150f };
            for (int i = 0; i < angles.Length; i++) {
                float testAngle = angles[i] * _detourTurnSign;
                Vector3 candDir = Quaternion.Euler(0, testAngle, 0) * flatDesired;
                Vector3 testPos = curPos + (candDir * (probeMeters * 100f));

                if (!IsLavaAtClientPosition(testPos) && !HasLavaAhead(curPos, candDir, probeMeters)) {
                    if (!IsObstacleAtPosition(testPos) && !HasObstacleAhead(curPos, candDir, probeMeters)) {
                        return candDir;
                    }
                }
            }

            return -flatDesired;
        }

        // =========================================================================
        // 3. DINO THREAT & AGGRO ESCAPE SYSTEM (Menjauhi Wilayah Dino)
        // =========================================================================

        public DamageableEntity GetActiveThreatEnemy(PlayerBehavior player, CombatSystem cs) {
            if (!EnableDinoFlee) return null;
            if (player == null || cs == null) return null;

            Vector3 pPos = player.CurrentPosition;
            if (pPos == Vector3.zero) pPos = player.transform.position;

            try {
                if (cs.DamageableEntities != null) {
                    var enemies = cs.DamageableEntities.GetEnemies();
                    if (enemies != null) {
                        DamageableEntity nearestThreat = null;
                        float nearestDist = 999999f;

                        foreach (var enemy in enemies) {
                            if (enemy == null || !enemy.IsAlive) continue;
                            if (IsPetOrFriendly(enemy)) continue;

                            string enemyTarget = enemy.TargetId;
                            bool isTargetingMe = (!string.IsNullOrEmpty(enemyTarget) && enemyTarget == player.EntityId);
                            float dUnits = Vector3.Distance(pPos, enemy.GetCurrentPosition());
                            float dMeters = dUnits / 100f;

                            // Mengancam jika:
                            // 1. Dino menargetkan player langsung (Aggro)
                            // 2. Atau player masuk CombatMode dan jarak dino < 14m
                            if (isTargetingMe || (cs.CombatMode && dMeters < 14f)) {
                                if (dUnits < nearestDist) {
                                    nearestDist = dUnits;
                                    nearestThreat = enemy;
                                }
                            }
                        }
                        return nearestThreat;
                    }
                }
            } catch {}

            return null;
        }

        public bool HandleThreatFlee(PlayerBehavior player, PlayerController controller, LocalMoveNavigator nav, CombatSystem cs) {
            if (!EnableDinoFlee) return false;
            // Hanya aktif saat bot gather berjalan dan bot tempur TIDAK aktif
            if (!IsEnabled || IsCombatBotEnabled) return false;

            DamageableEntity threat = GetActiveThreatEnemy(player, cs);

            if (threat != null || (_isFleeingDino && Time.time < _threatEscapeUntil)) {
                if (threat != null) {
                    _currentThreatEnemy = threat;
                    _threatEscapeUntil = Time.time + 2.0f;
                }

                _isFleeingDino = true;

                // Batalkan proses panen segera & tutup menu
                if (_isGathering) {
                    _isGathering = false;
                    StopAllCoroutines();
                    ForceCloseInteractionMenu();
                }

                Vector3 pPos = player.CurrentPosition;
                if (pPos == Vector3.zero) pPos = player.transform.position;

                Vector3 enemyPos = (_currentThreatEnemy != null && _currentThreatEnemy.IsAlive) ? _currentThreatEnemy.GetCurrentPosition() : (pPos - player.transform.forward * 500f);
                float distToEnemyMeters = Vector3.Distance(pPos, enemyPos) / 100f;

                // Jika sudah keluar dari wilayah teritorial dino (> DinoFleeLeashDistance) dan tidak combat
                if (distToEnemyMeters >= DinoFleeLeashDistance && (cs == null || !cs.CombatMode)) {
                    _isFleeingDino = false;
                    _currentThreatEnemy = null;
                    return false;
                }

                // Hitung arah kabur berlawanan dari posisi dino
                Vector3 fleeDir = (pPos - enemyPos);
                fleeDir.y = 0;
                if (fleeDir.sqrMagnitude < 0.001f) fleeDir = player.transform.forward;
                else fleeDir.Normalize();

                // Pastikan arah kabur aman dari lava dan tebing
                if (HasLavaAhead(pPos, fleeDir, 3.5f) || HasObstacleAhead(pPos, fleeDir, 3.5f)) {
                    fleeDir = FindSafeDetourDirection(pPos, fleeDir, 3.5f);
                }

                // Titik lari (15 meter di depan)
                Vector3 escapeDest = pPos + (fleeDir * 1500f);

                if (nav != null && Time.time >= _nextMoveCallTime) {
                    _nextMoveCallTime = Time.time + 0.25f;
                    nav.MoveToPosition(escapeDest, null, 100f, true);
                }

                string threatName = (_currentThreatEnemy != null && _currentThreatEnemy.GameObject != null) ? CleanObjectName(_currentThreatEnemy.GameObject.name) : "Dino Buas";
                _statusText = string.Format("<color=#e74c3c><b>[KABUR DINO]</b> Dikejar {0}! Menjauhi wilayah teritorial ({1:F1}m / {2:F0}m)...</color>", threatName, distToEnemyMeters, DinoFleeLeashDistance);
                return true;
            }

            _isFleeingDino = false;
            _currentThreatEnemy = null;
            return false;
        }

        // =========================================================================
        // 4. ISLAND SWEEP & WANDERING EXPLORATION (Jelajah 1 Map)
        // =========================================================================

        public void UpdateIslandSweepExploration(PlayerBehavior player, PlayerController controller, LocalMoveNavigator nav) {
            if (!EnableIslandSweep) {
                _statusText = string.Format("Mencari resource ({0} node terdeteksi)", _totalCandidatesFound);
                return;
            }

            Vector3 curPPos = player.CurrentPosition;
            if (curPPos == Vector3.zero) curPPos = player.transform.position;

            // Cek jika saat ini karakter berada di dekat lahar (Emergency Repel)
            if (IsLavaAtClientPosition(curPPos)) {
                if (nav != null) { try { nav.Stop(); } catch {} }
                if (controller != null) { try { controller.StopMove(); } catch {} }
                Vector3 repDir = -player.transform.forward;
                repDir.y = 0;
                repDir.Normalize();
                if (nav != null) nav.MoveToPosition(curPPos + (repDir * 200f), null, 100f, true);
                _statusText = "<color=#e74c3c><b>[BAHAYA LAHAR]</b> Mundur dari bibir lahar!</color>";
                return;
            }

            if (_islandSweepHeading == Vector3.zero) {
                _islandSweepHeading = player.transform.forward;
                _islandSweepHeading.y = 0;
                if (_islandSweepHeading.sqrMagnitude < 0.001f) _islandSweepHeading = Vector3.forward;
                else _islandSweepHeading.Normalize();
            }

            // Periksa rintangan lava / tebing di depan
            bool lavaAhead = HasLavaAhead(curPPos, _islandSweepHeading, 4.0f);
            bool obstAhead = HasObstacleAhead(curPPos, _islandSweepHeading, 4.0f);

            // Deteksi stuck saat jelajah
            bool isStuck = false;
            if (Time.time - _lastNavCheckTime > 0.8f) {
                float movedDist = Vector3.Distance(curPPos, _lastNavCheckPos);
                _lastNavCheckPos = curPPos;
                _lastNavCheckTime = Time.time;
                if (movedDist < 25f) { // Bergerak < 0.25 meter dalam 0.8 detik
                    isStuck = true;
                }
            }

            // Jika terhalang atau stuck, putar heading untuk menyusuri tepian pulau / kontur gunung
            if (lavaAhead || obstAhead || isStuck || Time.time >= _nextSweepChangeTime) {
                if (lavaAhead || obstAhead || isStuck) {
                    _islandSweepHeading = FindSafeDetourDirection(curPPos, _islandSweepHeading, 4.0f);
                    _nextSweepChangeTime = Time.time + 4.0f;
                }
            }

            Vector3 sweepDest = curPPos + (_islandSweepHeading * 1500f);

            if (nav != null && Time.time >= _nextMoveCallTime) {
                _nextMoveCallTime = Time.time + 0.25f;
                nav.MoveToPosition(sweepDest, null, 100f, true);
            }

            _statusText = "<color=#3498db><b>[JELAJAH PULAU]</b> Menyusuri pulau mencari node baru...</color>";
        }

        // =========================================================================
        // 5. SMART TARGET APPROACH WITH DETOUR (Menuju Node Tanpa Tertabrak Tebing / Lahar)
        // =========================================================================

        public void MoveTowardsTargetWithDetour(PlayerBehavior player, PlayerController controller, GameObject targetObj, Vector3 targetPos, LocalMoveNavigator nav) {
            Vector3 curPPos = player.CurrentPosition;
            if (curPPos == Vector3.zero) curPPos = player.transform.position;

            // 1. Cek Emergency Repel jika posisi saat ini sudah di bibir lahar
            if (IsLavaAtClientPosition(curPPos)) {
                if (nav != null) { try { nav.Stop(); } catch {} }
                if (controller != null) { try { controller.StopMove(); } catch {} }
                Vector3 escapeDir = -player.transform.forward;
                escapeDir.y = 0;
                escapeDir.Normalize();
                if (nav != null) nav.MoveToPosition(curPPos + (escapeDir * 200f), null, 100f, true);
                _statusText = "<color=#e74c3c><b>[BAHAYA LAHAR]</b> Mundur dari lahar!</color>";
                return;
            }

            Vector3 toTarget = targetPos - curPPos;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = player.transform.forward;
            else toTarget.Normalize();

            // 2. Cek bahaya lahar tepat di depan langkah
            bool lavaAhead = HasLavaAhead(curPPos, toTarget, 3.5f);

            // 3. Cek deteksi tertahan fisik / tebing
            if (Time.time - _lastPositionCheckTime > 0.7f) {
                float moved = Vector3.Distance(curPPos, _lastPlayerPos);
                _lastPlayerPos = curPPos;
                _lastPositionCheckTime = Time.time;

                if (moved < 18f && _targetDistance > 250f && (Time.time - _approachTargetStartTime > 1.0f)) {
                    // Karakter tertahan dinding / rintangan gunung!
                    _isDetourActive = true;
                    _detourExpireTime = Time.time + 2.0f;
                    _detourTurnSign = -_detourTurnSign;
                    _detourDirection = FindSafeDetourDirection(curPPos, toTarget, 3.5f);
                }
            }

            if (lavaAhead) {
                // Berhenti seketika agar momentum karakter tidak menyebur ke lahar!
                if (nav != null) { try { nav.Stop(); } catch {} }
                if (controller != null) { try { controller.StopMove(); } catch {} }

                _isDetourActive = true;
                _detourExpireTime = Time.time + 2.2f;
                _detourDirection = FindSafeDetourDirection(curPPos, toTarget, 4.0f);
                _statusText = "<color=#e67e22><b>[HINDARI LAHAR]</b> Belok memutar menyusuri bibir lahar...</color>";
            }

            // Eksekusi rute langkah
            if (_isDetourActive && Time.time < _detourExpireTime) {
                Vector3 detourPos = curPPos + (_detourDirection * 350f);
                if (nav != null && Time.time >= _nextMoveCallTime) {
                    _nextMoveCallTime = Time.time + 0.25f;
                    nav.MoveToPosition(detourPos, null, 100f, true);
                }
            } else {
                _isDetourActive = false;
                if (nav != null && Time.time >= _nextMoveCallTime) {
                    _nextMoveCallTime = Time.time + 0.25f;
                    try {
                        nav.MoveToTarget(targetObj, null, 100f, true);
                    } catch {
                        nav.MoveToPosition(targetPos, null, 100f, true);
                    }
                }
            }
        }
    }
}
