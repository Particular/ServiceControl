<script setup lang="ts">
import { onMounted, onUnmounted, ref, useTemplateRef, watch } from "vue";
import { licenseStatus } from "../../composables/serviceLicense";
import { patchToServiceControl, postToServiceControl, useTypedFetchFromServiceControl } from "../../composables/serviceServiceControlUrls";
import { useShowToast } from "../../composables/toast";
import { useCookies } from "vue3-cookies";
import OrderBy from "@/components/OrderBy.vue";
import LicenseExpired from "../../components/LicenseExpired.vue";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import MessageList, { IMessageList } from "./MessageList.vue";
import ConfirmDialog from "../ConfirmDialog.vue";
import PaginationStrip from "../../components/PaginationStrip.vue";
import { ExtendedFailedMessage, FailedMessageStatus } from "@/resources/FailedMessage";
import SortOptions, { SortDirection } from "@/resources/SortOptions";
import QueueAddress from "@/resources/QueueAddress";
import { TYPE } from "vue-toastification";
import GroupOperation from "@/resources/GroupOperation";
import { useIsMassTransitConnected } from "@/composables/useIsMassTransitConnected";
import { faArrowDownAZ, faArrowDownZA, faArrowDownShortWide, faArrowDownWideShort, faInfoCircle, faExternalLink, faFilter, faTimes, faArrowRightRotate } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";
import ActionButton from "@/components/ActionButton.vue";
import { faCheckSquare } from "@fortawesome/free-regular-svg-icons";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;

let refreshInterval: number | undefined;
let sortMethod: SortOptions<GroupOperation> | undefined;
const perPage = 50;
const cookies = useCookies().cookies;
const selectedPeriod = ref("All Pending Retries");
const endpoints = ref<string[]>([]);
const messageList = useTemplateRef<IMessageList>("messageList");
const messages = ref<ExtendedFailedMessage[]>([]);
const selectedQueue = ref("empty");
const showConfirmRetry = ref(false);
const showConfirmResolve = ref(false);
const showConfirmResolveAll = ref(false);
const showCantRetryAll = ref(false);
const showRetryAllConfirm = ref(false);
const pageNumber = ref(1);
const totalCount = ref(0);
const isInitialLoad = ref(true);
const sortOptions: SortOptions<GroupOperation>[] = [
  {
    description: "Time of failure",
    iconAsc: faArrowDownShortWide,
    iconDesc: faArrowDownWideShort,
  },
  {
    description: "Message Type",
    iconAsc: faArrowDownAZ,
    iconDesc: faArrowDownZA,
  },
  {
    description: "Time of retry request",
    iconAsc: faArrowDownShortWide,
    iconDesc: faArrowDownWideShort,
  },
];
const periodOptions = ["All Pending Retries", "Retried in the last 2 Hours", "Retried in the last 1 Day", "Retried in the last 7 Days"];
const isMassTransitConnected = useIsMassTransitConnected();

watch(pageNumber, () => loadPendingRetryMessages());

async function loadEndpoints() {
  const [, data] = await useTypedFetchFromServiceControl<QueueAddress[]>("errors/queues/addresses");
  endpoints.value = data.map((endpoint) => endpoint.physical_address);
}

function clearSelectedQueue() {
  selectedQueue.value = "empty";
  loadPendingRetryMessages();
}

function loadPendingRetryMessages() {
  let startDate = new Date(0);
  const endDate = new Date();

  switch (selectedPeriod.value) {
    case "Retried in the last 2 Hours":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 2);
      break;

    case "Retried in the last 1 Day":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 24);
      break;

    case "Retried in the last 7 days":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 24 * 7);
      break;
  }

  return loadPagedPendingRetryMessages(pageNumber.value, selectedQueue.value, startDate, endDate, sortMethod?.description.replaceAll(" ", "_").toLowerCase(), sortMethod?.dir);
}

async function loadPagedPendingRetryMessages(page: number, searchPhrase: string, startDate: Date, endDate: Date, sortBy?: string, direction?: SortDirection) {
  sortBy ??= "time_of_failure";
  direction ??= SortDirection.Descending;
  if (searchPhrase === "empty") searchPhrase = "";

  try {
    const [response, data] = await useTypedFetchFromServiceControl<ExtendedFailedMessage[]>(
      `errors?status=${FailedMessageStatus.RetryIssued}&page=${page}&per_page=${perPage}&sort=${sortBy}&direction=${direction}&queueaddress=${searchPhrase}&modified=${startDate.toISOString()}...${endDate.toISOString()}`
    );
    totalCount.value = parseInt(response.headers.get("Total-Count") ?? "0");

    messages.value.forEach((previousMessage: ExtendedFailedMessage) => {
      const receivedMessage = data.find((m) => m.id === previousMessage.id);
      if (receivedMessage) {
        if (previousMessage.last_modified === receivedMessage.last_modified) {
          receivedMessage.submittedForRetrial = previousMessage.submittedForRetrial;
          receivedMessage.resolved = previousMessage.resolved;
        }

        receivedMessage.selected = previousMessage.selected;
      }
    });

    messages.value = data;
  } catch (err) {
    console.log(err);
    const result = {
      message: "error",
    };
    return result;
  }
}

function numberDisplayed() {
  return messageList.value?.numberDisplayed();
}

function isAnythingDisplayed() {
  return messageList.value?.isAnythingDisplayed();
}

function isAnythingSelected() {
  return messageList.value?.isAnythingSelected();
}

function numberSelected() {
  return messageList.value?.getSelectedMessages()?.length ?? 0;
}

async function retrySelectedMessages() {
  const selectedMessages = messageList.value?.getSelectedMessages() ?? [];

  useShowToast(TYPE.INFO, "Info", "Selected messages were submitted for retry...");
  await postToServiceControl(
    "pendingretries/retry",
    selectedMessages.map((m) => m.id)
  );

  messageList.value?.deselectAll();
  selectedMessages.forEach((m) => (m.submittedForRetrial = true));
}

async function resolveSelectedMessages() {
  const selectedMessages = messageList.value?.getSelectedMessages() ?? [];

  useShowToast(TYPE.INFO, "Info", "Selected messages were marked as resolved.");
  await patchToServiceControl("pendingretries/resolve", { uniquemessageids: selectedMessages.map((m) => m.id) });
  messageList.value?.deselectAll();
  selectedMessages.forEach((m) => (m.resolved = true));
}

async function resolveAllMessages() {
  useShowToast(TYPE.INFO, "Info", "All filtered messages were marked as resolved.");
  await patchToServiceControl("pendingretries/resolve", { from: new Date(0).toISOString(), to: new Date().toISOString() });
  messageList.value?.deselectAll();
  messageList.value?.resolveAll();
}

async function retryAllMessages() {
  let url = "pendingretries/retry";
  const data: { from: string; to: string; queueaddress?: string } = {
    from: new Date(0).toISOString(),
    to: new Date(0).toISOString(),
  };
  if (selectedQueue.value !== "empty") {
    url = "pendingretries/queues/retry";
    data.queueaddress = selectedQueue.value;
  }

  await postToServiceControl(url, data);
  messages.value.forEach((message) => {
    message.selected = false;
    message.submittedForRetrial = true;
    message.retried = false;
  });
}

function retryAllClicked() {
  if (selectedQueue.value === "empty") {
    showCantRetryAll.value = true;
  } else {
    showRetryAllConfirm.value = true;
  }
}

function sortGroups(sort: SortOptions<GroupOperation>) {
  sortMethod = sort;

  if (!isInitialLoad.value) {
    loadPendingRetryMessages();
  }
}

function periodChanged(period: string) {
  selectedPeriod.value = period;
  cookies.set("pending_retries_period", period);

  loadPendingRetryMessages();
}

onUnmounted(() => {
  if (refreshInterval != null) {
    window.clearInterval(refreshInterval);
  }
});

onMounted(() => {
  let cookiePeriod = cookies.get("pending_retries_period");
  if (!cookiePeriod) {
    cookiePeriod = periodOptions[0]; //default All Pending Retries
  }

  selectedPeriod.value = cookiePeriod;

  loadEndpoints();

  loadPendingRetryMessages();

  refreshInterval = window.setInterval(() => {
    loadPendingRetryMessages();
  }, 5000);

  isInitialLoad.value = false;
});
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <ServiceControlNotAvailable />
    <template v-if="!connectionState.unableToConnect">
      <section name="pending_retries">
        <div class="row">
          <div class="col-12">
            <div class="alert alert-info">
              <FAIcon :icon="faInfoCircle" class="icon info" /> To check if a retried message was also processed successfully, enable
              <a href="https://docs.particular.net/nservicebus/operations/auditing" target="_blank">message auditing <FAIcon :icon="faExternalLink" /></a>
            </div>
          </div>
          <div class="col-12" v-if="isMassTransitConnected">
            <div class="alert alert-info">MassTransit endpoints currently do not report when a pending retry has succeeded, and therefore any messages associated with those endpoints will need to be manually marked as resolved.</div>
          </div>
        </div>
        <div class="row">
          <div class="col-6">
            <div class="filter-input">
              <div class="input-group mb-3">
                <label class="input-group-text"><FAIcon :icon="faFilter" size="sm" class="icon" /> <span class="hidden-xs">Filter</span></label>
                <select class="form-select" id="inputGroupSelect01" onchange="this.dataset.chosen = true;" @change="loadPendingRetryMessages()" v-model="selectedQueue">
                  <option selected disabled hidden class="placeholder" value="empty">Select a queue...</option>
                  <option v-for="(endpoint, index) in endpoints" :key="index" :value="endpoint">
                    {{ endpoint }}
                  </option>
                </select>
                <span class="input-group-btn">
                  <ActionButton @click="clearSelectedQueue()" :icon="faTimes" />
                </span>
              </div>
            </div>
          </div>
          <div class="col-6">
            <div class="msg-group-menu dropdown">
              <label class="control-label">Period:</label>
              <button type="button" class="btn btn-default dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
                {{ selectedPeriod }}
                <span class="caret"></span>
              </button>
              <ul class="dropdown-menu">
                <li v-for="(period, index) in periodOptions" :key="index">
                  <a @click.prevent="periodChanged(period)">{{ period }}</a>
                </li>
              </ul>
            </div>
            <OrderBy @sort-updated="sortGroups" :hideGroupBy="true" :sortOptions="sortOptions" sortSavePrefix="pending_retries"></OrderBy>
          </div>
        </div>
        <div class="row">
          <div class="col-6 col-xs-12 toolbar-menus">
            <div class="action-btns">
              <ActionButton :icon="faArrowRightRotate" :disabled="!isAnythingSelected()" @click="showConfirmRetry = true"><span>Retry</span> ({{ numberSelected() }})</ActionButton>
              <ActionButton :icon="faCheckSquare" :disabled="!isAnythingSelected()" @click="showConfirmResolve = true"><span>Mark as resolved</span> ({{ numberSelected() }})</ActionButton>
              <ActionButton :icon="faArrowRightRotate" :disabled="!isAnythingDisplayed()" @click="retryAllClicked()"><span>Retry all</span></ActionButton>
              <ActionButton :icon="faCheckSquare" @click="showConfirmResolveAll = true"><span>Mark all as resolved</span></ActionButton>
            </div>
          </div>
        </div>
        <div class="row">
          <div class="col-12">
            <MessageList :messages="messages" ref="messageList"></MessageList>
          </div>
        </div>
        <div class="row">
          <PaginationStrip v-model="pageNumber" :total-count="totalCount" :items-per-page="perPage" />
        </div>
        <Teleport to="#modalDisplay">
          <ConfirmDialog
            v-if="showConfirmRetry === true"
            @cancel="showConfirmRetry = false"
            @confirm="
              showConfirmRetry = false;
              retrySelectedMessages();
            "
            :heading="'Are you sure you want to retry the selected messages?'"
            :body="'Ensure that the selected messages were not processed previously as this will create a duplicate message.'"
            :second-paragraph="'NOTE: If the selection includes messages to be processed via unaudited endpoints, those messages will need to be marked as resolved once the retry is manually verified'"
          ></ConfirmDialog>

          <ConfirmDialog
            v-if="showConfirmResolve === true"
            @cancel="showConfirmResolve = false"
            @confirm="
              showConfirmResolve = false;
              resolveSelectedMessages();
            "
            :heading="'Are you sure you want to mark as resolved the selected messages?'"
            :body="`If you mark these messages as resolved they will not be available for Retry. Messages should only be marked as resolved only if they belong to unaudited endpoints.`"
          ></ConfirmDialog>

          <ConfirmDialog
            v-if="showConfirmResolveAll === true"
            @cancel="showConfirmResolveAll = false"
            @confirm="
              showConfirmResolveAll = false;
              resolveAllMessages();
            "
            :heading="'Are you sure you want to resolve all messages?'"
            :body="`Are you sure you want to mark all ${numberDisplayed()} messages as resolved? If you do they will not be available for Retry.`"
          ></ConfirmDialog>

          <ConfirmDialog
            v-if="showCantRetryAll === true"
            @cancel="showCantRetryAll = false"
            @confirm="showCantRetryAll = false"
            :hide-cancel="true"
            :heading="'Select a queue first'"
            :body="'Bulk retry of messages can only be done for one queue at the time to avoid producing unwanted message duplicates.'"
          ></ConfirmDialog>

          <ConfirmDialog
            v-if="showRetryAllConfirm === true"
            @cancel="showRetryAllConfirm = false"
            @confirm="
              showRetryAllConfirm = false;
              retryAllMessages();
            "
            :heading="'Confirm retry of all messages?'"
            :body="'Are you sure you want to retry all previously retried messages? If the selected messages were processed in the meanwhile, then duplicate messages will be produced.'"
          ></ConfirmDialog>
        </Teleport>
      </section>
    </template>
  </template>
</template>

<style scoped>
@import "../list.css";

.input-group-text {
  margin-bottom: 0;
}

.input-group-text > span {
  font-size: 14px;
  color: #555;
}

.input-group > select {
  font-size: 14px;
  color: #777777;
}

.input-group > select[data-chosen="true"] {
  color: #212529;
}

.input-group > select:hover {
  box-shadow: 0 0 10px 100px var(--bs-btn-hover-bg) inset;
  color: #212529;
}

.input-group-btn:last-child > .btn {
  border-top-left-radius: 0;
  border-bottom-left-radius: 0;
}

.action-btns > .btn {
  margin-right: 5px;
}

.dropdown-toggle.btn-default:hover {
  background: none;
  border: none;
  color: var(--sp-blue);
}

.icon {
  color: var(--reduced-emphasis);
  padding-right: 6px;
}

.icon.info {
  color: #31708f;
}
</style>
