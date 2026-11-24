using System;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using TwitchIntegration.Twitch;

namespace TwitchIntegration;

[BepInPlugin(GUID, MODNAME, VERSION)]

public class Plugin : BaseUnityPlugin
{
	public const string GUID = "Nexivyffulf.TwitchIntegration";
	public const string MODNAME = "TwitchIntegration";
	public const string VERSION = "0.2.0";
	internal static new ManualLogSource Logger;
	private readonly Harmony _harmony = new(GUID);
	public static ConfigEntry<bool> AutomaticRedeemHandling;
	public static ConfigEntry<bool> HideRedeemsOnQuit;
	public static ConfigEntry<string> Pagepath;
	public static ConfigEntry<string> Redeemspath;
	public static ConfigEntry<string> OAuthToken;

	public static Plugin Instance { get; private set; } = null;


	public static bool leftconnected = false;   //Certainly a way

	private TwitchClient Client;
	private OAuthLogin _login;
	public static Integrations integrations;
	public void Awake()
	{
		Logger = base.Logger;
		Instance = this;

		AutomaticRedeemHandling = Config.Bind("Settings",
			"Automatically Handle Redeems:",
			true,
			"Will create and dispose of the redeems for you.");

		HideRedeemsOnQuit = Config.Bind("Settings",
			"Should redeems be hidden instaid of killed:",
			false,
			"Lets you keep modified reward settings, like colour, picture, max per person...");

		Pagepath = Config.Bind("File paths",
			"page.html",
			"",
			"The path to page.html to not waste time looking for it.");

		Redeemspath = Config.Bind("File paths",
			"redeems.txt",
			"",
			"The path to redeems.txt to not waste time looking for it.");

		OAuthToken = Config.Bind("DO NOT SHARE THIS AT ALL!",
			"Generated OAuth key:",
			"",
			"This is a where a saved key would go if you pressed \"Store Login Info\" and is empty otherwise.");

		if (!File.Exists(Pagepath.Value) || !File.Exists(Redeemspath.Value))
		{
			Logger.LogInfo("Finding Companion files.");
			string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "BepInEx"), "*", SearchOption.AllDirectories);
			int found = 0;
			foreach (string path in files)
			{
				if (path.Contains("page.html"))
				{
					Pagepath.Value = path.Remove(0, AppContext.BaseDirectory.Length + 1);
					Logger.LogInfo("Found page.html!");
					found++;
				}
				if (path.Contains("Redeems.txt"))
				{
					Redeemspath.Value = path.Remove(0, AppContext.BaseDirectory.Length + 1);
					Logger.LogInfo("Found Redeems.txt!");
					found++;
				}
				if (found == 2)
					break;
			}
			if (found != 2)
				Logger.LogError("Missing Companion files!");
		}
		_harmony.PatchAll();
		Logger.LogInfo($"Plugin {MODNAME} is loaded!");

	}
	public void Connect()
	{
		if (Client != null)
		{
			Client?.Dispose();
			Client = null;
		}
		else
		{
			_login = new OAuthLogin();

			_login.Success += client =>
		   {
			   Logger.LogInfo("Successfully logged into Twitch!");
			   Client = client;
			   _login = null;

			   if ((Settings.settings.Find(s => s.name == "stay_logged_in") as SettingBool).value)
				   OAuthToken.Value = client.Api.Settings.AccessToken;

			   integrations = new();
		   };
			_login.Failure += () =>
			{
				Logger.LogError("Failed to log into Twitch!");
				Settings.settings.Find(s => s.name == "connect_to_twitch").SetValue(false); //Disable again
				SettingsMenu.instance.SelectTab(2); //Top ten ways to make a button update
				_login = null;
			};
		}
	}
	public void OnApplicationQuit()
	{
		if(AutomaticRedeemHandling.Value && Client.Connected)
		Client.KILLRedeems();	//RAAAHHH
	}
	public void Update()    //Go my updates
	{
		_login?.Update();
		Client?.Update();
	}
}

[HarmonyPatch(typeof(Settings), nameof(Settings.DefaultSettings))]
class Settings_DefaultSettings_Patcher
{
	static List<Setting> Postfix(List<Setting> list)
	{
		Plugin.Logger.LogInfo("Adding Settings...");
		list.Insert(0, new SettingBool
		{
			name = "connect_to_twitch",
			value = false,
			apply = delegate
			{
				if (!Plugin.leftconnected)  //Sets to false on game opening so it won't trigger
				{
					(Settings.settings.Find(s => s.name == "connect_to_twitch") as SettingBool).SetValue(false);    //Unpacked private function
					Plugin.leftconnected = true;

					return;
				}
				if ((Settings.settings.Find(s => s.name == "connect_to_twitch") as SettingBool).value)
					Plugin.Instance.Connect();   //Start connection process

			},
			category = Setting.SettingCategory.Game
		});
		list.Insert(1, new SettingBool
		{
			name = "stay_logged_in",
			value = false,
			apply = delegate
			{
				if (!(Settings.settings.Find(s => s.name == "stay_logged_in") as SettingBool).value)
					Plugin.OAuthToken.Value = "";
			},
			category = Setting.SettingCategory.Game
		});
		Plugin.Logger.LogInfo("Settings Added!");
		return list;
	}
}

[HarmonyPatch(typeof(Locale), nameof(Locale.LoadLanguage))]
class Locale_LoadLanguage_Patcher
{
	[HarmonyPriority(500)]  //Other mods may be unaware of add being able to force exit all postfixes
	static void Postfix()
	{
		Plugin.Logger.LogInfo("Adding localization...");
		if (!Locale.currentLang.other.ContainsKey("gamesetconnect_to_twitch"))    //You can write to these first for localization
			Locale.currentLang.other.Add("gamesetconnect_to_twitch", "Connect to Twitch");
		if (!Locale.currentLang.other.ContainsKey("gamesetconnect_to_twitchdsc"))
			Locale.currentLang.other.Add("gamesetconnect_to_twitchdsc", "Activates the mod and will open a page to ask you do authorise \"Grey Planet Live Feed\" and/or fetch the key if you already have.");
		if (!Locale.currentLang.other.ContainsKey("gamesetstay_logged_in"))
			Locale.currentLang.other.Add("gamesetstay_logged_in", "Stay logged in");
		if (!Locale.currentLang.other.ContainsKey("gamesetstay_logged_indsc"))
			Locale.currentLang.other.Add("gamesetstay_logged_indsc", "Saves the OAuth key, so connecting to Twitch does not need to open an external page.");
		Plugin.Logger.LogInfo("Localization added!");
	}
}