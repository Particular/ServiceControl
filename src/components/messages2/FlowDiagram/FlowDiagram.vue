<script setup lang="ts">
import { onMounted, ref, nextTick } from "vue";
import { type DefaultEdge, MarkerType, type Node, useVueFlow, VueFlow, XYPosition } from "@vue-flow/core";
import TimeSince from "../../TimeSince.vue";
import routeLinks from "@/router/routeLinks.ts";
import Message, { MessageIntent, MessageStatus, SagaInfo } from "@/resources/Message.ts";
import { NServiceBusHeaders } from "@/resources/Header.ts";
import { Controls } from "@vue-flow/controls";
import { useMessageStore } from "@/stores/MessageStore.ts";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import EndpointDetails from "@/resources/EndpointDetails.ts";
import { hexToCSSFilter } from "hex-to-css-filter";
import TextEllipses from "@/components/TextEllipses.vue";
import { useLayout } from "@/components/messages2/FlowDiagram/useLayout.ts";
import { formatTypeName } from "@/composables/formatUtils.ts";

enum MessageType {
  Event = "Event message",
  Timeout = "Timeout message",
  Command = "Command message",
}

const store = useMessageStore();
const { state, conversationData } = storeToRefs(store);

async function getConversation(conversationId: string) {
  await store.loadConversation(conversationId);

  return conversationData.value.data;
}

class SagaInvocation {
  id: string;
  sagaType: string;
  isSagaCompleted: boolean;
  isSagaInitiated: boolean;

  constructor(saga: SagaInfo, message: Message) {
    const sagaIdHeader = getHeaderByKey(message, NServiceBusHeaders.SagaId);
    const originatedSagaIdHeader = getHeaderByKey(message, NServiceBusHeaders.OriginatingSagaId);
    this.id = saga.saga_id;
    this.sagaType = formatTypeName(saga.saga_type);
    this.isSagaCompleted = saga.change_status === "Completed";
    this.isSagaInitiated = sagaIdHeader === undefined && originatedSagaIdHeader !== undefined;
  }
}

interface NodeData {
  label: string;
  timeSent: string;
  messageId: string;
  sendingEndpoint: EndpointDetails;
  receivingEndpoint: EndpointDetails;
  isError: boolean;
  sagaInvocations: SagaInvocation[];
  isPublished: boolean;
  isTimeout: boolean;
  isCommand: boolean;
  isEvent: boolean;
  message: Message;
  type: MessageType;
}

class MessageNode implements Node<NodeData> {
  readonly id: string;
  readonly type: string;
  readonly data: NodeData;
  readonly position: XYPosition;
  readonly draggable: boolean;

  constructor(message: Message) {
    this.id = message.id;
    this.type = "message";
    this.position = { x: 0, y: 0 };
    this.draggable = false;

    const isPublished = message.message_intent === MessageIntent.Publish;
    const isTimeout = getHeaderByKey(message, NServiceBusHeaders.IsSagaTimeoutMessage)?.toLowerCase() === "true";
    const sagas = message.invoked_sagas ?? [];

    if (message.originates_from_saga) {
      sagas.push(message.originates_from_saga);
    }

    this.data = {
      label: message.message_type,
      timeSent: message.time_sent,
      messageId: message.message_id,
      sendingEndpoint: message.sending_endpoint,
      receivingEndpoint: message.receiving_endpoint,
      isError: message.status !== MessageStatus.Successful && message.status !== MessageStatus.ResolvedSuccessfully,
      sagaInvocations: sagas.map((saga) => new SagaInvocation(saga, message)),
      isPublished,
      isTimeout,
      isEvent: isPublished && !isTimeout,
      isCommand: !isPublished && !isTimeout,
      message,
      type: isPublished ? MessageType.Event : isTimeout ? MessageType.Timeout : MessageType.Command,
    };
  }
}

function constructNodes(messages: Message[]): Node<NodeData>[] {
  const messageMap = new Map();

  messages.forEach((message) => {
    if (!messageMap.has(message.id)) {
      messageMap.set(message.id, new MessageNode(message));
    }
  });

  return Array.from(messageMap.values());
}

function getHeaderByKey(message: Message, key: NServiceBusHeaders) {
  return message.headers.find((header) => header.key === key)?.value;
}

function constructEdges(nodes: Node<NodeData>[]): DefaultEdge[] {
  const edges: DefaultEdge[] = [];

  for (const node of nodes) {
    const message = node.data?.message;
    if (message === undefined) continue;

    const relatedTo = getHeaderByKey(message, NServiceBusHeaders.RelatedTo);
    if (!relatedTo && relatedTo !== message.message_id) {
      continue;
    }

    let parentMessages = nodes.filter((n) => {
      const m = n.data?.message;
      if (m === undefined) return false;
      return m.receiving_endpoint !== undefined && m.sending_endpoint !== undefined && m.message_id === relatedTo && m.receiving_endpoint.name === message.sending_endpoint.name;
    });

    if (parentMessages.length === 0) {
      parentMessages = nodes.filter((n) => {
        const m = n.data?.message;
        if (m === undefined) return false;
        return m.receiving_endpoint !== undefined && m.sending_endpoint !== undefined && m.message_id === relatedTo && m.message_intent !== MessageIntent.Publish;
      });

      if (parentMessages.length === 0) {
        console.debug(`Fall back to match only on RelatedToMessageId for message with Id '${message.message_id}' matched but link could be invalid.`);
      }
    }

    switch (parentMessages.length) {
      case 0:
        console.warn(
          `No parent could be resolved for the message with Id '${message.message_id}' which has RelatedToMessageId set. This can happen if the parent has been purged due to retention expiration, an ServiceControl node to be unavailable, or because the parent message not been stored (yet).`
        );
        break;
      case 1:
        // Log nothing, this is what it should be
        break;
      default:
        console.warn(`Multiple parents matched for message id '${message.message_id}' possibly due to more-than-once processing, linking to all as it is unknown which processing attempt generated the message.`);
        break;
    }

    for (const parentMessage of parentMessages) {
      edges.push(addConnection(parentMessage, node));
    }
  }

  return edges;
}

function addConnection(parentMessage: Node<NodeData>, childMessage: Node<NodeData>): DefaultEdge {
  return {
    id: `${parentMessage.id}##${childMessage.id}`,
    source: `${parentMessage.id}`,
    target: `${childMessage.id}`,
    markerEnd: MarkerType.ArrowClosed,
    style: {
      strokeDasharray: childMessage.data?.isPublished ? "5, 3" : "",
      strokeWidth: 2,
    },
  };
}

const nodes = ref<Node[]>([]);
const edges = ref<DefaultEdge[]>([]);
const { layout } = useLayout();
const { fitView } = useVueFlow();
const backLink = ref<string>(routeLinks.failedMessage.failedMessages.link);

onMounted(async () => {
  const back = useRouter().currentRoute.value.query.back as string;
  if (back) {
    backLink.value = back;
  }

  if (!state.value.data.conversation_id) return;

  const messages = await getConversation(state.value.data.conversation_id);

  nodes.value = constructNodes(messages);
  edges.value = constructEdges(nodes.value);
});

async function layoutGraph() {
  nodes.value = layout(nodes.value, edges.value);

  await nextTick(() => {
    if (store.state.data.id) {
      fitView({ nodes: [store.state.data.id], maxZoom: 0.9 });
    }
  });
}

const errorColor = hexToCSSFilter("#be514a").filter;
const selectedErrorColor = hexToCSSFilter("#e8e6e8").filter;
</script>

<template>
  <div class="gap">
    <div v-if="store.conversationData.failed_to_load" class="alert alert-info">FlowDiagram data is unavailable.</div>
    <LoadingSpinner v-else-if="store.conversationData.loading" />
    <div v-else id="tree-container">
      <VueFlow :nodes="nodes" :edges="edges" :min-zoom="0.1" :max-zoom="1.2" :only-render-visible-elements="true" @nodes-initialized="layoutGraph">
        <Controls :show-interactive="false" position="top-left" class="controls" />
        <template #node-message="{ id, data }: { id: string; data: NodeData }">
          <TextEllipses class="address" :text="`${data.sendingEndpoint.name}@${data.sendingEndpoint.host}`" />
          <div class="node" :class="{ error: data.isError, 'current-message': id === store.state.data.id }">
            <div class="node-text">
              <i
                class="fa"
                :style="data.isError && id === store.state.data.id ? { filter: selectedErrorColor } : {}"
                :class="{ 'pa-flow-timeout': data.isTimeout, 'pa-flow-command': data.isCommand, 'pa-flow-event': data.isEvent }"
                v-tippy="data.type"
              />
              <div class="typeName">
                <RouterLink v-if="data.isError" :to="{ path: routeLinks.messages.failedMessage.link(id), query: { back: backLink } }"><TextEllipses style="width: 204px" :text="data.label" ellipses-style="LeftSide" /></RouterLink>
                <RouterLink v-else :to="{ path: routeLinks.messages.successMessage.link(data.messageId, id), query: { back: backLink } }"><TextEllipses style="width: 204px" :text="data.label" ellipses-style="LeftSide" /></RouterLink>
              </div>
              <i v-if="data.isError" class="fa pa-flow-failed" :style="id !== store.state.data.id ? { filter: errorColor } : { filter: selectedErrorColor }" />
              <div class="time-sent">
                <time-since class="time-since" :date-utc="data.timeSent" />
              </div>
              <div class="sagas" v-if="data.sagaInvocations.length > 0">
                <div class="saga" v-for="saga in data.sagaInvocations" :key="saga.id">
                  <i
                    class="fa"
                    v-tippy="saga.isSagaInitiated ? 'Message originated from Saga' : !saga.isSagaInitiated && saga.isSagaCompleted ? 'Saga Completed' : 'Saga Initiated / Updated'"
                    :class="{ 'pa-flow-saga-initiated': saga.isSagaInitiated, 'pa-flow-saga-completed': !saga.isSagaInitiated && saga.isSagaCompleted, 'pa-flow-saga-trigger': !saga.isSagaInitiated && !saga.isSagaCompleted }"
                  />
                  <div class="sagaName"><TextEllipses style="width: 182px" :text="saga.sagaType" ellipses-style="LeftSide" /></div>
                </div>
              </div>
            </div>
          </div>
          <TextEllipses class="address" :text="`${data.receivingEndpoint.name}@${data.receivingEndpoint.host}`" />
        </template>
      </VueFlow>
    </div>
  </div>
</template>

<style>
@import "@vue-flow/core/dist/style.css";
@import "@vue-flow/core/dist/theme-default.css";
@import "@vue-flow/controls/dist/style.css";
</style>

<style scoped>
@import "../../list.css";

.gap {
  margin-top: 5px;
  border-radius: 0.5rem;
  padding: 0.5rem;
  border: 1px solid #ccc;
  background: white;
}
.controls {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
}

#tree-container {
  width: inherit;
  height: 70vh;
}

.sagas {
  background-color: #333333;
}

.saga {
  display: flex;
}

.sagaName {
  color: #e6e6e6;
}
.node {
  --vf-handle: var(--vf-node-color, #1a192b);
  --vf-box-shadow: var(--vf-node-color, #1a192b);
  background: var(--vf-node-bg);
  border-color: var(--vf-node-color, #1a192b);
  border-radius: 3px;
  font-size: 12px;
  border-width: 1px;
  border-style: solid;
  color: var(--vf-node-text);
  text-align: left;
}

.node {
  background-color: #fff;
  border-color: #cccbcc;
  border-width: 3px;
}

.node .error {
  border-color: #be514a;
}

.node text {
  font: 12px sans-serif;
}

.node .time-sent .time-since {
  margin-left: 20px;
  padding-top: 0;
  color: #262727;
  text-transform: capitalize;
}

.node-text {
  padding: 3px 8px 1px;
}

.node-text i {
  display: inline-block;
  position: relative;
  margin-right: 5px;
}

.node-text .typeName {
  display: inline-block;
  position: relative;
  font-weight: bold;
}

.node-text a {
  color: #777f7f;
}

.address {
  color: #777f7f;
  font-size: 0.8em;
  width: 264px;
}

.current-message {
  border-color: #cccbcc;
  background-color: #cccbcc !important;
}

.current-message.error {
  border-color: #be514a;
  background-color: #be514a !important;
}

.current-message.error .node-text .typeName a {
  color: #e8e6e8;
  font-weight: 900;
}

.current-message.error .node-text .time-since {
  color: #e8e6e8;
}

.error {
  border-color: #be514a;
}

.current-message.error .node-text a:hover {
  cursor: text;
  text-decoration: none;
}

.node-text a {
  color: #000;
}

.error .node-text a:hover {
  text-decoration: underline;
}

.pa-flow-failed {
  background-image: url("@/assets/failed-msg.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}

.pa-flow-saga-completed {
  background-image: url("@/assets/saga-completed.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}
.pa-flow-saga-initiated {
  background-image: url("@/assets/saga-initiated.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}
.pa-flow-saga-trigger {
  background-image: url("@/assets/saga-trigger.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}

.pa-flow-timeout {
  background-image: url("@/assets/timeout.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}

.pa-flow-event {
  background-image: url("@/assets/event.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}

.pa-flow-command {
  background-image: url("@/assets/command.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}
</style>
