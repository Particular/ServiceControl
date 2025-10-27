<script setup lang="ts">
import { onMounted, ref } from "vue";
import LicenseNotExpired from "../LicenseNotExpired.vue";
import ServiceControlAvailable from "../ServiceControlAvailable.vue";
import HealthCheckNotifications_EmailConfiguration from "./HealthCheckNotifications_ConfigureEmail.vue";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import type UpdateEmailNotificationsSettingsRequest from "@/resources/UpdateEmailNotificationsSettingsRequest";
import OnOffSwitch from "../OnOffSwitch.vue";
import FAIcon from "@/components/FAIcon.vue";
import ActionButton from "@/components/ActionButton.vue";
import { faCheck, faEdit, faEnvelope, faExclamationTriangle } from "@fortawesome/free-solid-svg-icons";
import { useHealthChecksStore } from "@/stores/HealthChecksStore";
import { storeToRefs } from "pinia";

const healthChecksStore = useHealthChecksStore();
const { emailNotifications } = storeToRefs(healthChecksStore);

const emailTestSuccessful = ref<boolean | null>(null);
const emailTestInProgress = ref<boolean | null>(null);
const emailToggleSuccessful = ref<boolean | null>(null);
const emailUpdateSuccessful = ref<boolean | null>(null);
const showEmailConfiguration = ref(false);

async function toggleEmailNotifications() {
  emailTestSuccessful.value = null;
  emailUpdateSuccessful.value = null;
  emailToggleSuccessful.value = await healthChecksStore.toggleEmailNotifications();
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
  const saveSuccessful = await healthChecksStore.saveEmailNotifications(newSettings);
  if (saveSuccessful) {
    emailUpdateSuccessful.value = true;
    useShowToast(TYPE.INFO, "Info", "Email settings updated.");
  } else {
    emailUpdateSuccessful.value = false;
    useShowToast(TYPE.ERROR, "Error", "Failed to update the email settings.");
  }
}

async function testEmailNotifications() {
  emailTestInProgress.value = true;
  emailToggleSuccessful.value = null;
  emailUpdateSuccessful.value = null;
  emailTestSuccessful.value = await healthChecksStore.testEmailNotifications();
  emailTestInProgress.value = false;
}

onMounted(async () => {
  await healthChecksStore.refresh();
});
</script>

<template>
  <section name="notifications">
    <ServiceControlAvailable>
      <LicenseNotExpired>
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
                          <template v-if="emailToggleSuccessful === false"> <FAIcon :icon="faExclamationTriangle" /> Update failed </template>
                        </span>
                      </div>
                    </div>
                    <div class="col-xs-9 col-sm-10 col-lg-11">
                      <div class="row box-header">
                        <div class="col-12">
                          <p class="lead">Email notifications</p>
                          <p class="endpoint-metadata">
                            <ActionButton variant="link" size="sm" :icon="faEdit" @click="editEmailNotifications">Configure</ActionButton>
                          </p>
                          <p class="endpoint-metadata">
                            <ActionButton variant="link" size="sm" :icon="faEnvelope" @click="testEmailNotifications" :disabled="!!emailTestInProgress">Send test notification</ActionButton>
                            <span class="connection-test connection-testing">
                              <template v-if="emailTestInProgress">
                                <i class="glyphicon glyphicon-refresh rotate"></i>
                                Testing
                              </template>
                            </span>
                            <span class="connection-test connection-successful">
                              <template v-if="emailTestSuccessful === true"><FAIcon :icon="faCheck" /> Test email sent successfully </template>
                            </span>
                            <span class="connection-test connection-failed">
                              <template v-if="emailTestSuccessful === false"><FAIcon :icon="faExclamationTriangle" /> Test failed </template>
                            </span>
                            <span class="connection-test connection-successful">
                              <template v-if="emailUpdateSuccessful === true"><FAIcon :icon="faCheck" /> Update successful </template>
                            </span>
                            <span class="connection-test connection-failed">
                              <template v-if="emailUpdateSuccessful === false">
                                <FAIcon :icon="faExclamationTriangle" />
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
      </LicenseNotExpired>

      <Teleport to="#modalDisplay">
        <!-- use the modal component, pass in the prop -->
        <HealthCheckNotifications_EmailConfiguration v-if="showEmailConfiguration" v-bind="emailNotifications" @cancel="showEmailConfiguration = false" @save="saveEditedEmailNotifications"> </HealthCheckNotifications_EmailConfiguration>
      </Teleport>
    </ServiceControlAvailable>
  </section>
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
  color: var(--sp-blue);
  margin-right: 4px;
}

.btn-sm {
  color: var(--sp-blue);
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
