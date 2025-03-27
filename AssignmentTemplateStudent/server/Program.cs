using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using LibData;

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
        try
        {
            LoadSettings();
            if (setting == null || string.IsNullOrEmpty(setting.ServerIPAddress) || string.IsNullOrEmpty(setting.ClientIPAddress))
            {
                Console.WriteLine("Invalid configuration file.");
                return;
            }
            IPEndPoint serverEndPoint;
            try
            {
                serverEndpoint = new IPEndPoint(IPAddress.Any, setting.ServerPortNumber);
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverSocket.Connect(serverEndpoint);
                // Console.WriteLine($"[Client] Bound to port {setting.ClientPortNumber}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Client] Port {setting.ClientPortNumber} is already in use. Binding to a random available port.");
                serverEndPoint = new IPEndPoint(IPAddress.Any, 0);
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverSocket.Connect(serverEndPoint);
                Console.WriteLine($"[Client] Bound to random port {((IPEndPoint)serverSocket.LocalEndPoint!).Port}");
            }
            Console.WriteLine($"[Server] Listening on {setting.ServerIPAddress}:{setting.ServerPortNumber}");

            byte[] buffer = new byte[1024];
            EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                int receivedBytes = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                Console.WriteLine($"[Server] Received: {receivedMessage}");

                var message = JsonSerializer.Deserialize<Message>(receivedMessage);
                if (message == null) continue;

                if (message.MsgType == MessageType.Hello)
                {
                    HandleHello(clientEP);
                }
                else if (message.MsgType == MessageType.DNSLookup)
                {
                    HandleDNSLookup(message, clientEP);
                }
                else if (message.MsgType == MessageType.End)
                {
                    HandleEnd(clientEP);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Server] Error: " + ex.Message);
        }
    }

    private static void HandleHello(EndPoint clientEP)
    {
        Console.WriteLine("[Server] Sending WELCOME message...");
        var helloMessage = new Message { MsgId = 1, MsgType = MessageType.Welcome, Content = "Welcome from server" };
        byte[] helloBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
        serverSocket.SendTo(helloBytes, clientEP);
        Console.WriteLine("[Server] Sent: 'Welcome from server' to Client\n");
    }

    private static void HandleDNSLookup(Message message, EndPoint clientEP)
    {
        var dnsLookup = JsonSerializer.Deserialize<DNSRecord>(message.Content.ToString());
        var dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(File.ReadAllText(@"DNSrecords.json"));

        var dnsRecord = dnsRecords.FirstOrDefault(record => record.Type == dnsLookup.Type && record.Name == dnsLookup.Name);
        Message dnsReplyMessage;
        if (dnsRecord != null)
        {
            dnsReplyMessage = new Message { MsgId = message.MsgId, MsgType = MessageType.DNSLookupReply, Content = dnsRecord };
        }
        else
        {
            dnsReplyMessage = new Message { MsgId = message.MsgId, MsgType = MessageType.Error, Content = "DNS record not found" };
        }

        byte[] dnsReplyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsReplyMessage));
        serverSocket.SendTo(dnsReplyBytes, clientEP);

        // Receive Ack from the client
        byte[] buffer = new byte[1024];
        int receivedBytes = serverSocket.ReceiveFrom(buffer, ref clientEP);
        string ackMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        var ack = JsonSerializer.Deserialize<Message>(ackMessage);

        if (ack != null && ack.MsgType == MessageType.Ack)
        {
            Console.WriteLine("[Server] Received Ack for MsgId: " + ack.MsgId + "\n");
        }
    }

    private static void HandleEnd(EndPoint clientEP)
    {
        Console.WriteLine("[Server] Sending End message...");
        var endMessage = new Message { MsgId = 0, MsgType = MessageType.End, Content = "No more requests" };
        byte[] endBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(endMessage));
        serverSocket.SendTo(endBytes, clientEP);
    }
}











