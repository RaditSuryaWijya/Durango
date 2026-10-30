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

namespace Doorstop {
    public static class Entrypoint {
        public static void Start() {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string markPath = System.IO.Path.Combine(baseDir, @"XDLandsMark\XDLandsMark.dll");
                if (System.IO.File.Exists(markPath)) {
                    Assembly originalAsm = Assembly.LoadFrom(markPath);
                    Type origEntry = originalAsm.GetType("Doorstop.Entrypoint");
                    if (origEntry != null) {
                        MethodInfo startMethod = origEntry.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                        if (startMethod != null) {
                            startMethod.Invoke(null, null);
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Error memanggil original XDLandsMark: " + ex);
            }

            try {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) {
                    AutoGather.AutoGatherController.EnsureLoaded();
                };
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Gagal daftarkan sceneLoaded: " + ex);
            }

            try {
                AutoGather.AutoGatherController.EnsureLoaded();
            } catch {}
        }
    }
}
