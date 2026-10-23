using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace XDLands {
    public static class MarkHelper {
        public static void LoadAutoGather(GameObject go) {
            try {
                string path = Path.Combine(Application.dataPath, @"Managed\AutoGatherMod.dll");
                if (!File.Exists(path)) {
                    string parent = Path.GetDirectoryName(Application.dataPath);
                    string test = Path.Combine(parent, "AutoGatherMod.dll");
                    if (File.Exists(test)) path = test;
                    else path = "AutoGatherMod.dll";
                }
                Debug.Log("[AutoGather] Loading: " + path);
                Assembly a = Assembly.LoadFrom(path);
                Type t = a.GetType("AutoGather.AutoGatherController");
                if ((object)t != null) {
                    go.AddComponent(t);
                    Debug.Log("[AutoGather] Successfully loaded and attached AutoGatherController to XDLandsMark!");
                } else {
                    Debug.LogWarning("[AutoGather] AutoGatherController type not found in assembly!");
                }
            } catch (Exception ex) {
                Debug.LogWarning("[AutoGather] Load error: " + ex);
            }
        }
    }
}
