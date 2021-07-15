
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Manager;
using System;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DynamicBonesGravityPlugin
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class DynamicBonesGravityPlugin : BaseUnityPlugin
    {
        public const string GUID = "orange.spork.dynamicbonesgravityplugin";
        public const string PluginName = "DynamicBonesGravityPlugin";
        public const string Version = "1.0.0";

        public static ConfigEntry<bool> PluginEnabled { get; set; }
        public static ConfigEntry<bool> StrongerEnabled { get; set; }
        public static ConfigEntry<bool> RealismEnabled { get; set; }
        public static ConfigEntry<bool> AdvancedModeEnabled { get; set; }
        public static ConfigEntry<float> AdvancedGravityAdjustment { get; set; }
        public static ConfigEntry<bool> AlternateUpdateMode { get; set; }

        public static DynamicBonesGravityPlugin Instance { get; set; }

        internal BepInEx.Logging.ManualLogSource Log => Logger;

        public DynamicBonesGravityPlugin()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Singleton Only.");
            }

            Instance = this;

            PluginEnabled = Config.Bind("Config", "Enabled", false, new ConfigDescription("Enabled (Doubles Default)", null, new ConfigurationManagerAttributes { Order = 5 }));
            StrongerEnabled = Config.Bind("Config", "Stronger!", false, new ConfigDescription("Stronger (4 Times Default)", null, new ConfigurationManagerAttributes { Order = 4 }));
            RealismEnabled = Config.Bind("Config", "Realistic!!!", false, new ConfigDescription("Realish (Compensates for 10x size with 10 Times Default)", null, new ConfigurationManagerAttributes { Order = 3 }));
            AdvancedModeEnabled = Config.Bind("Config", "Advanced Mode", false, new ConfigDescription("Use the values below instead of defaults, control your own destiny!", null, new ConfigurationManagerAttributes { Order = 2 }));
            AdvancedGravityAdjustment = Config.Bind("Config", "Advanced Gravity", -0.015f, new ConfigDescription("Y Gravity Adjustment", new AcceptableValueRange<float>(-1f, -0.001f), new ConfigurationManagerAttributes { Order = 1 }));
            AlternateUpdateMode = Config.Bind("Config", "Alternate Update Mode", false, new ConfigDescription("Calc in FixedUpdate Instead of LateUpdate", null, new ConfigurationManagerAttributes {  Order = -1 }));

            Config.SettingChanged += ConfigUpdated;

            Harmony harmony = new Harmony(GUID);
            MethodInfo overrideMethod = AccessTools.Method(typeof(DynamicBone), "Start", null, null);
            harmony.Patch(overrideMethod, new HarmonyMethod(typeof(DynamicBonesGravityPlugin), "OverrideDynamicBonesGravity"), null, null, null);
            MethodInfo dynamicBoneFixedUpdateMethod = AccessTools.Method(typeof(DynamicBone), "FixedUpdate");
            harmony.Patch(dynamicBoneFixedUpdateMethod, new HarmonyMethod(typeof(DynamicBonesGravityPlugin), "DynamicBoneFixedUpdateOverride"), null, null, null);
            MethodInfo dyanmicBoneLateUpdateMethod = AccessTools.Method(typeof(DynamicBone), "LateUpdate");
            harmony.Patch(dyanmicBoneLateUpdateMethod, new HarmonyMethod(typeof(DynamicBonesGravityPlugin), "DynamicBoneLateUpdateOverride"), null, null, null);
        }        

       

        void Update()
        {
        //    Log.LogInfo(string.Format("Initiative: {0} Mode: {1} ModeCtrl: {2} AInfo: {3}", HSceneFlagCtrl.Instance?.initiative, HSceneManager.Instance?.Hscene?.StartAnimInfo?.ActionCtrl.Item1, HSceneManager.Instance?.Hscene?.StartAnimInfo?.ActionCtrl.Item2, HSceneManager.Instance?.Hscene?.StartAnimInfo));
        }

        static void OverrideDynamicBonesGravity(DynamicBone __instance)
        {
            DynamicBonesGravityPlugin.Instance.UpdateBone(__instance, false, false);
        }     

        public void ConfigUpdated(object sender, SettingChangedEventArgs settingChanged)
        {
            UpdateBones(true);
        }

        public void UpdateBone(DynamicBone dynamicBone, bool configChange, bool updateParameters = true)
        {
            if (dynamicBone.m_Gravity.y != 0.0f && !configChange)
            {
                return;
            }

            Vector3 oldGravity = dynamicBone.m_Gravity;
            if (!PluginEnabled.Value && configChange)
            {
                dynamicBone.m_Gravity = new Vector3(oldGravity.x, 0f, oldGravity.z);
            }
            else if (PluginEnabled.Value && AdvancedModeEnabled.Value)
            {
                dynamicBone.m_Gravity = new Vector3(oldGravity.x, AdvancedGravityAdjustment.Value, oldGravity.z);
            } 
            else if (PluginEnabled.Value)
            {
                if (RealismEnabled.Value)
                {
                    dynamicBone.m_Gravity = new Vector3(oldGravity.x, -0.010f, oldGravity.z);
                }
                else if (StrongerEnabled.Value)
                {
                    dynamicBone.m_Gravity = new Vector3(oldGravity.x, -0.004f, oldGravity.z);
                }
                else
                {
                    dynamicBone.m_Gravity = new Vector3(oldGravity.x, -0.002f, oldGravity.z);
                }
            }

            if (updateParameters)
            {
                dynamicBone.UpdateParameters();
            }
        }

        public void UpdateBones(bool configChange)
        {
            if (!PluginEnabled.Value && !configChange)
            {
                return;
            }
            DynamicBone[] dynamicBones = UnityEngine.Resources.FindObjectsOfTypeAll<DynamicBone>();
            foreach (DynamicBone dynamicBone in dynamicBones)
            {
                UpdateBone(dynamicBone, configChange);
            }
        }


        private static FieldInfo m_distantDisableField = AccessTools.Field(typeof(DynamicBone), "m_DistantDisable");
        private static FieldInfo m_distantDisabledField = AccessTools.Field(typeof(DynamicBone), "m_DistantDisabled");
        private static FieldInfo m_weightField = AccessTools.Field(typeof(DynamicBone), "m_Weight");
        private static MethodInfo preUpdateMethod = AccessTools.Method(typeof(DynamicBone), "PreUpdate");
        private static MethodInfo checkDistanceMethod = AccessTools.Method(typeof(DynamicBone), "CheckDistance");
        private static MethodInfo updateDynamicBonesMethod = AccessTools.Method(typeof(DynamicBone), "UpdateDynamicBones");

        public static bool DynamicBoneFixedUpdateOverride(DynamicBone __instance)
        {
            if (!AlternateUpdateMode.Value)
            {
                return true;
            }

            float m_Weight = (float)m_weightField.GetValue(__instance);
            bool m_DistantDisable = (bool)m_distantDisableField.GetValue(__instance);
            bool m_DistantDisabled = (bool)m_distantDisabledField.GetValue(__instance);

            preUpdateMethod.Invoke(__instance, null);
            if (m_DistantDisable)
            {
                checkDistanceMethod.Invoke(__instance, null);
            }
            if (m_Weight > 0f && (!m_DistantDisable || !m_DistantDisabled))
            {
                updateDynamicBonesMethod.Invoke(__instance, new object[] { Time.fixedDeltaTime });
            }
            return false;
        }

        public static bool DynamicBoneLateUpdateOverride(DynamicBone __instance)
        {
            if (!AlternateUpdateMode.Value)
            {
                return true;
            }
            return false;
        }
    }
}
