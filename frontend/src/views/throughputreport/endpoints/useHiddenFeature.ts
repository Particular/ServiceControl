import { ref, watchEffect } from "vue";

const keys = ref<string[]>([]);
const hiddenFeatureEnabled = ref(false);
const keyHandler = (event: KeyboardEvent) => {
  keys.value.push(event.key);
};

watchEffect((onCleanup) => {
  if (keys.value.length > 0) {
    const timeout = setTimeout(() => keys.value.splice(0), 5000);
    onCleanup(() => clearTimeout(timeout));
  }
});

window.document.addEventListener("keydown", keyHandler);

export function useHiddenFeature(keyCombo: string[]) {
  watchEffect(() => {
    if (keys.value.toString() === keyCombo.toString()) {
      hiddenFeatureEnabled.value = true;
    }
  });

  return hiddenFeatureEnabled;
}
