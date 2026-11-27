// GPL-3.0. Transport probe, not a public matchmaking service.
export const MAX_FRAME = 65536;
export const ROOM_MS = 60 * 60 * 1000;
export function chargeWindow(previous, bytes, now) {
  const value = !previous || now - previous.start >= 1000
    ? { start: now, messages: 0, bytes: 0 } : { ...previous };
  value.messages++; value.bytes += bytes;
  return { ...value, allowed: value.messages <= 120 && value.bytes <= 512 * 1024 };
}
export function capacity(messagesPerSecondPerPlayer, players = 2) {
  const requestsPerHour = messagesPerSecondPerPlayer * players * 3600 / 20;
  return { requestsPerHour, roomHoursByRequests: 100000 / requestsPerHour,
    roomHoursByDurationWorstCase: 13000 / 0.128 / 3600 };
}
