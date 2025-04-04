<script setup lang="ts">
import { ref, type Component, type DefineComponent, shallowRef } from "vue";

interface Tab {
  text: string;
  component: Component | DefineComponent;
}

const props = defineProps<{ tabs: Tab[] }>();
const activePanel = ref(0);
const activeComponent = shallowRef(props.tabs[0].component);

function togglePanel(panelIndex: number) {
  activePanel.value = panelIndex;
  activeComponent.value = props.tabs[panelIndex].component;
}
</script>

<template>
  <div class="nav tabs msg-tabs">
    <h5 :class="{ active: activePanel === index }" class="nav-item" @click.prevent="togglePanel(index)" v-for="(tab, index) in props.tabs" :key="tab.text">
      <a href="#">{{ tab.text }}</a>
    </h5>
  </div>
  <keep-alive>
    <component v-bind:is="activeComponent"></component>
  </keep-alive>
</template>

<style scoped></style>
