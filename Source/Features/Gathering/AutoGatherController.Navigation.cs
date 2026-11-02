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

        // Emergency Lava Escape state
        private bool _isEscapingLava = false;
        private Vector3 _lavaEscapeTarget = Vector3.zero;
        private float _lavaEscapeUntil = 0f;

        // Anti-Looping Breadcrumb Trail Memory
        private struct NavTrailPoint {
            public Vector3 Pos;
            public float Time;
        }
        private readonly List<NavTrailPoint> _navVisitedTrail = new List<NavTrailPoint>();
        private float _lastNavTrailTime = 0f;

        public void RecordNavTrail(Vector3 pos) {
            if (pos == Vector3.zero) return;
            if (Time.time - _lastNavTrailTime < 1.0f) return;
            _lastNavTrailTime = Time.time;

            _navVisitedTrail.Add(new NavTrailPoint { Pos = pos, Time = Time.time });

            // Hapus jejak lebih tua dari 180 detik (3 menit)
            float expire = Time.time - 180f;
            for (int i = _navVisitedTrail.Count - 1; i >= 0; i--) {
                if (_navVisitedTrail[i].Time < expire) _navVisitedTrail.RemoveAt(i);
            }
        }

        public float GetNavTrailPenalty(Vector3 candidatePos, float checkRadiusUnits) {
            float penalty = 0f;
            for (int i = 0; i < _navVisitedTrail.Count; i++) {
                float d = Vector3.Distance(candidatePos, _navVisitedTrail[i].Pos);
                if (d < checkRadiusUnits) {
                    float weight = (checkRadiusUnits - d) / checkRadiusUnits;
                    penalty += weight * 150f;
                }
            }
            return penalty;
        }

        // =========================================================================
        // 1. LAVA & HAZARD SAFETY SYSTEM (Terintegrasi World Pos, Tile Depth, & Biome)
        // =========================================================================

        private static Durango.Terrain.TerrainBase GetTerrainInstance() {
            try {
                if (Durango.Utils.Singleton<Durango.Terrain.TerrainBase>.HasInstance()) {
                    return Durango.Utils.Singleton<Durango.Terrain.TerrainBase>.Instance();
                }
            } catch {}
            try {
                return UnityEngine.Object.FindObjectOfType<Durango.Terrain.TerrainBase>();
            } catch {}
            return null;
        }

        public bool IsLavaAtPosition(Vector3 clientPos) {
            return IsLavaAtClientPosition(clientPos);
        }

        public bool IsLavaAtClientPosition(Vector3 clientPos) {
            if (!EnableLavaAvoidance) return false;
            try {
                Durango.Terrain.TerrainBase terrain = GetTerrainInstance();
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
                    float waterDepth = terrain.GetWaterDepth(worldPos);

                    // 3. Validasi Sekunder via TilePosition
                    Point2 pt = new Point2(Mathf.FloorToInt(tilePos.x), Mathf.FloorToInt(tilePos.y));
                    Biome bTile = terrain.TilePositionToBiome(pt);
                    if (bTile == Biome.Lava) return true;

                    bool isVolcanicEnv = (b == Biome.Volcanic || bTile == Biome.Volcanic);
                    bool isLiquid = (depth > 0.015f || waterDepth > 0.015f);

                    // Di lingkungan pulau vulkanik/kawah, semua ubin bertipe cairan atau kedalaman air adalah lahar panas!
                    if (isVolcanicEnv && isLiquid) {
                        return true;
                    }

                    if (isLiquid && (Durango.Terrain.Util.IsWater(b) || Durango.Terrain.Util.IsWater(bTile))) {
                        return true;
                    }

                    if (depth > 0.015f && Durango.Terrain.TerrainWater.GetWaterDepthLevel(depth) != Durango.Terrain.TerrainWater.WaterDepthLevel.Land) {
                        if (isVolcanicEnv || b == Biome.Lava || bTile == Biome.Lava) return true;
                    }

                    if (waterDepth > 0.015f && Durango.Terrain.TerrainWater.GetWaterDepthLevel(waterDepth) != Durango.Terrain.TerrainWater.WaterDepthLevel.Land) {
                        if (isVolcanicEnv || b == Biome.Lava || bTile == Biome.Lava) return true;
                    }
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
                new Vector3(-rUnits * 0.707f, 0, -rUnits * 0.707f),
                new Vector3(rUnits * 0.5f, 0, 0),
                new Vector3(-rUnits * 0.5f, 0, 0),
                new Vector3(0, 0, rUnits * 0.5f),
                new Vector3(0, 0, -rUnits * 0.5f)
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
            if (dist < 40f) return false; // Jarak sangat dekat (< 0.4m)

            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / 50f), 3, 100);
            for (int i = 1; i <= steps; i++) {
                float t = (float)i / (float)steps;
                Vector3 samplePos = Vector3.Lerp(fromClientPos, toClientPos, t);
                if (IsLavaAtClientPosition(samplePos)) {
                    return true;
                }
            }
            return false;
        }

        public bool HasLavaAhead(Vector3 curClientPos, Vector3 moveDir, float lookAheadMeters) {
            if (!EnableLavaAvoidance) return false;
            Vector3 dirFlat = new Vector3(moveDir.x, 0, moveDir.z);
            if (dirFlat.sqrMagnitude < 0.001f) return false;
            dirFlat.Normalize();

            float maxDistUnits = Mathf.Max(lookAheadMeters * 100f, 200f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(maxDistUnits / 50f), 3, 15);
            for (int i = 1; i <= steps; i++) {
                float dUnits = (float)i * (maxDistUnits / (float)steps);
                Vector3 p = curClientPos + (dirFlat * dUnits);
                if (IsLavaAtClientPosition(p)) return true;
            }
            Vector3 endPos = curClientPos + (dirFlat * maxDistUnits);
            if (IsTargetInOrNearLava(endPos, 0.4f)) return true;
            return false;
        }

        // =========================================================================
        // 2. OBSTACLE & CLIFF / TERRAIN MASK SYSTEM
        // =========================================================================

        public bool IsObstacleAtPosition(Vector3 clientPos) {
            if (!EnableObstacleDetour) return false;
            try {
                Durango.Terrain.TerrainBase terrain = GetTerrainInstance();
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

            float[] sampleDists = new float[] { 80f, 160f, 240f, lookAheadMeters * 100f };
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

            // Uji sudut belok kanan dan kiri secara simetris
            float[] angles = new float[] { 35f, -35f, 65f, -65f, 90f, -90f, 115f, -115f, 145f, -145f, 175f, -175f };
            Vector3 bestDir = Vector3.zero;
            float bestScore = float.MaxValue;

            for (int i = 0; i < angles.Length; i++) {
                float testAngle = angles[i];
                Vector3 candDir = Quaternion.Euler(0, testAngle, 0) * flatDesired;
                Vector3 testPos = curPos + (candDir * (probeMeters * 100f));

                if (!IsLavaAtClientPosition(testPos) && !HasLavaAhead(curPos, candDir, probeMeters)) {
                    if (!IsObstacleAtPosition(testPos) && !HasObstacleAhead(curPos, candDir, probeMeters)) {
                        // Hitung penalti jejak lama: arah yang belum pernah dilewati memiliki penalti 0!
                        float trailPenalty = GetNavTrailPenalty(testPos, 3000f);
                        float turnPenalty = Mathf.Abs(testAngle) > 90f ? 40f : 0f;
                        float totalScore = trailPenalty + turnPenalty;

                        if (totalScore < bestScore) {
                            bestScore = totalScore;
                            bestDir = candDir;
                            if (totalScore == 0f) break; // Sudah menemukan arah murni baru (unexplored)!
                        }
                    }
                }
            }

            if (bestDir != Vector3.zero) return bestDir;
            return -flatDesired;
        }

        // =========================================================================
        // EMERGENCY LAVA ESCAPE (Menjauh dari Bibir/Ubin Lahar Tanpa Terhenti)
        // =========================================================================
        public bool HandleEmergencyLavaEscape(PlayerBehavior player, PlayerController controller, LocalMoveNavigator nav, Vector3 curPPos) {
            if (!EnableLavaAvoidance) return false;

            // 1. Jika masih dalam periode escape aktif, terus bergerak ke target daratan aman tanpa interupsi
            if (Time.time < _lavaEscapeUntil) {
                if (nav != null && Time.time >= _nextMoveCallTime) {
                    _nextMoveCallTime = Time.time + 0.25f;
                    nav.MoveToPosition(_lavaEscapeTarget, null, 100f, true);
                }
                _statusText = "<color=#e74c3c><b>[AWAS LAHAR]</b> Menjauh ke daratan aman...</color>";
                return true;
            }

            // 2. Evaluasi apakah posisi saat ini berbahaya
            bool inLava = IsLavaAtClientPosition(curPPos);
            if (!inLava && player != null) {
                try {
                    Biome pb = player.GetBiome();
                    if (pb == Biome.Lava || ((pb == Biome.Volcanic || Durango.Terrain.Util.IsWater(pb)) && player.WaterDepth > 0.015f)) {
                        inLava = true;
                    }
                } catch {}
            }
            if (!inLava && IsTargetInOrNearLava(curPPos, 0.7f)) {
                inLava = true;
            }

            if (!inLava) {
                _isEscapingLava = false;
                return false;
            }

            // 3. Posisi bahaya: Cari daratan aman terdekat di 16 arah mata angin (radius 3.5m)
            Vector3 bestSafePos = Vector3.zero;
            float maxSafetyScore = -1f;

            for (int i = 0; i < 16; i++) {
                float angle = i * 22.5f;
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 testPos = curPPos + (dir * 350f); // 3.5 meter

                if (!IsLavaAtClientPosition(testPos) && !IsObstacleAtPosition(testPos)) {
                    float score = 100f;
                    if (!IsTargetInOrNearLava(testPos, 1.2f)) score += 200f;
                    if (!HasLavaAhead(curPPos, dir, 2.5f)) score += 100f;
                    if (player != null) {
                        float dot = Vector3.Dot(-player.transform.forward, dir);
                        if (dot > 0.3f) score += 50f;
                    }

                    if (score > maxSafetyScore) {
                        maxSafetyScore = score;
                        bestSafePos = testPos;
                    }
                }
            }

            if (bestSafePos == Vector3.zero) {
                Vector3 backDir = (player != null) ? -player.transform.forward : -Vector3.forward;
                backDir.y = 0;
                if (backDir.sqrMagnitude < 0.001f) backDir = -Vector3.forward;
                else backDir.Normalize();
                bestSafePos = curPPos + (backDir * 350f);
            }

            _isEscapingLava = true;
            _lavaEscapeTarget = bestSafePos;
            _lavaEscapeUntil = Time.time + 1.5f; // Kunci gerakan selama 1.5 detik penuh tanpa interupsi Stop()!

            if (nav != null) {
                _nextMoveCallTime = Time.time + 0.25f;
                nav.MoveToPosition(_lavaEscapeTarget, null, 100f, true);
            }

            _statusText = "<color=#e74c3c><b>[AWAS LAHAR]</b> Menjauh ke daratan aman...</color>";
            return true;
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

            // Catat jejak langkah secara berkala untuk anti-looping
            RecordNavTrail(curPPos);

            // 1. Emergency Repel jika posisi saat ini berada di bibir / dalam lahar
            if (HandleEmergencyLavaEscape(player, controller, nav, curPPos)) return;

            if (_islandSweepHeading == Vector3.zero) {
                _islandSweepHeading = player.transform.forward;
                _islandSweepHeading.y = 0;
                if (_islandSweepHeading.sqrMagnitude < 0.001f) _islandSweepHeading = Vector3.forward;
                else _islandSweepHeading.Normalize();
            }

            // Periksa rintangan lava / tebing di depan
            bool lavaAhead = HasLavaAhead(curPPos, _islandSweepHeading, 5.0f);
            bool obstAhead = HasObstacleAhead(curPPos, _islandSweepHeading, 4.0f);

            // Deteksi stuck saat jelajah
            bool isStuck = false;
            if (Time.time - _lastNavCheckTime > 0.8f) {
                float movedDist = Vector3.Distance(curPPos, _lastNavCheckPos);
                _lastNavCheckPos = curPPos;
                _lastNavCheckTime = Time.time;
                if (movedDist < 25f) {
                    isStuck = true;
                }
            }

            // Titik tujuan jelajah: perpanjang langkah ke 12 meter (1200 unit) agar memiliki momentum jelas
            Vector3 sweepDest = curPPos + (_islandSweepHeading * 1200f);
            bool pathBlockedByLava = IsPathBlockedByLava(curPPos, sweepDest);

            // Jika terhalang atau sudah waktunya memperbarui arah jelajah:
            if (lavaAhead || obstAhead || isStuck || pathBlockedByLava || Time.time >= _nextSweepChangeTime) {
                if (lavaAhead || obstAhead || isStuck || pathBlockedByLava) {
                    // Cari arah memutar yang menjauhi jejak lama
                    _islandSweepHeading = FindSafeDetourDirection(curPPos, _islandSweepHeading, 5.0f);
                    _nextSweepChangeTime = Time.time + 3.0f;
                } else if (Time.time >= _nextSweepChangeTime) {
                    // Evaluasi 8 arah mata angin: cari sektor terjauh yang paling minim jejak kunjungan!
                    Vector3 bestHeading = _islandSweepHeading;
                    float lowestTrailPenalty = float.MaxValue;

                    for (int a = 0; a < 8; a++) {
                        float angle = a * 45f;
                        Vector3 candHeading = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                        Vector3 probeDest = curPPos + (candHeading * 1500f);

                        if (!IsLavaAtClientPosition(probeDest) && !HasLavaAhead(curPPos, candHeading, 5.0f) && !IsPathBlockedByLava(curPPos, probeDest)) {
                            if (!IsObstacleAtPosition(probeDest) && !HasObstacleAhead(curPPos, candHeading, 4.0f)) {
                                float penalty = GetNavTrailPenalty(probeDest, 4000f);
                                if (penalty < lowestTrailPenalty) {
                                    lowestTrailPenalty = penalty;
                                    bestHeading = candHeading;
                                }
                            }
                        }
                    }

                    _islandSweepHeading = bestHeading;
                    _nextSweepChangeTime = Time.time + UnityEngine.Random.Range(5.0f, 9.0f);
                }
                sweepDest = curPPos + (_islandSweepHeading * 1200f);
            }

            if (nav != null && Time.time >= _nextMoveCallTime) {
                _nextMoveCallTime = Time.time + 0.25f;
                nav.MoveToPosition(sweepDest, null, 100f, true);
            }

            _statusText = "<color=#3498db><b>[JELAJAH PULAU]</b> Menjelajahi sektor baru mencari node...</color>";
        }

        // =========================================================================
        // 5. SMART TARGET APPROACH WITH DETOUR (Menuju Node Tanpa Tertabrak Tebing / Lahar)
        // =========================================================================

        public void MoveTowardsTargetWithDetour(PlayerBehavior player, PlayerController controller, GameObject targetObj, Vector3 targetPos, LocalMoveNavigator nav) {
            Vector3 curPPos = player.CurrentPosition;
            if (curPPos == Vector3.zero) curPPos = player.transform.position;
            RecordNavTrail(curPPos);

            // 1. Cek Emergency Repel jika posisi saat ini sudah di bibir lahar
            if (HandleEmergencyLavaEscape(player, controller, nav, curPPos)) return;

            // 2. CEK APAKAH TARGET ITU SENDIRI BERADA DI DALAM LAHAR
            if (IsTargetInOrNearLava(targetPos, 1.5f)) {
                if (targetObj != null) {
                    _recentlyGathered[targetObj] = Time.time + 120f;
                }
                _currentTargetObj = null;
                if (nav != null) { try { nav.Stop(); } catch {} }
                if (controller != null) { try { controller.StopMove(); } catch {} }
                _statusText = "<color=#e74c3c><b>[BATALKAN NODE]</b> Node berada di dalam lahar panas!</color>";
                return;
            }

            Vector3 toTarget = targetPos - curPPos;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = player.transform.forward;
            else toTarget.Normalize();

            // 3. Cek bahaya lahar / tebing tepat di depan langkah ke target
            bool lavaAhead = HasLavaAhead(curPPos, toTarget, 4.0f);
            bool obstAhead = HasObstacleAhead(curPPos, toTarget, 3.5f);

            // 4. Cek deteksi tertahan fisik / tebing
            if (Time.time - _lastPositionCheckTime > 0.7f) {
                float moved = Vector3.Distance(curPPos, _lastPlayerPos);
                _lastPlayerPos = curPPos;
                _lastPositionCheckTime = Time.time;

                if (moved < 18f && _targetDistance > 250f && (Time.time - _approachTargetStartTime > 1.0f)) {
                    _isDetourActive = true;
                    _detourExpireTime = Time.time + 2.0f;
                    _detourDirection = FindSafeDetourDirection(curPPos, toTarget, 4.0f);
                }
            }

            if (lavaAhead || obstAhead) {
                _isDetourActive = true;
                _detourExpireTime = Time.time + 2.0f;
                _detourDirection = FindSafeDetourDirection(curPPos, toTarget, 4.0f);
                _statusText = "<color=#e67e22><b>[HINDARI LAHAR/RINTANGAN]</b> Menghindar memutar ke arah aman...</color>";
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
