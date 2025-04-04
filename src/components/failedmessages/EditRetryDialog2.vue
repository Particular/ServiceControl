<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRetryEditedMessage } from "@/composables/serviceFailedMessage";
import MessageHeader from "./EditMessageHeader.vue";
import type Header from "@/resources/Header";
import parseContentType from "@/composables/contentTypeParser";
import { CodeLanguage } from "@/components/codeEditorTypes";
import CodeEditor from "@/components/CodeEditor.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore";
import { storeToRefs } from "pinia";
import LoadingSpinner from "@/components/LoadingSpinner.vue";

interface HeaderWithEditing extends Header {
  isLocked: boolean;
  isSensitive: boolean;
  isMarkedAsRemoved: boolean;
  isChanged: boolean;
}

const emit = defineEmits<{
  cancel: [];
  confirm: [];
}>();

interface LocalMessageState {
  isBodyChanged: boolean;
  isBodyEmpty: boolean;
  isContentTypeSupported: boolean;
  language?: CodeLanguage;
  bodyContentType: string | undefined;
  bodyUnavailable: boolean;
  isEvent: boolean;
  retried: boolean;
  headers: HeaderWithEditing[];
  messageBody: string;
}

const panel = ref(0);
const localMessage = ref<LocalMessageState>({
  isBodyChanged: false,
  isBodyEmpty: false,
  isContentTypeSupported: false,
  bodyContentType: undefined,
  bodyUnavailable: true,
  isEvent: false,
  retried: false,
  headers: [],
  messageBody: "",
});
let origMessageBody: string;

const showEditAndRetryConfirmation = ref(false);
const showCancelConfirmation = ref(false);
const showEditRetryGenericError = ref(false);
const store = useMessageViewStore();
const { state, headers, body, edit_and_retry_config } = storeToRefs(store);
const id = computed(() => state.value.data.id ?? "");
const messageBody = computed(() => body.value.data.value);

watch(messageBody, (newValue) => {
  if (newValue !== origMessageBody) {
    localMessage.value.isBodyChanged = true;
  }
  localMessage.value.isBodyEmpty = newValue === "";
});

function close() {
  emit("cancel");
}

function confirmEditAndRetry() {
  showEditAndRetryConfirmation.value = true;
}

function confirmCancel() {
  if (localMessage.value.isBodyChanged) {
    showCancelConfirmation.value = true;
    return;
  }

  if (localMessage.value.headers.some((header: HeaderWithEditing) => header.isChanged)) {
    showCancelConfirmation.value = true;
    return;
  }

  close();
}

function resetBodyChanges() {
  localMessage.value.messageBody = origMessageBody;
  localMessage.value.isBodyChanged = false;
}

function findHeadersByKey(key: string) {
  return localMessage.value.headers.find((header: HeaderWithEditing) => header.key === key);
}

function getContentType() {
  const header = findHeadersByKey("NServiceBus.ContentType");
  return header?.value;
}

function getMessageIntent() {
  const intent = findHeadersByKey("NServiceBus.MessageIntent");
  return intent?.value;
}

function removeHeadersMarkedAsRemoved() {
  localMessage.value.headers = localMessage.value.headers.filter((header: HeaderWithEditing) => !header.isMarkedAsRemoved);
}

async function retryEditedMessage() {
  removeHeadersMarkedAsRemoved();
  try {
    await useRetryEditedMessage(id.value, localMessage);
    localMessage.value.retried = true;
    return emit("confirm");
  } catch {
    showEditAndRetryConfirmation.value = false;
    showEditRetryGenericError.value = true;
  }
}

function initializeMessageBodyAndHeaders() {
  origMessageBody = body.value.data.value ?? "";
  localMessage.value = {
    isBodyChanged: false,
    isBodyEmpty: false,
    isContentTypeSupported: false,
    bodyContentType: undefined,
    bodyUnavailable: body.value.not_found ?? false,
    isEvent: false,
    retried: state.value.data.failure_status.retried ?? false,
    headers: headers.value.data.map((header: Header) => ({ ...header })) as HeaderWithEditing[],
    messageBody: body.value.data.value ?? "",
  };
  localMessage.value.isBodyEmpty = false;
  localMessage.value.isBodyChanged = false;

  const contentType = getContentType();
  localMessage.value.bodyContentType = contentType;
  const parsedContentType = parseContentType(contentType);
  localMessage.value.isContentTypeSupported = parsedContentType.isSupported;
  localMessage.value.language = parsedContentType.language;

  const messageIntent = getMessageIntent();
  localMessage.value.isEvent = messageIntent === "Publish";

  for (let index = 0; index < headers.value.data.length; index++) {
    const header: HeaderWithEditing = headers.value.data[index] as HeaderWithEditing;

    header.isLocked = false;
    header.isSensitive = false;
    header.isMarkedAsRemoved = false;
    header.isChanged = false;

    if (edit_and_retry_config.value.locked_headers.includes(header.key)) {
      header.isLocked = true;
    } else if (edit_and_retry_config.value.sensitive_headers.includes(header.key)) {
      header.isSensitive = true;
    }

    localMessage.value.headers[index] = header;
  }
}

function togglePanel(panelNum: number) {
  panel.value = panelNum;
  return false;
}

onMounted(() => {
  togglePanel(1);
  initializeMessageBodyAndHeaders();
});
</script>

<template>
  <section name="failed_message_editor">
    <div class="model modal-msg-editor" style="z-index: 1050; display: block" role="dialog" aria-label="edit and retry message">
      <div class="modal-mask">
        <div class="modal-dialog">
          <div class="modal-content">
            <div class="modal-header">
              <div class="modal-title">
                <h3>Edit and retry message</h3>
              </div>
            </div>
            <div class="modal-body">
              <div class="row">
                <div class="col-sm-12">
                  <div class="row msg-editor-tabs">
                    <div class="col-sm-12 no-side-padding">
                      <div role="tablist" class="tabs msg-tabs">
                        <h5 role="tab" :class="{ active: panel === 1 }" class="nav-item" @click.prevent="togglePanel(1)"><a href="#">Headers</a></h5>
                        <h5 role="tab" :class="{ active: panel === 2 }" class="nav-item" @click.prevent="togglePanel(2)"><a href="#">Message body</a></h5>
                      </div>
                    </div>
                  </div>
                  <div class="row msg-editor-content">
                    <div class="col-sm-12 no-side-padding">
                      <div class="alert alert-warning" v-if="localMessage.isEvent">
                        <div class="col-sm-12">
                          <i class="fa fa-exclamation-circle"></i> This message is an event. If it was already successfully handled by other subscribers, editing it now has the risk of changing the semantic meaning of the event and may result in
                          altering the system behavior.
                        </div>
                      </div>
                      <div class="alert alert-warning" v-if="!localMessage.isContentTypeSupported || localMessage.bodyUnavailable">
                        <div role="status" aria-label="cannot edit message body" class="col-sm-12">
                          <i class="fa fa-exclamation-circle"></i> Message body cannot be edited because content type "{{ localMessage.bodyContentType }}" is not supported. Only messages with content types "application/json" and "text/xml" can be
                          edited.
                        </div>
                      </div>
                      <div class="row alert alert-danger" v-if="showEditRetryGenericError">
                        <div class="col-sm-12"><i class="fa fa-exclamation-triangle"></i> An error occurred while retrying the message, please check the ServiceControl logs for more details on the failure.</div>
                      </div>
                      <table role="tabpanel" class="table" v-if="panel === 1">
                        <tbody>
                          <tr class="interactiveList" v-for="header in localMessage.headers" :key="header.key">
                            <MessageHeader :header="header"></MessageHeader>
                          </tr>
                        </tbody>
                      </table>
                      <div role="tabpanel" v-if="panel === 2 && !localMessage.bodyUnavailable" style="height: calc(100% - 260px)">
                        <div style="margin-top: 1.25rem">
                          <LoadingSpinner v-if="body.loading" />
                          <CodeEditor v-else aria-label="message body" :read-only="!localMessage.isContentTypeSupported" v-model="localMessage.messageBody" :language="localMessage.language" :show-gutter="true"></CodeEditor>
                        </div>
                        <span class="empty-error" v-if="localMessage.isBodyEmpty"><i class="fa fa-exclamation-triangle"></i> Message body cannot be empty</span>
                        <span class="reset-body" v-if="localMessage.isBodyChanged"><i class="fa fa-undo" v-tippy="`Reset changes`"></i> <a @click="resetBodyChanges()" href="javascript:void(0)">Reset changes</a></span>
                        <div class="alert alert-info" v-if="panel === 2 && localMessage.bodyUnavailable">{{ localMessage.bodyUnavailable }}</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
            <div class="modal-footer" v-if="!showEditAndRetryConfirmation && !showCancelConfirmation">
              <button class="btn btn-default" @click="confirmCancel()">Cancel</button>
              <button class="btn btn-primary" :disabled="localMessage.isBodyEmpty || localMessage.bodyUnavailable" @click="confirmEditAndRetry()">Retry</button>
            </div>
            <div class="modal-footer cancel-confirmation" v-if="showCancelConfirmation">
              <div>Are you sure you want to cancel? Any changes you made will be lost.</div>
              <button class="btn btn-default" @click="close()">Yes</button>
              <button class="btn btn-primary" @click="showCancelConfirmation = false">No</button>
            </div>
            <div class="modal-footer edit-retry-confirmation" v-if="showEditAndRetryConfirmation">
              <div>Are you sure you want to continue? If you edited the message, it may cause unexpected consequences in the system.</div>
              <button class="btn btn-default" @click="retryEditedMessage()">Yes</button>
              <button class="btn btn-primary" @click="showEditAndRetryConfirmation = false">No</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
@import "@/components/modal.css";

.cancel-confirmation,
.edit-retry-confirmation {
  background: #181919;
  color: #fff;
  border-bottom-right-radius: 6px;
  border-bottom-left-radius: 6px;
}

.cancel-confirmation div,
.edit-retry-confirmation div {
  display: inline-block;
  font-weight: bold;
  font-size: 14px;
  position: relative;
  top: 1px;
  margin-right: 20px;
}

.modal-msg-editor .reset-body {
  color: #00a3c4;
  font-weight: bold;
  text-align: left;
  margin-top: 15px;
  display: inline-block;
}

.modal-msg-editor .reset-body a:hover {
  cursor: pointer;
}

.modal-msg-editor .reset-body i.fa.fa-undo {
  color: #00a3c4;
}

.modal-msg-editor .empty-error {
  float: right;
  margin-top: 15px;
  color: #ce4844;
  font-weight: bold;
}

.modal-msg-editor .empty-error i.fa.fa-exclamation-triangle {
  color: #ce4844;
}

.modal-msg-editor .row.alert.alert-danger i.fa.fa-exclamation-triangle {
  color: #ce4844;
}

.modal-msg-editor .row.alert.alert-warning i.fa.fa-exclamation-circle {
  color: #8a6d3b;
}

.modal-msg-editor .modal-dialog {
  width: 70%;
}

.modal-msg-editor .modal-body {
  overflow-y: auto;
  height: 80vh;
}

.modal-msg-editor .msg-tabs {
  margin-top: 20px;
}

.modal-msg-editor .row.msg-editor-tabs {
  height: 52px;
  position: relative;
  box-shadow: -10px 20px 20px #fff;
  z-index: 10;
}

.modal-msg-editor .row.msg-editor-content table {
  margin-top: 20px;
}

.modal-msg-editor .row,
.modal-msg-editor .col-sm-12 {
  height: 100%;
}

.modal-msg-editor .row.msg-editor-content {
  height: calc(100% - 37px);
  overflow-y: auto;
  padding-right: 15px;
}
</style>
