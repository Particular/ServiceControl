import ToastPopup from "@/components/ToastPopup.vue";
import { TYPE, useToast } from "vue-toastification";

export function useShowToast(type: TYPE, title: string, message: string, doNotUseTimeout: boolean = false) {
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
  });
}
