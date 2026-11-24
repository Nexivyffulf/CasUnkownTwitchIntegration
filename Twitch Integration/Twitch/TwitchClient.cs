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

namespace TwitchIntegration.Twitch;

public class TwitchClient : IDisposable
{
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

    public TwitchClient(TwitchAPI api, EventSubWebsocketClient eventSub, string channelId)  //here
    {
        Api = api;
        ChannelId = channelId;
        this.eventSub = eventSub;

        LoadRedeems();
        
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
        Plugin.Logger.LogInfo("NEW REDEMPION DING DING DING!!");
        RedemptionQueue.Enqueue(new Redemption(e.Payload.Event.UserName, e.Payload.Event.Reward.Title));
    }

    public void Update()
    {
        if (!PlayerCamera.main && !PauseHandler.paused) //Only when you are playing
            if (RedemptionQueue.TryDequeue(out Redemption redemption)) //Seperate so it doesn't remove
            {
                string[] redeem = Redemptions[redemption.Title.ToLower()].Split(' ');
                switch (Plugin.integrations.Redeems.IndexOf(redeem[0]))
                {
                    case 1:
                        redemption.fulfilled = Plugin.integrations.BrainDamage();
                        break;
                    case 2:
                        if (redeem[1] == "random")
                            redemption.fulfilled = Plugin.integrations.GiveRandomItem(redeem.Length < 3 ? "uwu" : redeem[2]);
                        else
                            redemption.fulfilled = Plugin.integrations.GiveItem(redeem[1]);
                        break;
                }
                if (redemption.fulfilled == true)
                    Plugin.Logger.LogInfo($"Redeem \"{redemption.Title}\" from {redemption.Username} fullfilled!");
                else
                    Plugin.Logger.LogError($"Incorrect prompt \"{redemption.Title}\".");
            }
    }

    public void LoadRedeems()
    {
        try
        {
            StreamReader sr = new(Plugin.listofredeems);

            string line = sr.ReadLine();    //This will just be the cool title
            line = sr.ReadLine();   //first redeem
            Redemptions = [];
            while (line != null)
            {
                Redemptions.Add(line.Substring(0, line.IndexOf(":")).ToLower(), line.Substring(line.IndexOf(":") + 1, line.Length - 1 - line.IndexOf(":")).ToLower());
                line = sr.ReadLine();
            }

            sr.Close();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("Exception: " + e.Message);
        }
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