import { acceptHMRUpdate, defineStore } from "pinia";
import { reactive, ref } from "vue";
import Header from "@/resources/Header.ts";
import type EndpointDetails from "@/resources/EndpointDetails.ts";
import FailedMessage, { ExceptionDetails, FailedMessageStatus } from "@/resources/FailedMessage.ts";
import useEditAndRetry from "@/composables/useEditAndRetry.ts";
import { useFetchFromServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls.ts";
import Message from "@/resources/Message.ts";
import moment from "moment/moment";
import { useConfiguration } from "@/composables/configuration.ts";
import { parse, stringify } from "lossless-json";
import xmlFormat from "xml-formatter";

interface DataContainer<T> {
  loading?: boolean;
  failed_to_load?: boolean;
  not_found?: boolean;
  data: T;
}

interface Model {
  id?: string;
  message_id?: string;
  conversation_id?: string;
  message_type?: string;
  sending_endpoint?: EndpointDetails;
  receiving_endpoint?: EndpointDetails;
  body_url?: string;
  failure_status: Partial<{
    retried: boolean;
    archiving: boolean;
    restoring: boolean;
    archived: boolean;
    resolved: boolean;
    delete_soon: boolean;
    retry_in_progress: boolean;
    delete_in_progress: boolean;
    restore_in_progress: boolean;
    submitted_for_retrial: boolean;
  }>;
  failure_metadata: Partial<{
    exception: ExceptionDetails;
    number_of_processing_attempts: number;
    status: FailedMessageStatus;
    time_of_failure: string;
    last_modified: string;
    edited: boolean;
    edit_of: string;
    deleted_in: string;
    redirect: boolean;
  }>;
  dialog_status: Partial<{
    show_delete_confirm: boolean;
    show_restore_confirm: boolean;
    show_retry_confirm: boolean;
    show_edit_retry_modal: boolean;
  }>;
}

export const useMessageViewStore = defineStore("MessageViewStore", () => {
  const headers = ref<DataContainer<Header[]>>({ data: [] });
  const body = ref<DataContainer<{ value?: string; content_type?: string }>>({ data: {} });
  const state = reactive<DataContainer<Model>>({ data: { failure_metadata: {}, failure_status: {}, dialog_status: {} } });

  async function loadFailedMessage(id: string) {
    state.loading = true;
    state.failed_to_load = false;
    state.not_found = false;

    try {
      const response = await useFetchFromServiceControl(`errors/last/${id}`);
      if (response.status === 404) {
        state.not_found = true;
        return;
      } else if (!response.ok) {
        state.failed_to_load = true;
        return;
      }

      const message = (await response.json()) as FailedMessage;
      state.data.failure_status.archived = message.status === FailedMessageStatus.Archived;
      state.data.failure_status.resolved = message.status === FailedMessageStatus.Resolved;
      state.data.failure_status.retried = message.status === FailedMessageStatus.RetryIssued;
      state.data.failure_metadata.last_modified = message.last_modified;
    } catch {
      state.failed_to_load = headers.value.failed_to_load = true;
      return;
    } finally {
      state.loading = headers.value.loading = false;
    }

    const countdown = moment(state.data.failure_metadata.last_modified).add(error_retention_period, "hours");
    state.data.failure_status.delete_soon = countdown < moment();
    state.data.failure_metadata.deleted_in = countdown.format();

    // TODO: Maintain the mutations of the message in memory until the api returns a newer modified message
  }

  async function loadMessage(messageId: string, receivingEndpointName: string) {
    state.loading = headers.value.loading = true;
    state.failed_to_load = headers.value.failed_to_load = false;
    state.not_found = headers.value.not_found = false;

    try {
      const [, data] = await useTypedFetchFromServiceControl<Message[]>(`messages/search/${messageId}`);

      const message = data.find((value) => value.receiving_endpoint.name === receivingEndpointName);

      if (!message) {
        state.not_found = headers.value.not_found = true;
        return;
      }

      state.data.message_id = message.message_id;
      state.data.conversation_id = message.conversation_id;
      state.data.body_url = message.body_url;
      state.data.message_type = message.message_type;
      state.data.sending_endpoint = message.sending_endpoint;
      state.data.receiving_endpoint = message.receiving_endpoint;

      headers.value.data = message.headers;
    } catch {
      state.failed_to_load = headers.value.failed_to_load = true;
    } finally {
      state.loading = headers.value.loading = false;
    }
  }

  async function downloadBody() {
    if (body.value.not_found) {
      return;
    }

    body.value.loading = true;
    body.value.failed_to_load = false;

    try {
      if (!state.data.body_url) {
        return;
      }
      const response = await useFetchFromServiceControl(state.data.body_url.substring(1));
      if (response.status === 404) {
        body.value.not_found = true;

        return;
      }

      const contentType = response.headers.get("content-type");
      body.value.data.content_type = contentType ?? "text/plain";
      body.value.data.value = await response.text();

      if (contentType === "application/json") {
        body.value.data.value = stringify(parse(body.value.data.value), null, 2) ?? body.value.data.value;
      }
      if (contentType === "text/xml") {
        body.value.data.value = xmlFormat(body.value.data.value, { indentation: "  ", collapseContent: true });
      }
    } catch {
      body.value.failed_to_load = true;
    } finally {
      body.value.loading = false;
    }
  }

  const configuration = useConfiguration();
  const error_retention_period = moment.duration(configuration.value?.data_retention.error_retention_period).asHours();

  return {
    headers,
    body,
    state,
    edit_and_retry_config: useEditAndRetry(),
    loadMessage,
    loadFailedMessage,
    downloadBody,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useMessageViewStore, import.meta.hot));
}

export type MessageViewStore = ReturnType<typeof useMessageViewStore>;
