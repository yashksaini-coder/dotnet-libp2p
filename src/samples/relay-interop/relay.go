// Minimal Go circuit relay v2 for testing .NET ↔ Go interop.
// Run: go run relay.go
// Listens on /ip4/0.0.0.0/tcp/4001

package main

import (
	"context"
	"fmt"
	"log"
	"os"
	"os/signal"

	"github.com/libp2p/go-libp2p"
	relayv2 "github.com/libp2p/go-libp2p/p2p/protocol/circuitv2/relay"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	h, err := libp2p.New(
		libp2p.ListenAddrStrings("/ip4/0.0.0.0/tcp/4001"),
		libp2p.DisableRelay(), // we are the relay, don't use others
	)
	if err != nil {
		log.Fatal(err)
	}

	_, err = relayv2.New(h)
	if err != nil {
		log.Fatal(err)
	}

	fmt.Fprintf(os.Stderr, "Circuit relay v2 listening: %s/p2p/%s\n", "/ip4/0.0.0.0/tcp/4001", h.ID())
	fmt.Fprintf(os.Stderr, "Use RELAY_ADDR=/ip4/127.0.0.1/tcp/4001 for .NET peers.\n")

	ch := make(chan os.Signal, 1)
	signal.Notify(ch, os.Interrupt)
	<-ch
	cancel()
	if err := h.Close(); err != nil {
		log.Print(err)
	}
}
