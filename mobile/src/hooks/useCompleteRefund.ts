import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { billingApi } from '../api/client';
import type { CompleteRefundRequest, SettlementResponse } from '../types/api/billing';
import { REFUNDS_QUERY_PREFIX } from './useRefunds';
import { settlementQueryKey } from './useSettlement';

/**
 * Mutation that records that a refund was given back to the client. On success it stores the settlement it returns and
 * refreshes the list of pending refunds. It never retries: completing twice is rejected by the backend, so a retry after a
 * lost answer would only show an error for something that already worked; the list is refreshed instead.
 * @param bookingId Booking whose refund is completed.
 * @returns The TanStack Query mutation.
 */
export function useCompleteRefund(bookingId: string): UseMutationResult<SettlementResponse, Error, CompleteRefundRequest> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CompleteRefundRequest) => billingApi.completeRefund(bookingId, request),
    onSuccess: async (settlement) => {
      queryClient.setQueryData(settlementQueryKey(settlement.bookingId), settlement);
      await queryClient.invalidateQueries({ queryKey: REFUNDS_QUERY_PREFIX });
    },
  });
}
