using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    static string configFile = @"../Setting.json";
    static Setting? setting;
    private static IPEndPoint? serverEndPoint;
    private static Socket? clientSocket;

    static void LoadSettings()
    {
        try
        {
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);
            Console.WriteLine("[Client] Configuration loaded successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error loading settings: " + ex.Message);
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
                Console.WriteLine("[Client] Invalid settings, exiting.");
                return;
            }

            IPEndPoint clientEndPoint;
            try
            {
                clientEndPoint = new IPEndPoint(IPAddress.Any, setting.ClientPortNumber);
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                clientSocket.Connect(clientEndPoint);
                // Console.WriteLine($"[Client] Bound to port {setting.ClientPortNumber}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Client] Port {setting.ClientPortNumber} is already in use. Binding to a random available port.");
                clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                clientSocket.Bind(clientEndPoint);
                Console.WriteLine($"[Client] Bound to random port {((IPEndPoint)clientSocket.LocalEndPoint!).Port}");
            }

            serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);

            SendHello();
            PerformDNSLookups();

            // Send End message to server
            var endMessage = new Message { MsgId = 0, MsgType = MessageType.End, Content = "No more requests" };
            byte[] endBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(endMessage));
            clientSocket.SendTo(endBytes, serverEndPoint);

            // Receive End message from server
            byte[] buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
            string endMessageFromServer = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            Console.WriteLine("\n[Client] Received End message: " + endMessageFromServer);

            clientSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error: " + ex.Message);
        }
    }

    private static void SendHello()
    {
        Console.WriteLine("[Client] Sending HELLO message...");
        var helloMessage = new Message { MsgId = 1, MsgType = MessageType.Hello, Content = "Hello from client" };
        byte[] helloMessageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
        clientSocket.SendTo(helloMessageBytes, serverEndPoint);

        byte[] buffer = new byte[1024];
        EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        Console.WriteLine("[Client] Received: " + receivedMessage);
    }

    private static void PerformDNSLookups()
    {
        var dnsLookups = new List<DNSRecord>
        {
            new DNSRecord { Type = "A", Name = "www.outlook.com" },
            new DNSRecord { Type = "MX", Name = "example.com" },
            new DNSRecord { Type = "A", Name = "www.nonexistent.com" },
            new DNSRecord { Type = "M", Name = "exampl.com" }
        };

        int msgId = 2;
        foreach (var dnsLookup in dnsLookups)
        {
            var dnsLookupMessage = new Message { MsgId = msgId, MsgType = MessageType.DNSLookup, Content = dnsLookup };
            byte[] dnsLookupBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsLookupMessage));
            clientSocket.SendTo(dnsLookupBytes, serverEndPoint);

            byte[] buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
            string dnsLookupReply = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            Console.WriteLine("\n[Client] Received DNSLookupReply: " + dnsLookupReply);

            var dnsReplyMessage = JsonSerializer.Deserialize<Message>(dnsLookupReply);
            if (dnsReplyMessage != null && dnsReplyMessage.MsgType == MessageType.DNSLookupReply)
            {
                Console.WriteLine("[Client] Sending Ack for MsgId: " + dnsReplyMessage.MsgId);
                var ackMessage = new Message { MsgId = dnsReplyMessage.MsgId, MsgType = MessageType.Ack, Content = dnsReplyMessage.MsgId };
                byte[] ackBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ackMessage));
                clientSocket.SendTo(ackBytes, serverEndPoint);
            }
            else if (dnsReplyMessage != null && dnsReplyMessage.MsgType == MessageType.Error)
            {
                Console.WriteLine("[Client] Error received: " + dnsReplyMessage.Content);
                var ackMessage = new Message { MsgId = dnsReplyMessage.MsgId, MsgType = MessageType.Ack, Content = dnsReplyMessage.MsgId };
                byte[] ackBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ackMessage));
                clientSocket.SendTo(ackBytes, serverEndPoint);
            }

            msgId++;
        }
    }
}