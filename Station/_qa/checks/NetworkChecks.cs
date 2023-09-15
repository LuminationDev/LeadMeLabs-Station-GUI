using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace Station._qa.checks;

public class NetworkChecks
{
    public List<QaDetail> GetNetworkInterfaceByIpAddress(string ipAddressString)
    {
        List<QaDetail> qaDetails = new();
        IPAddress ipAddress = IPAddress.Parse(ipAddressString);
        
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (NetworkInterface networkInterface in networkInterfaces)
        {
            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
            
            foreach (UnicastIPAddressInformation unicastAddress in ipProperties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && IPAddress.Equals(unicastAddress.Address, ipAddress))
                {
                    
                    qaDetails.Add(new QaDetail("macAddress", Manager.macAddress));
                    qaDetails.Add(new QaDetail("defaultGateway", ipProperties.GatewayAddresses.Count > 0 ? ipProperties.GatewayAddresses[0].Address.ToString() : ""));
                    qaDetails.Add(new QaDetail("dnsServer", ipProperties.DnsAddresses.Count > 0 ? ipProperties.DnsAddresses[0].ToString() : ""));
                    qaDetails.Add(new QaDetail("altDnsServer", ipProperties.DnsAddresses.Count > 1 ? ipProperties.DnsAddresses[1].ToString() : ""));
                }
            }
        }

        return qaDetails;
    }
}