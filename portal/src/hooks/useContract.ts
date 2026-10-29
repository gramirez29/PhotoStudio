import { useMutation, useQuery, useQueryClient, type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { portalApi } from '../api/client';
import type { PortalBooking, PortalContract } from '../types/api/portal';
import type { SignContractVariables } from '../types/hooks/useContract.types';
import { portalBookingKey } from './usePortalBooking';

/**
 * Key of the cached contract of a link.
 * @param token Secret token taken from the URL.
 * @returns The query key.
 */
export function portalContractKey(token: string): readonly [string, string] {
  return ['portal-contract', token];
}

/**
 * Loads the contract of the booking. The text holds personal data (the phone and, once signed, the identity number), so it is
 * dropped from the cache as soon as nothing shows it, and it is never retried on its own: a refused session needs a new code.
 * @param token Secret token taken from the URL.
 * @param accessToken Token of the session obtained by verifying a code.
 * @returns The TanStack Query result.
 */
export function useContract(token: string, accessToken: string): UseQueryResult<PortalContract, Error> {
  return useQuery({
    queryKey: portalContractKey(token),
    queryFn: ({ signal }) => portalApi.getContract(token, accessToken, signal),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
}

/**
 * Mutation that signs the contract. On success the booking it answers with replaces the cached one, so the page shows the
 * contract as signed (and the booking as confirmed when the deposit was covered) without loading again. It is never retried
 * on its own: a second signature is refused by the server anyway.
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query mutation.
 */
export function useSignContract(token: string): UseMutationResult<PortalBooking, Error, SignContractVariables> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ accessToken, request }: SignContractVariables) => portalApi.signContract(token, accessToken, request),
    onSuccess: (booking) => {
      queryClient.setQueryData(portalBookingKey(token), booking);
      queryClient.removeQueries({ queryKey: portalContractKey(token) });
    },
  });
}
