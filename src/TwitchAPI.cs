using System.ComponentModel.DataAnnotations;
using System.IO;
using System.ComponentModel.Design.Serialization;
using System;
using System.Net;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace DehydrationLieDetector
{
   public class TwitchAPI
   {
      public string OAuth { get; private set; }
      private string AuthorizationOAuth;
      private string AuthorizationBearer;
      private string clientID;
      public string ClientId
      {
         get
         {
            if (string.IsNullOrEmpty(clientID))
            {
               ValidateOAuth();
            }
            return clientID;
         }
      }

      private string login;
      public string Login
      {
         get
         {
            if (string.IsNullOrEmpty(login))
            {
               ValidateOAuth();
            }
            return login;
         }
      }
      public TwitchAPI(string OAuth)
      {
         this.OAuth = OAuth;
         AuthorizationOAuth = $"Authorization: OAuth {OAuth}";
         AuthorizationBearer = $"Authorization: Bearer {OAuth}";
      }
      private void ValidateOAuth()
      {
         var request = (HttpWebRequest)WebRequest.Create("https://id.twitch.tv/oauth2/validate");
         request.Headers.Add(AuthorizationOAuth);
         var jsonResponse = SendJsonRequest(request);
         clientID = jsonResponse["client_id"].Value<string>();
         login = jsonResponse["login"].Value<string>();
      }
      public string GetIdOfChannel(string channelname)
      {
         var url = $"https://api.twitch.tv/helix/search/channels?query={channelname}";
         var request = (HttpWebRequest)WebRequest.Create(url);
         request.Headers.Add(AuthorizationBearer);
         request.Headers.Add($"Client-Id: {ClientId}");
         var response = SendJsonRequest(request);
         return (from channel in response["data"].Children()
                 where channel["display_name"].Value<string>() == channelname
                 select channel["id"].Value<string>()).FirstOrDefault();

      }
      private JObject SendJsonRequest(HttpWebRequest request)
      {
         var response = (HttpWebResponse)request.GetResponse();
         using (var s = response.GetResponseStream())
         {
            var r = new StreamReader(s);
            return JObject.Parse(r.ReadToEnd());
         }
      }


   }
}
