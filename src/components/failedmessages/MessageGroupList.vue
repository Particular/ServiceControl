<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useShowToast } from "../../composables/toast";
import { isError, useAcknowledgeArchiveGroup, useArchiveExceptionGroup, useDeleteNote, useEditOrCreateNote, useGetExceptionGroups, useRetryExceptionGroup } from "../../composables/serviceMessageGroup";
import NoData from "../NoData.vue";
import TimeSince from "../TimeSince.vue";
import FailedMessageGroupNoteEdit from "./FailedMessageGroupNoteEdit.vue";
import ConfirmDialog from "../ConfirmDialog.vue";
import routeLinks from "@/router/routeLinks";
import { TYPE } from "vue-toastification";
import GroupOperation from "@/resources/GroupOperation";
import FAIcon from "@/components/FAIcon.vue";
import { faArrowRotateRight, faEnvelope, faEraser, faExclamationTriangle, faPencil, faStickyNote, faTrash } from "@fortawesome/free-solid-svg-icons";
import { faClock } from "@fortawesome/free-regular-svg-icons";
import ProgressMessage from "./ProgressMessage.vue";
import ActionButton from "@/components/ActionButton.vue";

interface WorkflowState {
  status: string;
  total?: number;
  failed?: boolean;
  message?: string;
}

interface ExtendedGroupOperation extends GroupOperation {
  index: number;
  hover2?: boolean;
  hover3?: boolean;
  workflow_state: WorkflowState;
  operation_remaining_count?: number;
  operation_start_time?: string;
}

const emit = defineEmits<{
  InitialLoadComplete: [];
  ExceptionGroupCountUpdated: [number];
}>();

let pollingFaster = false;
let refreshInterval: number | undefined = undefined;
const props = defineProps<{
  sortFunction: (a: GroupOperation, b: GroupOperation) => number;
}>();
const router = useRouter();
let groupsWithNotesAdded: {
  groupId: string;
  comment: string;
  alreadySaved?: boolean;
}[] = [];
let savedGroupBy: string | undefined = undefined;

const selectedGroup = ref<ExtendedGroupOperation>();

const exceptionGroups = ref<ExtendedGroupOperation[]>([]);
const loadingData = ref(true);
const initialLoadComplete = ref(false);
const showDeleteNoteModal = ref(false);
const showEditNoteModal = ref(false);
const showDeleteGroupModal = ref(false);
const showRetryGroupModal = ref(false);
const noteSaveSuccessful = ref<boolean | null>(null);
const groupDeleteSuccessful = ref<boolean | null>(null);
const groupRetrySuccessful = ref<boolean | null>(null);

async function getExceptionGroups(classifier?: string) {
  const result = await useGetExceptionGroups(classifier);
  if (props.sortFunction) {
    result.sort(props.sortFunction);
  }

  groupsWithNotesAdded.forEach((note) => {
    const groupFromSC = result.find((group) => {
      return group.id === note.groupId;
    });
    if (groupFromSC && !groupFromSC.comment) {
      groupFromSC.comment = note.comment;
    } else {
      note.alreadySaved = true;
    }
  });

  // need a map in some ui state for controlling animations
  exceptionGroups.value = result.map(initializeGroupState);

  groupsWithNotesAdded = groupsWithNotesAdded.filter((note) => !note.alreadySaved);
}

function initializeGroupState(group: GroupOperation, index: number): ExtendedGroupOperation {
  let operationStatus = (group.operation_status ? group.operation_status.toLowerCase() : null) || "none";
  if (operationStatus === "preparing" && group.operation_progress === 1) {
    operationStatus = "queued";
  }
  return {
    ...group,
    workflow_state: createWorkflowState(operationStatus, group.operation_progress, group.operation_failed),
    index,
  };
}

async function loadFailedMessageGroups(groupBy?: string) {
  loadingData.value = true;

  if (groupBy) {
    savedGroupBy = groupBy;
  }

  await getExceptionGroups(savedGroupBy);
  loadingData.value = false;
  initialLoadComplete.value = true;

  emit("InitialLoadComplete");
}

//delete comment note
function deleteNote(group: ExtendedGroupOperation) {
  noteSaveSuccessful.value = null;
  selectedGroup.value = group;
  showDeleteNoteModal.value = true;
}

async function saveDeleteNote(group?: GroupOperation, hideToastMessage?: boolean) {
  showDeleteNoteModal.value = false;

  if (group) {
    const result = await useDeleteNote(group.id);
    if (isError(result)) {
      noteSaveSuccessful.value = false;
      if (!hideToastMessage) {
        useShowToast(TYPE.ERROR, "Error", `Failed to delete a Note: ${result.message}`);
      }
    } else {
      noteSaveSuccessful.value = true;
      if (!hideToastMessage) {
        useShowToast(TYPE.INFO, "Info", "Note deleted successfully");
      }

      loadFailedMessageGroups(); //reload the groups
    }
  }
}

// create comment note
async function saveNote(group: GroupOperation, comment: string) {
  noteSaveSuccessful.value = null;
  showEditNoteModal.value = false;

  groupsWithNotesAdded.push({ groupId: group.id, comment: comment });

  const result = await useEditOrCreateNote(group.id, comment);
  if (isError(result)) {
    noteSaveSuccessful.value = false;
    useShowToast(TYPE.ERROR, "Error", `Failed to update Note: ${result.message}`);
  } else {
    noteSaveSuccessful.value = true;
    useShowToast(TYPE.INFO, "Info", "Note updated successfully");
    loadFailedMessageGroups(); //reload the groups
  }
}

function saveCreatedNote(group: ExtendedGroupOperation, comment: string) {
  saveNote(group, comment);
}

function saveEditedNote(group: ExtendedGroupOperation, comment: string) {
  saveNote(group, comment);
}

function editNote(group: ExtendedGroupOperation) {
  noteSaveSuccessful.value = null;
  selectedGroup.value = group;
  showEditNoteModal.value = true;
}

//delete a group
const statusesForArchiveOperation = ["archivestarted", "archiveprogressing", "archivefinalizing", "archivecompleted"];
function deleteGroup(group: ExtendedGroupOperation) {
  groupDeleteSuccessful.value = null;
  selectedGroup.value = group;
  showDeleteGroupModal.value = true;
}

async function saveDeleteGroup(group?: ExtendedGroupOperation) {
  showDeleteGroupModal.value = false;
  if (group) {
    group.workflow_state = { status: "archivestarted", message: "Delete request initiated..." };

    // We've started a delete, so increase the polling frequency
    changeRefreshInterval(1000);

    saveDeleteNote(group, true); // delete comment note when group is archived
    const result = await useArchiveExceptionGroup(group.id);
    if (isError(result)) {
      groupDeleteSuccessful.value = false;
      useShowToast(TYPE.ERROR, "Error", `Failed to delete the group: ${result.message}`);
    } else {
      groupDeleteSuccessful.value = true;
      useShowToast(TYPE.INFO, "info", "Group delete started...");
    }
  }
}

//create workflow state
function createWorkflowState(optionalStatus?: string, optionalTotal?: number, optionalFailed?: boolean) {
  if (optionalTotal && optionalTotal <= 1) {
    optionalTotal = optionalTotal * 100;
  }
  return {
    status: optionalStatus || "working",
    total: Math.round(optionalTotal || 0),
    failed: optionalFailed || false,
  };
}

//getClasses
const getClasses = function (stepStatus: string, currentStatus: string, statusArray: string[]) {
  const indexOfStep = statusArray.indexOf(stepStatus);
  const indexOfCurrent = statusArray.indexOf(currentStatus);
  return {
    "left-to-do": indexOfStep > indexOfCurrent,
    active: indexOfStep === indexOfCurrent,
    completed: indexOfStep < indexOfCurrent,
  };
};

//getClassesForArchiveOperation
function getClassesForArchiveOperation(stepStatus: string, currentStatus: string) {
  return getClasses(stepStatus, currentStatus, statusesForArchiveOperation);
}

//Retry operation
function retryGroup(group: ExtendedGroupOperation) {
  groupRetrySuccessful.value = null;
  selectedGroup.value = group;
  showRetryGroupModal.value = true;
}

async function saveRetryGroup(group?: ExtendedGroupOperation) {
  showRetryGroupModal.value = false;
  if (group) {
    group.workflow_state = { status: "waiting", message: "Retry Group Request Enqueued..." };

    // We've started a retry, so increase the polling frequency
    changeRefreshInterval(1000);

    saveDeleteNote(group, true);
    const result = await useRetryExceptionGroup(group.id);
    if (isError(result)) {
      groupRetrySuccessful.value = false;
      useShowToast(TYPE.ERROR, "Error", `Failed to retry the group: ${result.message}`);
    } else groupRetrySuccessful.value = true;
  }
}

const statusesForRetryOperation = ["waiting", "preparing", "queued", "forwarding"];
function getClassesForRetryOperation(stepStatus: string, currentStatus: string) {
  if (currentStatus === "queued") {
    currentStatus = "forwarding";
  }
  return getClasses(stepStatus, currentStatus, statusesForRetryOperation);
}

const acknowledgeGroup = async function (group: GroupOperation) {
  const result = await useAcknowledgeArchiveGroup(group.id);
  if (isError(result)) useShowToast(TYPE.ERROR, "Error", `Acknowledging Group Failed: ${result.message}`);
  else {
    if (group.operation_status === "ArchiveCompleted") {
      useShowToast(TYPE.INFO, "Info", "Group deleted successfully");
    } else {
      useShowToast(TYPE.INFO, "Info", "Group retried successfully");
    }
    loadFailedMessageGroups(); //reload the groups
  }
};

function isBeingArchived(status: string) {
  return status === "archivestarted" || status === "archiveprogressing" || status === "archivefinalizing" || status === "archivecompleted";
}

function isBeingRetried(group: ExtendedGroupOperation) {
  return group.workflow_state.status !== "none" && (group.workflow_state.status !== "completed" || group.need_user_acknowledgement === true) && !isBeingArchived(group.workflow_state.status);
}

function clearInMemoryData() {
  groupsWithNotesAdded = [];
}

function navigateToGroup(groupId: string) {
  router.push(routeLinks.failedMessage.group.link(groupId));
}

function isRetryOrDeleteOperationInProgress() {
  return exceptionGroups.value.some((group) => group.operation_status !== "None" && group.operation_status !== "ArchiveCompleted" && group.operation_status !== "Completed");
}

function changeRefreshInterval(milliseconds: number) {
  if (refreshInterval) {
    clearInterval(refreshInterval);
  }

  refreshInterval = window.setInterval(() => {
    // If we're currently polling at 5 seconds and there is a retry or delete in progress, then change the polling interval to poll every 1 second
    if (!pollingFaster && isRetryOrDeleteOperationInProgress()) {
      changeRefreshInterval(1000);
      pollingFaster = true;
    } else if (pollingFaster && !isRetryOrDeleteOperationInProgress()) {
      // if we're currently polling every 1 second but all retries or deletes are done, change polling frequency back to every 5 seconds
      changeRefreshInterval(5000);
      pollingFaster = false;
    }

    loadFailedMessageGroups();
  }, milliseconds);
}

onUnmounted(() => {
  if (refreshInterval) {
    clearInterval(refreshInterval);
  }
});

onMounted(() => {
  // Initialize the poll interval to 5 seconds
  changeRefreshInterval(5000);
});

export interface IMessageGroupList {
  loadFailedMessageGroups(groupBy?: string): Promise<void>;
  clearInMemoryData(): void;
}

defineExpose<IMessageGroupList>({
  loadFailedMessageGroups,
  clearInMemoryData,
});
</script>

<template>
  <div class="messagegrouplist">
    <div class="row">
      <div class="col-sm-12">
        <no-data v-if="exceptionGroups.length === 0 && !loadingData" title="message groups" message="There are currently no grouped message failures"></no-data>
      </div>
    </div>

    <div class="row">
      <div class="col-sm-12 no-mobile-side-padding">
        <div v-if="exceptionGroups.length > 0">
          <div
            :class="`row box box-group wf-${group.workflow_state.status} failed-message-group repeat-modify`"
            v-for="(group, index) in exceptionGroups"
            :key="index"
            :disabled="group.count == 0"
            @mouseenter="group.hover2 = true"
            @mouseleave="group.hover2 = false"
            @click="navigateToGroup(group.id)"
          >
            <div class="col-sm-12 no-mobile-side-padding">
              <div class="row">
                <div class="col-sm-12 no-side-padding">
                  <div class="row box-header">
                    <div class="col-sm-12 no-side-padding">
                      <p class="lead break" :class="{ 'msg-type-hover': group.hover2, 'msg-type-hover-off': group.hover3 }">{{ group.title }}</p>
                      <p class="metadata" v-if="!isBeingRetried(group) && !isBeingArchived(group.workflow_state.status)">
                        <span class="metadata">
                          <FAIcon class="icon" :icon="faEnvelope" />
                          {{ group.count }} message<span v-if="group.count > 1">s</span>
                          <span v-if="group.operation_remaining_count"> (currently retrying {{ group.operation_remaining_count }} </span>
                        </span>

                        <span class="metadata">
                          <FAIcon class="icon" :icon="faClock" />
                          First failed:
                          <time-since :date-utc="group.first"></time-since>
                        </span>

                        <span class="metadata">
                          <FAIcon class="icon" :icon="faClock" />
                          Last failed:
                          <time-since :date-utc="group.last"></time-since>
                        </span>

                        <span class="metadata">
                          <FAIcon class="icon" :icon="faArrowRotateRight" />
                          Last retried:
                          <time-since :date-utc="group.operation_completion_time"></time-since>
                        </span>
                      </p>
                    </div>
                  </div>

                  <div class="row" v-if="!isBeingRetried(group) && !isBeingArchived(group.workflow_state.status)">
                    <div class="col-sm-12 no-side-padding">
                      <div class="note" v-if="group.comment">
                        <span> <strong>NOTE:</strong> {{ group.comment }} </span>
                      </div>
                    </div>
                  </div>
                  <div class="row" v-if="!isBeingRetried(group) && !isBeingArchived(group.workflow_state.status)">
                    <div class="col-sm-12 no-side-padding">
                      <ActionButton
                        variant="link"
                        size="sm"
                        :icon="faArrowRotateRight"
                        :disabled="group.count == 0 || isBeingRetried(group)"
                        @mouseenter="group.hover3 = true"
                        @mouseleave="group.hover3 = false"
                        v-if="exceptionGroups.length > 0"
                        @click.stop="retryGroup(group)"
                      >
                        <span>Request retry</span>
                      </ActionButton>

                      <ActionButton
                        variant="link"
                        size="sm"
                        :icon="faTrash"
                        :disabled="group.count == 0 || isBeingRetried(group)"
                        @mouseenter="group.hover3 = true"
                        @mouseleave="group.hover3 = false"
                        v-if="exceptionGroups.length > 0"
                        @click.stop="deleteGroup(group)"
                      >
                        <span>Delete group</span>
                      </ActionButton>
                      <ActionButton variant="link" size="sm" :icon="faStickyNote" v-if="!group.comment" @click.stop="editNote(group)">Add note</ActionButton>
                      <ActionButton variant="link" size="sm" :icon="faPencil" v-if="group.comment" @click.stop="editNote(group)">Edit note</ActionButton>
                      <ActionButton variant="link" size="sm" :icon="faEraser" v-if="group.comment" @click.stop="deleteNote(group)">Remove note</ActionButton>
                    </div>
                  </div>

                  <!--isBeingRetried-->
                  <div class="row" v-if="isBeingRetried(group)">
                    <div class="col-sm-12 no-side-padding">
                      <div class="panel panel-default panel-retry">
                        <div class="panel-body">
                          <ul class="retry-request-progress">
                            <ProgressMessage v-if="group.workflow_state.status !== 'completed'" v-bind="getClassesForRetryOperation('waiting', group.workflow_state.status)">Initialize retry request...</ProgressMessage>
                            <ProgressMessage v-if="group.workflow_state.status !== 'completed'" v-bind="getClassesForRetryOperation('preparing', group.workflow_state.status)" class="inset">
                              <div class="row">
                                <div class="col-xs-12 col-sm-4 col-md-3 no-side-padding">
                                  <span class="bulk-retry-progress-status">Prepare messages...</span>
                                </div>

                                <div class="col-xs-12 col-sm-6">
                                  <div class="progress bulk-retry-progress" v-if="group.workflow_state.status === 'preparing'">
                                    <div
                                      class="progress-bar progress-bar-striped active"
                                      role="progressbar"
                                      aria-valuenow="{{group.workflow_state.total}}"
                                      aria-valuemin="0"
                                      aria-valuemax="100"
                                      :style="{ 'min-width': '2em', width: group.workflow_state.total + '%' }"
                                    >
                                      {{ group.workflow_state.total }}%
                                    </div>
                                  </div>
                                </div>
                              </div>
                            </ProgressMessage>
                            <ProgressMessage v-if="group.workflow_state.status !== 'completed'" v-bind="getClassesForRetryOperation('forwarding', group.workflow_state.status)" class="inset">
                              <div class="row">
                                <div class="col-xs-9 col-sm-4 col-md-3 no-side-padding">
                                  <div class="bulk-retry-progress-status">Send messages to retry...</div>
                                </div>
                                <div class="col-xs-3 col-sm-3 retry-op-queued" v-if="group.workflow_state.status === 'queued'">(Queued)</div>
                                <div class="col-xs-12 col-sm-6">
                                  <div class="progress bulk-retry-progress" v-if="group.workflow_state.status === 'forwarding'">
                                    <div
                                      class="progress-bar progress-bar-striped active"
                                      role="progressbar"
                                      aria-valuenow="{{group.workflow_state.total}}"
                                      aria-valuemin="0"
                                      aria-valuemax="100"
                                      :style="{ 'min-width': '2em', width: group.workflow_state.total + '%' }"
                                    >
                                      {{ group.workflow_state.total }}%
                                    </div>
                                  </div>
                                </div>
                              </div>
                            </ProgressMessage>
                            <ProgressMessage v-if="group.workflow_state.status === 'completed'" v-bind="{ 'left-to-do': false, active: false, completed: false }">
                              <div class="retry-completed bulk-retry-progress-status">Retry request completed</div>
                              <button type="button" class="btn btn-default btn-primary btn-xs btn-retry-dismiss" v-if="group.need_user_acknowledgement == true" @click.stop="acknowledgeGroup(group)">Dismiss</button>
                              <div class="danger sc-restart-warning" v-if="group.workflow_state.failed">
                                <FAIcon :icon="faExclamationTriangle" class="icon danger" /> <strong>WARNING: </strong>Not all messages will be retried because ServiceControl had to restart. You need to request retrying the remaining messages.
                              </div>
                            </ProgressMessage>
                          </ul>

                          <div class="op-metadata">
                            <span class="metadata"> <FAIcon :icon="faEnvelope" class="icon" /> {{ group.workflow_state.status === "completed" ? "Messages sent:" : "Messages to send:" }} {{ group.operation_remaining_count || group.count }} </span>
                            <span class="metadata"><FAIcon :icon="faClock" class="icon" /> Retry request started: <time-since :date-utc="group.operation_start_time"></time-since></span>
                            <span class="metadata" v-if="group.workflow_state.status === 'completed'"><FAIcon :icon="faClock" class="icon" /> Retry request completed: <time-since :date-utc="group.operation_completion_time"></time-since></span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!--isBeingArchived-->
                  <div class="row" v-if="isBeingArchived(group.workflow_state.status)">
                    <div class="col-sm-12 no-side-padding">
                      <div class="panel panel-default panel-retry">
                        <div class="panel-body">
                          <ul class="retry-request-progress">
                            <li v-if="group.workflow_state.status !== 'archivecompleted'" v-bind:class="getClassesForArchiveOperation('archivestarted', group.workflow_state.status)">
                              <div class="bulk-retry-progress-status">Initialize delete request...</div>
                            </li>
                            <li v-if="group.workflow_state.status !== 'archivecompleted'" v-bind:class="getClassesForArchiveOperation('archiveprogressing', group.workflow_state.status)">
                              <div class="row">
                                <div class="col-xs-12 col-sm-4 col-md-3 no-side-padding">
                                  <div class="bulk-retry-progress-status">Delete request in progress...</div>
                                </div>
                                <div class="col-xs-12 col-sm-6">
                                  <div class="progress bulk-retry-progress" v-if="group.workflow_state.status === 'archiveprogressing'">
                                    <div
                                      class="progress-bar progress-bar-striped active"
                                      role="progressbar"
                                      aria-valuenow="{{group.workflow_state.total}}"
                                      aria-valuemin="0"
                                      aria-valuemax="100"
                                      :style="{ 'min-width': '2em', width: group.workflow_state.total + '%' }"
                                    >
                                      {{ group.workflow_state.total }} %
                                    </div>
                                  </div>
                                </div>
                              </div>
                            </li>
                            <li v-if="group.workflow_state.status !== 'archivecompleted'" v-bind:class="getClassesForArchiveOperation('archivefinalizing', group.workflow_state.status)">
                              <div class="row">
                                <div class="col-xs-12 col-sm-4 col-md-3 no-side-padding">
                                  <div class="bulk-retry-progress-status">Cleaning up...</div>
                                </div>
                              </div>
                            </li>
                            <li v-if="group.workflow_state.status === 'archivecompleted'">
                              <div class="retry-completed bulk-retry-progress-status">Delete request completed</div>
                              <button type="button" class="btn btn-default btn-primary btn-xs btn-retry-dismiss" v-if="group.need_user_acknowledgement == true" @click.stop="acknowledgeGroup(group)">Dismiss</button>
                            </li>
                          </ul>

                          <div class="op-metadata">
                            <span class="metadata">
                              <FAIcon :icon="faClock" class="icon" />
                              Delete request started: <time-since :date-utc="group.operation_start_time"></time-since>
                            </span>
                            <span class="metadata">
                              <FAIcon :icon="faEnvelope" class="icon" />
                              Messages left to delete:
                              {{ group.operation_remaining_count || 0 }}
                            </span>
                            <span class="metadata">
                              <FAIcon :icon="faEnvelope" class="icon" />
                              Messages deleted:
                              {{ group.operation_messages_completed_count || 0 }}
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
  <!--modal display - create new/edit comment note-->
  <Teleport to="#modalDisplay">
    <FailedMessageGroupNoteEdit
      v-if="showEditNoteModal === true && selectedGroup"
      :groupid="selectedGroup.id"
      :comment="selectedGroup.comment"
      @cancelEditNote="showEditNoteModal = false"
      @createNoteConfirmed="saveCreatedNote(selectedGroup, $event.comment)"
      @editNoteConfirmed="saveEditedNote(selectedGroup, $event.comment)"
    ></FailedMessageGroupNoteEdit>
  </Teleport>

  <!--modal display - delete group-->
  <Teleport to="#modalDisplay">
    <ConfirmDialog
      v-if="showDeleteGroupModal"
      @cancel="showDeleteGroupModal = false"
      @confirm="
        showDeleteGroupModal = false;
        saveDeleteGroup(selectedGroup);
      "
      :heading="'Are you sure you want to delete this group?'"
      :body="'Messages that are deleted will be cleaned up according to the ServiceControl retention policy, and aren\'t available for retrying unless they\'re restored.'"
    ></ConfirmDialog>

    <ConfirmDialog
      v-if="showRetryGroupModal"
      @cancel="showRetryGroupModal = false"
      @confirm="
        showRetryGroupModal = false;
        saveRetryGroup(selectedGroup);
      "
      :heading="'Are you sure you want to retry this group?'"
      :body="`Retrying a whole group can take some time and put extra load on your system. Are you sure you want to retry this group of ${selectedGroup?.count ?? 0} messages?`"
    ></ConfirmDialog>

    <ConfirmDialog
      v-if="showDeleteNoteModal"
      @cancel="showDeleteNoteModal = false"
      @confirm="
        showDeleteNoteModal = false;
        saveDeleteNote(selectedGroup);
      "
      :heading="'Are you sure you want to delete this note?'"
      :body="`Deleted note will not be available.`"
    ></ConfirmDialog>
  </Teleport>
</template>

<style scoped>
@import "../list.css";
@import "./failedmessages.css";

div.col-xs-3.col-sm-3.retry-op-queued {
  color: #b0b5b5 !important;
}

div.progress-bar.progress-bar-striped.active {
  color: #fff !important;
}

.fake-link i {
  padding-right: 0.2em;
}

.toolbar-menus > .msg-group-menu {
  margin: 0;
}

.inset {
  padding-left: 13px;
}
</style>
