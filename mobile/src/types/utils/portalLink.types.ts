/**
 * How an attempt to share the booking link ended.
 * - `shared`: the link was created and WhatsApp opened with the message ready.
 * - `invalid-phone`: the link was created but the client phone has no digits to build a WhatsApp link.
 * - `whatsapp-unavailable`: the link was created but the device could not open WhatsApp.
 */
export type ShareLinkOutcome = 'shared' | 'invalid-phone' | 'whatsapp-unavailable';
