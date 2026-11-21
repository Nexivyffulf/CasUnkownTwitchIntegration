using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

namespace TwitchIntegration.Twitch;

public class TwitchClient : IDisposable
{
    public readonly TwitchAPI Api;
    public readonly string ChannelId;
    private readonly EventSubWebsocketClient eventSub;
    public bool? ChannelPointsAvailable { get; private set; }
    private bool disposed;

    public class Redemption(string userName, string prompt)
    {
        public readonly string Username = userName;
        public readonly string Prompt = prompt;
        public bool fulfilled = false;
    }
    public ConcurrentQueue<Redemption> RedemptionQueue = [];
    
    public TwitchClient(TwitchAPI api, EventSubWebsocketClient eventSub, string channelId)
    {
        Api = api;
        ChannelId = channelId;
        this.eventSub = eventSub;

        this.eventSub.ErrorOccurred += delegate (object _, ErrorOccuredArgs args)
        {
            Plugin.Logger.LogError(args.Message + "\n" + args.Exception);
        };
        this.eventSub.WebsocketConnected += OnConnected;
        this.eventSub.WebsocketDisconnected += OnDisconnected;
        this.eventSub.ChannelPointsCustomRewardRedemptionAdd += OnRedemption;
    }

    private async void OnConnected(object sender, WebsocketConnectedArgs e)
    {
        await Api.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", new Dictionary<string, string> { { "broadcaster_user_id", ChannelId } }, EventSubTransportMethod.Websocket, eventSub.SessionId);
    }

    private async void OnDisconnected(object sender, EventArgs e)
    {
        int delay = 1000;
        while (!await eventSub.ReconnectAsync() && !disposed)
        {
            await Task.Delay(delay);
            delay = Math.Min(delay * 2, 30000);
        }
    }
    

    private void OnRedemption(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        Plugin.Logger.LogInfo("NEW REDEMPION DING DING DING!!");
        RedemptionQueue.Enqueue(new Redemption(e.Notification.Payload.Event.UserName, e.Notification.Payload.Event.Reward.Prompt));
    }

    public void Update()
    {
        if (RedemptionQueue.TryDequeue(out var redemption))
        {
            string redeem = redemption.Prompt.Substring(0, redemption.Prompt.IndexOf('_')).ToLower();
            string item = redemption.Prompt.Substring(redemption.Prompt.LastIndexOf('_'),redemption.Prompt.Length).ToLower();
            if (redeem == "give")
                if (redemption.Prompt.ToLower().IndexOf("random") != 0)
                    redemption.fulfilled = Integrations.CUIntegrations.GiveRandomItem(item);
                else
                    redemption.fulfilled = Integrations.CUIntegrations.GiveItem(item);

            if (redemption.fulfilled == true)
                Plugin.Logger.LogInfo($"Redeem \"{redemption.Prompt}\" from {redemption.Username} fullfilled!");
            else
                Plugin.Logger.LogError($"Incorrect prompt \"{redemption.Prompt}\".");
        }
    }

    public Task<bool> Connect(Uri uri = null)
    {
        return eventSub.ConnectAsync(uri);
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