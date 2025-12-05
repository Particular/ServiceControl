import { defineStore, acceptHMRUpdate } from "pinia";
import { ref } from "vue";
import { useRoute, useRouter, type RouteLocationNormalizedLoaded } from "vue-router";
import { useCookies } from "vue3-cookies";

export interface MonitoringHistoryPeriod {
  pVal: number;
  text: string;
  refreshIntervalVal: number;
  refreshIntervalText: string;
}

export const useMonitoringHistoryPeriodStore = defineStore("MonitoringHistoryPeriodStore", () => {
  const { cookies } = useCookies();
  const route = useRoute();
  const router = useRouter();

  const periods: MonitoringHistoryPeriod[] = [
    { pVal: 1, text: "1m", refreshIntervalVal: 1 * 1000, refreshIntervalText: "Show data from the last minute. Refreshes every 1 second" },
    { pVal: 5, text: "5m", refreshIntervalVal: 5 * 1000, refreshIntervalText: "Show data from the last 5 minutes. Refreshes every 5 seconds" },
    { pVal: 10, text: "10m", refreshIntervalVal: 10 * 1000, refreshIntervalText: "Show data from the last 10 minutes. Refreshes every 10 seconds" },
    { pVal: 15, text: "15m", refreshIntervalVal: 15 * 1000, refreshIntervalText: "Show data from the last 15 minutes. Refreshes every 15 seconds" },
    { pVal: 30, text: "30m", refreshIntervalVal: 30 * 1000, refreshIntervalText: "Show data from the last 30 minutes. Refreshes every 30 seconds" },
    { pVal: 60, text: "1h", refreshIntervalVal: 60 * 1000, refreshIntervalText: "Show data from the last hour. Refreshes every 1 minute" },
  ];

  function getHistoryPeriod(route?: RouteLocationNormalizedLoaded, requestedPeriod?: string) {
    const period = requestedPeriod ?? (route?.query?.historyPeriod?.toString() || cookies.get("history_period"));

    return allPeriods.value.find((index) => index.pVal === parseInt(period)) ?? periods[0];
  }

  const allPeriods = ref<MonitoringHistoryPeriod[]>(periods);

  const historyPeriod = ref<MonitoringHistoryPeriod>(getHistoryPeriod(route));

  /**
   * @param {String} requestedPeriod - The history period value
   * @description Sets the history period based on, in order of importance, a passed parameter, the url query string, saved cookie, or default value
   */
  async function setHistoryPeriod(requestedPeriod?: string) {
    historyPeriod.value = getHistoryPeriod(route, requestedPeriod);
    cookies.set("history_period", historyPeriod.value.pVal.toString());
    await router.replace({ query: { ...route.query, historyPeriod: historyPeriod.value.pVal } });
  }

  return {
    allPeriods,
    historyPeriod,
    setHistoryPeriod,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useMonitoringHistoryPeriodStore, import.meta.hot));
}

export type MonitoringHistoryPeriodStore = ReturnType<typeof useMonitoringHistoryPeriodStore>;
