import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { maintenanceApi } from '../api/client';
import type { MaintenanceResponse } from '../types/api/maintenance';
import { BOOKING_QUERY_PREFIX } from './useBooking';
import { BOOKINGS_QUERY_PREFIX } from './useBookings';

/**
 * Mutation that runs a maintenance pass on the backend (expires the bookings whose hold ended). On success it refreshes
 * every cached booking and booking list, because expired bookings change status. It never retries: the backend limits
 * the call to one per minute, so a retry would only be rejected.
 * @returns The TanStack Query mutation.
 */
export function useRunMaintenance(): UseMutationResult<MaintenanceResponse, Error, void> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => maintenanceApi.runMaintenance(),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: BOOKINGS_QUERY_PREFIX }),
        queryClient.invalidateQueries({ queryKey: BOOKING_QUERY_PREFIX }),
      ]);
    },
  });
}
