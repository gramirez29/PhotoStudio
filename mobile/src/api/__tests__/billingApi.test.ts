import { createBillingApi, parseRefundList, parseSettlement } from '../billingApi';
import { ResponseShapeError } from '../guards';
import { createHttpClient } from '../httpClient';
import { pendingSettlementPayload } from '../__fixtures__/billingPayloads';
import { jsonResponse, stubFetch } from '../__fixtures__/testResponses';

describe('parseSettlement', () => {
  it('parses a pending refund', () => {
    expect(parseSettlement(pendingSettlementPayload)).toEqual(pendingSettlementPayload);
  });

  it('parses a refund that was given back', () => {
    const settlement = parseSettlement({
      ...pendingSettlementPayload,
      refundStatus: 'Completed',
      refundMethod: 'SinpeMovil',
      refundNote: 'ref 1',
      refundCompletedAt: '2026-10-18T10:00:00+00:00',
    });

    expect(settlement).toMatchObject({ refundStatus: 'Completed', refundMethod: 'SinpeMovil', refundNote: 'ref 1' });
  });

  it('rejects an unknown reason, status or method and a mistyped field', () => {
    expect(() => parseSettlement({ ...pendingSettlementPayload, reason: 'Other' })).toThrow('settlement.reason');
    expect(() => parseSettlement({ ...pendingSettlementPayload, refundStatus: 'Done' })).toThrow('settlement.refundStatus');
    expect(() => parseSettlement({ ...pendingSettlementPayload, refundMethod: 'Bitcoin' })).toThrow('settlement.refundMethod');
    expect(() => parseSettlement({ ...pendingSettlementPayload, clientName: 3 })).toThrow(ResponseShapeError);
    expect(() => parseSettlement({ ...pendingSettlementPayload, refundAmount: null })).toThrow(ResponseShapeError);
  });
});

describe('parseRefundList', () => {
  it('parses the list with its counter', () => {
    expect(parseRefundList({ items: [pendingSettlementPayload], pendingCount: 1 })).toEqual({
      items: [pendingSettlementPayload],
      pendingCount: 1,
    });
  });

  it('points at the element that is invalid', () => {
    expect(() => parseRefundList({ items: [{ ...pendingSettlementPayload, id: 1 }], pendingCount: 1 })).toThrow(
      'refunds.items[0].id',
    );
    expect(() => parseRefundList({ items: [], pendingCount: '0' })).toThrow('refunds.pendingCount');
  });
});

describe('createBillingApi', () => {
  const bookingId = pendingSettlementPayload.bookingId;

  it('lists the pending refunds with GET', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { items: [pendingSettlementPayload], pendingCount: 1 }));
    const api = createBillingApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.listRefunds()).resolves.toMatchObject({ pendingCount: 1 });
    expect(urls).toEqual(['https://api.example.test/api/billing/refunds']);
  });

  it('reads the settlement of a booking with GET', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, pendingSettlementPayload));
    const api = createBillingApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.getSettlement(bookingId)).resolves.toMatchObject({ bookingId });
    expect(urls).toEqual([`https://api.example.test/api/billing/settlements/${bookingId}`]);
  });

  it('posts the method and note to complete a refund', async () => {
    const requests: { readonly url: string; readonly init: RequestInit | undefined }[] = [];
    const client = createHttpClient('https://api.example.test', {
      fetchFn: (url, init) => {
        requests.push({ url, init });
        return Promise.resolve(jsonResponse(200, { ...pendingSettlementPayload, refundStatus: 'Completed', refundMethod: 'Cash' }));
      },
    });

    const settlement = await createBillingApi(client).completeRefund(bookingId, { method: 'Cash', note: 'ok' });

    expect(requests[0]?.url).toBe(`https://api.example.test/api/billing/settlements/${bookingId}/refund/complete`);
    expect(requests[0]?.init?.method).toBe('POST');
    expect(JSON.parse(String(requests[0]?.init?.body))).toEqual({ method: 'Cash', note: 'ok' });
    expect(settlement.refundStatus).toBe('Completed');
  });
});
