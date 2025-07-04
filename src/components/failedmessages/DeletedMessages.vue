<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from "vue";
import { licenseStatus } from "../../composables/serviceLicense";
import { connectionState } from "../../composables/serviceServiceControl";
import { usePatchToServiceControl, useTypedFetchFromServiceControl } from "../../composables/serviceServiceControlUrls";
import { useShowToast } from "../../composables/toast";
import { onBeforeRouteLeave, useRoute } from "vue-router";
import { useCookies } from "vue3-cookies";
import LicenseExpired from "../../components/LicenseExpired.vue";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import MessageList, { IMessageList } from "./MessageList.vue";
import ConfirmDialog from "../ConfirmDialog.vue";
import PaginationStrip from "../../components/PaginationStrip.vue";
import moment from "moment";
import { ExtendedFailedMessage } from "@/resources/FailedMessage";
import { TYPE } from "vue-toastification";
import FailureGroup from "@/resources/FailureGroup";
import { useConfiguration } from "@/composables/configuration";
import FAIcon from "@/components/FAIcon.vue";
import { faArrowRotateRight } from "@fortawesome/free-solid-svg-icons";

let pollingFaster = false;
let refreshInterval: number | undefined;
const perPage = 50;

const route = useRoute();
const groupId = ref<string>(route.params.groupId as string);
const groupName = ref("");
const pageNumber = ref(1);
const totalCount = ref(0);
const cookies = useCookies().cookies;
const periodOptions = ["All Deleted", "Deleted in the last 2 Hours", "Deleted in the last 1 Day", "Deleted in the last 7 days"] as const;
type PeriodOption = (typeof periodOptions)[number];
const selectedPeriod = ref<PeriodOption>("Deleted in the last 7 days");
const showConfirmRestore = ref(false);
const messageList = ref<IMessageList | undefined>();
const messages = ref<ExtendedFailedMessage[]>([]);

watch(pageNumber, () => loadMessages());
const configuration = useConfiguration();

function loadMessages() {
  let startDate = new Date(0);
  const endDate = new Date();

  switch (selectedPeriod.value) {
    case "All Deleted":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 24 * 365);
      break;
    case "Deleted in the last 2 Hours":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 2);
      break;
    case "Deleted in the last 1 Day":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 24);
      break;
    case "Deleted in the last 7 days":
      startDate = new Date();
      startDate.setHours(startDate.getHours() - 24 * 7);
      break;
  }
  return loadPagedMessages(groupId.value, pageNumber.value, "", "", startDate.toISOString(), endDate.toISOString());
}

async function loadGroupDetails(groupId: string) {
  const [, data] = await useTypedFetchFromServiceControl<FailureGroup>(`archive/groups/id/${groupId}`);
  groupName.value = data.title;
}

function loadPagedMessages(groupId?: string, page: number = 1, sortBy: string = "modified", direction: string = "desc", startDate: string = new Date(0).toISOString(), endDate: string = new Date().toISOString()) {
  const dateRange = startDate + "..." + endDate;
  let loadGroupDetailsPromise;
  if (groupId && !groupName.value) {
    loadGroupDetailsPromise = loadGroupDetails(groupId);
  }

  async function loadDelMessages() {
    try {
      const [response, data] = await useTypedFetchFromServiceControl<ExtendedFailedMessage[]>(
        `${groupId ? `recoverability/groups/${groupId}/` : ""}errors?status=archived&page=${page}&per_page=${perPage}&sort=${sortBy}&direction=${direction}&modified=${dateRange}`
      );

      totalCount.value = parseInt(response.headers.get("Total-Count") ?? "0");

      if (messages.value.length && data.length) {
        // merge the previously selected messages into the new list so we can replace them
        messages.value.forEach((previousMessage) => {
          const receivedMessage = data.find((m) => m.id === previousMessage.id);
          if (receivedMessage) {
            if (previousMessage.last_modified === receivedMessage.last_modified) {
              receivedMessage.retryInProgress = previousMessage.retryInProgress;
              receivedMessage.deleteInProgress = previousMessage.deleteInProgress;
            }

            receivedMessage.selected = previousMessage.selected;
          }
        });
      }
      messages.value = updateMessagesScheduledDeletionDate(data);
    } catch (err) {
      console.log(err);
      const result = {
        message: "error",
      };
      return result;
    }
  }

  const loadDelMessagesPromise = loadDelMessages();

  if (loadGroupDetailsPromise) {
    return Promise.all([loadGroupDetailsPromise, loadDelMessagesPromise]);
  }

  return loadDelMessagesPromise;
}

function updateMessagesScheduledDeletionDate(messages: ExtendedFailedMessage[]) {
  //check deletion time
  messages.forEach((message) => {
    message.error_retention_period = moment.duration(configuration.value?.data_retention.error_retention_period).asHours();
    const countdown = moment(message.last_modified).add(message.error_retention_period, "hours");
    message.delete_soon = countdown < moment();
    message.deleted_in = countdown.format();
  });
  return messages;
}

function numberSelected() {
  return messageList.value?.getSelectedMessages()?.length ?? 0;
}

function selectAll() {
  messageList.value?.selectAll();
}

function deselectAll() {
  messageList.value?.deselectAll();
}

function isAnythingSelected() {
  return messageList.value?.isAnythingSelected();
}

async function restoreSelectedMessages() {
  changeRefreshInterval(1000);
  const selectedMessages = messageList.value?.getSelectedMessages() ?? [];
  selectedMessages.forEach((m) => (m.restoreInProgress = true));
  useShowToast(TYPE.INFO, "Info", `restoring ${selectedMessages.length} messages...`);

  await usePatchToServiceControl(
    "errors/unarchive",
    selectedMessages.map((m) => m.id)
  );
  messageList.value?.deselectAll();
}

function periodChanged(period: PeriodOption) {
  selectedPeriod.value = period;
  cookies.set("all_deleted_messages_period", period);

  loadMessages();
}

function isRestoreInProgress() {
  return messages.value.some((message) => message.restoreInProgress);
}

function changeRefreshInterval(milliseconds: number) {
  if (refreshInterval != null) {
    window.clearInterval(refreshInterval);
  }

  refreshInterval = window.setInterval(() => {
    // If we're currently polling at 5 seconds and there is a restore in progress, then change the polling interval to poll every 1 second
    if (!pollingFaster && isRestoreInProgress()) {
      changeRefreshInterval(1000);
      pollingFaster = true;
    } else if (pollingFaster && !isRestoreInProgress()) {
      // if we're currently polling every 1 second but all restores are done, change polling frequency back to every 5 seconds
      changeRefreshInterval(5000);
      pollingFaster = false;
    }

    loadMessages();
  }, milliseconds);
}

onBeforeRouteLeave(() => {
  groupId.value = "";
  groupName.value = "";
});

onUnmounted(() => {
  if (refreshInterval != null) {
    window.clearInterval(refreshInterval);
  }
});

onMounted(() => {
  let cookiePeriod = cookies.get("all_deleted_messages_period") as PeriodOption;
  if (!cookiePeriod) {
    cookiePeriod = periodOptions[periodOptions.length - 1]; //default is last 7 days
  }
  selectedPeriod.value = cookiePeriod;
  loadMessages();

  changeRefreshInterval(5000);
});
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <ServiceControlNotAvailable />
    <template v-if="!connectionState.unableToConnect">
      <section name="message_groups">
        <div class="row" v-if="groupName && messages.length > 0">
          <div class="col-sm-12">
            <h1 v-if="groupName" class="active break group-title">
              {{ groupName }}
            </h1>
            <h3 class="active group-title group-message-count">{{ totalCount }} messages in group</h3>
          </div>
        </div>
        <div class="row">
          <div class="col-9">
            <div class="btn-toolbar">
              <button type="button" class="btn btn-default select-all" @click="selectAll" v-if="!isAnythingSelected()">Select all</button>
              <button type="button" class="btn btn-default select-all" @click="deselectAll" v-if="isAnythingSelected()">Clear selection</button>
              <button type="button" class="btn btn-default" @click="showConfirmRestore = true" :disabled="!isAnythingSelected()"><FAIcon :icon="faArrowRotateRight" class="icon" /> Restore {{ numberSelected() }} selected</button>
            </div>
          </div>
          <div class="col-3">
            <div class="msg-group-menu dropdown">
              <label class="control-label">Show:</label>
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
          </div>
        </div>
        <div class="row">
          <div class="col-12">
            <MessageList :messages="messages" ref="messageList"></MessageList>
          </div>
        </div>
        <div class="row" v-if="messages.length > 0">
          <PaginationStrip v-model="pageNumber" :total-count="totalCount" :items-per-page="perPage" />
        </div>
        <Teleport to="#modalDisplay">
          <ConfirmDialog
            v-if="showConfirmRestore"
            @cancel="showConfirmRestore = false"
            @confirm="
              showConfirmRestore = false;
              restoreSelectedMessages();
            "
            :heading="'Are you sure you want to restore the selected messages?'"
            :body="'Restored messages will be moved back to the list of failed messages.'"
          ></ConfirmDialog>
        </Teleport>
      </section>
    </template>
  </template>
</template>

<style scoped>
.dropdown > button:hover {
  background: none;
  border: none;
  color: var(--sp-blue);
  text-decoration: underline;
}

.icon {
  color: var(--reduced-emphasis);
}
</style>
