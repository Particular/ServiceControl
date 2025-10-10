<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import LicenseExpired from "../LicenseExpired.vue";
import { licenseStatus } from "@/composables/serviceLicense";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import { connectionState } from "@/composables/serviceServiceControl";
import NoData from "../NoData.vue";
import BusyIndicator from "../BusyIndicator.vue";
import { useShowToast } from "@/composables/toast";
import TimeSince from "../TimeSince.vue";
import { useCreateRedirects, useDeleteRedirects, useRedirects, useRetryPendingMessagesForQueue, useUpdateRedirects } from "@/composables/serviceRedirects";
import ConfirmDialog from "../ConfirmDialog.vue";
import { TYPE } from "vue-toastification";
import type Redirect from "@/resources/Redirect";
import RetryRedirectEdit, { type RetryRedirect } from "@/components/configuration/RetryRedirectEdit.vue";
import redirectCountUpdated from "@/components/configuration/redirectCountUpdated";
import FAIcon from "@/components/FAIcon.vue";
import ActionButton from "@/components/ActionButton.vue";
import { faClock } from "@fortawesome/free-regular-svg-icons";

const isExpired = licenseStatus.isExpired;

const loadingData = ref(true);
const redirects = reactive<{ total: number; data: Redirect[] }>({
  total: 0,
  data: [],
});

const showDelete = ref(false);
const showEdit = ref(false);
const selectedRedirect = ref<Redirect & { queues: string[] }>({
  message_redirect_id: "",
  from_physical_address: "",
  to_physical_address: "",
  last_modified: "",
  queues: [],
});

const redirectSaveSuccessful = ref<boolean | null>(null);

async function getRedirect() {
  loadingData.value = true;
  const result = await useRedirects();
  if (redirects.total !== result.total) {
    redirectCountUpdated.count = result.total;
  }
  redirects.total = result.total;
  redirects.data = result.data;
  selectedRedirect.value.queues = result.queues;
  loadingData.value = false;
}

function createRedirect() {
  redirectSaveSuccessful.value = null;
  selectedRedirect.value.message_redirect_id = "";
  selectedRedirect.value.from_physical_address = "";
  selectedRedirect.value.to_physical_address = "";
  showEdit.value = true;
}

function editRedirect(redirect: Redirect) {
  redirectSaveSuccessful.value = null;
  selectedRedirect.value.message_redirect_id = redirect.message_redirect_id;
  selectedRedirect.value.from_physical_address = redirect.from_physical_address;
  selectedRedirect.value.to_physical_address = redirect.to_physical_address;
  showEdit.value = true;
}

async function saveEditedRedirect(redirect: RetryRedirect) {
  redirectSaveSuccessful.value = null;
  showEdit.value = false;
  const result = await useUpdateRedirects(redirect.redirectId, redirect.sourceQueue, redirect.targetQueue);
  if (result.message === "success") {
    redirectSaveSuccessful.value = true;
    useShowToast(TYPE.INFO, "Info", "Redirect updated successfully");
    getRedirect();
  } else {
    redirectSaveSuccessful.value = false;
    if (result.status === 409) {
      useShowToast(TYPE.ERROR, "Error", "Failed to update a redirect, can not create redirect to a queue" + redirect.targetQueue + " as it already has a redirect. Provide a different queue or end the redirect.");
    } else {
      useShowToast(TYPE.ERROR, "Error", result.message);
    }
  }
  if (result.message === "success" && redirect.immediatelyRetry) {
    return useRetryPendingMessagesForQueue(redirect.sourceQueue);
  } else {
    return result;
  }
}

async function saveCreatedRedirect(redirect: RetryRedirect) {
  redirectSaveSuccessful.value = null;
  showEdit.value = false;
  const result = await useCreateRedirects(redirect.sourceQueue, redirect.targetQueue);
  if (result.message === "success") {
    redirectSaveSuccessful.value = true;
    useShowToast(TYPE.INFO, "Info", "Redirect created successfully");
    getRedirect();
  } else {
    redirectSaveSuccessful.value = false;
    if (result.status === 409 && result.statusText === "Duplicate") {
      useShowToast(TYPE.ERROR, "Error", "Failed to create a redirect, can not create more than one redirect for queue: " + redirect.sourceQueue);
    } else if (result.status === 409 && result.statusText === "Dependents") {
      useShowToast(TYPE.ERROR, "Error", "Failed to create a redirect, can not create a redirect to a queue that already has a redirect or is a target of a redirect.");
    } else {
      useShowToast(TYPE.ERROR, "Error", result.message);
    }
  }
}

function deleteRedirect(redirect: Redirect) {
  redirectSaveSuccessful.value = null;
  selectedRedirect.value.message_redirect_id = redirect.message_redirect_id;
  showDelete.value = true;
}

async function saveDeleteRedirect() {
  const result = await useDeleteRedirects(selectedRedirect.value.message_redirect_id);
  if (result.message === "success") {
    redirectSaveSuccessful.value = true;
    useShowToast(TYPE.INFO, "Info", "Redirect deleted");
    await getRedirect();
  } else {
    redirectSaveSuccessful.value = false;
    useShowToast(TYPE.ERROR, "Error", result.message);
  }
}

onMounted(() => {
  getRedirect();
});
</script>

<template>
  <LicenseExpired />
  <template v-if="!isExpired">
    <section name="redirects">
      <ServiceControlNotAvailable />
      <template v-if="!connectionState.unableToConnect">
        <section>
          <busy-indicator v-if="loadingData"></busy-indicator>

          <div class="row">
            <div class="col-sm-12">
              <div class="btn-toolbar">
                <ActionButton @click="createRedirect"><i class="fa pa-redirect-source pa-redirect-small"></i> Create Redirect</ActionButton>
                <span></span>
              </div>
            </div>
          </div>

          <NoData v-if="redirects.total === 0 && !loadingData" title="Redirects" message="There are currently no redirects"></NoData>

          <div class="row">
            <template v-if="redirects.total > 0">
              <div class="col-sm-12">
                <template v-for="redirect in redirects.data" :key="redirect.message_redirect_id">
                  <div class="row box repeat-modify">
                    <div class="row" id="{{redirect.from_physical_address}}">
                      <div class="col-sm-12">
                        <p class="lead hard-wrap truncate" :title="redirect.from_physical_address">
                          <i class="fa pa-redirect-source pa-redirect-small" title="Source queue name"></i>
                          {{ redirect.from_physical_address }}
                        </p>
                        <p class="lead hard-wrap truncate" :title="redirect.to_physical_address">
                          <i class="fa pa-redirect-destination pa-redirect-small" title="Destination queue name"></i>
                          {{ redirect.to_physical_address }}
                        </p>
                        <p class="metadata">
                          <FAIcon :icon="faClock" size="sm" class="icon" />
                          Last modified: <time-since :dateUtc="redirect.last_modified"></time-since>
                        </p>
                      </div>
                    </div>
                    <div class="row">
                      <div class="col-sm-12">
                        <p class="small">
                          <ActionButton variant="link" size="sm" @click="deleteRedirect(redirect)">End Redirect</ActionButton>
                          <ActionButton variant="link" size="sm" @click="editRedirect(redirect)">Modify Redirect</ActionButton>
                        </p>
                      </div>
                    </div>
                  </div>
                </template>
              </div>
            </template>
          </div>
          <Teleport to="#modalDisplay">
            <ConfirmDialog
              v-if="showDelete"
              @cancel="showDelete = false"
              @confirm="
                showDelete = false;
                saveDeleteRedirect();
              "
              :heading="'Are you sure you want to end the redirect?'"
              :body="'Once the redirect is ended, any affected messages will be sent to the original destination queue. Ensure this queue is ready to accept messages again.'"
            ></ConfirmDialog>
          </Teleport>
          <Teleport to="#modalDisplay">
            <RetryRedirectEdit v-if="showEdit" v-bind="selectedRedirect" @cancel="showEdit = false" @create="saveCreatedRedirect" @edit="saveEditedRedirect"></RetryRedirectEdit>
          </Teleport>
        </section>
      </template>
    </section>
  </template>
</template>

<style scoped>
.pa-redirect-source {
  background-image: url("@/assets/redirect-source.svg");
  background-position: center;
  background-repeat: no-repeat;
}

.pa-redirect-small {
  position: relative;
  top: 1px;
  height: 14px;
  width: 14px;
}

.pa-redirect-destination {
  background-image: url("@/assets/redirect-destination.svg");
  background-position: center;
  background-repeat: no-repeat;
}

.icon {
  color: var(--reduced-emphasis);
}
</style>
