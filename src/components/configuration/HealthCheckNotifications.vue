<script setup lang="ts">
import { onMounted, ref } from "vue";
import LicenseExpired from "../LicenseExpired.vue";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import { licenseStatus } from "@/composables/serviceLicense";
import { connectionState } from "@/composables/serviceServiceControl";
import HealthCheckNotifications_EmailConfiguration from "./HealthCheckNotifications_ConfigureEmail.vue";
import { useEmailNotifications, useTestEmailNotifications, useToggleEmailNotifications, useUpdateEmailNotifications } from "@/composables/serviceNotifications";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import type UpdateEmailNotificationsSettingsRequest from "@/resources/UpdateEmailNotificationsSettingsRequest";
import type EmailSettings from "@/components/configuration/EmailSettings";
import OnOffSwitch from "../OnOffSwitch.vue";

const isExpired = licenseStatus.isExpired;
const emailTestSuccessful = ref<boolean | null>(null);
const emailTestInProgress = ref<boolean | null>(null);
const emailToggleSuccessful = ref<boolean | null>(null);
const emailUpdateSuccessful = ref<boolean | null>(null);
const showEmailConfiguration = ref<boolean | null>(null);

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

async function toggleEmailNotifications() {
  emailTestSuccessful.value = null;
  emailUpdateSuccessful.value = null;
  const result = await useToggleEmailNotifications(emailNotifications.value.enabled === null ? true : !emailNotifications.value.enabled);
  if (result.message === "success") {
    emailToggleSuccessful.value = true;
  } else {
    emailToggleSuccessful.value = false;
    //set it back to what it was
    emailNotifications.value.enabled = !emailNotifications.value.enabled;
  }
}

function editEmailNotifications() {
  emailToggleSuccessful.value = null;
  emailTestSuccessful.value = null;
  emailUpdateSuccessful.value = null;
  showEmailConfiguration.value = true;
}

async function saveEditedEmailNotifications(newSettings: UpdateEmailNotificationsSettingsRequest) {
  emailUpdateSuccessful.value = null;
  showEmailConfiguration.value = false;
  const result = await useUpdateEmailNotifications(newSettings);
  if (result.message === "success") {
    emailUpdateSuccessful.value = true;
    useShowToast(TYPE.INFO, "Info", "Email settings updated.");
    emailNotifications.value.enable_tls = newSettings.enable_tls;
    emailNotifications.value.smtp_server = newSettings.smtp_server;
    emailNotifications.value.smtp_port = newSettings.smtp_port;
    emailNotifications.value.authentication_account = newSettings.authorization_account;
    emailNotifications.value.authentication_password = newSettings.authorization_password;
    emailNotifications.value.from = newSettings.from;
    emailNotifications.value.to = newSettings.to;
  } else {
    emailUpdateSuccessful.value = false;
    useShowToast(TYPE.ERROR, "Error", "Failed to update the email settings.");
  }
}

async function testEmailNotifications() {
  emailTestInProgress.value = true;
  emailToggleSuccessful.value = null;
  emailUpdateSuccessful.value = null;
  const result = await useTestEmailNotifications();
  emailTestSuccessful.value = result.message === "success";
  emailTestInProgress.value = false;
}

async function getEmailNotifications() {
  showEmailConfiguration.value = false;
  const result = await useEmailNotifications();
  emailNotifications.value.enabled = result.enabled;
  emailNotifications.value.enable_tls = result.enable_tls;
  emailNotifications.value.smtp_server = result.smtp_server ? result.smtp_server : "";
  emailNotifications.value.smtp_port = result.smtp_port ? result.smtp_port : null;
  emailNotifications.value.authentication_account = result.authentication_account ? result.authentication_account : "";
  emailNotifications.value.authentication_password = result.authentication_password ? result.authentication_password : "";
  emailNotifications.value.from = result.from ? result.from : "";
  emailNotifications.value.to = result.to ? result.to : "";
}

onMounted(async () => {
  await getEmailNotifications();
});
</script>

<template>
  <LicenseExpired />
  <template v-if="!isExpired">
    <section name="notifications">
      <ServiceControlNotAvailable />
      <template v-if="!connectionState.unableToConnect">
        <section>
          <div class="row">
            <div class="col-12">
              <p class="screen-intro">Configure notifications for health checks built into ServiceControl (low disk space, stale database indexes, audit ingestion, etc.).</p>
            </div>
          </div>
          <div class="notifications row">
            <div class="col-12">
              <div class="row box box-no-click">
                <div class="col-12 no-side-padding">
                  <div class="row">
                    <div class="col-auto">
                      <OnOffSwitch id="emailNotifications" @toggle="toggleEmailNotifications" :value="emailNotifications.enabled" />
                      <div>
                        <span class="connection-test connection-failed">
                          <template v-if="emailToggleSuccessful === false"> <i class="fa fa-exclamation-triangle"></i> Update failed </template>
                        </span>
                      </div>
                    </div>
                    <div class="col-xs-9 col-sm-10 col-lg-11">
                      <div class="row box-header">
                        <div class="col-12">
                          <p class="lead">Email notifications</p>
                          <p class="endpoint-metadata">
                            <button class="btn btn-link btn-sm" type="button" @click="editEmailNotifications"><i class="fa fa-edit"></i>Configure</button>
                          </p>
                          <p class="endpoint-metadata">
                            <button class="btn btn-link btn-sm" type="button" @click="testEmailNotifications" :disabled="!!emailTestInProgress"><i class="fa fa-envelope"></i>Send test notification</button>
                            <span class="connection-test connection-testing">
                              <template v-if="emailTestInProgress">
                                <i class="glyphicon glyphicon-refresh rotate"></i>
                                Testing
                              </template>
                            </span>
                            <span class="connection-test connection-successful">
                              <template v-if="emailTestSuccessful === true"> <i class="fa fa-check"></i> Test email sent successfully </template>
                            </span>
                            <span class="connection-test connection-failed">
                              <template v-if="emailTestSuccessful === false"> <i class="fa fa-exclamation-triangle"></i> Test failed </template>
                            </span>
                            <span class="connection-test connection-successful">
                              <template v-if="emailUpdateSuccessful === true"> <i class="fa fa-check"></i> Update successful </template>
                            </span>
                            <span class="connection-test connection-failed">
                              <template v-if="emailUpdateSuccessful === false">
                                <i class="fa fa-exclamation-triangle"></i>
                                Update failed
                              </template>
                            </span>
                          </p>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>
      </template>

      <Teleport to="#modalDisplay">
        <!-- use the modal component, pass in the prop -->
        <HealthCheckNotifications_EmailConfiguration v-if="showEmailConfiguration === true" v-bind="emailNotifications" @cancel="showEmailConfiguration = false" @save="saveEditedEmailNotifications"> </HealthCheckNotifications_EmailConfiguration>
      </Teleport>
    </section>
  </template>
</template>

<style scoped>
@import "../list.css";

.screen-intro {
  margin: 30px 0;
}

.box-header {
  padding-bottom: 3px;
  padding-top: 2px;
}

.box-header ul {
  list-style-type: none;
  margin: 0;
  padding: 0;
}

p.endpoint-metadata {
  display: inline-block;
  margin-top: 4px;
  padding-right: 30px;
}

.endpoint-metadata button i {
  color: #00a3c4;
  margin-right: 4px;
}

.btn-sm {
  color: #00a3c4;
  font-size: 14px;
  font-weight: bold;
  padding: 0 36px 10px 0;
  text-decoration: none;
}

.notifications .btn-sm {
  padding: 0;
}

.notifications .connection-test {
  top: 2px;
}
</style>
