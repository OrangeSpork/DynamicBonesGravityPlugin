
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
            AdvancedGravityAdjustment = Config.Bind("Config", "Advanced Gravity", -0.015f, new ConfigDescription("Y Gravity Adjustment", new AcceptableValueRange<float>(-.1f, -0.001f), new ConfigurationManagerAttributes { Order = 1 }));

            Config.SettingChanged += ConfigUpdated;

            Harmony harmony = new Harmony(GUID);
            MethodInfo overrideMethod = AccessTools.Method(typeof(DynamicBone), "Start", null, null);
            harmony.Patch(overrideMethod, new HarmonyMethod(typeof(DynamicBonesGravityPlugin), "OverrideDynamicBonesGravity"), null, null, null);          
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
    }
}
