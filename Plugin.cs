﻿using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using Jotunn.Utils;
using System.Reflection;
using UnityEngine;
using BepInEx.Bootstrap;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using DunGen;
using UnityEngine.UIElements;

namespace CustomHUD
{
    [BepInPlugin("me.eladnlg.customhud", "Elads HUD", "1.2.3")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        public AssetBundle assets;
        public GameObject HUD;

        internal static ConfigEntry<PocketFlashlightOptions> pocketedFlashlightDisplayMode;
        internal static ConfigEntry<StaminaTextOptions> detailedStamina;
        internal static ConfigEntry<bool> displayTimeLeft;
        internal static ConfigEntry<float> hudScale;
        internal static ConfigEntry<bool> autoHideHealthbar;
        internal static ConfigEntry<float> healthbarHideDelay;
        internal static ConfigEntry<bool> hidePlanetInfo;
        public static ConfigEntry<bool> shouldDoKGConversion;

        private void Awake()
        {
            if (instance != null) {
                throw new System.Exception("what the cuck??? more than 1 plugin instance.");
            }
            instance = this;
            // Plugin startup logic
            hudScale = Config.Bind("General", "HUDScale", 1f, "The size of the HUD.");
            autoHideHealthbar = Config.Bind("General", "HideHealthbarAutomatically", true, "Should the healthbar be hidden after not taking damage for a while.");
            healthbarHideDelay = Config.Bind("General", "HealthbarHideDelay", 4f, "The amount of time before the healthbar starts fading away.");
            pocketedFlashlightDisplayMode = Config.Bind("General", "FlashlightBattery", PocketFlashlightOptions.Separate,
@"How the flashlight battery is displayed whilst unequipped.
Disabled - Flashlight battery will not be displayed.
Vanilla - Flashlight battery will be displayed when you don't have a battery-using item equipped.
Separate - Flashlight battery will be displayed using a dedicated panel. (recommended)");
            detailedStamina = Config.Bind("General", "DetailedStamina", StaminaTextOptions.PercentageOnly, 
@"What the stamina text should display.
Disabled - The stamina text will be hidden.
PercentageOnly - Only the percentage will be displayed. (recommended)
Full - Both percentage and rate of gain/loss will be displayed.");
            displayTimeLeft = Config.Bind("General", "DisplayTimeLeft", true, "Should the uses/time left for a battery-using item be displayed.");
            hidePlanetInfo = Config.Bind("General", "HidePlanetInfo", false, "Should planet info be hidden. If modifying from an in-game menu, this requires you to rejoin the game.");
            shouldDoKGConversion = Config.Bind("General", "UseMetricUnits", false, "Use Metric weight unit (kg) for the weight display");

            Logger.LogInfo($"Plugin Elad's HUD is loaded!");

            // load hud
            assets = AssetUtils.LoadAssetBundleFromResources("customhud", typeof(PlayerPatches).Assembly);
            HUD = assets.LoadAsset<GameObject>("PlayerInfo");

            // patch game
            var harmony = new Harmony("me.eladnlg.customhud");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void Start()
        {
            Logger.LogInfo(Chainloader.PluginInfos.Count + " plugins loaded");
            foreach (var chain in Chainloader.PluginInfos)
            {
                Logger.LogInfo("Plugin GUID: " + chain.Value.Metadata.GUID);
            }

            if (Chainloader.PluginInfos.Any(pair => pair.Value.Metadata.GUID == "com.zduniusz.lethalcompany.lbtokg")) {
                shouldDoKGConversion.Value = true;
            }
        }
    }

    [HarmonyPatch(typeof(HUDManager))]
    public class HUDPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void Awake_Postfix(HUDManager __instance)
        {
            HUDElement[] elements = __instance.GetPrivateField<HUDElement[]>("HUDElements");
            HUDElement topLeftCorner = elements[2];

            GameObject HUD = Object.Instantiate(Plugin.instance.HUD, topLeftCorner.canvasGroup.transform.parent);
            HUD.transform.localScale = Vector3.one * 0.75f * Plugin.hudScale.Value;

            topLeftCorner.canvasGroup.alpha = 0;

            // fix for planetary info not showing when landing
            Transform cinematicGraphics = topLeftCorner.canvasGroup.transform.Find("CinematicGraphics");
            if (cinematicGraphics != null && !Plugin.hidePlanetInfo.Value)
            {
                cinematicGraphics.SetParent(HUD.transform.parent);
            }
            elements[2].canvasGroup = HUD.GetComponent<CanvasGroup>();
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB))]
    public static class PlayerPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("LateUpdate")]
        static void LateUpdate_Prefix(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner || (__instance.IsServer && !__instance.isHostPlayerObject))
                return;

            if (CustomHUD_Mono.instance == null)
                return;

            CustomHUD_Mono.instance.UpdateFromPlayer(__instance);
        }
    }
}
