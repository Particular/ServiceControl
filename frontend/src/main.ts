import makeRouter from "./router";
import { mount } from "./mount";
import "vue-toastification/dist/index.css";
import "vue3-simple-typeahead/dist/vue3-simple-typeahead.css"; //Optional default CSS
import "./assets/main.css";
import "tippy.js/dist/tippy.css";
import { setDefaultConfig } from "./defaultConfig";

async function conditionallyEnableMocking() {
  if (process.env.NODE_ENV !== "dev-mocks") {
    return;
  }

  const { worker } = await import("@/../test/mocks/browser");

  // `worker.start()` returns a Promise that resolves
  // once the Service Worker is up and ready to intercept requests.
  return worker.start();
}

// eslint-disable-next-line promise/catch-or-return
conditionallyEnableMocking()
  .then(async () => {
    const response = await fetch("js/app.constants.json", {
      method: "GET",
    });

    // eslint-disable-next-line promise/always-return
    if (response.ok) {
      const appConstants = await response.json();
      setDefaultConfig(appConstants);
    } else {
      console.error("Failed to load app constants");
    }
  })
  // eslint-disable-next-line promise/always-return
  .then(() => {
    mount({ router: makeRouter() });
  });
