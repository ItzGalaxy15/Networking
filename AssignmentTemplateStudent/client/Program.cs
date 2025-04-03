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
                clientSocket.Bind(clientEndPoint);
                Console.WriteLine($"[Client] Bound to port {setting.ClientPortNumber}");
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

            // Wait for End message from server
            Console.WriteLine("[Client] Waiting for End message from server...");
            byte[] endBuffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = clientSocket.ReceiveFrom(endBuffer, ref remoteEP);
            string endMessageFromServer = Encoding.UTF8.GetString(endBuffer, 0, receivedBytes);
            var endMessage = JsonSerializer.Deserialize<Message>(endMessageFromServer);

            if (endMessage != null && endMessage.MsgType == MessageType.End)
            {
                Console.WriteLine("\n[Client] Received End message: " + endMessage.Content);
                Console.WriteLine("[Client] Terminating client...");
                clientSocket.Close();
            }
            else
            {
                Console.WriteLine("[Client] Error: Received unexpected message type while waiting for End message");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error: " + ex.Message);
            if (clientSocket != null)
            {
                clientSocket.Close();
            }
        }
    }

    private static void SendHello()
    {
        try
        {
            Console.WriteLine("[Client] Sending HELLO message...");
            var helloMessage = new Message { MsgId = 1, MsgType = MessageType.Hello, Content = "Hello from client" };
            byte[] helloMessageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
            clientSocket.SendTo(helloMessageBytes, serverEndPoint);

            byte[] buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            var welcomeMessage = JsonSerializer.Deserialize<Message>(receivedMessage);

            if (welcomeMessage != null && welcomeMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("[Client] Received: " + receivedMessage);
            }
            else
            {
                Console.WriteLine("[Client] Error: Expected Welcome message but received different message type");
                throw new Exception("Protocol error: Expected Welcome message");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error in SendHello: " + ex.Message);
            throw;
        }
    }

    private static void PerformDNSLookups()
    {
        try
        {
            var dnsLookups = new List<DNSRecord>
            {
                new DNSRecord { Type = "A", Name = "www.outlook.com" },
                new DNSRecord { Type = "MX", Name = "example.com" },
                new DNSRecord { Type = "A", Name = "www.nonexistent.com" },
                new DNSRecord { Type = "M", Name = "exampl.com" }
            };

            int msgId = 33;  // Starting with the sample message ID
            foreach (var dnsLookup in dnsLookups)
            {
                var dnsLookupMessage = new Message { MsgId = msgId, MsgType = MessageType.DNSLookup, Content = dnsLookup };
                byte[] dnsLookupBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsLookupMessage));
                Console.WriteLine("\n[Client] Sending DNSLookup: " + JsonSerializer.Serialize(dnsLookupMessage));
                clientSocket.SendTo(dnsLookupBytes, serverEndPoint);

                byte[] buffer = new byte[1024];
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
                string dnsLookupReply = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                Console.WriteLine("\n[Client] Received DNSLookupReply: " + dnsLookupReply);

                var dnsReplyMessage = JsonSerializer.Deserialize<Message>(dnsLookupReply);
                if (dnsReplyMessage == null)
                {
                    Console.WriteLine("[Client] Error: Received invalid message format");
                    continue;
                }

                if (dnsReplyMessage.MsgType == MessageType.DNSLookupReply)
                {
                    Console.WriteLine("[Client] Sending Ack for MsgId: " + dnsReplyMessage.MsgId);
                    var ackMessage = new Message { MsgId = dnsReplyMessage.MsgId, MsgType = MessageType.Ack, Content = dnsReplyMessage.MsgId };
                    byte[] ackBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ackMessage));
                    clientSocket.SendTo(ackBytes, serverEndPoint);
                }
                else if (dnsReplyMessage.MsgType == MessageType.Error)
                {
                    Console.WriteLine("[Client] Error received: " + dnsReplyMessage.Content);
                    var ackMessage = new Message { MsgId = dnsReplyMessage.MsgId, MsgType = MessageType.Ack, Content = dnsReplyMessage.MsgId };
                    byte[] ackBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ackMessage));
                    clientSocket.SendTo(ackBytes, serverEndPoint);
                }
                else
                {
                    Console.WriteLine($"[Client] Error: Unexpected message type received: {dnsReplyMessage.MsgType}");
                    continue;
                }

                msgId++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error in PerformDNSLookups: " + ex.Message);
            throw;
        }
    }
}