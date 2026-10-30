import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { studioApi } from '../api/client';
import type { StudioCodeSent, StudioLoginResult, StudioUser } from '../types/api/studio';
import type { StudioLoginVariables, StudioVerifyVariables } from '../types/hooks/useStudioMutations.types';
import { clearPendingCode } from '../utils/pendingCode';
import { STUDIO_DATA_PREFIX, STUDIO_SESSION_KEY } from './useStudioSession';

/**
 * Mutation of the password step. A remembered browser gets its session right away, and then the cached session is the user; any
 * other browser gets a code, and the page shows the code step. It is never retried on its own: every request can send an email.
 * @returns The TanStack Query mutation.
 */
export function useStudioLogin(): UseMutationResult<StudioLoginResult, Error, StudioLoginVariables> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ username, password }: StudioLoginVariables) => studioApi.login(username, password),
    onSuccess: (result) => {
      if (result.status === 'SignedIn') {
        clearPendingCode();
        queryClient.setQueryData(STUDIO_SESSION_KEY, result.user);
      }
    },
  });
}

/**
 * Mutation of the code step. When the code is right the session cookie is set and the user becomes the cached session. It is never
 * retried on its own: a wrong code counts against the limit.
 * @returns The TanStack Query mutation.
 */
export function useStudioVerifyCode(): UseMutationResult<StudioUser, Error, StudioVerifyVariables> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ code, rememberDevice }: StudioVerifyVariables) => studioApi.verifyCode(code, rememberDevice),
    onSuccess: (user) => {
      clearPendingCode();
      queryClient.setQueryData(STUDIO_SESSION_KEY, user);
    },
  });
}

/**
 * Mutation that asks for another code. It is never retried on its own: every request sends an email.
 * @returns The TanStack Query mutation.
 */
export function useStudioResendCode(): UseMutationResult<StudioCodeSent, Error, void> {
  return useMutation({ mutationFn: () => studioApi.resendCode() });
}

/**
 * Mutation that signs out. It closes the session on the server, and then the cached session and every cached gallery are dropped,
 * so nothing of the photographer stays in memory.
 * @returns The TanStack Query mutation; its variable says whether to forget this browser too.
 */
export function useStudioLogout(): UseMutationResult<void, Error, boolean> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (forgetDevice: boolean) => studioApi.logout(forgetDevice),
    onSettled: () => {
      queryClient.removeQueries({ queryKey: STUDIO_DATA_PREFIX });
      queryClient.setQueryData(STUDIO_SESSION_KEY, null);
    },
  });
}
