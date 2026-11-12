#include <jni.h>
#include <android/log.h>
#include <pthread.h>
#include <unistd.h>
#include <dlfcn.h>
#include <sys/mman.h>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>
#include <mutex>
#include "And64InlineHook.hpp"

#define LOG_TAG "DurangoCombatMod"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define LOGW(...) __android_log_print(ANDROID_LOG_WARN, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

#define DEG2RAD 0.017453292519943295f
#define RAD2DEG 57.29577951308232f

// -------------------------------------------------------------
// Data Structures
// -------------------------------------------------------------

struct Vector3 {
    float x;
    float y;
    float z;
};

struct Il2CppString {
    void* klass;
    void* monitor;
    int32_t length;
    uint16_t chars[1];
};

struct Il2CppArrayBounds {
    uintptr_t length;
    int32_t lower_bound;
};

struct Il2CppArray {
    void* klass;
    void* monitor;
    Il2CppArrayBounds* bounds;
    uintptr_t max_length;
    void* vector[1];
};

struct Yaml_PlayerAction {
    void* klass;
    void* monitor;
    Il2CppString* Id;
};

struct BattleAction {
    void* klass;
    void* monitor;
    Yaml_PlayerAction* Data;     // 0x10
    char pad_0x18[0x20];
    double ProhibitedUntil;      // 0x38
    char pad_0x40[0x10];
    Il2CppString* _motion;       // 0x50
};

struct UsingAction {
    void* klass;
    void* monitor;
    char pad_0x10[0x18];
    BattleAction* _action;       // 0x28
    void* _tamingItem;           // 0x30
    void* _target;               // 0x38
    char pad_0x40[0x8];
    int32_t _state;              // 0x48
    float _stateFinishTime;      // 0x4C
};

struct CombatSystem {
    void* klass;
    void* monitor;
    char pad_0x10[0x40];
    bool _combatMode;            // 0x50
    char pad_0x51[0x7];
    UsingAction* _usingAction;   // 0x58
};

// -------------------------------------------------------------
// Alert Tracking
// -------------------------------------------------------------

enum AlertShapeType {
    SHAPE_NONE = 0,
    SHAPE_RECT = 1,
    SHAPE_ARC = 2
};

struct AlertInfo {
    void* alertPtr;
    AlertShapeType shape;
    Vector3 center;
    float width;
    float height;
    float angle;
    float radius;
    float startAngle;
    float endAngle;
    float startAt;
    float finishAt;
    float hideAt;
    bool active;
};

static AlertInfo g_alerts[32];
static std::mutex g_alertMutex;

// -------------------------------------------------------------
// Settings
// -------------------------------------------------------------

struct ModSettings {
    bool enableAutoCounterAttack = true;
    bool enableAutoDodgeRoll = true;
    bool enableCounterAssist = true;
    float counterAttackTiming = 0.30f;
    float dodgeRollTiming = 0.35f;
    float alertWarningTiming = 0.80f;
};

static ModSettings g_settings;
static time_t g_lastConfigCheck = 0;

static void LoadSettings() {
    time_t now = time(nullptr);
    if (now - g_lastConfigCheck < 2) return;
    g_lastConfigCheck = now;

    const char* path = "/sdcard/Android/data/online.xdlands.game/files/durango_settings.ini";
    FILE* fp = fopen(path, "r");
    if (!fp) {
        // Create default settings file if not existing
        fp = fopen(path, "w");
        if (fp) {
            fprintf(fp, "[Combat]\n");
            fprintf(fp, "EnableAutoCounterAttack = true\n");
            fprintf(fp, "EnableAutoDodgeRoll = true\n");
            fprintf(fp, "EnableCounterAssist = true\n");
            fprintf(fp, "CounterAttackTiming = 0.30\n");
            fprintf(fp, "DodgeRollTiming = 0.35\n");
            fprintf(fp, "AlertWarningTiming = 0.80\n");
            fclose(fp);
            LOGI("Created default durango_settings.ini at %s", path);
        }
        return;
    }

    char line[256];
    while (fgets(line, sizeof(line), fp)) {
        char key[64], val[64];
        if (sscanf(line, "%63[^= ] = %63s", key, val) == 2 || sscanf(line, "%63[^=]=%63s", key, val) == 2) {
            if (strcmp(key, "EnableAutoCounterAttack") == 0) {
                g_settings.enableAutoCounterAttack = (strcasecmp(val, "true") == 0 || strcmp(val, "1") == 0);
            } else if (strcmp(key, "EnableAutoDodgeRoll") == 0) {
                g_settings.enableAutoDodgeRoll = (strcasecmp(val, "true") == 0 || strcmp(val, "1") == 0);
            } else if (strcmp(key, "EnableCounterAssist") == 0) {
                g_settings.enableCounterAssist = (strcasecmp(val, "true") == 0 || strcmp(val, "1") == 0);
            } else if (strcmp(key, "CounterAttackTiming") == 0) {
                g_settings.counterAttackTiming = strtof(val, nullptr);
            } else if (strcmp(key, "DodgeRollTiming") == 0) {
                g_settings.dodgeRollTiming = strtof(val, nullptr);
            } else if (strcmp(key, "AlertWarningTiming") == 0) {
                g_settings.alertWarningTiming = strtof(val, nullptr);
            }
        }
    }
    fclose(fp);
}

// -------------------------------------------------------------
// IL2CPP Function Pointers
// -------------------------------------------------------------

static uintptr_t g_il2cppBase = 0;

typedef void* (*t_PlayerBehavior_get_LocalPlayer)();
typedef Vector3 (*t_PlayerBehavior_get_CurrentPosition)(void* this_ptr);
typedef bool (*t_PlayerBehavior_get_IsRiding)(void* this_ptr);
typedef bool (*t_CharacterBehavior_get_IsAlive)(void* this_ptr);
typedef void (*t_PlayerBehavior_TurnToYaw)(void* this_ptr, float yaw, bool bSnap);
typedef void (*t_PlayerBehavior_PlayMotionForcely)(void* this_ptr, Il2CppString* clipName, float playbackRate, bool immediately);

typedef void* (*t_Connections_get_Frontend)();
typedef double (*t_Connection_GetPredictedServerTime)(void* this_ptr);

typedef void* (*t_PlayerController_get_MotionUpdater)();
typedef void (*t_MotionUpdater_ClearReservedMotions)(void* this_ptr);

typedef void (*t_CombatSystem_UseBattleAction)(void* this_ptr, Il2CppString* actionId);
typedef Il2CppArray* (*t_CombatSystem_get_ActionSlots)(void* this_ptr);

static t_PlayerBehavior_get_LocalPlayer PlayerBehavior_get_LocalPlayer = nullptr;
static t_PlayerBehavior_get_CurrentPosition PlayerBehavior_get_CurrentPosition = nullptr;
static t_PlayerBehavior_get_IsRiding PlayerBehavior_get_IsRiding = nullptr;
static t_CharacterBehavior_get_IsAlive CharacterBehavior_get_IsAlive = nullptr;
static t_PlayerBehavior_TurnToYaw PlayerBehavior_TurnToYaw = nullptr;
static t_PlayerBehavior_PlayMotionForcely PlayerBehavior_PlayMotionForcely = nullptr;
static t_Connections_get_Frontend Connections_get_Frontend = nullptr;
static t_Connection_GetPredictedServerTime Connection_GetPredictedServerTime = nullptr;
static t_PlayerController_get_MotionUpdater PlayerController_get_MotionUpdater = nullptr;
static t_MotionUpdater_ClearReservedMotions MotionUpdater_ClearReservedMotions = nullptr;
static t_CombatSystem_UseBattleAction CombatSystem_UseBattleAction = nullptr;
static t_CombatSystem_get_ActionSlots CombatSystem_get_ActionSlots = nullptr;

// -------------------------------------------------------------
// Original Function Pointers for Hooks
// -------------------------------------------------------------

static void (*orig_FillBorderAlert_SetRect)(void* this_ptr, Vector3 pos, float width, float height, float angle) = nullptr;
static void (*orig_FillBorderAlert_SetArc)(void* this_ptr, Vector3 pos, float radius, float startAngle, float endAngle) = nullptr;
static void (*orig_FillBorderAlert_Show)(void* this_ptr, float startAt, float finishAt, float showAt, float hideAt) = nullptr;
static void (*orig_FillBorderAlert_Stop)(void* this_ptr, float delay) = nullptr;
static void (*orig_FillBorderAlert_OnDisable)(void* this_ptr) = nullptr;
static void (*orig_CombatSystem_Update)(void* this_ptr) = nullptr;

// -------------------------------------------------------------
// String Conversion Helper
// -------------------------------------------------------------

static std::string Il2CppStringToStd(Il2CppString* str) {
    if (!str) return "";
    int32_t len = str->length;
    if (len <= 0 || len > 256) return "";
    std::string res;
    res.reserve(len);
    for (int i = 0; i < len; ++i) {
        uint16_t c = str->chars[i];
        res.push_back((char)(c < 128 ? tolower(c) : '?'));
    }
    return res;
}

// -------------------------------------------------------------
// Helper to get Server Time
// -------------------------------------------------------------

static double GetServerTime() {
    if (Connections_get_Frontend && Connection_GetPredictedServerTime) {
        void* frontend = Connections_get_Frontend();
        if (frontend) {
            double st = Connection_GetPredictedServerTime(frontend);
            if (st > 0.0) return st;
        }
    }
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (double)ts.tv_sec + (double)ts.tv_nsec * 1e-9;
}

// -------------------------------------------------------------
// Alert Hooks
// -------------------------------------------------------------

static void hook_FillBorderAlert_SetRect(void* this_ptr, Vector3 pos, float width, float height, float angle) {
    {
        std::lock_guard<std::mutex> lock(g_alertMutex);
        int slot = -1;
        for (int i = 0; i < 32; ++i) {
            if (g_alerts[i].alertPtr == this_ptr || (!g_alerts[i].active && slot == -1)) {
                slot = i;
                if (g_alerts[i].alertPtr == this_ptr) break;
            }
        }
        if (slot != -1) {
            g_alerts[slot].alertPtr = this_ptr;
            g_alerts[slot].shape = SHAPE_RECT;
            g_alerts[slot].center = pos;
            g_alerts[slot].width = width;
            g_alerts[slot].height = height;
            g_alerts[slot].angle = angle;
        }
    }
    orig_FillBorderAlert_SetRect(this_ptr, pos, width, height, angle);
}

static void hook_FillBorderAlert_SetArc(void* this_ptr, Vector3 pos, float radius, float startAngle, float endAngle) {
    {
        std::lock_guard<std::mutex> lock(g_alertMutex);
        int slot = -1;
        for (int i = 0; i < 32; ++i) {
            if (g_alerts[i].alertPtr == this_ptr || (!g_alerts[i].active && slot == -1)) {
                slot = i;
                if (g_alerts[i].alertPtr == this_ptr) break;
            }
        }
        if (slot != -1) {
            g_alerts[slot].alertPtr = this_ptr;
            g_alerts[slot].shape = SHAPE_ARC;
            g_alerts[slot].center = pos;
            g_alerts[slot].radius = radius;
            g_alerts[slot].startAngle = startAngle;
            g_alerts[slot].endAngle = endAngle;
        }
    }
    orig_FillBorderAlert_SetArc(this_ptr, pos, radius, startAngle, endAngle);
}

static void hook_FillBorderAlert_Show(void* this_ptr, float startAt, float finishAt, float showAt, float hideAt) {
    {
        std::lock_guard<std::mutex> lock(g_alertMutex);
        for (int i = 0; i < 32; ++i) {
            if (g_alerts[i].alertPtr == this_ptr) {
                g_alerts[i].startAt = startAt;
                g_alerts[i].finishAt = finishAt;
                g_alerts[i].hideAt = hideAt;
                g_alerts[i].active = true;
                break;
            }
        }
    }
    orig_FillBorderAlert_Show(this_ptr, startAt, finishAt, showAt, hideAt);
}

static void hook_FillBorderAlert_Stop(void* this_ptr, float delay) {
    {
        std::lock_guard<std::mutex> lock(g_alertMutex);
        for (int i = 0; i < 32; ++i) {
            if (g_alerts[i].alertPtr == this_ptr) {
                g_alerts[i].active = false;
                break;
            }
        }
    }
    orig_FillBorderAlert_Stop(this_ptr, delay);
}

static void hook_FillBorderAlert_OnDisable(void* this_ptr) {
    {
        std::lock_guard<std::mutex> lock(g_alertMutex);
        for (int i = 0; i < 32; ++i) {
            if (g_alerts[i].alertPtr == this_ptr) {
                g_alerts[i].active = false;
                break;
            }
        }
    }
    orig_FillBorderAlert_OnDisable(this_ptr);
}

// -------------------------------------------------------------
// Action Overwrite Engine
// -------------------------------------------------------------

static void ExecuteCounterCancel(CombatSystem* cs, void* localPlayer, BattleAction* spaceAction, double serverTime) {
    if (!cs || !localPlayer || !spaceAction || !spaceAction->Data) return;

    LOGI("[CombatMod] OVERWRITE: Counter Attack triggered!");

    // 1. Force interrupt active UsingAction
    UsingAction* ua = cs->_usingAction;
    if (ua) {
        ua->_state = 2; // State.Finishing / Cancel
        ua->_action = nullptr;
    }

    // 2. Reset prohibited cooldown
    spaceAction->ProhibitedUntil = serverTime - 1.0;

    // 3. Clear reserved motions in animator
    if (PlayerController_get_MotionUpdater && MotionUpdater_ClearReservedMotions) {
        void* mu = PlayerController_get_MotionUpdater();
        if (mu) MotionUpdater_ClearReservedMotions(mu);
    }

    // 4. Send battle action packet
    CombatSystem_UseBattleAction(cs, spaceAction->Data->Id);

    // 5. Force play counter motion immediately
    if (spaceAction->_motion && PlayerBehavior_PlayMotionForcely) {
        PlayerBehavior_PlayMotionForcely(localPlayer, spaceAction->_motion, 1.0f, true);
    }
}

static void ExecuteDodgeCancel(CombatSystem* cs, void* localPlayer, BattleAction* dodgeAction, Vector3 alertCenter, Vector3 playerPos, double serverTime) {
    if (!cs || !localPlayer || !dodgeAction || !dodgeAction->Data) return;

    LOGI("[CombatMod] OVERWRITE: Dodge Roll triggered!");

    // 1. Turn player away from hitbox center
    float dx = playerPos.x - alertCenter.x;
    float dz = playerPos.z - alertCenter.z;
    float escapeYaw = atan2f(dx, dz) * RAD2DEG;
    if (PlayerBehavior_TurnToYaw) {
        PlayerBehavior_TurnToYaw(localPlayer, escapeYaw, true);
    }

    // 2. Force interrupt active UsingAction
    UsingAction* ua = cs->_usingAction;
    if (ua) {
        ua->_state = 2; // State.Finishing / Cancel
        ua->_action = nullptr;
    }

    // 3. Reset prohibited cooldown
    dodgeAction->ProhibitedUntil = serverTime - 1.0;

    // 4. Clear reserved motions
    if (PlayerController_get_MotionUpdater && MotionUpdater_ClearReservedMotions) {
        void* mu = PlayerController_get_MotionUpdater();
        if (mu) MotionUpdater_ClearReservedMotions(mu);
    }

    // 5. Send battle action packet
    CombatSystem_UseBattleAction(cs, dodgeAction->Data->Id);

    // 6. Force play roll/dodge motion
    if (dodgeAction->_motion && PlayerBehavior_PlayMotionForcely) {
        PlayerBehavior_PlayMotionForcely(localPlayer, dodgeAction->_motion, 1.0f, true);
    }
}

// -------------------------------------------------------------
// Combat Tick Loop (Inside CombatSystem.Update)
// -------------------------------------------------------------

static double g_lastActionTriggerTime = 0.0;

static void OnCombatModUpdate(CombatSystem* cs) {
    if (!cs) return;

    LoadSettings();
    if (!g_settings.enableAutoCounterAttack && !g_settings.enableAutoDodgeRoll) return;

    if (!PlayerBehavior_get_LocalPlayer) return;
    void* localPlayer = PlayerBehavior_get_LocalPlayer();
    if (!localPlayer) return;

    if (PlayerBehavior_get_IsRiding && PlayerBehavior_get_IsRiding(localPlayer)) return;
    if (CharacterBehavior_get_IsAlive && !CharacterBehavior_get_IsAlive(localPlayer)) return;

    Vector3 playerPos = PlayerBehavior_get_CurrentPosition(localPlayer);
    double serverTime = GetServerTime();

    if (serverTime - g_lastActionTriggerTime < 0.35) return;

    // Check Action Slots
    if (!CombatSystem_get_ActionSlots) return;
    Il2CppArray* slots = CombatSystem_get_ActionSlots(cs);
    if (!slots || slots->max_length == 0) return;

    BattleAction* spaceAction = (BattleAction*)slots->vector[0];
    BattleAction* dodgeAction = nullptr;

    for (uintptr_t i = 0; i < slots->max_length; ++i) {
        BattleAction* act = (BattleAction*)slots->vector[i];
        if (!act || !act->Data || !act->Data->Id) continue;
        std::string id = Il2CppStringToStd(act->Data->Id);
        if (id.find("dodge") != std::string::npos ||
            id.find("roll") != std::string::npos ||
            id.find("evade") != std::string::npos) {
            dodgeAction = act;
            break;
        }
    }
    if (!dodgeAction) dodgeAction = spaceAction;

    // Check active alerts
    std::lock_guard<std::mutex> lock(g_alertMutex);
    for (int i = 0; i < 32; ++i) {
        if (!g_alerts[i].active) continue;

        AlertInfo& a = g_alerts[i];
        float timeRemaining = a.finishAt - (float)serverTime;
        if (timeRemaining < -0.2f || timeRemaining > 3.0f) {
            a.active = false;
            continue;
        }

        // Hitbox Math check
        bool inside = false;
        if (a.shape == SHAPE_RECT) {
            float yawRad = a.angle * DEG2RAD;
            float fwdX = sinf(yawRad);
            float fwdZ = cosf(yawRad);
            float latX = fwdZ;
            float latZ = -fwdX;

            float dx = playerPos.x - a.center.x;
            float dz = playerPos.z - a.center.z;

            float projFwd = dx * fwdX + dz * fwdZ;
            float projLat = dx * latX + dz * latZ;

            float halfL = (a.height * 0.5f) + 0.45f;
            float halfW = (a.width * 0.5f) + 0.45f;

            inside = (fabsf(projFwd) <= halfL && fabsf(projLat) <= halfW);
        } else if (a.shape == SHAPE_ARC) {
            float dx = playerPos.x - a.center.x;
            float dz = playerPos.z - a.center.z;
            float distSq = dx * dx + dz * dz;
            float maxR = a.radius + 0.45f;

            if (distSq <= maxR * maxR) {
                if (fabsf(a.endAngle - a.startAngle) >= 355.0f) {
                    inside = true;
                } else {
                    float deg = atan2f(dx, dz) * RAD2DEG;
                    if (deg < 0.0f) deg += 360.0f;
                    float a1 = a.startAngle;
                    float a2 = a.endAngle;
                    while (a1 < 0.0f) a1 += 360.0f;
                    while (a2 < 0.0f) a2 += 360.0f;
                    if (a1 <= a2) inside = (deg >= a1 && deg <= a2);
                    else inside = (deg >= a1 || deg <= a2);
                }
            }
        }

        if (inside && timeRemaining > 0.0f) {
            // Priority 1: Auto Counter Attack
            bool canCounter = false;
            if (spaceAction && spaceAction->Data && spaceAction->Data->Id) {
                std::string sId = Il2CppStringToStd(spaceAction->Data->Id);
                if (sId.find("counter") != std::string::npos || sId.find("parry") != std::string::npos) {
                    canCounter = true;
                }
            }

            if (g_settings.enableAutoCounterAttack && canCounter && timeRemaining <= g_settings.counterAttackTiming) {
                ExecuteCounterCancel(cs, localPlayer, spaceAction, serverTime);
                g_lastActionTriggerTime = serverTime;
                break;
            }
            // Priority 2: Auto Dodge Roll
            else if (g_settings.enableAutoDodgeRoll && timeRemaining <= g_settings.dodgeRollTiming) {
                ExecuteDodgeCancel(cs, localPlayer, dodgeAction, a.center, playerPos, serverTime);
                g_lastActionTriggerTime = serverTime;
                break;
            }
        }
    }
}

static void hook_CombatSystem_Update(void* this_ptr) {
    orig_CombatSystem_Update(this_ptr);
    OnCombatModUpdate((CombatSystem*)this_ptr);
}

// -------------------------------------------------------------
// Module Base Finder & Initialization Thread
// -------------------------------------------------------------

static uintptr_t GetModuleBase(const char* module_name) {
    FILE* fp = fopen("/proc/self/maps", "rt");
    if (!fp) return 0;
    char line[512];
    uintptr_t base = 0;
    while (fgets(line, sizeof(line), fp)) {
        if (strstr(line, module_name)) {
            base = (uintptr_t)strtoull(line, nullptr, 16);
            break;
        }
    }
    fclose(fp);
    return base;
}

static void* InitThread(void*) {
    LOGI("[CombatMod] Native mod thread started. Waiting for libil2cpp.so...");

    while (g_il2cppBase == 0) {
        g_il2cppBase = GetModuleBase("libil2cpp.so");
        if (g_il2cppBase == 0) usleep(100000); // 100ms
    }

    LOGI("[CombatMod] libil2cpp.so found at 0x%lx! Resolving symbols...", g_il2cppBase);

    // Function pointers resolution from dump RVAs
    PlayerBehavior_get_LocalPlayer = (t_PlayerBehavior_get_LocalPlayer)(g_il2cppBase + 0x3EDBA08);
    PlayerBehavior_get_CurrentPosition = (t_PlayerBehavior_get_CurrentPosition)(g_il2cppBase + 0x3EEA30C);
    PlayerBehavior_get_IsRiding = (t_PlayerBehavior_get_IsRiding)(g_il2cppBase + 0x3EEA670);
    CharacterBehavior_get_IsAlive = (t_CharacterBehavior_get_IsAlive)(g_il2cppBase + 0x18C0AD8);
    PlayerBehavior_TurnToYaw = (t_PlayerBehavior_TurnToYaw)(g_il2cppBase + 0x3EEF974);
    PlayerBehavior_PlayMotionForcely = (t_PlayerBehavior_PlayMotionForcely)(g_il2cppBase + 0x3EF1E10);
    Connections_get_Frontend = (t_Connections_get_Frontend)(g_il2cppBase + 0x184B7BC);
    Connection_GetPredictedServerTime = (t_Connection_GetPredictedServerTime)(g_il2cppBase + 0x18481A4);
    PlayerController_get_MotionUpdater = (t_PlayerController_get_MotionUpdater)(g_il2cppBase + 0x3EDDEF4);
    MotionUpdater_ClearReservedMotions = (t_MotionUpdater_ClearReservedMotions)(g_il2cppBase + 0x195D620);
    CombatSystem_UseBattleAction = (t_CombatSystem_UseBattleAction)(g_il2cppBase + 0x3D3C5FC);
    CombatSystem_get_ActionSlots = (t_CombatSystem_get_ActionSlots)(g_il2cppBase + 0x3D394BC);

    // Install Inline Hooks
    LOGI("[CombatMod] Installing A64 Inline Hooks...");

    A64HookFunction((void*)(g_il2cppBase + 0x3E794C0), (void*)hook_FillBorderAlert_SetRect, (void**)&orig_FillBorderAlert_SetRect);
    A64HookFunction((void*)(g_il2cppBase + 0x3E78F38), (void*)hook_FillBorderAlert_SetArc, (void**)&orig_FillBorderAlert_SetArc);
    A64HookFunction((void*)(g_il2cppBase + 0x3E79AF8), (void*)hook_FillBorderAlert_Show, (void**)&orig_FillBorderAlert_Show);
    A64HookFunction((void*)(g_il2cppBase + 0x3E79CA4), (void*)hook_FillBorderAlert_Stop, (void**)&orig_FillBorderAlert_Stop);
    A64HookFunction((void*)(g_il2cppBase + 0x3E79D9C), (void*)hook_FillBorderAlert_OnDisable, (void**)&orig_FillBorderAlert_OnDisable);
    A64HookFunction((void*)(g_il2cppBase + 0x3D3AC68), (void*)hook_CombatSystem_Update, (void**)&orig_CombatSystem_Update);

    LOGI("[CombatMod] All Combat Mod hooks installed successfully!");
    return nullptr;
}

__attribute__((constructor))
void ModMain() {
    LOGI("[CombatMod] libdurangomod.so loaded into process!");
    pthread_t th;
    pthread_create(&th, nullptr, InitThread, nullptr);
    pthread_detach(th);
}
