/* eslint-env node */
require("@rushstack/eslint-patch/modern-module-resolution");

module.exports = {
  root: true,
  env: {
    node: true,
    es6: true,
  },
  ignorePatterns: ["node_modules/**/*", "dist/**/*", "*.ts"],
  extends: ["plugin:vue/vue3-essential", "eslint:recommended", "plugin:@typescript-eslint/recommended", "@vue/eslint-config-prettier/skip-formatting"],
  parser: "vue-eslint-parser",
  plugins: ["@typescript-eslint", "github", "import", "unused-imports", "prefer-arrow"],
  parserOptions: {
    ecmaVersion: "latest",
    parser: "@typescript-eslint/parser",
    extraFileExtensions: [".vue"],
  },
  rules: {
    "prettier/prettier": "error",
    "require-await": "error",
    "no-await-in-loop": "error",
    "github/no-then": "error",
    "prefer-rest-params": "error",
    "prefer-spread": "error",
    "no-var": "error",
    "prefer-const": "error",
    eqeqeq: ["error", "smart"],
    "unused-imports/no-unused-imports": "error",
    "import/no-duplicates": "error",
    "no-throw-literal": "error",
  },
};
