<script setup lang="ts">
import { TYPE } from "vue-toastification";

const props = defineProps<{
  type: TYPE;
  title: string;
  message: string;
}>();
</script>

<template>
  <div>
    <template v-if="props.type === TYPE.SUCCESS">
      <div class="toast-success toast-message">
        <p>
          <strong>{{ props.title }}</strong>
        </p>
        <span v-html="props.message"></span>
      </div>
    </template>
    <template v-if="props.type === TYPE.INFO">
      <div class="toast-info toast-message">
        <p>
          <strong>{{ props.title }}</strong>
        </p>
        <span v-html="props.message"></span>
      </div>
    </template>
    <template v-if="props.type === TYPE.ERROR">
      <div class="toast-error toast-message">
        <p>
          <strong>{{ props.title }}</strong>
        </p>
        <span v-html="props.message"></span>
      </div>
    </template>
    <template v-if="props.type === TYPE.WARNING">
      <div class="toast-warning toast-message">
        <p>
          <strong>{{ props.title }}</strong>
        </p>
        <span v-html="props.message"></span>
      </div>
    </template>
    <template v-if="props.type === TYPE.DEFAULT">
      <div class="toast-default toast-message">
        <p>
          <strong>{{ props.title }}</strong>
        </p>
        <span v-html="props.message"></span>
      </div>
    </template>
  </div>
</template>
