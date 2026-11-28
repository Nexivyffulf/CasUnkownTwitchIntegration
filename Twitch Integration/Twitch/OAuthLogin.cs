using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Interfaces;
using TwitchLib.Communication.Clients;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Extensions;
using UnityEngine;

namespace TwitchIntegration.Twitch;

internal class OAuthLogin : IDisposable
{
	private readonly CancellationTokenSource ctokensource;

	private HttpListener server;

	private Task<TwitchClient> loginTask;

	private static readonly List<AuthScopes> authScopes =
    [
        AuthScopes.Channel_Read_Redemptions,
		AuthScopes.Channel_Manage_Redemptions
	];

	public event Action<TwitchClient> Success;
	public event Action Failure;

	public OAuthLogin()
	{
		ctokensource = new CancellationTokenSource();
		loginTask = Login(ctokensource.Token);
		loginTask.ContinueWith(ValidateLogin);
	}

	public static bool ValidateLogin(Task task)
	{
		if (task.Exception != null)
		{
			foreach (var inner in task.Exception.InnerExceptions)
			{
				Plugin.Logger.LogError("Task failed!\n" + Environment.StackTrace);
				Plugin.Logger.LogError(inner);
			}
		}
		return !task.IsFaulted;
	}

	public void Update()
	{
		if (loginTask != null && loginTask.IsCompleted)
		{
			if (!loginTask.IsFaulted)
			{
				Close();
				loginTask = null;
			}
		}
	}

	private void Close()
	{
		if (loginTask != null && loginTask.IsCompleted && !loginTask.IsFaulted && loginTask.Result != null)
			Success?.Invoke(loginTask.Result);
		else
			Failure?.Invoke();
		Dispose();
	}

	private IServiceCollection GetDefaultServices()
	{
		return new ServiceCollection()
			.AddLogging(logger =>
				{
					logger
						.SetMinimumLevel(LogLevel.Debug)
						.AddProvider(new BepInExLoggerProvider());
				})
			.AddSingleton<IApiSettings>(new ApiSettings()
				{
					Scopes = authScopes
				})
			.AddSingleton<WebSocketClient>()
			.AddSingleton<TwitchAPI>()
			.AddTwitchLibEventSubWebsockets();
	}

	private async Task<TwitchClient> Login(CancellationToken ctoken)
	{
		ServiceProvider services = GetDefaultServices().BuildServiceProvider();
		TwitchAPI api = services.GetRequiredService<TwitchAPI>();
		EventSubWebsocketClient eventSub = services.GetRequiredService<EventSubWebsocketClient>();
		api.Settings.ClientId = Secrets.client_id;  //Grey Planet Live Feed's ID btw not yours.

		ValidateAccessTokenResponse validation = null;

		if (Plugin.OAuthToken.Value != "")
		{
			api.Settings.AccessToken = Plugin.OAuthToken.Value;
			Plugin.Logger.LogInfo("Trying saved login info...");
			validation = await api.Auth.ValidateAccessTokenAsync();

			ctoken.ThrowIfCancellationRequested();
		}
		if (validation == null) //Continue to normal if stored did not work.
		{
			Plugin.Logger.LogInfo("Opening OAuth page.");
			api.Settings.AccessToken = await GetOAuthToken(api, ctoken);

			ctoken.ThrowIfCancellationRequested();
			Plugin.Logger.LogInfo("Validating Login info...");
			validation = await api.Auth.ValidateAccessTokenAsync();

			ctoken.ThrowIfCancellationRequested();
		}
		if (validation == null)
		{
			Plugin.Logger.LogInfo("Failed to login.");
			return null;
		}
		Plugin.Logger.LogInfo("Logged in. UserId=" + validation.UserId + ", Login=" + validation.Login);

		Plugin.Logger.LogInfo("Connecting to Twitch events...");
		TwitchClient client = new(api, eventSub, validation.UserId);

		if (await client.Connect())
		{
			Plugin.Logger.LogInfo("Connected!");
			return client;
		}
		else
		{
			Plugin.Logger.LogError("Failed to connect!");
			return null;
		}
	}

	private async Task<string> GetOAuthToken(TwitchAPI api, CancellationToken ctoken)
	{
		TwitchClient.RunningState = Application.runInBackground;
		Application.runInBackground = true;
		string url = api.Auth.GetAuthorizationCodeUrl("http://localhost:2296/", api.Settings.Scopes).Replace("response_type=code", "response_type=token");

		//Set up server to receive result
		HttpListener server = new();
		server.Prefixes.Add("http://localhost:2296/"); //Waouh, reference (the number is arbitrary)
		this.server = server;
		try
		{
			server.Start();
			ctoken.ThrowIfCancellationRequested();
			string accessToken = null;

			Application.OpenURL(url);
			byte[] response = File.ReadAllBytes(Plugin.Pagepath.Value);
			while (accessToken == null)
			{
				ctoken.ThrowIfCancellationRequested();

				int status = 400;
				string text;

				HttpListenerContext client = await server.GetContextAsync();
				if (client.Request.HttpMethod == "POST")
				{
					string code = new StreamReader(client.Request.InputStream).ReadToEnd();
					string[] array = code.Split('&');
					foreach (string entry in array)
					{
						string[] args = entry.Split('=');
						if (args.Length == 2 && args[0] == "access_token")
						{
							accessToken = args[1];
							break;
						}
					}

					client.Response.ContentType = "text/plain";
					if (accessToken != null)
					{
						ValidateAccessTokenResponse validation = await api.Auth.ValidateAccessTokenAsync(accessToken);
						if (validation == null)
							text = "Error: Couldn't validate login token!";
						else
						{
							if (string.IsNullOrEmpty((await api.Helix.Users.GetUsersAsync([validation.UserId], null, accessToken)).Users[0].BroadcasterType))
								text = "Error: " + validation.Login + " must be a partner or affiliate to use this mod!";
							else
							{
								status = 200;
								text = "Success: Logged in as " + validation.Login + "!";
							}
						}
					}
					else
						text = "Error: Couldn't read login token!";
					client.Response.StatusCode = status;
					client.Response.Close(Encoding.ASCII.GetBytes(text), false);
				}
				else
				{
					string localPath = client.Request.Url.LocalPath;
					if (localPath == "/" || localPath == "/code")
					{
						client.Response.ContentType = "text/html";
						client.Response.StatusCode = 200;
						client.Response.Close(response, false);
					}
					else
					{
						client.Response.StatusCode = 404;
						client.Response.Close();
					}
				}
			}
			ctoken.ThrowIfCancellationRequested();
			return accessToken;
		}
		finally
		{
			server.Close();
		}
	}

	public void Dispose()
	{
		server?.Close();
		ctokensource.Cancel();
		ctokensource.Dispose();
	}
}