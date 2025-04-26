using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json; // We'll add this package in next step

List<Packet> packets = new List<Packet>();

try
{
    packets = StreamAllPackets();

    var missingSequences = FindMissingSequences(packets);

    foreach (var missingSeq in missingSequences)
    {
        var missingPacket = RequestResendPacket(missingSeq);
        if (missingPacket != null)
        {
            packets.Add(missingPacket);
        }
    }

    // Sort packets by sequence
    packets = packets.OrderBy(p => p.Sequence).ToList();

    // Save to JSON
    string json = JsonConvert.SerializeObject(packets, Formatting.Indented);
    File.WriteAllText("output.json", json);

    Console.WriteLine("Data saved to output.json successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// ================== FUNCTIONS ====================

List<Packet> StreamAllPackets()
{
    List<Packet> packets = new List<Packet>();
    TcpClient client = new TcpClient("localhost", 3000);
    NetworkStream stream = client.GetStream();

    // Send Stream All Packets request
    byte[] request = new byte[2];
    request[0] = 1; // callType = 1
    request[1] = 0; // resendSeq not needed
    stream.Write(request, 0, request.Length);

    byte[] buffer = new byte[17]; // packet size is 17 bytes
    int bytesRead;

    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
    {
        Packet packet = ParsePacket(buffer);
        packets.Add(packet);
    }

    client.Close();
    return packets;
}

Packet ParsePacket(byte[] data)
{
    string symbol = Encoding.ASCII.GetString(data, 0, 4);
    char buySell = (char)data[4];
    int quantity = BitConverter.ToInt32(data.Skip(5).Take(4).Reverse().ToArray(), 0);
    int price = BitConverter.ToInt32(data.Skip(9).Take(4).Reverse().ToArray(), 0);
    int sequence = BitConverter.ToInt32(data.Skip(13).Take(4).Reverse().ToArray(), 0);

    return new Packet
    {
        Symbol = symbol,
        BuySellIndicator = buySell,
        Quantity = quantity,
        Price = price,
        Sequence = sequence
    };
}

List<int> FindMissingSequences(List<Packet> packets)
{
    var sequences = packets.Select(p => p.Sequence).ToList();
    int min = sequences.Min();
    int max = sequences.Max();

    List<int> missing = new List<int>();
    for (int i = min; i <= max; i++)
    {
        if (!sequences.Contains(i))
        {
            missing.Add(i);
        }
    }

    return missing;
}

Packet RequestResendPacket(int sequence)
{
    try
    {
        TcpClient client = new TcpClient("localhost", 3000);
        NetworkStream stream = client.GetStream();

        byte[] request = new byte[2];
        request[0] = 2; // callType = 2
        request[1] = (byte)sequence;
        stream.Write(request, 0, request.Length);

        byte[] buffer = new byte[17];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        client.Close();

        if (bytesRead > 0)
        {
            return ParsePacket(buffer);
        }
        return null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to resend packet {sequence}: {ex.Message}");
        return null;
    }
}
