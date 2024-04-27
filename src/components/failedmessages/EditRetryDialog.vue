<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRetryEditedMessage } from "../../composables/serviceFailedMessage";
import MessageHeader from "./EditMessageHeader.vue";
import { EditAndRetryConfig } from "@/resources/Configuration";
import type Header from "@/resources/Header";
import { ExtendedFailedMessage } from "@/resources/FailedMessage";

interface HeaderWithEditing extends Header {
  isLocked: boolean;
  isSensitive: boolean;
  isMarkedAsRemoved: boolean;
  isChanged: boolean;
}

const emit = defineEmits<{
  cancel: [];
  retried: [];
}>();

const props = defineProps<{
  id: string;
  message: ExtendedFailedMessage;
  configuration: EditAndRetryConfig;
}>();

const panel = ref();
const localMessage = ref();
let origMessageBody: string;

const showEditAndRetryConfirmation = ref(false);
const showCancelConfirmation = ref(false);
const showEditRetryGenericError = ref(false);

const id = computed(() => props.id);
const messageBody = computed(() => props.message.messageBody);

watch(messageBody, (newValue) => {
  if (newValue !== origMessageBody) {
    localMessage.value.isBodyChanged = true;
  }
  if (newValue === "") {
    localMessage.value.isBodyEmpty = true;
  } else {
    localMessage.value.isBodyEmpty = false;
  }
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
  return header.value;
}

function isContentTypeSupported(contentType: string) {
  return contentType === "application/json" || contentType === "text/xml";
}

function getMessageIntent() {
  const intent = findHeadersByKey("NServiceBus.MessageIntent");
  return intent.value;
}

function removeHeadersMarkedAsRemoved() {
  localMessage.value.headers = localMessage.value.headers.filter((header: HeaderWithEditing) => !header.isMarkedAsRemoved);
}

async function retryEditedMessage() {
  removeHeadersMarkedAsRemoved();
  try {
    await useRetryEditedMessage(id.value, localMessage);
    localMessage.value.retried = true;
    return emit("retried");
  } catch {
    showEditAndRetryConfirmation.value = false;
    showEditRetryGenericError.value = true;
  }
}

function initializeMessageBodyAndHeaders() {
  origMessageBody = props.message.messageBody;
  localMessage.value = props.message;
  localMessage.value.isBodyEmpty = false;
  localMessage.value.isBodyChanged = false;

  const contentType = getContentType();
  localMessage.value.bodyContentType = contentType;
  localMessage.value.isContentTypeSupported = isContentTypeSupported(contentType);

  const messageIntent = getMessageIntent();
  localMessage.value.isEvent = messageIntent.value === "Publish";

  for (let index = 0; index < props.message.headers.length; index++) {
    const header: HeaderWithEditing = props.message.headers[index] as HeaderWithEditing;

    header.isLocked = false;
    header.isSensitive = false;
    header.isMarkedAsRemoved = false;
    header.isChanged = false;

    if (props.configuration.locked_headers.includes(header.key)) {
      header.isLocked = true;
    } else if (props.configuration.sensitive_headers.includes(header.key)) {
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
    <div class="model modal-msg-editor" style="z-index: 1050; display: block">
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
                      <div class="tabs msg-tabs">
                        <h5 :class="{ active: panel === 1 }" class="nav-item" @click="togglePanel(1)"><a href="javascript:void(0)">Headers</a></h5>
                        <h5 :class="{ active: panel === 2 }" class="nav-item" @click="togglePanel(2)"><a href="javascript:void(0)">Message body</a></h5>
                      </div>
                    </div>
                  </div>
                  <div class="row msg-editor-content">
                    <div class="col-sm-12 no-side-padding">
                      <div class="row alert alert-warning" v-if="localMessage?.isEvent">
                        <div class="col-sm-12">
                          <i class="fa fa-exclamation-circle"></i> This message is an event. If it was already successfully handled by other subscribers, editing it now has the risk of changing the semantic meaning of the event and may result in
                          altering the system behavior.
                        </div>
                      </div>
                      <div class="row alert alert-warning" v-if="!localMessage?.isContentTypeSupported || localMessage?.bodyUnavailable">
                        <div class="col-sm-12">
                          <i class="fa fa-exclamation-circle"></i> Message body cannot be edited because content type ({{ localMessage?.bodyContentType }}) is not supported. Only messages with body content serialized as JSON or XML can be edited.
                        </div>
                      </div>
                      <div class="row alert alert-danger" v-if="showEditRetryGenericError">
                        <div class="col-sm-12"><i class="fa fa-exclamation-triangle"></i> An error occurred while retrying the message, please check the ServiceControl logs for more details on the failure.</div>
                      </div>
                      <table class="table" v-if="panel === 1">
                        <tbody>
                          <tr class="interactiveList" v-for="header in localMessage?.headers" :key="header.key">
                            <MessageHeader :header="header"></MessageHeader>
                          </tr>
                        </tbody>
                      </table>
                      <div v-if="panel === 2 && !localMessage?.bodyUnavailable" style="height: calc(100% - 260px)">
                        <textarea class="form-control" :disabled="!localMessage?.isContentTypeSupported" v-model="localMessage.messageBody"></textarea>
                        <span class="empty-error" v-if="localMessage?.isBodyEmpty"><i class="fa fa-exclamation-triangle"></i> Message body cannot be empty</span>
                        <span class="reset-body" v-if="localMessage?.isBodyChanged"><i class="fa fa-undo" uib-tooltip="Reset changes"></i> <a @click="resetBodyChanges()" href="javascript:void(0)">Reset changes</a></span>
                        <div class="alert alert-info" v-if="localMessage?.panel === 2 && localMessage.bodyUnavailable">{{ localMessage.bodyUnavailable }}</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
            <div class="modal-footer" v-if="!showEditAndRetryConfirmation && !showCancelConfirmation">
              <button class="btn btn-default" @click="confirmCancel()">Cancel</button>
              <button class="btn btn-primary" :disabled="localMessage?.isBodyEmpty || localMessage?.bodyUnavailable" @click="confirmEditAndRetry()">Retry</button>
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

<style>
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
</style>
