using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using TwitchIntegration.Twitch;
using UnityEngine;

namespace TwitchIntegration;

[BepInPlugin(GUID, MODNAME, VERSION)]

public class Plugin : BaseUnityPlugin
{
	public const string GUID = "Nexivyffulf.TwitchIntegration";
	public const string MODNAME = "TwitchIntegration";
	public const string VERSION = "0.1.0.0";
	internal static new ManualLogSource Logger;
	private readonly Harmony _harmony = new(GUID);
	public static ConfigEntry<string> OAuthToken;
	public static Plugin Instance { get; private set; } = null;

	public static bool leftconnected = false;   //Certainly a way

	public TwitchClient Client;
	private OAuthLogin _login;

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
		   };
			_login.Failure += () =>
			{
				Logger.LogError("Failed to log into Twitch!");
				Settings.settings.Find(s => s.name == "connect_to_twitch").SetValue(false); //Disable again
				SettingsMenu.instance.SelectTab(2);	//Top ten ways to make a button update
				_login = null;
			};
		}
	}
	public void Awake()
	{
		Logger = base.Logger;
		Instance = this;

		OAuthToken = Config.Bind("DO NOT SHARE THIS AT ALL!",
								 "Generated OAuth key:",
								 "",
								 "This is a where a saved key would go if you pressed \"Store Login Info\" and is empty otherwise.");

		_harmony.PatchAll();
		Logger.LogInfo($"Plugin {MODNAME} is loaded!");
	}
	public void Update()	//Go my updates
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
		list.Add(new SettingBool
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
		list.Add(new SettingBool
		{
			name = "stay_logged_in",
			value = Plugin.OAuthToken.Value != "",
			apply = delegate
			{
				if (!(Settings.settings.Find(s => s.name == "stay_logged_in") as SettingBool).value)
					Plugin.OAuthToken.Value = "";
			},
			category = Setting.SettingCategory.Game
		});
		Plugin.Logger.LogInfo("Settings Added.");
		return list;
	}
}