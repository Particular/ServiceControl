<script setup lang="ts">
import { computed, ref } from "vue";
import FAIcon from "@/components/FAIcon.vue";
import { faInfoCircle } from "@fortawesome/free-solid-svg-icons";

export interface RetryRedirect {
  redirectId: string;
  sourceQueue: string;
  targetQueue: string;
  immediatelyRetry: boolean;
}

const emit = defineEmits<{
  create: [retry: RetryRedirect];
  edit: [retry: RetryRedirect];
  cancel: [];
}>();
const model = withDefaults(
  defineProps<{
    message_redirect_id: string;
    from_physical_address: string;
    to_physical_address: string;
    immediately_retry?: boolean;
    queues: string[];
  }>(),
  { immediately_retry: false }
);

const sourceQueue = ref(model.from_physical_address);
const targetQueue = ref(model.to_physical_address);
const immediatelyRetry = ref(model.immediately_retry);

const sourceQueueIsValid = computed(() => {
  return !!sourceQueue.value;
});
const targetQueueIsValid = computed(() => {
  return targetQueue.value && targetQueue.value !== sourceQueue.value;
});

const formIsValid = computed(() => {
  return sourceQueueIsValid.value && targetQueueIsValid.value;
});

const notKnownQueue = computed(() => {
  return !model.queues.includes(targetQueue.value);
});

const noKnownQueues = computed(() => {
  return model.queues.length === 0;
});

const sourceQueueTooltip = "Choose a queue that is known to Service Control";
const targetQueueTooltip = "Choose a queue that is known to Service Control or provide a custom queue";

function selectToAddress(item: string) {
  targetQueue.value = item;
}

function create() {
  const redirect = {
    redirectId: "",
    sourceQueue: sourceQueue.value,
    targetQueue: targetQueue.value,
    immediatelyRetry: immediatelyRetry.value,
  };
  emit("create", redirect);
}

function edit() {
  const redirect = {
    redirectId: model.message_redirect_id,
    sourceQueue: sourceQueue.value,
    targetQueue: targetQueue.value,
    immediatelyRetry: immediatelyRetry.value,
  };
  emit("edit", redirect);
}

function close() {
  emit("cancel");
}
</script>

<template>
  <div class="modal-mask">
    <div class="modal-wrapper">
      <div class="modal-container modal-content">
        <div class="modal-header">
          <h3 class="modal-title" v-if="model.message_redirect_id">Modify redirect</h3>
          <h3 class="modal-title" v-if="!model.message_redirect_id">Create redirect</h3>
        </div>

        <form name="redirectForm" class="redirectForm" novalidate>
          <div class="modal-body">
            <div class="row">
              <div class="form-group">
                <label for="sourceQueue">From physical address</label>
                <FAIcon :icon="faInfoCircle" v-tippy="sourceQueueTooltip" class="info" size="sm" />
                <div :class="{ 'has-error': !sourceQueueIsValid, 'has-success': sourceQueueIsValid }">
                  <select id="sourceQueue" name="sourceQueue" v-model="sourceQueue" class="form-select" required :disabled="!!model.message_redirect_id">
                    <option v-for="option in model.queues" :value="option" :key="option">
                      {{ option }}
                    </option>
                  </select>
                </div>
              </div>
              <div class="row"></div>
              <div class="form-group">
                <label for="targetQueue">To physical address</label>
                <FAIcon :icon="faInfoCircle" v-tippy="targetQueueTooltip" class="info" size="sm" />
                <div :class="{ 'has-error': !targetQueueIsValid, 'has-success': targetQueueIsValid }">
                  <vue3-simple-typeahead
                    id="targetQueue"
                    name="targetQueue"
                    :defaultItem="model.to_physical_address"
                    v-model="targetQueue"
                    @selectItem="selectToAddress"
                    class="form-control"
                    required
                    placeholder="Start writing..."
                    :items="model.queues"
                    :minInputLength="1"
                  >
                  </vue3-simple-typeahead>

                  <template v-if="noKnownQueues">
                    <div :class="{ 'has-error': noKnownQueues }">
                      <p class="control-label">No known queues found. You can enter a queue name manually, but if you don't provide a valid address, the redirected message will be lost.</p>
                    </div>
                  </template>
                  <template v-if="notKnownQueue">
                    <div :class="{ 'has-error': notKnownQueue }">
                      <p class="control-label">Target queue does not match any known queue. You can enter a queue name manually, but if you don't provide a valid address, the redirected message will be lost.</p>
                    </div>
                  </template>
                </div>
              </div>
              <div class="form-group">
                <input type="checkbox" v-model="immediatelyRetry" class="check-label" id="immediatelyRetry" />
                <label for="immediatelyRetry">Immediately retry any matching failed messages</label>
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button v-if="model.message_redirect_id" class="btn btn-primary" :disabled="!formIsValid" @click="edit">Modify</button>
            <button v-if="!model.message_redirect_id" class="btn btn-primary" :disabled="!formIsValid" @click="create">Create</button>
            <button class="btn btn-default" @click="close">Cancel</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "@/components/modal.css";

.redirectForm {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

p.control-label {
  margin-bottom: 2px;
}

.info {
  margin-left: 4px;
  color: var(--info-icon);
}
</style>
