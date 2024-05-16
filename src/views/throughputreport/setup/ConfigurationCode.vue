<script setup lang="ts">
import VCodeBlock from "@wdns/vue-code-block";
import { ThroughputConnectionSetting } from "@/resources/ThroughputConnectionSettings";
import { computed, ref } from "vue";
import DropDown, { Item } from "@/components/DropDown.vue";

const props = withDefaults(
  defineProps<{
    settings: ThroughputConnectionSetting[];
    configFileName?: string;
  }>(),
  { configFileName: "ServiceControl.exe.config" }
);

const config = computed(() => {
  const list: string[] = [];
  for (const item of props.settings) {
    list.push(`<!-- ${item.description} -->`);
    list.push(`<add key="${item.name}" value="enter value here" />`);
  }
  return list.join("\n");
});

const bash = computed(() => {
  const list: string[] = [];
  for (const item of props.settings) {
    list.push(`# ${item.description}`);
    list.push(`export ${item.name.replaceAll("/", "_").toUpperCase()}="enter value here"`);
  }
  return list.join("\n");
});

const windows = computed(() => {
  const list: string[] = [];
  for (const item of props.settings) {
    list.push(`rem ${item.description}`);
    list.push(`set ${item.name}="enter value here"`);
  }
  return list.join("\n");
});

const languageSelected = ref("config");
const codeSelected = computed(() => {
  switch (languageSelected.value) {
    case "bash":
      return { code: bash.value, lang: "bash" };
    case "windows":
      return { code: windows.value, lang: "cmd" };
    default:
      return { code: config.value, lang: "xml" };
  }
});

const languages: Item[] = [
  {
    text: props.configFileName,
    value: "config",
  },
  {
    text: "Bash environment variables",
    value: "bash",
  },
  {
    text: "Windows environment variables",
    value: "windows",
  },
];

function languageChanged(item: Item) {
  languageSelected.value = item.value;
}
</script>
<template>
  <div>
    <drop-down :select-item="languages.find((v) => v.value === languageSelected)" :callback="languageChanged" :items="languages" />
  </div>
  <div class="configuration">
    <VCodeBlock :code="codeSelected.code" :lang="codeSelected.lang" />
    <div class="instructions">
      <slot name="configInstructions" v-if="languageSelected === 'config'"></slot>
      <slot name="environmentVariableInstructions" v-else></slot>
    </div>
  </div>
</template>

<style scoped>
.configuration {
  height: 100%;
}
.instructions {
  font-size: 13px;
  color: #8c8c8c;
}
</style>
