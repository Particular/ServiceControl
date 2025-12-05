<script setup lang="ts">
defineProps<{
  displayed: number;
  total: number;
}>();
</script>

<template>
  <div class="col format-showing-results">
    <div>Showing {{ displayed }} of {{ total }} result(s)</div>
  </div>
</template>

<style scoped>
.format-showing-results {
  display: flex;
  align-items: flex-end;
  font-style: italic;
}
</style>
