import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { portalApi } from '../api/client';
import type { PortalBooking, PortalCodeSent, PortalSession } from '../types/api/portal';
import type { SubmitProofVariables } from '../types/hooks/useProofMutations.types';
import { portalBookingKey } from './usePortalBooking';

/**
 * Mutation that asks for a verification code. It is never retried on its own: every request sends an email, and the server
 * limits how often that can happen.
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query mutation.
 */
export function useRequestCode(token: string): UseMutationResult<PortalCodeSent, Error, void> {
  return useMutation({ mutationFn: () => portalApi.requestCode(token) });
}

/**
 * Mutation that checks the code the client typed. It is never retried on its own: a wrong code counts against the limit.
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query mutation.
 */
export function useVerifyCode(token: string): UseMutationResult<PortalSession, Error, string> {
  return useMutation({ mutationFn: (code: string) => portalApi.verifyCode(token, code) });
}

/**
 * Mutation that registers the payment proof. On success the booking it answers with replaces the cached one, so the amounts and
 * the list of payments update without loading the page again. It is never retried on its own; repeating a send is safe anyway,
 * because the idempotency key of the form registers the proof once.
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query mutation.
 */
export function useSubmitProof(token: string): UseMutationResult<PortalBooking, Error, SubmitProofVariables> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ accessToken, request }: SubmitProofVariables) => portalApi.submitProof(token, accessToken, request),
    onSuccess: (booking) => {
      queryClient.setQueryData(portalBookingKey(token), booking);
    },
  });
}
