import { describe, expect, it } from 'vitest';
import type { PortalSession } from '../types/api/portal';
import { contractFlowReducer, INITIAL_CONTRACT_FLOW } from './contractFlow';

const NOW = Date.parse('2026-10-16T12:00:00Z');
const FRESH: PortalSession = { accessToken: 'jwt', expiresAt: new Date(NOW + 20 * 60_000).toISOString() };
const ABOUT_TO_EXPIRE: PortalSession = { accessToken: 'jwt', expiresAt: new Date(NOW + 30_000).toISOString() };

describe('contractFlowReducer', () => {
  it('asks for a code when opened without a session', () => {
    expect(contractFlowReducer(INITIAL_CONTRACT_FLOW, { type: 'open', now: NOW, session: null }).step).toBe('request');
  });

  it('skips the code when the page already has a valid session', () => {
    const state = contractFlowReducer(INITIAL_CONTRACT_FLOW, { type: 'open', now: NOW, session: FRESH });

    expect(state.step).toBe('read');
    expect(state.session).toBe(FRESH);
  });

  it('does not trust a session that is about to expire', () => {
    expect(contractFlowReducer(INITIAL_CONTRACT_FLOW, { type: 'open', now: NOW, session: ABOUT_TO_EXPIRE }).step).toBe('request');
  });

  it('walks from the code to the contract and then to signed', () => {
    let state = contractFlowReducer(INITIAL_CONTRACT_FLOW, { type: 'open', now: NOW, session: null });
    state = contractFlowReducer(state, { type: 'codeSent', maskedEmail: 'm***@example.com' });
    expect(state).toMatchObject({ step: 'enter', maskedEmail: 'm***@example.com' });
    state = contractFlowReducer(state, { type: 'verified', session: FRESH });
    expect(state.step).toBe('read');
    expect(contractFlowReducer(state, { type: 'signed' }).step).toBe('signed');
  });

  it('goes back to asking for a code when the session expires, dropping it', () => {
    const state = contractFlowReducer({ step: 'read', maskedEmail: null, session: FRESH }, { type: 'sessionExpired' });

    expect(state).toMatchObject({ step: 'request', session: null });
  });

  it('closes', () => {
    expect(contractFlowReducer({ step: 'signed', maskedEmail: null, session: FRESH }, { type: 'close' }).step).toBe('closed');
  });
});
