using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;
using IrcDotNet;
using Newtonsoft.Json;

namespace DehydrationLieDetector
{
   public class ChatAPI
   {
      public string Server { get; private set; }
      public string Password { get; private set; }
      public string Channel { get; private set; }
      public string Username { get; private set; }
      private IrcDotNet.TwitchIrcClient TwitchClient;
      public ChatAPI(string server, string password, string channel, string username)
      {
         Server = server;
         Password = password;
         Channel = channel;
         Username = username;
         TwitchClient = new IrcDotNet.TwitchIrcClient();
         TwitchClient.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
         TwitchClient.Disconnected += IrcClient_Disconnected;
         TwitchClient.Registered += IrcClient_Registered;
         // Wait until connection has succeeded or timed out.
         using (var registeredEvent = new ManualResetEventSlim(false))
         {
            using (var connectedEvent = new ManualResetEventSlim(false))
            {
               TwitchClient.Connected += (sender2, e2) => connectedEvent.Set();
               TwitchClient.Registered += (sender2, e2) => registeredEvent.Set();
               TwitchClient.Connect(Server, false, new IrcUserRegistrationInfo()
               {
                  NickName = Username,
                  Password = Password,
                  UserName = Username
               });
               if (!connectedEvent.Wait(10000))
               {
                  throw new InvalidOperationException($"Connection to '{Server}' timed out.");
               }
            }
            Console.Out.WriteLine("Now connected to '{0}'.", Server);
            if (!registeredEvent.Wait(10000))
            {
               throw new InvalidOperationException($"Could not register to '{Server}'.");
            }
            Console.Out.WriteLine("Now registered to '{0}' as '{1}'.", Server, Username);
         }
      }
      public void HandleEventLoop()
      {
         JoinChannel(TwitchClient, Channel);
         bool isExit = false;
         while (!isExit)
         {
            Console.Write("> ");
            var command = Console.ReadLine();
            switch (command)
            {
               case "exit":
                  isExit = true;
                  SendMessageInChannel("Dehydration Bot Offline BibleThump");
                  File.WriteAllText("persist.json", JsonConvert.SerializeObject(new { Time = Program.Time }));
                  Thread.Sleep(1000);
                  Environment.Exit(0);
                  break;
               default:
                  if (!string.IsNullOrEmpty(command))
                  {
                     if (command.StartsWith("/") && command.Length > 1)
                     {
                        TwitchClient.SendRawMessage(command.Substring(1));
                     }
                     else if (command.StartsWith("#") && command.Length > 1)
                     {
                        SendMessageInChannel(TwitchClient, Channel, command.Substring(1));
                     }
                     else
                     {
                        Console.WriteLine("unknown command '{0}'", command);
                     }
                  }
                  break;
            }
         }
         TwitchClient.Disconnect();
      }

      private void IrcClient_Registered(object sender, EventArgs e)
      {
         var client = (IrcClient)sender;

         client.LocalUser.NoticeReceived += IrcClient_LocalUser_NoticeReceived;
         client.LocalUser.MessageReceived += IrcClient_LocalUser_MessageReceived;
         client.LocalUser.JoinedChannel += IrcClient_LocalUser_JoinedChannel;
         client.LocalUser.LeftChannel += IrcClient_LocalUser_LeftChannel;
      }

      private void IrcClient_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
      {
         var localUser = (IrcLocalUser)sender;

         e.Channel.UserJoined -= IrcClient_Channel_UserJoined;
         e.Channel.UserLeft -= IrcClient_Channel_UserLeft;
         e.Channel.MessageReceived -= IrcClient_Channel_MessageReceived;
         e.Channel.NoticeReceived -= IrcClient_Channel_NoticeReceived;

         Console.WriteLine("You left the channel {0}.", e.Channel.Name);
      }

      private void IrcClient_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
      {
         var localUser = (IrcLocalUser)sender;

         e.Channel.UserJoined += IrcClient_Channel_UserJoined;
         e.Channel.UserLeft += IrcClient_Channel_UserLeft;
         e.Channel.MessageReceived += IrcClient_Channel_MessageReceived;
         e.Channel.NoticeReceived += IrcClient_Channel_NoticeReceived;

         Console.WriteLine("You joined the channel {0}.", e.Channel.Name);
      }

      private void IrcClient_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
      {
         var channel = (IrcChannel)sender;

         Console.WriteLine("[{0}] Notice: {1}.", channel.Name, e.Text);
      }

      private void IrcClient_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
      {
         var channel = (IrcChannel)sender;
         if (e.Source is IrcUser)
         {
            if (e.Text.ToLower().Contains("dehydrationliedetection"))
            {
               SendMessageInChannel($"@{e.Source.Name} I am a robot. Look at my insides https://github.com/JakeHandsome/DehydrationLieDetection.");
            }
            else if (e.Text.ToLower().StartsWith("!drink"))
            {
               SendDehydrationUpdate();
            }
#if DEBUG
            else if (e.Text.ToLower().StartsWith("!debug"))
            {
               Program.Time = Int32.Parse(e.Text.Split(' ')[1]);
               SendDehydrationUpdate();
            }
#endif
            // Read message.
            Console.WriteLine("[{0}]({1}): {2}.", channel.Name, e.Source.Name, e.Text);
         }
         else
         {
            Console.WriteLine("[{0}]({1}) Message: {2}.", channel.Name, e.Source.Name, e.Text);
         }
      }

      private static void IrcClient_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
      {
         var channel = (IrcChannel)sender;
         Console.WriteLine("[{0}] User {1} left the channel.", channel.Name, e.ChannelUser.User.NickName);
      }

      private static void IrcClient_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
      {
         var channel = (IrcChannel)sender;
         Console.WriteLine("[{0}] User {1} joined the channel.", channel.Name, e.ChannelUser.User.NickName);
      }

      private static void IrcClient_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
      {
         var localUser = (IrcLocalUser)sender;

         if (e.Source is IrcUser)
         {
            // Read message.
            Console.WriteLine("({0}): {1}.", e.Source.Name, e.Text);
         }
         else
         {
            Console.WriteLine("({0}) Message: {1}.", e.Source.Name, e.Text);
         }
      }

      private static void IrcClient_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
      {
         var localUser = (IrcLocalUser)sender;
         Console.WriteLine("Notice: {0}.", e.Text);
      }

      private static void IrcClient_Disconnected(object sender, EventArgs e)
      {
         var client = (IrcClient)sender;
      }

      private static void IrcClient_Connected(object sender, EventArgs e)
      {
         var client = (IrcClient)sender;
      }

      private static void JoinChannel(TwitchIrcClient client, string channel)
      {
         client.SendRawMessage($"join #{channel}");
      }

      public void SendMessageInChannel(string message)
      {
         SendMessageInChannel(TwitchClient, Channel, message);
      }
      static void SendMessageInChannel(TwitchIrcClient client, string channel, string message)
      {
         client.SendRawMessage($"privmsg #{channel} : {message}");
      }

      public void SendDehydrationUpdate()
      {
         string displayedTime = GetTimeAsMinimalString(Math.Abs(Program.Time));
         if (Program.Time == 0)
         {
            SendMessageInChannel($"@kkcomics can drink 🌊💦💦💦");
         }
         else if (Program.Time > 0)
         {
            SendMessageInChannel($"@kkcomics cannot drink 🚫💦🚫 for {displayedTime}");
         }
         else
         {
            SendMessageInChannel($"@kkcomics can drink 🌊💦💦💦. Kyle has a {displayedTime} buffer.");
         }
      }

      private string GetTimeAsMinimalString(int time)
      {
         var ts = TimeSpan.FromSeconds(time);
         if (time < 60)
         {
            return ts.ToString("s' seconds'");
         }
         else if (time < 60 * 60)
         {
            return ts.ToString("m':'ss");
         }
         else if (time < 60 * 60 * 24)
         {
            return ts.ToString("h':'mm':'ss");
         }
         else
         {
            return ts.ToString("d' days 'h':'mm':'ss");
         }
      }
   }
}
