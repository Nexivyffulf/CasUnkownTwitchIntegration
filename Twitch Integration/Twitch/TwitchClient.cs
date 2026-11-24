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
using UnityEngine.UIElements;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;

namespace TwitchIntegration.Twitch;

public class TwitchClient : IDisposable
{
	public bool Connected = false;
	public readonly TwitchAPI Api;
	public readonly string ChannelId;
	private readonly EventSubWebsocketClient eventSub;
	private bool disposed;

	public class Redemption(string userName, string title)
	{
		public readonly string Username = userName;
		public readonly string Title = title;
		public bool fulfilled = false;
	}
	public ConcurrentQueue<Redemption> RedemptionQueue = [];
	public static Dictionary<string, string> Redemptions;
	private Dictionary<string, string> RedeemTitleIds;

	public TwitchClient(TwitchAPI api, EventSubWebsocketClient eventSub, string channelId)  //here
	{
		Api = api;
		ChannelId = channelId;
		this.eventSub = eventSub;

		CreateRedeems();

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
		if (Redemptions.ContainsKey(e.Payload.Event.Reward.Title))
		{
			Plugin.Logger.LogInfo("NEW REDEMPION DING DING DING!!");
			RedemptionQueue.Enqueue(new Redemption(e.Payload.Event.UserName, e.Payload.Event.Reward.Title));
		}
	}

	public void Update()
	{
		if (!PlayerCamera.main && !PauseHandler.paused) //Only when you are playing
			if (RedemptionQueue.TryDequeue(out Redemption redemption)) //Seperate so it doesn't remove
			{
				string[] redeem = Redemptions[redemption.Title.ToLower()].Split(' ');
				switch (Plugin.integrations.Redeems.IndexOf(redeem[0]))
				{
					case 0:
						redemption.fulfilled = Plugin.integrations.BrainDamage();
						break;
					case 1:
						redemption.fulfilled = Plugin.integrations.DrainBatteries();
						break;
					case 2:
						if (redeem[1] == "random")
							redemption.fulfilled = Plugin.integrations.GiveRandomItem(redeem.Length < 3 ? "uwu" : redeem[2]);
						else
							redemption.fulfilled = Plugin.integrations.GiveItem(redeem[1]);
						break;
					case 3:
						redemption.fulfilled = Plugin.integrations.Teleport();
						break;
					case 4:
						redemption.fulfilled = Plugin.integrations.Trip();
						break;
				}
				if (redemption.fulfilled == true)
				{
					Plugin.Logger.LogInfo($"Redeem \"{redemption.Title}\" from {redemption.Username} fullfilled!");
					TILogToConsole($"{redemption.Username} has redeemed {redemption.Title}!");
				}
				else
					Plugin.Logger.LogError($"Incorrect prompt \"{redemption.Title}\".");
			}
	}

	public async void CreateRedeems()
	{
		try
		{
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

				Redemptions.Add(tpcdr[0], tpcdr[3]);
				line = sr.ReadLine();
			}
			sr.Close();
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
			Plugin.Logger.LogInfo("Redeems Murdered!!");
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
		eventSub?.DisconnectAsync().ContinueWith(delegate (Task<bool> task)
		{
			if (!task.Result)
				Plugin.Logger.LogError("Failed to disconnect from EventSub.");
		});
	}
}