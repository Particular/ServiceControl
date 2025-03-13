import { createApp } from "vue";
import type { Router } from "vue-router";
import App from "./App.vue";
import Toast, { type PluginOptions, POSITION } from "vue-toastification";
import VueTippy from "vue-tippy";
import { createPinia } from "pinia";
import SimpleTypeahead from "vue3-simple-typeahead";
import { createVCodeBlock } from "@wdns/vue-code-block";
import "highlight.js/styles/github-dark.css";

const toastOptions: PluginOptions = {
  position: POSITION.BOTTOM_RIGHT,
  timeout: 5000,
  transition: "Vue-Toastification__fade",
  hideProgressBar: true,
  containerClassName: "toast-container",
  toastClassName: "vue-toast",
  closeButtonClassName: "toast-close-button",
};

export function mount({ router }: { router: Router }) {
  router.beforeEach((to, _from, next) => {
    document.title = to.meta.title || "ServicePulse";
    next();
  });

  const VCodeBlock = createVCodeBlock({
    theme: "github-dark",
    cssPath: "highlight.js/styles/github-dark.css",
    highlightjs: true,
  });

  const app = createApp(App);
  app.use(router).use(Toast, toastOptions).use(SimpleTypeahead).use(VCodeBlock).use(createPinia()).use(VueTippy);
  app.mount(`#app`);

  app.config.errorHandler = (err, instance) => {
    console.error(instance, err);
  };

  return app;
}
