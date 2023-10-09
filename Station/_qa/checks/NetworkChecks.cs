using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace Station._qa.checks;

public class NetworkChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa()
    {
        _qaChecks.AddRange(GetNetworkInterfaceChecks());
        return _qaChecks;
    }
    
    public List<QaCheck> GetNetworkInterfaceChecks()
    {
        List<QaCheck> qaChecks = new();
        IPAddress ipAddress = IPAddress.Parse(Manager.localEndPoint.Address.ToString());
        
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (NetworkInterface networkInterface in networkInterfaces)
        {
            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
            
            foreach (UnicastIPAddressInformation unicastAddress in ipProperties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && IPAddress.Equals(unicastAddress.Address, ipAddress))
                {
                    QaCheck defaultGateway = new QaCheck("default_gateway_is_correct");
                    if (ipProperties.GatewayAddresses.Count > 0)
                    {
                        if (ipProperties.GatewayAddresses[0].Address.ToString().Equals("12.245.42.1"))
                        {
                            defaultGateway.SetPassed(null);
                        }
                        else
                        {
                            defaultGateway.SetFailed($"Default gateway {ipProperties.GatewayAddresses[0].Address.ToString()} does not match expected default gateway");
                        }
                    }
                    else
                    {
                        defaultGateway.SetFailed("Could not find default gateway");
                    }
                    
                    QaCheck dnsServer = new QaCheck("dns_server_is_correct");
                    if (ipProperties.DnsAddresses.Count > 0)
                    {
                        if (ipProperties.DnsAddresses[0].Address.ToString().Equals("192.168.1.1"))
                        {
                            dnsServer.SetPassed(null);
                        }
                        else
                        {
                            dnsServer.SetFailed($"DNS server {ipProperties.DnsAddresses[0].Address.ToString()} does not match expected default gateway");
                        }
                    }
                    else
                    {
                        dnsServer.SetFailed("Could not find DNS server");
                    }
                    
                    QaCheck altDnsServer = new QaCheck("alt_dns_server_is_correct");
                    if (ipProperties.DnsAddresses.Count > 1)
                    {
                        if (ipProperties.DnsAddresses[1].Address.ToString().Equals("8.8.8.8"))
                        {
                            altDnsServer.SetPassed(null);
                        }
                        else
                        {
                            altDnsServer.SetFailed($"Alt DNS server {ipProperties.DnsAddresses[1].Address.ToString()} does not match expected default gateway");
                        }
                    }
                    else
                    {
                        altDnsServer.SetFailed("Could not find alt DNS server");
                    }
                    
                    QaCheck staticIpAddress = new QaCheck("static_ip_is_default");
                    string expectedAddress =
                        $"12.245.42.1{Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}";
                    if (Manager.localEndPoint.Address.ToString().Equals(expectedAddress))
                    {
                        staticIpAddress.SetPassed(null);
                    }
                    else
                    {
                        staticIpAddress.SetFailed($"Actual IP address {Manager.localEndPoint.Address} did not match expected IP address {expectedAddress}");
                    }
                    
                    qaChecks.Add(defaultGateway);
                    qaChecks.Add(dnsServer);
                    qaChecks.Add(altDnsServer);
                    qaChecks.Add(staticIpAddress);
                }
            }
        }

        return qaChecks;
    }
}