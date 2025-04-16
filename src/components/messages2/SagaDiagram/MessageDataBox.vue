<script setup lang="ts">
defineProps<{
  propertyKey?: string;
  propertyValue?: string;
  title?: string;
}>();
</script>

<template>
  <div class="message-data-box">
    <b class="message-data-box-text">{{ propertyKey || "OrderId" }}</b>
    <span class="message-data-box-text">=</span>
    <span class="message-data-box-text--ellipsis" :title="title || propertyValue || 'Sample ID'">{{ propertyValue || "Sample ID" }}</span>
  </div>
</template>

<style scoped>
.message-data-box {
  display: flex;
}

.message-data-box-text {
  display: inline-block;
  margin-right: 0.25rem;
}

.message-data-box-text--ellipsis {
  display: inline-block;
  overflow: hidden;
  max-width: 100%;
  padding: 0%;
  white-space: nowrap;
  text-overflow: ellipsis;
}
</style>
