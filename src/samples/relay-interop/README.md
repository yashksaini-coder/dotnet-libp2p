# Relay interop sample

Tests **dialing one .NET peer from another using a Go peer as circuit relay** (Circuit Relay v2). See [docs/circuit-relay-go-interop.md](../../../docs/circuit-relay-go-interop.md) for protocol compatibility and current gaps.

## What this sample does

1. **Go relay** — runs a minimal circuit relay v2 (hop/stop, stream bridging).
2. **.NET peer A (reserve)** — dials the relay and sends RESERVE; prints its peer ID and stays running.
3. **.NET peer B (connect)** — dials the relay and sends CONNECT(peer=A); receives STATUS OK.

This proves **handshake-level interop**: the Go relay accepts reservation and connect from .NET, and the relay handshakes succeed. Full data flow over the relayed stream would require a circuit transport or session-from-stream support on the .NET side.

## Prerequisites

- .NET 10 SDK
- Go 1.22+ (only to run the relay)

## 1. Start the Go relay

From this directory:

```bash
go mod tidy
go run relay.go
```

Leave it running. It listens on `0.0.0.0:4001`.

## 2. Run .NET peer A (reserve)

In another terminal, from this directory (set env vars then run):

```bash
cd src/samples/relay-interop
RELAY_ADDR=/ip4/127.0.0.1/tcp/4001 ROLE=reserve dotnet run
```

Note the printed **peer ID** (e.g. `12D3KooW...`). Keep this process running.

## 3. Run .NET peer B (connect)

In a third terminal, set `RELAY_TARGET_PEER_ID` to the peer ID from step 2:

```bash
RELAY_ADDR=/ip4/127.0.0.1/tcp/4001 ROLE=connect RELAY_TARGET_PEER_ID=<paste_peer_id_here> dotnet run
```

You should see `CONNECT OK — relay handshake succeeded.`

## Environment variables

| Variable               | Required | Description |
|------------------------|----------|-------------|
| `RELAY_ADDR`           | Yes      | Relay multiaddr (e.g. `/ip4/127.0.0.1/tcp/4001`) |
| `ROLE`                 | Yes      | `reserve` or `connect` |
| `RELAY_TARGET_PEER_ID` | For connect | Peer ID of the reserving peer (from step 2) |

## Building from repo root

```bash
dotnet build src/samples/relay-interop/RelayInterop.csproj
RELAY_ADDR=/ip4/127.0.0.1/tcp/4001 ROLE=reserve dotnet run --project src/samples/relay-interop/RelayInterop.csproj
```
