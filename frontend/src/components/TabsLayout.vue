<script setup lang="ts">
import { ref, type Component, type DefineComponent, shallowRef, watch } from "vue";

interface Tab {
  text: string;
  component: Component | DefineComponent;
}

const props = defineProps<{ tabs: Tab[] }>();
const activePanel = ref(props.tabs[0].text);
const activeComponent = shallowRef(props.tabs[0].component);

function togglePanel(panel: string) {
  activePanel.value = panel;
  activeComponent.value = props.tabs.find((value) => value.text === panel)!.component;
}

watch(
  () => props.tabs,
  (newTabs) => {
    // Reset selected tab to first one if the previous selected tab no longer exists
    if (!newTabs.find((value) => value.text === activePanel.value)) {
      togglePanel(newTabs[0].text);
    }
  }
);
</script>

<template>
  <div class="nav tabs msg-tabs">
    <h5 :class="{ active: activePanel === tab.text }" class="nav-item" @click.prevent="togglePanel(tab.text)" v-for="tab in props.tabs" :key="tab.text">
      <a href="#">{{ tab.text }}</a>
    </h5>
  </div>
  <keep-alive>
    <component v-bind:is="activeComponent"></component>
  </keep-alive>
</template>

<style scoped></style>
