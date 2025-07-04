<script setup lang="ts">
import { onMounted, ref, useTemplateRef } from "vue";
import { licenseStatus } from "../../composables/serviceLicense";
import { connectionState } from "../../composables/serviceServiceControl";
import { useTypedFetchFromServiceControl } from "../../composables/serviceServiceControlUrls";
import { useCookies } from "vue3-cookies";
import LicenseExpired from "../../components/LicenseExpired.vue";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import LastTenOperations from "../failedmessages/LastTenOperations.vue";
import MessageGroupList, { IMessageGroupList } from "../failedmessages/MessageGroupList.vue";
import OrderBy from "@/components/OrderBy.vue";
import SortOptions, { SortDirection } from "@/resources/SortOptions";
import GroupOperation from "@/resources/GroupOperation";
import getSortFunction from "@/components/getSortFunction";
import { faArrowDownAZ, faArrowDownZA, faArrowDownShortWide, faArrowDownWideShort, faArrowDown19, faArrowDown91 } from "@fortawesome/free-solid-svg-icons";

const selectedClassifier = ref<string>("");
const classifiers = ref<string[]>([]);
const messageGroupList = useTemplateRef<IMessageGroupList>("messageGroupList");
const sortMethod = ref<SortOptions<GroupOperation>["sort"]>();

function sortGroups(sort: SortOptions<GroupOperation>) {
  sortMethod.value = sort.sort ?? getSortFunction(sort.selector, SortDirection.Ascending);

  // force a re-render of the messagegroup list
  messageGroupList.value?.loadFailedMessageGroups();
}

const sortOptions: SortOptions<GroupOperation>[] = [
  {
    description: "Name",
    selector: (group) => group.title,
    iconAsc: faArrowDownAZ,
    iconDesc: faArrowDownZA,
  },
  {
    description: "Number of messages",
    selector: (group) => group.count,
    iconAsc: faArrowDown19,
    iconDesc: faArrowDown91,
  },
  {
    description: "First Failed Time",
    selector: (group) => group.first!,
    iconAsc: faArrowDownShortWide,
    iconDesc: faArrowDownWideShort,
  },
  {
    description: "Last Failed Time",
    selector: (group) => group.last!,
    iconAsc: faArrowDownShortWide,
    iconDesc: faArrowDownWideShort,
  },
  {
    description: "Last Retried Time",
    selector: (group) => group.last_operation_completion_time!,
    iconAsc: faArrowDownShortWide,
    iconDesc: faArrowDownWideShort,
  },
];

async function getGroupingClassifiers() {
  const [, data] = await useTypedFetchFromServiceControl<string[]>("recoverability/classifiers");
  classifiers.value = data;
}

function saveDefaultGroupingClassifier(classifier: string) {
  const cookies = useCookies().cookies;
  cookies.set("failed_groups_classification", classifier);
}

function classifierChanged(classifier: string) {
  selectedClassifier.value = classifier;
  saveDefaultGroupingClassifier(classifier);
  messageGroupList.value?.loadFailedMessageGroups(classifier);
}

function loadDefaultGroupingClassifier() {
  const cookies = useCookies().cookies;
  const cookieGrouping = cookies.get("failed_groups_classification");

  if (cookieGrouping) {
    return cookieGrouping;
  }

  return null;
}

onMounted(async () => {
  await getGroupingClassifiers();
  let savedClassifier = loadDefaultGroupingClassifier();

  if (!savedClassifier) {
    savedClassifier = classifiers.value[0];
  }

  selectedClassifier.value = savedClassifier;
  messageGroupList.value?.loadFailedMessageGroups(savedClassifier);
});
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <ServiceControlNotAvailable />
    <template v-if="!connectionState.unableToConnect">
      <section name="message_groups">
        <LastTenOperations></LastTenOperations>
        <div class="row">
          <div class="col-6 list-section">
            <h3>Failed message group</h3>
          </div>
          <div class="col-6 toolbar-menus no-side-padding">
            <div class="msg-group-menu dropdown">
              <label class="control-label">Group by:</label>
              <button type="button" class="btn btn-default dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
                {{ selectedClassifier }}
                <span class="caret"></span>
              </button>
              <ul class="dropdown-menu">
                <li v-for="(classifier, index) in classifiers" :key="index">
                  <a @click.prevent="classifierChanged(classifier)">{{ classifier }}</a>
                </li>
              </ul>
            </div>
            <OrderBy @sort-updated="sortGroups" :sortOptions="sortOptions"></OrderBy>
          </div>
        </div>
        <div class="box-container" v-if="sortMethod">
          <div class="row">
            <div class="col-12">
              <div class="list-section">
                <div class="col-12 form-group">
                  <MessageGroupList :sortFunction="sortMethod" ref="messageGroupList"></MessageGroupList>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </template>
  </template>
</template>

<style scoped>
.dropdown > button:hover {
  background: none;
  border: none;
  color: var(--sp-blue);
  text-decoration: underline;
}
</style>
