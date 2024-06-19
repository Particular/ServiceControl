<script setup lang="ts">
export interface Item {
  value: string;
  text: string;
}

const props = defineProps<{
  label?: string;
  selectItem?: Item;
  items: Item[];
  callback: (item: Item) => void;
}>();
</script>

<template>
  <div class="dropdown">
    <label v-if="label" class="control-label" style="float: inherit">{{ props.label }}:</label>
    <button type="button" class="btn btn-dropdown dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">{{ props.selectItem?.text ?? props.items[0].text }}</button>
    <ul class="dropdown-menu">
      <li v-for="item in props.items" :key="item.value">
        <a href="#" @click.prevent="callback(item)">{{ item.text }}</a>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.dropdown .dropdown-menu {
  top: 36px;
  margin-left: 72px;
}

.btn.btn-dropdown {
  padding: 8px 16px;
}

.btn.dropdown-toggle::after {
  vertical-align: middle;
}

ul.dropdown-menu li a span {
  color: #aaa;
}
</style>
