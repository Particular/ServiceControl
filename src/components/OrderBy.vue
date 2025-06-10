<script setup generic="T" lang="ts">
import { onMounted, ref } from "vue";
import { useCookies } from "vue3-cookies";
import SortOptions, { SortDirection } from "@/resources/SortOptions";
import getSortFunction from "@/components/getSortFunction";

const emit = defineEmits<{
  sortUpdated: [option: SortOptions<T>];
}>();

const props = withDefaults(
  defineProps<{
    hideSort?: boolean;
    sortOptions: SortOptions<T>[];
    sortSavePrefix?: string;
  }>(),
  {
    hideSort: false,
  }
);

const cookies = useCookies().cookies;

const selectedSort = ref(props.sortOptions[0].description);

function getSortOptions() {
  return props.sortOptions;
}

function saveSortOption(sortCriteria: string, sortDirection: SortDirection) {
  cookies.set(`${props.sortSavePrefix ? props.sortSavePrefix : ""}sortCriteria`, sortCriteria);
  cookies.set(`${props.sortSavePrefix ? props.sortSavePrefix : ""}sortDirection`, sortDirection);
}

function loadSavedSortOption() {
  const criteria = cookies.get(`${props.sortSavePrefix ? props.sortSavePrefix : ""}sortCriteria`);
  const direction = cookies.get(`${props.sortSavePrefix ? props.sortSavePrefix : ""}sortDirection`) as SortDirection;

  if (criteria && direction) {
    const sortBy = getSortOptions().find((sort) => {
      return sort.description.toLowerCase() === criteria.toLowerCase();
    });
    if (sortBy) {
      return {
        ...sortBy,
        sort: getSortFunction(sortBy.selector, direction),
        dir: direction,
      };
    }
  }

  return props.sortOptions[0];
}

function sortUpdated(sort: SortOptions<T>, dir: SortDirection) {
  selectedSort.value = sort.description + (dir === SortDirection.Descending ? " (Descending)" : "");
  saveSortOption(sort.description, dir);

  emit("sortUpdated", {
    ...sort,
    dir: dir,
    sort: getSortFunction<T>(sort.selector, dir),
  });
}

function setSortOptions() {
  const savedSort = loadSavedSortOption();
  selectedSort.value = `${savedSort.description}${savedSort.dir === SortDirection.Descending ? " (Descending)" : ""}`;

  emit("sortUpdated", savedSort);
}

onMounted(() => {
  setSortOptions();
});
</script>

<template>
  <div class="msg-group-menu dropdown" v-show="!props.hideSort">
    <label class="control-label">Sort by:</label>
    <button type="button" class="btn dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
      {{ selectedSort }}
      <span class="caret"></span>
    </button>
    <ul class="dropdown-menu">
      <span v-for="(sort, index) in getSortOptions()" :key="index">
        <li>
          <button @click="sortUpdated(sort, SortDirection.Ascending)"><i class="fa" :class="`${sort.icon}asc`"></i>{{ sort.description }}</button>
        </li>
        <li>
          <button @click="sortUpdated(sort, SortDirection.Descending)"><i class="fa" :class="`${sort.icon}desc`"></i>{{ sort.description }}<span> (Descending)</span></button>
        </li>
      </span>
    </ul>
  </div>
</template>

<style scoped>
@import "@/assets/dropdown.css";

.dropdown-menu .fa {
  padding-right: 5px;
}

.btn.sp-btn-menu {
  background: none;
  border: none;
  color: #00a3c4;
  padding-right: 16px;
  padding-left: 16px;
}

.sp-btn-menu:hover {
  background: none;
  border: none;
  color: #00a3c4;
  text-decoration: underline;
}

.msg-group-menu {
  margin: 21px 0px 0 6px;
  padding-right: 15px;
  float: right;
}

.btn.sp-btn-menu:active {
  background: none;
  border: none;
  color: #00a3c4;
  text-decoration: underline;
  -webkit-box-shadow: none;
  box-shadow: none;
}

.sp-btn-menu > i {
  color: #00a3c4;
}

.dropdown-menu li button {
  width: 100%;
  text-align: left;
}
</style>
