/**
 * Enables refresh functionality, either auto or manual
 * @param refreshAction The action to perform (by default) when refreshing
 * @param defaultTimeout The time between refreshes in ms or null if no auto-refresh is desired
 */
export default function useAutoRefresh(refreshAction: () => Promise<void>, defaultTimeout: number | null, startImmediately = true) {
  let refreshInterval: ReturnType<typeof setTimeout> | null = null;
  const timeout = { value: defaultTimeout };

  function stopTimer() {
    if (refreshInterval !== null) {
      clearTimeout(refreshInterval);
      refreshInterval = null;
    }
  }

  function startTimer() {
    if (timeout.value === null) return;

    stopTimer();
    refreshInterval = setTimeout(() => {
      executeAndResetTimer();
    }, timeout.value as number);
  }

  async function executeAndResetTimer(overrideAction?: () => Promise<void>) {
    try {
      stopTimer();
      await (overrideAction ?? refreshAction)();
    } finally {
      startTimer();
    }
  }

  /**
   * Updates the timeout interval between refreshes
   * @param updatedTimeout The new time between refreshes in ms or null if no auto-refresh is desired
   */
  async function updateTimeout(updatedTimeout: number | null) {
    timeout.value = updatedTimeout;
    await executeAndResetTimer();
  }

  // eslint-disable-next-line promise/catch-or-return,promise/prefer-await-to-then,promise/valid-params
  if (startImmediately) executeAndResetTimer().then();

  return {
    executeAndResetTimer,
    updateTimeout,
  };
}
