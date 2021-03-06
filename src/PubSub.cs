using System.Threading;
using System;
using System.Threading.Tasks;
using Websocket.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DehydrationLieDetector
{
   public class PubSub
   {
      private static Uri url = new Uri("wss://pubsub-edge.twitch.tv");
      private WebsocketClient client;
      private System.Timers.Timer pingTimer;
      private System.Timers.Timer pongTimer;
      private bool pongHappened;
      private string channelId;
      private string authToken;
      private ManualResetEvent initialPong;
      private ChatAPI ChatAPI;
      public PubSub(string channelId, string authToken, ChatAPI chatAPI)
      {
         this.ChatAPI = chatAPI;
         this.authToken = authToken;
         this.channelId = channelId;
         client = new WebsocketClient(url);
         client.MessageReceived.Subscribe(OnMessageReceived);
         pingTimer = new System.Timers.Timer(4.5 * 60 * 1000);
         pingTimer.AutoReset = true;
         pingTimer.Elapsed += PingTimerElapsed;
         pongTimer = new System.Timers.Timer(10 * 1000)
         {
            AutoReset = false,
            Enabled = false
         };
         pongTimer.Elapsed += (o, e) =>
            {
               if (!pongHappened) throw new InvalidOperationException("Did not receive PONG in time");
            };
      }
      private void PingTimerElapsed(object _, EventArgs __)
      {
         if (client.IsStarted)
         {
            SendWebSocketMessage(JsonConvert.SerializeObject(new { type = "PING" }));
            pongTimer.Start();
            pongHappened = false;
         }
      }
      public async Task Connect()
      {
         await client.Start();
      }

      public void Subscribe()
      {
         PingTimerElapsed(null, null);
         pingTimer.Enabled = true;
         initialPong = new ManualResetEvent(false);
         initialPong.WaitOne();
         SendWebSocketMessage(JsonConvert.SerializeObject(new
         {
            type = "LISTEN",
            nonce = Guid.NewGuid().ToString(),
            data = new
            {
               topics = new[] { $"community-points-channel-v1.{channelId}" },
               auth_token = authToken
            }
         }));

         while (true)
         {
            new ManualResetEvent(false).WaitOne();
         }
      }
      public void OnMessageReceived(ResponseMessage msg)
      {
         Console.WriteLine($"Received: { msg.Text}");
         var json = JObject.Parse(msg.Text);
         var type = json["type"].Value<string>();
         switch (type)
         {
            case "PONG":
               pongHappened = true;
               initialPong.Set();
               break;
            case "MESSAGE":
               if (json["data"]["topic"].Value<string>().StartsWith("community-points-channel-v1."))
               {
                  ParseChannelPointRedemption(JObject.Parse(json["data"]["message"].Value<string>()));
               }
               break;
            case "RESPONSE":
               if (!string.IsNullOrEmpty(json["error"].Value<string>()))
               {
                  Console.WriteLine($"Response Error: {json["error"].Value<string>()}");
               }
               break;
            default:
               Console.WriteLine($"Unknown type {type}");
               break;
         }
      }

      public void ParseChannelPointRedemption(JObject data)
      {
         if (data["type"].Value<string>() == "reward-redeemed")
         {
            var userName = data["data"]["redemption"]["user"]["display_name"].Value<string>();
            var reward = data["data"]["redemption"]["reward"];
            var title = reward["title"].Value<string>();
            var cost = reward["cost"].Value<int>();
            switch (title)
            {
               case "Dehydrate":
                  Program.Time += cost + 10;
                  ChatAPI.SendDehydrationUpdate();
                  break;
               case "REhydrate":
                  Program.Time -= cost + 10;
                  ChatAPI.SendDehydrationUpdate();
                  break;
               default:
                  Console.WriteLine($"Redeemed {title}, don't care");
                  break;
            }
         }
      }


      private void SendWebSocketMessage(string text)
      {
         Console.WriteLine($"Sent: {text}");
         client.Send(text);
      }
   }
}
