using System;
using System.Net;
using System.Threading;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._network;
using Station.Components._notification;
using Station.Components._scripts;
using Station.Components._utils;

namespace Station.MVC.Controller;

public class MessageController
{
    /// <summary>
    /// On start up or Station address change send the status, steam list and the current volume to the NUC.
    /// </summary>
    public static void InitialStartUp()
    {
        SendResponse("NUC", "Station", "SetValue:status:On");
        SendResponse("NUC", "Station", "SetValue:gameName:");
        SendResponse("Android", "Station", "SetValue:gameId:");
        SendResponse("NUC", "Station", $"SetValue:volume:{CommandLine.GetVolume()}");
    }
    
    /// <summary>
    /// Create a new script thread and start it, passing in the data collected from 
    /// the recently connected client.
    /// </summary>
    public static void RunScript(string data)
    {
        ScriptThread script = new(data);
        Thread scriptThread = new(script.Run);
        scriptThread.Start();
    }

    /// <summary>
    /// Send a response back to the android server detailing what has happened.
    /// </summary>
    public static void SendResponse(string destination, string actionNamespace, string? additionalData, bool writeToLog = true)
    {
        IPAddress? address = null;
        int? port = null;
        
        string source = $"Station,{Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}";
        string? response = $"{source}:{destination}:{actionNamespace}";
        if (additionalData != null)
        {
            response = $"{response}:{additionalData}";
        }
        
        if (destination.StartsWith("QA:"))
        {
            address = IPAddress.Parse(destination.Substring(3).Split(":")[0]);
            port = Int32.Parse(destination.Substring(3).Split(":")[1]);
            response = additionalData;
        }
        if (response == null) return;

        Logger.WriteLog($"Sending: {response}", MockConsole.LogLevel.Normal, writeToLog);

        string? key = Environment.GetEnvironmentVariable("AppKey", EnvironmentVariableTarget.Process);
        if (key is null) {
            Logger.WriteLog("Encryption key not set", MockConsole.LogLevel.Normal);
            return;
        }

        string encryptedText;
        if (MainController.isNucUtf8)
        {
            encryptedText = EncryptionHelper.Encrypt(response, key);
        }
        else
        {
            encryptedText = EncryptionHelper.UnicodeEncrypt(response, key);
        }
        
        SocketClient client = new(encryptedText);
        if (address != null && port != null)
        {
            client.Send(writeToLog, address, port);
        }
        else
        {
            client.Send(writeToLog);
        }
    }
}