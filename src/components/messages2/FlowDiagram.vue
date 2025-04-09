<script setup lang="ts">
import { onMounted, ref } from "vue";
import { type DefaultEdge, MarkerType, type Node, type Styles, VueFlow } from "@vue-flow/core";
import TimeSince from "../TimeSince.vue";
import routeLinks from "@/router/routeLinks";
import Message, { MessageStatus } from "@/resources/Message";
import { NServiceBusHeaders } from "@/resources/Header";
import { ControlButton, Controls } from "@vue-flow/controls";
import { useMessageStore } from "@/stores/MessageStore";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import { storeToRefs } from "pinia";
import EndpointDetails from "@/resources/EndpointDetails.ts";
import { hexToCSSFilter } from "hex-to-css-filter";
import TextEllipses from "@/components/TextEllipses.vue";

enum MessageType {
  Event = "Event message",
  Timeout = "Timeout message",
  Command = "Command message",
}

interface MappedMessage {
  nodeName: string;
  id: string;
  messageId: string;
  sendingEndpoint: EndpointDetails;
  receivingEndpoint: EndpointDetails;
  parentId: string;
  parentEndpoint: string;
  type: MessageType;
  isError: boolean;
  sagaName: string;
  link: {
    name: string;
    nodeName: string;
  };
  timeSent: string;
  level: number;
  width: number;
  XPos: number;
}

const nodeSpacingX = 300;
const nodeSpacingY = 200;

const store = useMessageStore();
const { state } = storeToRefs(store);

async function getConversation(conversationId: string) {
  await store.loadConversation(conversationId);

  return store.conversationData.data;
}

function mapMessage(message: Message): MappedMessage {
  let parentId = "",
    parentEndpoint = "",
    sagaName = "";
  const header = message.headers.find((header) => header.key === NServiceBusHeaders.RelatedTo);
  if (header) {
    parentId = header.value ?? "";
    parentEndpoint = message.headers.find((h) => h.key === "NServiceBus.OriginatingEndpoint")?.value ?? "";
  }

  const sagaHeader = message.headers.find((header) => header.key === NServiceBusHeaders.OriginatingSagaType);
  if (sagaHeader) {
    sagaName = sagaHeader.value?.split(", ")[0] ?? "";
  }

  const type = (() => {
    if (message.headers.find((header) => header.key === NServiceBusHeaders.MessageIntent)?.value === "Publish") return MessageType.Event;
    else if (message.headers.find((header) => header.key === NServiceBusHeaders.IsSagaTimeoutMessage)?.value?.toLowerCase() === "true") return MessageType.Timeout;
    return MessageType.Command;
  })();

  return {
    nodeName: message.message_type,
    id: message.id,
    messageId: message.message_id,
    sendingEndpoint: message.sending_endpoint,
    receivingEndpoint: message.receiving_endpoint,
    parentId,
    parentEndpoint,
    type,
    isError: message.status !== MessageStatus.Successful && message.status !== MessageStatus.ResolvedSuccessfully,
    sagaName,
    level: 0,
    width: 0,
    XPos: 0,
    link: {
      name: `Link ${message.id}`,
      nodeName: message.id,
    },
    timeSent: message.time_sent,
  };
}

function constructNodes(mappedMessages: MappedMessage[]): Node<MappedMessage>[] {
  return (
    mappedMessages
      //group by level
      .reduce((groups: MappedMessage[][], message: MappedMessage) => {
        groups[message.level] = [...(groups[message.level] ?? []), message];
        return groups;
      }, [])
      //ensure each level has their items in the same "grouped" order as the level above
      .map((group, level, messagesByLevel) => {
        const previousLevel = level > 0 ? messagesByLevel[level - 1] : null;
        return group.sort(
          (a, b) =>
            (previousLevel?.findIndex((plMessage) => a.parentId === plMessage.messageId && a.parentEndpoint === plMessage.receivingEndpoint.name) ?? 1) -
            (previousLevel?.findIndex((plMessage) => b.parentId === plMessage.messageId && b.parentEndpoint === plMessage.receivingEndpoint.name) ?? 1)
        );
      })
      //flatten to actual flow diagram nodes, with positioning based on parent node/level
      .flatMap((group, level, messagesByLevel) => {
        const previousLevel = level > 0 ? messagesByLevel[level - 1] : null;
        return group.reduce(
          ({ result, currentWidth, previousParent }, message) => {
            //position on current level needs to be based on parent Node, so see if one exists
            const parentMessage = previousLevel?.find((plMessage) => message.parentId === plMessage.messageId && message.parentEndpoint === plMessage.receivingEndpoint.name) ?? null;
            //if the current parent node is the same as the previous parent node, then the current position needs to be to the right of siblings
            const currentParentWidth = previousParent === parentMessage ? currentWidth : 0;
            const startX = parentMessage == null ? 0 : parentMessage.XPos - parentMessage.width / 2;
            //store the position of the node against the message, so child nodes can use it to determine their start position
            message.XPos = startX + (currentParentWidth + message.width / 2);
            return {
              result: [
                ...result,
                {
                  id: `${message.messageId}##${message.receivingEndpoint.name}`,
                  type: "message",
                  data: message,
                  label: message.nodeName,
                  position: { x: message.XPos * nodeSpacingX, y: message.level * nodeSpacingY },
                },
              ],
              currentWidth: currentParentWidth + message.width,
              previousParent: parentMessage,
            };
          },
          { result: [] as Node[], currentWidth: 0, previousParent: null as MappedMessage | null }
        ).result;
      })
  );
}

function constructEdges(mappedMessages: MappedMessage[]): DefaultEdge[] {
  return mappedMessages
    .filter((message) => message.parentId)
    .map((message) => ({
      id: `${message.parentId}##${message.messageId}`,
      source: `${message.parentId}##${message.parentEndpoint}`,
      target: `${message.messageId}##${message.receivingEndpoint.name}`,
      markerEnd: MarkerType.ArrowClosed,
      style: {
        "stroke-dasharray": message.type === MessageType.Event && "5, 3",
      } as Styles,
    }));
}

const elements = ref<(Node | DefaultEdge)[]>([]);

onMounted(async () => {
  if (!state.value.data.conversation_id) return;

  const messages = await getConversation(state.value.data.conversation_id);
  const mappedMessages = messages.map(mapMessage);

  const assignDescendantLevelsAndWidth = (message: MappedMessage, level = 0) => {
    message.level = level;
    const children = mappedMessages.filter((mm) => mm.parentId === message.messageId && mm.parentEndpoint === message.receivingEndpoint.name);
    message.width =
      children.length === 0
        ? 1 //leaf node
        : children.map((child) => (child.width === 0 ? assignDescendantLevelsAndWidth(child, level + 1) : child)).reduce((sum, { width }) => sum + width, 0);
    return message;
  };
  for (const root of mappedMessages.filter((message) => !message.parentId)) {
    assignDescendantLevelsAndWidth(root);
  }

  const nodes = constructNodes(mappedMessages);
  const edges = constructEdges(nodes.map((n) => n.data as MappedMessage));

  elements.value = [...nodes, ...edges];
});

function typeIcon(type: MessageType) {
  switch (type) {
    case MessageType.Timeout:
      return "pa-flow-timeout";
    case MessageType.Event:
      return "pa-flow-event";
    default:
      return "pa-flow-command";
  }
}

const showAddress = ref(false);

function toggleAddress() {
  showAddress.value = !showAddress.value;
}

const blackColor = hexToCSSFilter("#000000").filter;
const greenColor = hexToCSSFilter("#00c468").filter;
</script>

<template>
  <div v-if="store.conversationData.failed_to_load" class="alert alert-info">FlowDiagram data is unavailable.</div>
  <LoadingSpinner v-else-if="store.conversationData.loading" />
  <div v-else id="tree-container">
    <VueFlow v-model="elements" :min-zoom="0.1" :fit-view-on-init="true" :only-render-visible-elements="true">
      <Controls position="top-left" class="controls">
        <ControlButton v-tippy="showAddress ? `Hide endpoints` : `Show endpoints`" @click="toggleAddress">
          <i class="fa pa-flow-endpoint" :style="{ filter: showAddress ? greenColor : blackColor }"></i>
        </ControlButton>
      </Controls>
      <template #node-message="{ data }: { data: MappedMessage }">
        <div v-if="showAddress">
          <TextEllipses class="address" :text="`${data.sendingEndpoint.name}@${data.sendingEndpoint.host}`" />
        </div>
        <div class="node" :class="{ error: data.isError, 'current-message': data.id === store.state.data.id }">
          <div class="node-text">
            <i v-if="data.isError" class="fa pa-flow-failed" />
            <i class="fa" :class="typeIcon(data.type)" v-tippy="data.type" />
            <div class="lead">
              <strong>
                <RouterLink v-if="data.isError" :to="{ path: routeLinks.messages.failedMessage.link(data.id) }"><TextEllipses style="width: 204px" :text="data.nodeName" ellipses-style="LeftSide" /></RouterLink>
                <RouterLink v-else :to="{ path: routeLinks.messages.successMessage.link(data.messageId, data.id) }"><TextEllipses style="width: 204px" :text="data.nodeName" ellipses-style="LeftSide" /></RouterLink>
              </strong>
            </div>
            <div class="time-sent">
              <time-since class="time-since" :date-utc="data.timeSent" />
            </div>
            <template v-if="data.sagaName">
              <i class="fa pa-flow-saga" />
              <div class="saga lead"><TextEllipses style="width: 182px" :text="data.sagaName" ellipses-style="LeftSide" /></div>
            </template>
          </div>
        </div>
        <div v-if="showAddress">
          <TextEllipses class="address" :text="`${data.receivingEndpoint.name}@${data.receivingEndpoint.host}`" />
        </div>
      </template>
    </VueFlow>
  </div>
</template>

<style>
@import "@vue-flow/core/dist/style.css";
@import "@vue-flow/core/dist/theme-default.css";
@import "@vue-flow/controls/dist/style.css";
</style>

<style scoped>
@import "../list.css";

.controls {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
}

#tree-container {
  width: 90vw;
  height: 60vh;
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
  border-color: red;
}

.node text {
  font: 12px sans-serif;
}

.node .time-sent .time-since {
  margin-left: 20px;
  padding-top: 0;
  color: #777f7f;
  text-transform: capitalize;
}

.node-text {
  padding: 3px 8px 1px;
}

.node-text i {
  display: inline-block;
  position: relative;
  top: -1px;
  margin-right: 5px;
  filter: brightness(0) saturate(100%) invert(0%) sepia(0%) saturate(0%) hue-rotate(346deg) brightness(104%) contrast(104%);
}

.node-text .lead {
  display: inline-block;
  position: relative;
  top: 4px;
}

.error .node-text .lead,
.current-message.error .node-text .lead {
  width: 184px;
}

.node-text .lead.saga {
  font-weight: normal;
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

.current-message.error .node-text,
.current-message .node-text .lead {
  color: #fff !important;
}

.error .node-text i:not(.pa-flow-saga) {
  filter: brightness(0) saturate(100%) invert(46%) sepia(9%) saturate(4493%) hue-rotate(317deg) brightness(81%) contrast(82%);
}

.current-message.error .node-text i {
  color: #fff;
  filter: brightness(0) saturate(100%) invert(100%) sepia(0%) saturate(7475%) hue-rotate(21deg) brightness(100%) contrast(106%);
}

.current-message.error .node-text strong {
  color: #fff;
}

.current-message.error .node-text .time-sent .time-since {
  color: #ffcecb !important;
}

.error {
  border-color: #be514a;
}

.current-message.error .node-text a {
  color: #fff;
}

.current-message.error .node-text a:hover {
  cursor: text;
  text-decoration: none;
}

.node-text a {
  color: #000;
}

.error .node-text a {
  color: #be514a;
}

.error .node-text .time-sent .time-since {
  color: #be514a;
}

.error .node-text .lead.saga {
  color: #be514a;
}

.error .node-text a:hover {
  text-decoration: underline;
}

.pa-flow-endpoint {
  background-image: url("@/assets/endpoint.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}

.pa-flow-failed {
  background-image: url("@/assets/failed-msg.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
}

.pa-flow-saga {
  background-image: url("@/assets/saga.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 15px;
  width: 15px;
  margin-left: 20px;
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

path.link {
  fill: none;
  stroke: #ccc;
  stroke-width: 2px;
}
</style>
