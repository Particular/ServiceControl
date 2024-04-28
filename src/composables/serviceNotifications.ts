import { usePostToServiceControl, useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";

import type EmailNotifications from "@/resources/EmailNotifications";
import type UpdateEmailNotificationsSettingsRequest from "@/resources/UpdateEmailNotificationsSettingsRequest";
import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

export async function useEmailNotifications() {
  try {
    const [, data] = await useTypedFetchFromServiceControl<EmailNotifications>("notifications/email");
    return data;
  } catch (err) {
    console.log(err);
    return {
      enabled: false,
      enable_tls: false,
    };
  }
}

export async function useUpdateEmailNotifications(settings: UpdateEmailNotificationsSettingsRequest) {
  try {
    const response = await usePostToServiceControl("notifications/email", settings);
    return {
      message: response.ok ? "success" : `error:${response.statusText}`,
    };
  } catch (err) {
    console.log(err);
    return {
      message: "error",
    };
  }
}

export async function useTestEmailNotifications() {
  try {
    const response = await usePostToServiceControl("notifications/email/test");
    const responseStatusText = useIsSupported(environment.sc_version, "5.2") ? response.headers.get("X-Particular-Reason") : response.statusText;
    return {
      message: response.ok ? "success" : `error:${responseStatusText}`,
    };
  } catch (err) {
    console.log(err);
    return {
      message: "error",
    };
  }
}

export async function useToggleEmailNotifications(enabled: boolean) {
  try {
    const response = await usePostToServiceControl("notifications/email/toggle", {
      enabled: enabled,
    });
    return {
      message: response.ok ? "success" : `error:${response.statusText}`,
    };
  } catch (err) {
    console.log(err);
    return {
      message: "error",
    };
  }
}
