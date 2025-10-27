import EmailSettings from "@/components/configuration/EmailSettings";
import { acceptHMRUpdate, defineStore } from "pinia";
import { ref } from "vue";
import { useServiceControlStore } from "./ServiceControlStore";
import EmailNotifications from "@/resources/EmailNotifications";
import UpdateEmailNotificationsSettingsRequest from "@/resources/UpdateEmailNotificationsSettingsRequest";
import { useEnvironmentAndVersionsStore } from "./EnvironmentAndVersionsStore";

export const useHealthChecksStore = defineStore("HealthChecksStore", () => {
  const emailNotifications = ref<EmailSettings>({
    enabled: null,
    enable_tls: null,
    smtp_server: "",
    smtp_port: null,
    authentication_account: "",
    authentication_password: "",
    from: "",
    to: "",
  });

  const serviceControlStore = useServiceControlStore();
  const environmentStore = useEnvironmentAndVersionsStore();
  const hasResponseStatusInHeaders = environmentStore.serviceControlIsGreaterThan("5.2");

  async function refresh() {
    let result: EmailNotifications | null = null;
    try {
      const [, data] = await serviceControlStore.fetchTypedFromServiceControl<EmailNotifications>("notifications/email");
      result = data;
    } catch (err) {
      console.error(err);
      result = {
        enabled: false,
        enable_tls: false,
      };
    }

    emailNotifications.value = {
      enabled: result.enabled,
      enable_tls: result.enable_tls,
      smtp_server: result.smtp_server ? result.smtp_server : "",
      smtp_port: result.smtp_port ? result.smtp_port : null,
      authentication_account: result.authentication_account ? result.authentication_account : "",
      authentication_password: result.authentication_password ? result.authentication_password : "",
      from: result.from ? result.from : "",
      to: result.to ? result.to : "",
    };
  }

  async function toggleEmailNotifications() {
    const result = await getResponseOrError(() =>
      serviceControlStore.postToServiceControl("notifications/email/toggle", {
        enabled: !(emailNotifications.value.enabled ?? true),
      })
    );
    if (result.message === "success") return true;
    else {
      console.error(result.message);
      //set it back to what it was
      emailNotifications.value.enabled = !emailNotifications.value.enabled;
      return false;
    }
  }

  async function testEmailNotifications() {
    const result = await getResponseOrError(
      () => serviceControlStore.postToServiceControl("notifications/email/test"),
      (response) => (hasResponseStatusInHeaders.value ? (response.headers.get("X-Particular-Reason") ?? response.statusText) : response.statusText)
    );
    if (result.message === "success") return true;
    else {
      console.error(result.message);
      return false;
    }
  }

  async function saveEmailNotifications(newSettings: UpdateEmailNotificationsSettingsRequest) {
    const result = await getResponseOrError(() => serviceControlStore.postToServiceControl("notifications/email", newSettings));
    if (result.message === "success") {
      emailNotifications.value = {
        enabled: emailNotifications.value.enabled,
        enable_tls: newSettings.enable_tls,
        smtp_server: newSettings.smtp_server,
        smtp_port: newSettings.smtp_port,
        authentication_account: newSettings.authorization_account,
        authentication_password: newSettings.authorization_password,
        from: newSettings.from,
        to: newSettings.to,
      };
      return true;
    } else {
      console.error(result.message);
      return false;
    }
  }

  async function getResponseOrError(action: () => Promise<Response>, responseStatusTextOverride?: (response: Response) => string) {
    const responseStatusTextDefault = (response: Response) => response.statusText;
    const responseStatusText = responseStatusTextOverride ?? responseStatusTextDefault;
    try {
      const response = await action();
      return {
        message: response.ok ? "success" : `error:${responseStatusText(response)}`,
      };
    } catch (err) {
      return {
        message: (err as Error).message ?? err,
      };
    }
  }

  return {
    refresh,
    emailNotifications,
    toggleEmailNotifications,
    saveEmailNotifications,
    testEmailNotifications,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useHealthChecksStore, import.meta.hot));
}

export type HealthChecksStore = ReturnType<typeof useHealthChecksStore>;
