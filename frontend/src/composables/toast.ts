import ToastPopup from "@/components/ToastPopup.vue";
import { TYPE, useToast } from "vue-toastification";
import { ToastOptions } from "vue-toastification/dist/types/types";

export function useShowToast(type: TYPE, title: string, message: string, doNotUseTimeout: boolean = false, options?: ToastOptions) {
  const toast = useToast();
  const content = {
    // Your component or JSX template
    component: ToastPopup,

    // Props are just regular props, but these won't be reactive
    props: {
      type: type,
      title: title,
      message: message,
    },
  };
  toast(content, {
    timeout: doNotUseTimeout ? false : undefined,
    type: type,
    ...options,
  });
}

export const showToastAfterOperation = async (operation: () => Promise<void>, toastType: TYPE, title: string, message: string) => {
  await operation();
  useShowToast(toastType, title, message);
};
