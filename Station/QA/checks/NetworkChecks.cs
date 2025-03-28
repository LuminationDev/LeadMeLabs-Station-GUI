﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using LeadMeLabsLibrary;
using Station.MVC.Controller;

namespace Station.QA.checks;

public class NetworkChecks
{
    private List<QaCheck> _qaChecks = new();
    
    public List<QaCheck> RunQa(string networkType)
    {
        _qaChecks = new List<QaCheck>();
        if (networkType.Equals("Milesight"))
        {
            _qaChecks.AddRange(GetNetworkInterfaceChecks());
        }
        else
        {
            _qaChecks.Add(IsStaticIpAddressPresent());
        }
        
        _qaChecks.Add(IsAllowedThroughFirewall());
        _qaChecks.Add(IsLauncherAllowedThroughFirewall());
        _qaChecks.Add(CanAccessStationHeroku());
        _qaChecks.Add(CanAccessLauncherHeroku());
        return _qaChecks;
    }
    
    /// <summary>
    /// Check if the currently active ip address is a statically set one.
    /// </summary>
    private QaCheck IsStaticIpAddressPresent()
    {
        QaCheck qaCheck = new QaCheck("static_ip_is_present");

        IPAddress ipAddress = IPAddress.Parse(MainController.localEndPoint.Address.ToString());
        
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (NetworkInterface networkInterface in networkInterfaces)
        {
            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

            foreach (UnicastIPAddressInformation unicastAddress in ipProperties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && IPAddress.Equals(unicastAddress.Address, ipAddress))
                {
                    if (unicastAddress.AddressPreferredLifetime == uint.MaxValue)
                    {
                        qaCheck.SetPassed($"{ipAddress} is a static IP address.");
                    }
                    else
                    {
                        qaCheck.SetFailed($"{ipAddress} is not a static IP address.");
                    }
                }
            }
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Is program allowed through firewall
    /// </summary>
    private QaCheck IsAllowedThroughFirewall()
    {
        QaCheck qaCheck = new QaCheck("allowed_through_firewall");

        string result = FirewallManagement.IsProgramAllowedThroughFirewall();
        if (result.Equals("Allowed"))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("Program not allowed through firewall");
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Is launcher allowed through firewall
    /// </summary>
    private QaCheck IsLauncherAllowedThroughFirewall()
    {
        QaCheck qaCheck = new QaCheck("launcher_allowed_through_firewall");

        //Only need to parse the executable name, this way it can be located anywhere on the computer
        string result = FirewallManagement.IsProgramAllowedThroughFirewall("LeadMe.exe");
        if (result.Equals("Allowed"))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("Program not allowed through firewall");
        }

        return qaCheck;
    }
    
    private QaCheck CanAccessStationHeroku()
    {
        QaCheck qaCheck = new QaCheck("can_access_station_hosting");
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync("https://leadme-internal.sgp1.vultrobjects.com/Station/version").GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                qaCheck.SetPassed(null);
            }
            else
            {
                qaCheck.SetFailed("Accessing station heroku failed with status code: " + response.StatusCode);
            }
        }
        catch (Exception e)
        {
            qaCheck.SetFailed("Accessing station heroku failed with exception: " + e);
        }

        return qaCheck;
    }
    
    private QaCheck CanAccessLauncherHeroku()
    {
        QaCheck qaCheck = new QaCheck("can_access_launcher_hosting");
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync("https://leadme-tools.sgp1.vultrobjects.com/leadme-launcher/latest.yml").GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                qaCheck.SetPassed(null);
            }
            else
            {
                qaCheck.SetFailed("Accessing launcher heroku failed with status code: " + response.StatusCode);
            }
        }
        catch (Exception e)
        {
            qaCheck.SetFailed("Accessing launcher heroku failed with exception: " + e);
        }

        return qaCheck;
    }
    
    public List<QaCheck> GetNetworkInterfaceChecks()
    {
        List<QaCheck> qaChecks = new();
        IPAddress ipAddress = IPAddress.Parse(MainController.localEndPoint.Address.ToString());
        
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
                        if (ipProperties.GatewayAddresses[0].ToString()?.Equals("12.245.42.1") ?? false)
                        {
                            defaultGateway.SetPassed(null);
                        }
                        else
                        {
                            defaultGateway.SetFailed($"Default gateway {ipProperties.GatewayAddresses[0]} does not match expected default gateway");
                        }
                    }
                    else
                    {
                        defaultGateway.SetFailed("Could not find default gateway");
                    }
                    
                    QaCheck dnsServer = new QaCheck("dns_server_is_correct");
                    if (ipProperties.DnsAddresses.Count > 0)
                    {
                        if (ipProperties.DnsAddresses[0].ToString().Equals("192.168.1.1"))
                        {
                            dnsServer.SetPassed(null);
                        }
                        else
                        {
                            dnsServer.SetFailed($"DNS server {ipProperties.DnsAddresses[0]} does not match expected DNS server");
                        }
                    }
                    else
                    {
                        dnsServer.SetFailed("Could not find DNS server");
                    }
                    
                    QaCheck altDnsServer = new QaCheck("alt_dns_server_is_correct");
                    if (ipProperties.DnsAddresses.Count > 1)
                    {
                        if (ipProperties.DnsAddresses[1].ToString().Equals("8.8.8.8"))
                        {
                            altDnsServer.SetPassed(null);
                        }
                        else
                        {
                            altDnsServer.SetFailed($"Alt DNS server {ipProperties.DnsAddresses[1]} does not match expected alt DNS server");
                        }
                    }
                    else
                    {
                        altDnsServer.SetFailed("Could not find alt DNS server");
                    }
                    
                    QaCheck staticIpAddress = new QaCheck("static_ip_is_default");
                    string expectedAddress =
                        $"12.245.42.1{Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}";
                    if (MainController.localEndPoint.Address.ToString().Equals(expectedAddress))
                    {
                        staticIpAddress.SetPassed(null);
                    }
                    else
                    {
                        staticIpAddress.SetWarning($"Actual IP address {MainController.localEndPoint.Address} did not match expected IP address {expectedAddress}");
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