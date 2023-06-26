﻿using System;
using System.Net.Http;

namespace Station
{ 
    public class Network
    {
        private static readonly bool connected = RunInternetCheck();

        private static bool RunInternetCheck()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var response = httpClient.GetAsync("http://learninglablauncher.herokuapp.com/program-station-version").GetAwaiter().GetResult();
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckIfConnectedToInternet()
        {
            return connected;
        }
    }
}
