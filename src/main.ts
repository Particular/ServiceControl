import makeRouter from "./router";
import { mount } from "./mount";
import "vue-toastification/dist/index.css";
import "vue3-simple-typeahead/dist/vue3-simple-typeahead.css"; //Optional default CSS
import "./assets/main.css";

mount({ router: makeRouter() });
