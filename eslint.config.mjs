import globals from "globals";
import pluginJs from "@eslint/js";
import tseslint from "typescript-eslint";
import pluginVue from "eslint-plugin-vue";
import pluginPromise from "eslint-plugin-promise";

export default tseslint.config(
  {
    ignores: ["node_modules/**", "dist/**", "public/js/app.constants.js"],
  },
  {
    files: ["**/*.{js,mjs,ts,vue}"],
    languageOptions: { globals: globals.browser, ecmaVersion: "latest", parserOptions: { parser: tseslint.parser } },
    extends: [pluginJs.configs.recommended, ...tseslint.configs.recommended, ...pluginVue.configs["flat/essential"], pluginPromise.configs["flat/recommended"]],
    rules: {
      "no-duplicate-imports": "error",
      "promise/prefer-await-to-then": "error",
      "require-await": "error",
      "no-await-in-loop": "warn",
      "prefer-rest-params": "error",
      "prefer-spread": "error",
      "no-var": "error",
      "prefer-const": "error",
      eqeqeq: ["error", "smart"],
      "no-throw-literal": "warn",
    },
  }
);
