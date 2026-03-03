// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

// Minimal sample: dial one .NET peer from another using a Go peer as circuit relay.
// See docs/circuit-relay-go-interop.md and README.md.

using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Multiformats.Address;
using Nethermind.Libp2p;
using Nethermind.Libp2p.Core;
using Nethermind.Libp2p.Protocols;
using Nethermind.Libp2p.Protocols.Relay.Dto;

string? relayAddr = Environment.GetEnvironmentVariable("RELAY_ADDR");
string? role = Environment.GetEnvironmentVariable("ROLE");
string? targetPeerIdStr = Environment.GetEnvironmentVariable("RELAY_TARGET_PEER_ID");

if (string.IsNullOrEmpty(relayAddr) || string.IsNullOrEmpty(role))
{
    Console.Error.WriteLine("Usage: set RELAY_ADDR and ROLE (reserve|connect). For connect, set RELAY_TARGET_PEER_ID to the reserving peer's ID.");
    Console.Error.WriteLine("Example (reserve):  RELAY_ADDR=/ip4/127.0.0.1/tcp/4001 ROLE=reserve dotnet run");
    Console.Error.WriteLine("Example (connect):  RELAY_ADDR=/ip4/127.0.0.1/tcp/4001 ROLE=connect RELAY_TARGET_PEER_ID=<peer_id> dotnet run");
    return 1;
}

var services = new ServiceCollection()
    .AddLibp2p(b => b.WithRelay())
    .AddLogging(builder => builder.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "[HH:mm:ss] "; }));

using var sp = services.BuildServiceProvider();
IPeerFactory factory = sp.GetRequiredService<IPeerFactory>();
ILocalPeer peer = factory.Create();

Multiaddress addr = relayAddr;
Console.Error.WriteLine($"Dialing relay {addr}...");
ISession relaySession = await peer.DialAsync(addr);

if (role.Equals("reserve", StringComparison.OrdinalIgnoreCase))
{
    var reserve = new HopMessage { Type = HopMessage.Types.Type.Reserve };
    HopMessage response = await relaySession.DialAsync<RelayHopProtocol, HopMessage, HopMessage>(reserve);
    if (response.Status != Status.Ok)
    {
        Console.Error.WriteLine($"RESERVE failed: {response.Status}");
        return 1;
    }
    Console.Error.WriteLine("RESERVE OK.");
    Console.WriteLine(peer.Identity.PeerId.ToString());
    Console.Error.WriteLine("Keep this process running; start the connect peer with RELAY_TARGET_PEER_ID=" + peer.Identity.PeerId);
    await Task.Delay(TimeSpan.FromMinutes(10));
    return 0;
}

if (role.Equals("connect", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrEmpty(targetPeerIdStr))
    {
        Console.Error.WriteLine("RELAY_TARGET_PEER_ID is required for connect role.");
        return 1;
    }
    PeerId targetId = new PeerId(targetPeerIdStr);
    var connect = new HopMessage
    {
        Type = HopMessage.Types.Type.Connect,
        Peer = new Peer { Id = ByteString.CopyFrom(targetId.Bytes) }
    };
    HopMessage response = await relaySession.DialAsync<RelayHopProtocol, HopMessage, HopMessage>(connect);
    if (response.Status != Status.Ok)
    {
        Console.Error.WriteLine($"CONNECT failed: {response.Status}");
        return 1;
    }
    Console.Error.WriteLine("CONNECT OK — relay handshake succeeded. (Data over relayed stream would require circuit transport.)");
    return 0;
}

Console.Error.WriteLine("ROLE must be 'reserve' or 'connect'.");
return 1;
