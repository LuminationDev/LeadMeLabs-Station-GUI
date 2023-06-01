using NetFwTypeLib;
using System;
using System.Diagnostics;

namespace Station
{
    public static class FirewallManagement
    {
        public static string IsProgramAllowedThroughFirewall()
        {
            string programPath = GetExecutablePath();

            INetFwPolicy2 firewallPolicy = GetFirewallPolicy();

            NET_FW_ACTION_ action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;

            Trace.WriteLine(GetExecutablePath());

            foreach (INetFwRule rule in firewallPolicy.Rules)
            {
                if (rule.ApplicationName != null)
                {
                    if (rule.Action == action && rule.ApplicationName.Equals(programPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return "Allowed";
                    }
                }
            }

            return "Not allowed";
        }

        private static INetFwPolicy2 GetFirewallPolicy()
        {
            Type type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            return Activator.CreateInstance(type) as INetFwPolicy2;
        }

        private static string GetExecutablePath()
        {
            string executablePath = null;

            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                if (currentProcess != null)
                {
                    executablePath = currentProcess.MainModule.FileName;
                }
            }
            catch
            {
                // Handle any exceptions that occur during path retrieval
            }

            return executablePath;
        }
    }
}
