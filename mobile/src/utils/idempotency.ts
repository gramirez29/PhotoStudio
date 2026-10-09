/**
 * Creates a key that identifies one attempt to record a payment, so sending the same payment twice (a double tap, a retry
 * after a lost answer) is recognised by the backend and counted once. It is not a secret, only unique enough.
 * @param random Source of numbers in [0, 1); injectable for tests.
 * @param now Current time in milliseconds; injectable for tests.
 * @returns The key, for example `pay-lq3k9x-4f8a1c2e9b7d3a60`.
 */
export function createIdempotencyKey(random: () => number = Math.random, now: number = Date.now()): string {
  const randomPart = Array.from({ length: 16 }, () => Math.floor(random() * 16).toString(16)).join('');
  return `pay-${now.toString(36)}-${randomPart}`;
}
