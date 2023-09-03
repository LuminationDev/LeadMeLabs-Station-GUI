using System.Net;
using System.Net.NetworkInformation;

namespace Station._qa.checks;

public class NetworkInfo
{
    public string? NetworkInterfaceName { get; set; }
    public string? Description { get; set; }
    public PhysicalAddress? MacAddress { get; set; }
    public string? DefaultGateway { get; set; }
    public string? DnsServer { get; set; }
    public string? AltDnsServer { get; set; }
}

public class NetworkChecks
{
    public NetworkInfo? GetNetworkInterfaceByIpAddress(string ipAddressString)
    {
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
                    NetworkInfo networkInfo = new NetworkInfo
                    {
                        NetworkInterfaceName = networkInterface.Name,
                        Description = networkInterface.Description,
                        MacAddress = networkInterface.GetPhysicalAddress(),
                        DefaultGateway = ipProperties.GatewayAddresses.Count > 0
                            ? ipProperties.GatewayAddresses[0].Address.ToString()
                            : null,
                        DnsServer = ipProperties.DnsAddresses.Count > 0
                            ? ipProperties.DnsAddresses[0].ToString()
                            : null,
                        AltDnsServer = ipProperties.DnsAddresses.Count > 1
                            ? ipProperties.DnsAddresses[1].ToString()
                            : null
                    };
                    
                    return networkInfo;
                }
            }
        }

        return null;
    }
}