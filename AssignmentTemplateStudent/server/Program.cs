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
                serverSocket.Bind(serverEndpoint);
                Console.WriteLine($"[Server] Bound to port {setting.ServerPortNumber}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Server] Port {setting.ServerPortNumber} is already in use. Binding to a random available port.");
                serverEndpoint = new IPEndPoint(IPAddress.Any, 0);
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverSocket.Bind(serverEndpoint);
                Console.WriteLine($"[Server] Bound to random port {((IPEndPoint)serverSocket.LocalEndPoint!).Port}");
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
                if (message == null)
                {
                    Console.WriteLine("[Server] Error: Received invalid message format");
                    continue;
                }

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
                else
                {
                    Console.WriteLine($"[Server] Error: Unexpected message type received: {message.MsgType}");
                    SendError(clientEP, message.MsgId, $"Unexpected message type: {message.MsgType}");
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
        var helloMessage = new Message { MsgId = 4, MsgType = MessageType.Welcome, Content = "Welcome from server" };
        byte[] helloBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
        serverSocket.SendTo(helloBytes, clientEP);
        Console.WriteLine("[Server] Sent: 'Welcome from server' to Client\n");
    }

    private static void HandleDNSLookup(Message message, EndPoint clientEP)
    {
        try
        {
            if (message.Content == null)
            {
                Console.WriteLine("[Server] Error: DNSLookup message missing content");
                SendError(clientEP, message.MsgId, "DNSLookup message missing content");
                return;
            }

            var dnsLookup = JsonSerializer.Deserialize<DNSRecord>(message.Content.ToString());
            if (dnsLookup == null)
            {
                Console.WriteLine("[Server] Error: Invalid DNS record format");
                SendError(clientEP, message.MsgId, "Invalid DNS record format");
                return;
            }

            var dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(File.ReadAllText(@"DNSrecords.json"));
            if (dnsRecords == null)
            {
                Console.WriteLine("[Server] Error: Failed to read DNS records file");
                SendError(clientEP, message.MsgId, "Server configuration error");
                return;
            }

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
                
                // Check if this was the last DNS lookup (MsgId 36 is the last one in the client's sequence)
                if (ack.MsgId == 36)
                {
                    Console.WriteLine("[Server] Last Ack received, sending End message...");
                    var endMessage = new Message { MsgId = 36, MsgType = MessageType.End, Content = "No more requests" };
                    byte[] endBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(endMessage));
                    serverSocket.SendTo(endBytes, clientEP);
                    Console.WriteLine("[Server] Sent End message to client\n");
                }
            }
            else
            {
                Console.WriteLine("[Server] Error: Expected Ack message but received different message type");
                SendError(clientEP, message.MsgId, "Protocol error: Expected Ack message");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Server] Error processing DNS lookup: " + ex.Message);
            SendError(clientEP, message.MsgId, "Internal server error");
        }
    }

    private static void HandleEnd(EndPoint clientEP)
    {
        Console.WriteLine("[Server] Sending End message...");
        var endMessage = new Message { MsgId = 0, MsgType = MessageType.End, Content = "No more requests" };
        byte[] endBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(endMessage));
        serverSocket.SendTo(endBytes, clientEP);
    }

    private static void SendError(EndPoint clientEP, int msgId, string errorMessage)
    {
        var errorMessageObj = new Message { MsgId = msgId, MsgType = MessageType.Error, Content = errorMessage };
        byte[] errorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorMessageObj));
        serverSocket.SendTo(errorBytes, clientEP);
        Console.WriteLine($"[Server] Sent error message: {errorMessage}");
    }
}











