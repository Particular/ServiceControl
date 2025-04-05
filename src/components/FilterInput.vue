<script setup lang="ts">
const model = defineModel<string>({ required: true });
defineProps({
  placeholder: {
    type: String,
    default: "Filter by name...", // Default value
  },
});
</script>

<template>
  <div role="search" aria-label="filter" class="filter-input">
    <input type="search" :placeholder="placeholder" aria-label="filter by name" class="form-control-static filter-input" v-model="model" />
  </div>
</template>

<style scoped>
.filter-input input {
  display: inline-block;
  width: 100%;
  padding-right: 10px;
  padding-left: 30px;
  border: 1px solid #aaa;
  border-radius: 4px;
  height: 100%;
}

div.filter-input {
  position: relative;
  width: 280px;
  height: 36px;
}

.filter-input:before {
  font-family: "FontAwesome";
  width: 1.43em;
  content: "\f0b0";
  color: #919e9e;
  position: absolute;
  top: calc(50% - 0.7em);
  left: 0.75em;
}
</style>
