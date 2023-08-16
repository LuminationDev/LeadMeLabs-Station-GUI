using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;

namespace Station
{
    public static class RemoteAccess
    {
        private static readonly string remoteConfigFilePath =
            $"{CommandLine.stationLocation}\\_config\\remote-config.env";

        private static string remoteRefreshToken = "";
        private static string remoteAccessToken = "";
        private static string remoteUid = "";
        private static bool? remoteConfigIsEnabled = null;
        private static DateTime expiresAt = DateTime.Now;

        public async static Task<bool> IsRemoteConfigEnabled()
        {
            if (remoteConfigIsEnabled == null)
            {
                remoteConfigIsEnabled = File.Exists(remoteConfigFilePath);
            }

            return remoteConfigIsEnabled ?? false;
        }

        private async static Task LoadRemoteConfig()
        {
            string text = File.ReadAllText(remoteConfigFilePath);
            if (text.Length == 0)
            {
                Logger.WriteLog($"Warning, Remote config file empty:{remoteConfigFilePath}", MockConsole.LogLevel.Debug);
                return;
            }

            string decryptedText = EncryptionHelper.DecryptNode(text);

            foreach (var line in decryptedText.Split('\n'))
            {
                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    if (parts.Length == 1)
                    {
                        MockConsole.WriteLine($"Remote config error, config incomplete:{parts[0]} has no value", MockConsole.LogLevel.Error);
                        return;
                    }

                    if (parts[0].Equals("uid"))
                    {
                        remoteUid = parts[1];
                    }
                    if (parts[0].Equals("refreshToken"))
                    {
                        remoteRefreshToken = parts[1];
                    }
                }
            }
        }

        public async static Task<string> GetAccessToken()
        {
            if (!await IsRemoteConfigEnabled())
            {
                return remoteAccessToken;
            }
            if (String.IsNullOrEmpty(remoteAccessToken))
            {
                string refreshToken = await GetRemoteRefreshToken();
                if (!String.IsNullOrEmpty(refreshToken))
                {
                    await LoadAccessToken(refreshToken);
                    return remoteAccessToken;
                }
            }

            if (DateTime.Now > expiresAt)
            {
                string refreshToken = await GetRemoteRefreshToken();
                if (!String.IsNullOrEmpty(refreshToken))
                {
                    await LoadAccessToken(refreshToken);
                }
            }

            return remoteAccessToken;
        }
        
        public async static Task<string> GetRemoteUid()
        {
            return remoteUid;
        }

        private async static Task LoadAccessToken(string refreshToken)
        {
            using var httpClient = new HttpClient();
            string strJSON = String.Format("{{\n\t\"grant_type\": \"refresh_token\",\n\t\"refresh_token\":\"{0}\"\n}}", refreshToken);
        
            StringContent objData = new StringContent(strJSON, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(
                $"https://securetoken.googleapis.com/v1/token?key=AIzaSyA5O7Ri4P6nfUX7duZIl19diSuT-wxICRc",
                objData
            );
            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JObject.Parse(responseString);
            if (!responseData.ContainsKey("access_token") || !responseData.ContainsKey("expires_in"))
            {
                // todo report error
                return;
            }
            remoteAccessToken = responseData.GetValue("id_token").ToString();
            expiresAt = DateTime.Now.AddSeconds(Int32.Parse(responseData.GetValue("expires_in").ToString()));
        }

        public async static Task<string> GetRemoteRefreshToken()
        {
            if (String.IsNullOrEmpty(remoteRefreshToken))
            {
                if (await IsRemoteConfigEnabled())
                {
                    await LoadRemoteConfig();
                }
            }

            return remoteRefreshToken;
        }
    }
}