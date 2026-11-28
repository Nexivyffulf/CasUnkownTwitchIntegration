using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using TwitchIntegration.Twitch;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TwitchIntegration;

[BepInPlugin(GUID, MODNAME, VERSION)]
public class Plugin : BaseUnityPlugin
{
	public const string GUID = "Nexivyffulf.TwitchIntegration";
	public const string MODNAME = "TwitchIntegration";
	public const string VERSION = "0.2.2";
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
		StartCoroutine(LoadAllMusic());

		Application.wantsToQuit += OnWantsToQuit;

		try
		{
		_harmony.PatchAll();
		Logger.LogInfo($"Plugin {MODNAME} is loaded!");
		}
		catch(Exception e)
		{
			Logger.LogError(e.Message);
		}
	}
	private IEnumerator LoadAllMusic()
	{
		string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "CasualtiesUnknown_Data", "custommusic"), "*.*");
		string[] array = files;
		foreach (string file in array)
		{
			if (!file.EndsWith(".mp3") && !file.EndsWith(".wav") && !file.EndsWith(".ogg"))
			{
				continue;
			}
			string uri = "file://" + file;
			using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
			yield return www.SendWebRequest();
			if (www.result == UnityWebRequest.Result.Success)
			{
				AudioClip content = DownloadHandlerAudioClip.GetContent(www);
				Integrations.integrations.clips.Add(content);
			}
			else
				Logger.LogError("Failed to load: " + file);
		}
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

			   Integrations.integrations = new();
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
	public string TestingHook(string test)
	{
		if (!Client.Connected)
			return "You must at least be connected.";
		Client.RedemptionQueue.Enqueue(new TwitchClient.Redemption("You", ':' + test));
		return $"Testing Redeem: {test}";
	}
	private bool OnWantsToQuit()
	{
		Logger.LogInfo("Cleaning up.");
		Disconnect();
		return true;
	}
	public void Disconnect()
	{
		if (AutomaticRedeemHandling.Value && Client.Connected)
			Client.KILLRedeems();   //RAAAHHH
		Client = null;
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
					(Settings.settings.Find(s => s.name == "connect_to_twitch") as SettingBool).SetValue(false);    //Unpacked private function grrr
					Plugin.leftconnected = true;

					return;
				}
				if ((Settings.settings.Find(s => s.name == "connect_to_twitch") as SettingBool).value)
					Plugin.Instance.Connect();   //Start connection process
				else
					Plugin.Instance.Disconnect();   //Wow amazing behavior

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

[HarmonyPatch(typeof(ConsoleScript), nameof(ConsoleScript.RegisterAllCommands))]
class ConsoleScript_RegisterAllCommands_Patcher
{
	static void Postfix()
	{
		Plugin.Logger.LogInfo("Adding console commands.");

		ConsoleScript.Commands.Add(new Command("TI_TestRedeem", "Test a redeem via command", delegate (string[] args)
		{
			Plugin.Logger.LogInfo(Plugin.Instance.TestingHook(string.Join(" ", args.Skip(1))));
		}, null, ("redeem syntax", "braindamage, command, drainbatteries, give, randomiseliquids, spawnelder, teleport, trip")));

	}
}

[HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.StartRun))]
[HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.StartTutorial))]
class PreRunScript_Startthing_Patcher //Fix these so thay are normal again
{
	static void Prefix()
	{
		Plugin.Logger.LogInfo("Reverting permanent redeem effects...");
		Liquids.Registry = Integrations.originalliquids;
		Plugin.Logger.LogInfo("Reverted!");
	}
}