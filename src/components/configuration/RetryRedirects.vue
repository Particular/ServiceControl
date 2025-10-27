<script setup lang="ts">
import { onMounted, ref } from "vue";
import LicenseNotExpired from "../LicenseNotExpired.vue";
import ServiceControlAvailable from "../ServiceControlAvailable.vue";
import NoData from "../NoData.vue";
import { useShowToast } from "@/composables/toast";
import TimeSince from "../TimeSince.vue";
import ConfirmDialog from "../ConfirmDialog.vue";
import { TYPE } from "vue-toastification";
import type Redirect from "@/resources/Redirect";
import RetryRedirectEdit, { type RetryRedirect } from "@/components/configuration/RetryRedirectEdit.vue";
import FAIcon from "@/components/FAIcon.vue";
import ActionButton from "@/components/ActionButton.vue";
import { faClock } from "@fortawesome/free-regular-svg-icons";
import useEnvironmentAndVersionsAutoRefresh from "@/composables/useEnvironmentAndVersionsAutoRefresh";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { useRedirectsStore } from "@/stores/RedirectsStore";
import LoadingSpinner from "../LoadingSpinner.vue";

const { store: environmentStore } = useEnvironmentAndVersionsAutoRefresh();
const hasResponseStatusInHeader = environmentStore.serviceControlIsGreaterThan("5.2.0");
const serviceControlStore = useServiceControlStore();
const redirectsStore = useRedirectsStore();

const loadingData = ref(true);
const redirects = redirectsStore.redirects;

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
  await redirectsStore.refresh();
  selectedRedirect.value.queues = redirectsStore.redirects.queues;
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

async function saveUpdatedRedirect(redirect: RetryRedirect) {
  redirectSaveSuccessful.value = null;
  showEdit.value = false;
  const result = handleResponse(
    await serviceControlStore.putToServiceControl(`redirects/${redirect.redirectId}`, {
      id: redirect.redirectId,
      fromphysicaladdress: redirect.sourceQueue,
      tophysicaladdress: redirect.targetQueue,
    })
  );
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
    return redirectsStore.retryPendingMessagesForQueue(redirect.sourceQueue);
  } else {
    return result;
  }
}

async function saveCreatedRedirect(redirect: RetryRedirect) {
  redirectSaveSuccessful.value = null;
  showEdit.value = false;
  const result = handleResponse(
    await serviceControlStore.postToServiceControl("redirects", {
      fromphysicaladdress: redirect.sourceQueue,
      tophysicaladdress: redirect.targetQueue,
    })
  );
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

async function saveDeletedRedirect() {
  const result = handleResponse(await serviceControlStore.deleteFromServiceControl(`redirects/${selectedRedirect.value.message_redirect_id}`));
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

function handleResponse(response: Response) {
  const responseStatusText = hasResponseStatusInHeader.value ? response.headers.get("X-Particular-Reason") : response.statusText;
  return {
    message: response.ok ? "success" : `error:${response.statusText}`,
    status: response.status,
    statusText: responseStatusText,
    data: response,
  };
}
</script>

<template>
  <section name="redirects">
    <ServiceControlAvailable>
      <LicenseNotExpired>
        <section>
          <LoadingSpinner v-if="loadingData" />

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
                saveDeletedRedirect();
              "
              :heading="'Are you sure you want to end the redirect?'"
              :body="'Once the redirect is ended, any affected messages will be sent to the original destination queue. Ensure this queue is ready to accept messages again.'"
            ></ConfirmDialog>
          </Teleport>
          <Teleport to="#modalDisplay">
            <RetryRedirectEdit v-if="showEdit" v-bind="selectedRedirect" @cancel="showEdit = false" @create="saveCreatedRedirect" @edit="saveUpdatedRedirect"></RetryRedirectEdit>
          </Teleport>
        </section>
      </LicenseNotExpired>
    </ServiceControlAvailable>
  </section>
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
