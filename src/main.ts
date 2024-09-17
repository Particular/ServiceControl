import makeRouter from "./router";
import { mount } from "./mount";
import "vue-toastification/dist/index.css";
import "vue3-simple-typeahead/dist/vue3-simple-typeahead.css"; //Optional default CSS
import "./assets/main.css";
import "tippy.js/dist/tippy.css";

async function conditionallyEnableMocking() {
  if (process.env.NODE_ENV !== "dev-mocks") {
    return;
  }

  const { worker } = await import("@/../test/mocks/browser");

  // `worker.start()` returns a Promise that resolves
  // once the Service Worker is up and ready to intercept requests.
  return worker.start();
}

// eslint-disable-next-line promise/catch-or-return,promise/always-return
conditionallyEnableMocking().then(() => {
  mount({ router: makeRouter() });
});
