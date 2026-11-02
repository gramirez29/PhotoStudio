/**
 * Runs an async function over a list with at most a given number of calls at the same time. The results come back in the order of
 * the list, whatever the order the calls finish in. The function must not throw: a failure is a value of its result, so one bad item
 * never stops the others.
 * @param items The items.
 * @param limit Most calls running at once.
 * @param run Function to run for each item.
 * @returns The results, in the order of the items.
 */
export async function mapWithLimit<T, R>(items: readonly T[], limit: number, run: (item: T, index: number) => Promise<R>): Promise<R[]> {
  const results = new Array<R>(items.length);
  let next = 0;

  /** Takes the next item until there are none left. */
  async function worker(): Promise<void> {
    while (next < items.length) {
      const index = next++;
      results[index] = await run(items[index] as T, index);
    }
  }

  await Promise.all(Array.from({ length: Math.max(1, Math.min(limit, items.length)) }, () => worker()));
  return results;
}
