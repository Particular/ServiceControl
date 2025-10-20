import { postToServiceControl, useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";

import type EmailNotifications from "@/resources/EmailNotifications";
import type UpdateEmailNotificationsSettingsRequest from "@/resources/UpdateEmailNotificationsSettingsRequest";

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
    const response = await postToServiceControl("notifications/email", settings);
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

export async function useTestEmailNotifications(hasResponseStatusInHeaders: boolean) {
  try {
    const response = await postToServiceControl("notifications/email/test");
    const responseStatusText = hasResponseStatusInHeaders ? response.headers.get("X-Particular-Reason") : response.statusText;
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
    const response = await postToServiceControl("notifications/email/toggle", {
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
