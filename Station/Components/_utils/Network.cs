using System;
using System.Net.Http;

namespace Station.Components._utils;

public static class Network
{
    private static bool connected = RunInternetCheck();

    private static bool RunInternetCheck()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync("https://leadme-internal.sgp1.vultrobjects.com/Station/version").GetAwaiter().GetResult();
            connected = response.IsSuccessStatusCode;
            if (!connected)
            {
                // backup in case they haven't allowlisted vultr yet
                var responseBackup = httpClient.GetAsync("http://learninglablauncher.herokuapp.com/program-station-version").GetAwaiter().GetResult();
                connected = responseBackup.IsSuccessStatusCode;
            }
            return connected;
        }
        catch
        {
            return false;
        }
    }

    public static bool CheckIfConnectedToInternet(bool refresh = false)
    {
        if (refresh)
        {
            RunInternetCheck();
        }
        return connected;
    }
}
