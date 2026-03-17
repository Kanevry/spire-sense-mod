# SpireSense Mod API Documentation

## Overview

The SpireSense mod exposes a local HTTP API and WebSocket server for external tools to access real-time game state from Slay the Spire 2.

- **HTTP API**: `http://localhost:8080`
- **WebSocket**: `ws://localhost:8081`

## Authentication

No authentication required — the servers only listen on localhost.

## Rate Limits

No rate limits. The API is designed for continuous polling by the companion web app.

## HTTP Endpoints

### GET /api/state

Returns the complete current game state.

**Response**: `200 OK`
```json
{
  "screen": "card_reward",
  "character": "silent",
  "act": 2,
  "floor": 18,
  "ascension": 10,
  "seed": "XYZ789",
  "deck": [...],
  "relics": [...],
  "combat": null,
  "map": [...],
  "cardRewards": [...],
  "shopCards": null,
  "shopRelics": null,
  "eventOptions": null
}
```

### GET /api/health

Health check endpoint.

**Response**: `200 OK`
```json
{
  "status": "ok",
  "mod": "SpireSense",
  "version": "0.1.0",
  "port": 8080
}
```

### GET /api/deck

Returns current deck contents.

**Response**: `200 OK`
```json
{
  "deck": [...],
  "count": 15
}
```

### GET /api/combat

Returns current combat state.

**Response**: `200 OK` (in combat) or `404 Not Found` (not in combat)

## WebSocket Protocol

### Connection

```javascript
const ws = new WebSocket('ws://localhost:8081');
```

On connection, the server immediately sends a `state_update` event with the current game state.

### Event Format

All events follow this format:
```json
{
  "type": "event_type",
  "data": { ... }
}
```

### Event Types

See README.md for the complete list of event types and their data payloads.

## CORS

The HTTP API includes CORS headers allowing requests from any origin:
- `Access-Control-Allow-Origin: *`
- `Access-Control-Allow-Methods: GET, POST, OPTIONS`
- `Access-Control-Allow-Headers: Content-Type`

## Error Handling

Errors return JSON with an `error` field:
```json
{
  "error": "Not in combat"
}
```
