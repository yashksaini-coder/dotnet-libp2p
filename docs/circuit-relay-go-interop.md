# Circuit Relay v2: .NET ↔ Go Interop

## Summary

**Can we dial one .NET peer from another using a Go peer as relay?**  
**Yes at the protocol level** (handshakes interoperate). **Full end-to-end relayed sessions** need more work on the .NET side: circuit transport (dialing `p2p-circuit` addrs) and/or accepting relayed connections as new sessions.

## Protocol compatibility

- **Spec:** [Circuit Relay v2](https://github.com/libp2p/specs/blob/master/relay/circuit-v2.md) (Recommendation, active).
- **Protocol IDs:** Hop `/libp2p/circuit/relay/0.2.0/hop`, Stop `/libp2p/circuit/relay/0.2.0/stop` — same in Go and this .NET stack.
- **Wire format:** Protobuf messages (`HopMessage`, `StopMessage`, etc.) match the spec; .NET and Go implementations can talk to each other.

So a **Go relay** can serve **.NET clients** (reservation and connect) and **.NET targets** (reservation + stop handling) as long as the .NET stack can act as relay client and use the relayed stream as a session.

## Current .NET gaps

1. **.NET as relay (server)**  
   Hop/Stop are implemented and handshakes complete, but the relay **does not bridge** the hop and stop streams. So a .NET peer acting as relay returns STATUS OK but data does not flow between the two peers. See `RelayHopProtocol.cs` TODO: *"Bridge hop channel and stop stream for full relayed connection"*.

2. **.NET as relay client (dialer)**  
   There is **no circuit transport**: no `ITransportProtocol` that matches multiaddrs containing `p2p-circuit`. So you cannot today `peer.DialAsync("/ip4/relay/tcp/4001/p2p/<relay>/p2p-circuit/p2p/<target>")` and get a session to the target. To get that, we’d need a transport that:
   - Matches circuit addrs (e.g. `addr.Has<P2pCircuit>()`),
   - Dials the relay’s direct address,
   - Opens a hop stream and sends CONNECT(target),
   - Exposes the hop stream as the new connection and runs the rest of the stack on it.

3. **.NET as relay target (listener)**  
   A .NET peer can dial a Go relay and send RESERVE (hop). When the relay later opens a stop stream to that peer, the existing stack will run `RelayStopProtocol.ListenAsync`: read CONNECT, send STATUS OK. After that, the stop stream is the “relayed connection” from the initiator, but it is **not** turned into a first-class session (no new `Session` for the initiator, no identify/ping on that stream). So we’d need a path like “accept relayed connection” that creates a session and runs the usual upgrade chain on that channel.

## Using a Go relay with .NET

- **Go relay:** Use go-libp2p with circuit v2 relay enabled, or the [relay daemon](https://github.com/libp2p/go-libp2p-relay-daemon) (e.g. from `dist.ipfs.tech`). The relay will bridge hop and stop streams.
- **.NET peer A (target):**  
  - Dial the Go relay (e.g. TCP + Noise + Yamux).  
  - Send RESERVE on the hop protocol; keep the connection to the relay open.  
  - Have RelayHop + RelayStop in the stack so that when the relay opens a stop stream, the peer handles it and responds OK.  
  - Full “session over relay” would still require treating the stop stream as a new session (see above).
- **.NET peer B (dialer):**  
  - Dial the relay, open a hop stream, send CONNECT(peer = A).  
  - Receive STATUS OK; the hop stream is then the relayed connection to A (Go relay bridges it to A’s stop stream).  
  - Today the stack does not expose this stream as a session; you’d need a circuit transport or an API that returns the hop stream channel after CONNECT.

## Minimal interop test

A practical “try” is:

1. Run a **Go relay** (e.g. relay daemon or a small host with relay enabled).
2. **.NET A:** Create a peer with `WithRelay()`, dial the relay, call `session.DialAsync<RelayHopProtocol>(new HopMessage { Type = Reserve })` and check STATUS OK.
3. **.NET B:** Dial the same relay, then `relaySession.DialAsync<RelayHopProtocol>(new HopMessage { Type = Connect, Peer = new Peer { Id = ByteString.CopyFrom(A_id.Bytes) } })` and check STATUS OK.

That confirms **handshake-level interop** between .NET and the Go relay (reservation and connect both succeed). Proving **data flow** (e.g. ping over the relayed path) would require either a circuit transport or an API that exposes the hop/stop stream as a session.

## References

- [Circuit Relay v2 spec](https://github.com/libp2p/specs/blob/master/relay/circuit-v2.md)
- [libp2p Circuit Relay concepts](https://docs.libp2p.io/concepts/nat/circuit-relay/)
- This repo: `src/libp2p/Libp2p.Protocols.Relay/` (Hop/Stop, reservation store, README)
