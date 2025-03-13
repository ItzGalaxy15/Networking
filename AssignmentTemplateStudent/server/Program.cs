using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

public class ServerUDP
{
    static string configFile = @"../Setting.json";
    static Setting? setting;
    static IPEndPoint serverEndpoint;
    static Socket? serverSocket;

    static void LoadSettings()
    {
        try
        {
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading settings file: " + ex.Message);
            Environment.Exit(1);
        }
    }

    public static void start()
    {
        // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]
        try
        {
            LoadSettings();
            if (setting == null || setting.ServerIPAddress == null)
            {
                Console.WriteLine("Invalid configuration file.");
                return;
            }
            
            IPEndPoint serverEndPoint = new(IPAddress.Any, setting.ServerPortNumber);
            Socket serverSocket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(serverEndPoint);
            Console.WriteLine($"[Server] Listening on {setting.ServerIPAddress}:{setting.ServerPortNumber}");

            byte[] buffer = new byte[1024];
            EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                int receivedBytes = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                Console.WriteLine($"[Server] Received: {receivedMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Server] Error: " + ex.Message);
        }
    }
}



        



        // TODO:[Receive and print a received Message from the client]




        // TODO:[Receive and print Hello]



        // TODO:[Send Welcome to the client]


        // TODO:[Receive and print DNSLookup]


        // TODO:[Query the DNSRecord in Json file]

        // TODO:[If found Send DNSLookupReply containing the DNSRecord]



        // TODO:[If not found Send Error]


        // TODO:[Receive Ack about correct DNSLookupReply from the client]


        // TODO:[If no further requests receieved send End to the client]