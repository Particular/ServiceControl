import { isSupported, isUpgradeAvailable } from "@/composables/serviceSemVer";
import { useTypedFetchFromMonitoring, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import Release from "@/resources/Release";
import RootUrls from "@/resources/RootUrls";
import { useMemoize } from "@vueuse/core";
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, reactive } from "vue";

export const useEnvironmentAndVersionsStore = defineStore("EnvironmentAndVersionsStore", () => {
  const environment = reactive({
    monitoring_version: "",
    sc_version: "",
    minimum_supported_sc_version: "6.6.0",
    is_compatible_with_sc: true,
    sp_version: window.defaultConfig && window.defaultConfig.version ? window.defaultConfig.version : "1.2.0",
    supportsArchiveGroups: false,
    endpoints_error_url: "",
    known_endpoints_url: "",
    endpoints_message_search_url: "",
    endpoints_messages_url: "",
    endpoints_url: "",
    errors_url: "",
    configuration: "",
    message_search_url: "",
    sagas_url: "",
  });

  const newVersions = reactive({
    newSPVersion: {
      newspversion: false,
      newspversionlink: "",
      newspversionnumber: "",
    },
    newSCVersion: {
      newscversion: false,
      newscversionlink: "",
      newscversionnumber: "",
    },
    newMVersion: {
      newmversion: false,
      newmversionlink: "",
      newmversionnumber: "",
    },
  });

  const serviceControlIsGreaterThan = useMemoize((requiredVersion: string) => computed(() => isSupported(environment.sc_version, requiredVersion)));

  async function refresh() {
    const productsResult = useServiceProductUrls();
    const scResult = getPrimaryVersion();
    const mResult = setMonitoringVersion();

    const [products, scVer] = await Promise.all([productsResult, scResult, mResult]);
    if (scVer) {
      environment.supportsArchiveGroups = !!scVer.archived_groups_url;
      environment.is_compatible_with_sc = isSupported(environment.sc_version, environment.minimum_supported_sc_version);
      environment.endpoints_error_url = scVer && scVer.endpoints_error_url;
      environment.known_endpoints_url = scVer && scVer.known_endpoints_url;
      environment.endpoints_message_search_url = scVer.endpoints_message_search_url;
      environment.endpoints_messages_url = scVer.endpoints_messages_url;
      environment.endpoints_url = scVer.endpoints_url;
      environment.errors_url = scVer.errors_url;
      environment.configuration = scVer.configuration;
      environment.message_search_url = scVer.message_search_url;
      environment.sagas_url = scVer.sagas_url;
    }
    if (products.latestSP && isUpgradeAvailable(environment.sp_version, products.latestSP.tag)) {
      newVersions.newSPVersion.newspversion = true;
      newVersions.newSPVersion.newspversionlink = products.latestSP.release;
      newVersions.newSPVersion.newspversionnumber = products.latestSP.tag;
    }
    if (products.latestSC && isUpgradeAvailable(environment.sc_version, products.latestSC.tag)) {
      newVersions.newSCVersion.newscversion = true;
      newVersions.newSCVersion.newscversionlink = products.latestSC.release;
      newVersions.newSCVersion.newscversionnumber = products.latestSC.tag;
    }
    if (products.latestSC && isUpgradeAvailable(environment.monitoring_version, products.latestSC.tag)) {
      newVersions.newMVersion.newmversion = true;
      newVersions.newMVersion.newmversionlink = products.latestSC.release;
      newVersions.newMVersion.newmversionnumber = products.latestSC.tag;
    }
  }

  async function getPrimaryVersion() {
    try {
      const [response, data] = await useTypedFetchFromServiceControl<RootUrls>("");
      environment.sc_version = response.headers.get("X-Particular-Version") ?? "";
      return data;
    } catch {
      return null;
    }
  }

  async function setMonitoringVersion() {
    try {
      const [response] = await useTypedFetchFromMonitoring("");
      if (response) {
        environment.monitoring_version = response.headers.get("X-Particular-Version") ?? "";
      }
    } catch {
      environment.monitoring_version = "";
    }
  }

  return {
    refresh,
    environment,
    newVersions,
    serviceControlIsGreaterThan,
  };
});

async function getData(url: string) {
  try {
    const response = await fetch(url);
    return (await response.json()) as unknown as Release[];
  } catch (e) {
    console.log(e);
    return [
      {
        tag: "Unknown",
        release: "Unknown",
        published: "Unknown",
      },
    ];
  }
}

async function useServiceProductUrls() {
  const spURL = "https://platformupdate.particular.net/servicepulse.txt";
  const scURL = "https://platformupdate.particular.net/servicecontrol.txt";

  const servicePulse = getData(spURL);
  const serviceControl = getData(scURL);

  const [sp, sc] = await Promise.all([servicePulse, serviceControl]);
  const latestSP = sp[0];
  const latestSC = sc[0];

  return { latestSP, latestSC };
}

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useEnvironmentAndVersionsStore, import.meta.hot));
}

export type StatsStore = ReturnType<typeof useEnvironmentAndVersionsStore>;
