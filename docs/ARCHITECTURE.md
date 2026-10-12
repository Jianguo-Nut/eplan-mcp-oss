# Architecture

This document describes the structure of `eplan-mcp` at a high level. It is a
conceptual overview, not an implementation guide.

## Goals

- Let an MCP client drive a running EPLAN P8 instance through a small set of
  well-defined operations.
- Keep the integration layered, so each part can evolve independently.
- Provide a documented extension point (the add-in) so users can add the
  operations their own workflows need.

## The three layers

`eplan-mcp` is organized as three cooperating layers.

```
  ┌──────────────────────────────┐
  │  MCP client                  │   e.g. Claude Code or another
  │                              │   Model Context Protocol client
  └──────────────┬───────────────┘
                 │  MCP over stdio
  ┌──────────────▼───────────────┐
  │  MCP server                  │   registers and serves the tools
  │  (mcp_server.py)             │
  └──────────────┬───────────────┘
                 │  in-process call
  ┌──────────────▼───────────────┐
  │  Bridge                      │   relays a request and waits for a result
  │  (eplan_bridge.py)           │
  └──────────────┬───────────────┘
                 │  id/op messages over a swappable transport
  ┌──────────────▼───────────────┐
  │  Add-in (inside EPLAN P8)    │   performs the operation
  │                              │
  └──────────────────────────────┘
```

### 1. MCP server

The MCP server is the public face of the package. It registers each operation
as a tool and serves them to an MCP client over stdio. Its responsibility is
limited to defining the tool surface and forwarding calls; it does not talk to
EPLAN directly.

### 2. Bridge

The bridge is a small Python layer that sits between the server and the add-in.
It receives a request from the server, hands it to the add-in through the
transport, waits for the add-in to respond, and returns the outcome. It depends
only on the Python standard library.

The bridge also includes a helper that can dismiss pending modal dialogs, which
keeps automation from stalling when EPLAN shows a dialog during an operation.

The request/reply exchange travels over a small, swappable transport. The
reference build uses a shared working directory because it needs no extra
dependencies, but the carrier is an implementation detail: it can be replaced
without changing the layers above it.

### 3. Add-in

The add-in is a component loaded inside EPLAN P8. It is the only layer that
calls the EPLAN API. It receives requests from the bridge, performs the
requested operation, and reports the result.

The add-in is the package's extension point. The reference add-in implements
the full operation surface: project lifecycle (open/create/close/save/
import/export), pages (create/delete/copy/import/export), device placement by
symbol, macro or function definition, electrical connections between device
pins, and drawing through the Graphics API. Implement additional handlers in
`OnRequest()` as your workflow needs them.

## The message contract

The server and bridge run as one process; the add-in runs inside EPLAN as a
separate process. They cooperate by exchanging messages: the bridge submits a
request, the add-in performs the operation, and the add-in returns a reply.

Each exchange carries an identifier so a reply can be matched to its request.
This is the only contract the two sides share — a small message format — which
keeps the layers loosely coupled: either side can be replaced as long as it
honors that format.

Batch operations (placing several devices, creating several connections,
drawing) would not fit a flat `key=value` line comfortably, so the contract
also supports two optional **base64(JSON)** fields that stay inside the flat
format: `payload` on requests and `data` on replies. The bridge encodes and
decodes these automatically; they are part of the message contract, not the
transport.

How the messages are carried between the processes is a separate, swappable
concern handled by a transport. The reference transport uses a shared working
directory because it needs no extra dependencies; the directory and the
location of the EPLAN executable are supplied through configuration (a config
file or environment variables), so no machine- or project-specific paths are
baked into the code. The transport can be replaced without changing the message
contract.

## Extending the system

To add an operation:

1. Implement a handler for it in your add-in (the reference add-in shows where
   handlers go and how requests and responses are shaped).
2. Add a corresponding tool in the MCP server that forwards the request through
   the bridge.

Because the layers are decoupled by the message contract, you can extend the
add-in and the tool surface without changing the bridge or the transport.

## Scope

This package provides the framework — the MCP server, the bridge, a reference
add-in, and the message contract. The operations you build on top of it are
yours to define.
