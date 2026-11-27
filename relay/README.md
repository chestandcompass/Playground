# Playground online relay

Experimental two-player relay deployed using `wrangler.online.jsonc`. Uses Cloudflare Workers and SQLite Durable Objects with WebSocket hibernation.

Run `npm ci`, `npm test`, then configure a private `ROOM_KEY` deployment secret with Wrangler before deploying. Never commit that secret or include it in the game. The client uses built-in .NET ClientWebSocket; no relay SDK is required by players.

Role-bound room capabilities expire after four hours. Allocation and shared daily traffic credits are bounded in online-policy.js. A lobby accepts one host and one guest. Either departure ends the lobby; a new code creates a new packet session. Game payloads are not logged or persisted. Public room browsing and automatic direct/relay selection are not implemented.

The service passed live .NET transport tests and native isolated host-lobby tests. These do not replace full two-network gameplay testing. See DEVELOPMENT.md for evidence and limitations.

GPL-3.0, as part of Playground.
