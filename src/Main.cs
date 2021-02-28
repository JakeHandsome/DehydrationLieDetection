using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Threading;
using System;
using System.Threading.Tasks;

namespace DehydrationLieDetector
{
   internal static class Program
   {
      public static int Time { get; set; }
      public static async Task Main(string[] args)
      {
         var channelname = "kkcomics";
         var TwitchHttpsAPI = new TwitchAPI(Environment.GetEnvironmentVariable("TWITCH_OAUTH"));
         var channelId = TwitchHttpsAPI.GetIdOfChannel(channelname);
         var ChatAPI = new ChatAPI(
            server: "irc.chat.twitch.tv",
            password: $"oauth:{TwitchHttpsAPI.OAuth}",
            channel: channelname,
            username: TwitchHttpsAPI.Login
         );

         var CancelLoopSourceChat = new CancellationTokenSource();
         var ChatTask = new Task(() => ChatAPI.HandleEventLoop(), CancelLoopSourceChat.Token, TaskCreationOptions.LongRunning);
         var ps = new PubSub(channelId, TwitchHttpsAPI.OAuth, ChatAPI);
         await ps.Connect();
         var CancelLoopSourcePubSub = new CancellationTokenSource();
         var PubSubTask = new Task(() => ps.Subscribe(), CancelLoopSourcePubSub.Token, TaskCreationOptions.LongRunning);

         var main = new ManualResetEvent(false);
         ChatTask.Start();
         PubSubTask.Start();

         var t = new System.Threading.Timer((o) =>
         {
            if (Time > 0)
            {
               Time--;
            }
            else if (Time < 0)
            {
               Time++;
            }
         }, null, 0, 1000);
#if DEBUG
         ChatAPI.SendMessageInChannel($"MrDestructoid This is a Test {Guid.NewGuid()} MrDestructoid");
#else
         ChatAPI.SendMessageInChannel("Dehyrdation Bot online MrDestructoid");
#endif
         main.WaitOne();
      }
   }
}
