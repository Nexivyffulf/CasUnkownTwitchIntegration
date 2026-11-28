using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using UnityEngine;
using System.Linq;

namespace TwitchIntegration.Twitch;

public class TwitchClient : IDisposable
{
	public static bool RunningState = Application.runInBackground;
	public bool Connected = false;
	public readonly TwitchAPI Api;
	public readonly string ChannelId;
	private readonly EventSubWebsocketClient eventSub;
	private bool disposed;

	public class Redemption(string userName, string title)
	{
		public readonly string Username = userName;
		public readonly string Title = title;
	}
	public ConcurrentQueue<Redemption> RedemptionQueue = [];
	public static Dictionary<string, string> Redemptions;
	private Dictionary<string, string> RedeemTitleIds;
	private string LyingTitle = "Spawn Thornback Elder";  //For the fake elder spawn

	public TwitchClient(TwitchAPI api, EventSubWebsocketClient eventSub, string channelId)  //here
	{
		Api = api;
		ChannelId = channelId;
		this.eventSub = eventSub;

		this.eventSub.ErrorOccurred += async (_, args) => Plugin.Logger.LogError(args.Message + "\n" + args.Exception);
		this.eventSub.WebsocketConnected += OnConnected;
		this.eventSub.WebsocketDisconnected += OnDisconnected;
		this.eventSub.ChannelPointsCustomRewardRedemptionAdd += OnRedemption;
	}
	public Task<bool> Connect()
	{
		return eventSub.ConnectAsync();
	}
	private async Task OnConnected(object sender, WebsocketConnectedArgs e)
	{
		Connected = true;
		CreateRedeems();

		await Api.Helix.EventSub.CreateEventSubSubscriptionAsync(
			"channel.channel_points_custom_reward_redemption.add", "1",
			new() { { "broadcaster_user_id", ChannelId } },
			EventSubTransportMethod.Websocket,
			eventSub.SessionId);
	}
	private async Task OnDisconnected(object sender, EventArgs e)
	{
		int delay = 1000;
		while (!await eventSub.ReconnectAsync() && !disposed)
		{
			await Task.Delay(delay);
			delay = Math.Min(delay * 2, 30000);
		}
	}
	private async Task OnRedemption(object sender, ChannelPointsCustomRewardRedemptionArgs e)
	{
		if (Redemptions.ContainsKey(e.Payload.Event.Reward.Title.ToLower()))
		{
			Plugin.Logger.LogInfo("NEW REDEMPION DING DING DING!!");
			RedemptionQueue.Enqueue(new Redemption(e.Payload.Event.UserName, e.Payload.Event.Reward.Title));
		}
	}
	public void Update()
	{
		if (!PauseHandler.paused) //Only when you are playing
			if (RedemptionQueue.TryDequeue(out Redemption redemption)) //Seperate so it doesn't remove
			{
				Plugin.Logger.LogInfo("Trying redemption...");
				if (!PlayerCamera.main)
				{
					Plugin.Logger.LogInfo("Skipping Redemption.");
					return;
				}
				try
				{
					bool fulfilled = false;
					string[] redeem = redemption.Title[0] == ':' ? redemption.Title.Substring(1).ToLower().Split(' ') : Redemptions[redemption.Title.ToLower()].Split(' ');
					switch (Integrations.integrations.Redeems.IndexOf(redeem[0]))
					{
						case 0:
							break;
						case 1:
							fulfilled = Integrations.integrations.BrainDamage();
							break;
						case 2:
							if (redeem.Length > 1)
								fulfilled = Integrations.integrations.Command(string.Join(" ", redeem.Skip(1)));
							break;
						case 3:
							fulfilled = Integrations.integrations.DrainBatteries();
							break;
						case 4:
							if (redeem.Length == 3)
								fulfilled = Integrations.integrations.Feed(Convert.ToInt32(redeem[1]), Convert.ToInt32(redeem[2]));
							break;
						case 5:
							if (redeem.Length == 2)
								if (redeem[1] == "random")
									fulfilled = Integrations.integrations.GiveRandomItem(redeem.Length < 3 ? "uwu" : redeem[2]);
								else
									fulfilled = Integrations.integrations.GiveItem(redeem[1]);
							break;
						case 6:
							fulfilled = Integrations.integrations.Light(redeem[1]);
							break;
						case 7:
							if (redeem.Length == 4)
								fulfilled = Integrations.integrations.Opiate(Convert.ToInt32(redeem[1]), Convert.ToInt32(redeem[2]), Convert.ToInt32(redeem[3]));
							break;
						case 8:
							if (redeem.Length == 1)
								fulfilled = Integrations.integrations.PlayMusic();
							else
								fulfilled = Integrations.integrations.PlayMusic(redeem[1]);
							break;
						case 9:
							fulfilled = Integrations.integrations.RandomiseLiquids(string.Concat(redeem.Skip(1)));
							break;
						case 10:
							fulfilled = Integrations.integrations.Spawnelder(redeem.Length >= 2);
							break;
						case 11:
							fulfilled = Integrations.integrations.Swaphands();
							break;
						case 12:
							fulfilled = Integrations.integrations.Teleport();
							break;
						case 13:
							fulfilled = Integrations.integrations.Trip();
							break;
						default:
							fulfilled = Integrations.integrations.CustomRedeem(redeem);   //For other mods if they so wish, you need to be on redeems to get here
							break;
					}
					if (fulfilled == true)
					{
						Plugin.Logger.LogInfo($"Redeem \"{redemption.Title}\" from {redemption.Username} fullfilled!");
						if (redeem[0] == "spawnelder" && redeem[1] == "fake")
							TILogToConsole($"{redemption.Username} has redeemed {LyingTitle}!");
						else
							TILogToConsole($"{redemption.Username} has redeemed {redemption.Title}!");
					}
					else
						Plugin.Logger.LogError($"Incorrect prompt \"{redemption.Title}\".");
				}
				catch (Exception e)
				{
					Plugin.Logger.LogError("Exception: " + e.Message);
				}
			}
	}
	public async void CreateRedeems()
	{
		try
		{
			Plugin.Logger.LogInfo("Adding Redeems to channel...");
			StreamReader sr = new(Plugin.Redeemspath.Value);

			string line = sr.ReadLine();    //This will just be the cool title
			string[] tpcdr;
			Redemptions = [];
			CreateCustomRewardsRequest requestnew;
			UpdateCustomRewardRequest requestupdate;
			var RewardsList = await Api.Helix.ChannelPoints.GetCustomRewardAsync(ChannelId);
			RedeemTitleIds = [];
			foreach (var reward in RewardsList.Data)
				RedeemTitleIds.Add(reward.Title, reward.Id);

			line = sr.ReadLine();   //First redeem
			while (line != null)
			{
				tpcdr = line.Split(':');
				if (Plugin.AutomaticRedeemHandling.Value && Connected)
				{
					if (tpcdr[4] == "spawnelder")
						LyingTitle = tpcdr[0];  //Save name of real spawnelder
					if (RedeemTitleIds.ContainsKey(tpcdr[0]))   //If they already exist, just enable
					{
						requestupdate = new() { IsEnabled = true };
						await Api.Helix.ChannelPoints.UpdateCustomRewardAsync(ChannelId, RedeemTitleIds[tpcdr[0]], requestupdate);
					}
					else
					{
						requestnew = new()
						{
							Title = tpcdr[0],
							Prompt = tpcdr[1],
							Cost = Convert.ToInt32(tpcdr[2]),
							IsGlobalCooldownEnabled = Convert.ToInt32(tpcdr[3]) > 0,
							GlobalCooldownSeconds = Convert.ToInt32(tpcdr[3]) > 0 ? Convert.ToInt32(tpcdr[3]) : null,
							IsEnabled = true
						};
						await Api.Helix.ChannelPoints.CreateCustomRewardsAsync(ChannelId, requestnew);
					}
				}
				Redemptions.Add(tpcdr[0].ToLower(), tpcdr[4]);
				line = sr.ReadLine();
			}
			sr.Close();
			Plugin.Logger.LogInfo("Redeems added!");
		}
		catch (Exception e)
		{
			Plugin.Logger.LogError("Exception: " + e.Message);
		}
	}
	public async void KILLRedeems()
	{
		if (Connected && Plugin.AutomaticRedeemHandling.Value)
		{
			Plugin.Logger.LogInfo("Murdering redeems...");
			if (Plugin.HideRedeemsOnQuit.Value)
			{
				foreach (var redeem in RedeemTitleIds)  //Hide redeems
					if (Redemptions.ContainsKey(redeem.Key))
						await Api.Helix.ChannelPoints.UpdateCustomRewardAsync(ChannelId, redeem.Value, new() { IsEnabled = false });
			}
			else
				foreach (var redeem in RedeemTitleIds)  //KILL REDEEMS
					if (Redemptions.ContainsKey(redeem.Key))
						await Api.Helix.ChannelPoints.DeleteCustomRewardAsync(ChannelId, redeem.Value);
			Plugin.Logger.LogInfo("Redeems murdered!!");
		}
	}

	private void TILogToConsole(string text)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
		ConsoleScript.instance.logs.Add("[<alpha=#55>" + timeSpan.ToString("mm\\:ss") + "<alpha=#FF>] " + text);
		if (ConsoleScript.instance.logs.Count > 100)
			ConsoleScript.instance.logs.RemoveAt(0);
		if (ConsoleScript.instance.active)
			ConsoleScript.instance.logText.text = string.Join("\n", ConsoleScript.instance.logs);
	}
	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		Application.runInBackground = RunningState;
		eventSub?.DisconnectAsync().ContinueWith(delegate (Task<bool> task)
		{
			if (!task.Result)
				Plugin.Logger.LogError("Failed to disconnect from EventSub.");
		});
	}
}