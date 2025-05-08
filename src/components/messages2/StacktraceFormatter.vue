<script lang="ts" setup>
import { ref, watch } from "vue";

// Define TypeScript interfaces for settings and supported languages
interface NetStackOptions {
  frame?: string;
  type?: string;
  method?: string;
  paramsList?: string;
  paramType?: string;
  paramName?: string;
  file?: string;
  line?: string;
}

interface Language {
  name: string;
  at: string;
  in: string;
  line: string;
}

type Text = string;

interface Node {
  params: Array<{ name: string; type: string }>;
  type: string;
  lineNumber?: number;
  file?: string;
  method: string;
  spaces: string;
}

type Element = Text | Node;

// Props
const props = withDefaults(defineProps<{ stackTrace: string; options?: NetStackOptions }>(), {
  options: () => ({
    frame: "st-frame",
    type: "st-type",
    method: "st-method",
    paramsList: "st-frame-params",
    paramType: "st-param-type",
    paramName: "st-param-name",
    file: "st-file",
    line: "st-line",
  }),
});

// Supported languages and their keywords
const languages: Language[] = [
  { name: "english", at: "at", in: "in", line: "line" },
  { name: "danish", at: "ved", in: "i", line: "linje" },
  { name: "german", at: "bei", in: "in", line: "Zeile" },
  { name: "spanish", at: "en", in: "en", line: "línea" },
  { name: "russian", at: "в", in: "в", line: "строка" },
  { name: "chinese", at: "在", in: "位置", line: "行号" },
];

// Reactive variables and setup state
const formattedStack = ref<Element[]>([]);
const selectedLanguage = ref<Language>(languages[0]);

// Helper function to detect languages in the stack trace
const detectLanguagesInOrder = (text: string): Language[] => {
  const languageRegexes = {
    english: /\s+at .*?\)/g,
    danish: /\s+ved .*?\)/g,
    german: /\s+bei .*?\)/g,
    spanish: /\s+en .*?\)/g,
    russian: /\s+в .*?\)/g,
    chinese: /\s+在 .*?\)/g,
  };

  const detectedLanguages: Language[] = [];
  for (const lang in languageRegexes) {
    if (languageRegexes[lang as keyof typeof languageRegexes].test(text)) {
      const foundLang = languages.find((l) => l.name === lang);
      if (foundLang) {
        detectedLanguages.push(foundLang);
      }
    }
  }

  return detectedLanguages;
};

// Core formatting logic
const formatStackTrace = (stackTrace: string, selectedLang: Language): Element[] => {
  const lines = stackTrace.split("\n");
  const fileAndLineNumberRegEx = new RegExp(`${selectedLang.in} (.+):${selectedLang.line} (\\d+)`);
  const atRegex = new RegExp(`(\\s*)(${selectedLang.at}) (.+?)\\((.*?)\\)`);

  return lines.map((line) => {
    const match = line.match(atRegex);
    if (match) {
      const [, spaces, , methodWithType, paramsWithFile] = match;

      const [type, method] = (() => {
        const parts = methodWithType.split(".");
        const method = parts.pop() ?? "";
        const type = parts.join(".");
        return [type, method];
      })();

      const params = paramsWithFile.split(", ").map((param) => {
        const [paramType, paramName] = param.split(" ");
        return { name: paramName, type: paramType };
      });

      const matchFile = line.match(fileAndLineNumberRegEx);
      let file, lineNumber;
      if (matchFile) {
        [, file, lineNumber] = matchFile;
      }

      return <Node>{ method, type, params, file, lineNumber, spaces };
    } else {
      return line;
    }
  });
};

// Process the provided stack trace
const processStackTrace = (): void => {
  const rawContent = props.stackTrace;
  const detectedLanguages = detectLanguagesInOrder(rawContent);

  if (!detectedLanguages.length) {
    formattedStack.value = [rawContent]; // If no language detected, output raw content
    return;
  }

  selectedLanguage.value = detectedLanguages[0]; // Use the first detected language
  formattedStack.value = formatStackTrace(rawContent, selectedLanguage.value);
};

watch(
  () => props.stackTrace,
  () => {
    processStackTrace();
  },
  { immediate: true }
);
</script>

<template>
  <!-- prettier-ignore -->
  <div class="stack-trace-container">
    <template v-for="line in formattedStack" :key="line">
      <template v-if="typeof line === 'string'">
        <span>{{ line }}</span>
      </template>
      <div v-else>
        {{ line.spaces }}{{ selectedLanguage.at }}
        <span :class="props.options.frame">
          <span :class="props.options.type">{{ line.type }}</span>.<span :class="props.options.method">{{ line.method }}</span>(<span :class="props.options.paramsList">
            <template v-for="(param, index) in line.params" :key="param.name">
              <span :class="props.options.paramType"> {{ param.type }}</span> <span :class="props.options.paramName">{{ param.name }}</span>
              <span v-if="index !== line.params.length - 1">, </span>
            </template>
        </span>)
        </span>
        <template v-if="line.file">
          {{ selectedLanguage.in }} <span :class="props.options.file">{{ line.file }}</span>:{{ selectedLanguage.line }} <span :class="props.options.line">{{ line.lineNumber }}</span>
        </template>
      </div>
    </template>
  </div>
</template>

<style scoped>
.stack-trace-container {
  font-family: monospace;
  white-space: pre-wrap;
}

.st-frame {
  color: #007bff;
}

.st-type {
  color: #d63384;
}

.st-method {
  color: #28a745;
}

.st-file {
  color: #fd7e14;
}

.st-line {
  color: #6c757d;
}

.st-param-type {
  font-style: italic;
  color: #6f42c1;
}

.st-param-name {
  font-weight: bold;
  color: #343a40;
}

.st-frame-params {
  color: #495057;
}
</style>
