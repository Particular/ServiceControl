<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { type DefaultEdge, MarkerType, VueFlow, type Styles, type Node } from "@vue-flow/core";
import TimeSince from "../TimeSince.vue";
import routeLinks from "@/router/routeLinks";
import Message from "@/resources/Message";
import { NServiceBusHeaders } from "@/resources/Header";
import { useRoute } from "vue-router";
import { ExtendedFailedMessage } from "@/resources/FailedMessage";
import { Controls } from "@vue-flow/controls";

const props = defineProps<{
  message: ExtendedFailedMessage;
}>();

enum MessageType {
  Event = "Event message",
  Timeout = "Timeout message",
  Command = "Command message",
}

interface MappedMessage {
  nodeName: string;
  id: string;
  messageId: string;
  sendingEndpoint: string;
  receivingEndpoint: string;
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
  level?: number;
  width?: number;
  XPos?: number;
}

const nodeSpacingX = 300;
const nodeSpacingY = 200;

const route = useRoute();

async function getConversation(conversationId: string) {
  const [, data] = await useTypedFetchFromServiceControl<Message[]>(`conversations/${conversationId}`);
  return data;
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
    sendingEndpoint: message.sending_endpoint?.name,
    receivingEndpoint: message.receiving_endpoint?.name,
    parentId,
    parentEndpoint,
    type,
    isError:
      message.headers.findIndex(function (x) {
        return x.key === NServiceBusHeaders.ExceptionInfoExceptionType;
      }) > -1,
    sagaName,
    link: {
      name: `Link ${message.id}`,
      nodeName: message.id,
    },
    timeSent: message.time_sent,
  };
}

function constructNodes(mappedMessages: MappedMessage[]): Node[] {
  return (
    mappedMessages
      //group by level
      .reduce((groups: MappedMessage[][], message: MappedMessage) => {
        groups[message.level!] = [...(groups[message.level!] ?? []), message];
        return groups;
      }, [])
      //ensure each level has their items in the same "grouped" order as the level above
      .map((group, level, messagesByLevel) => {
        const previousLevel = level > 0 ? messagesByLevel[level - 1] : null;
        return group.sort(
          (a, b) =>
            (previousLevel?.findIndex((plMessage) => a.parentId === plMessage.messageId && a.parentEndpoint === plMessage.receivingEndpoint) ?? 1) -
            (previousLevel?.findIndex((plMessage) => b.parentId === plMessage.messageId && b.parentEndpoint === plMessage.receivingEndpoint) ?? 1)
        );
      })
      //flatten to actual flow diagram nodes, with positioning based on parent node/level
      .flatMap((group, level, messagesByLevel) => {
        const previousLevel = level > 0 ? messagesByLevel[level - 1] : null;
        return group.reduce(
          ({ result, currentWidth, previousParent }, message) => {
            //position on current level needs to be based on parent Node, so see if one exists
            const parentMessage = previousLevel?.find((plMessage) => message.parentId === plMessage.messageId && message.parentEndpoint === plMessage.receivingEndpoint) ?? null;
            //if the current parent node is the same as the previous parent node, then the current position needs to be to the right of siblings
            const currentParentWidth = previousParent === parentMessage ? currentWidth : 0;
            const startX = parentMessage == null ? 0 : parentMessage.XPos! - parentMessage.width! / 2;
            //store the position of the node against the message, so child nodes can use it to determine their start position
            message.XPos = startX + (currentParentWidth + message.width! / 2);
            return {
              result: [
                ...result,
                {
                  id: `${message.messageId}##${message.receivingEndpoint}`,
                  type: "message",
                  data: message,
                  label: message.nodeName,
                  position: { x: message.XPos * nodeSpacingX, y: message.level! * nodeSpacingY },
                },
              ],
              currentWidth: currentParentWidth + message.width!,
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
      target: `${message.messageId}##${message.receivingEndpoint}`,
      markerEnd: MarkerType.ArrowClosed,
      style: {
        "stroke-dasharray": message.type === MessageType.Event && "5, 3",
      } as Styles,
    }));
}

const elements = ref<(Node | DefaultEdge)[]>([]);

onMounted(async () => {
  if (!props.message.conversationId) return;

  const messages = await getConversation(props.message.conversationId);
  const mappedMessages = messages.map(mapMessage);

  const assignDescendantLevelsAndWidth = (message: MappedMessage, level = 0) => {
    message.level = level;
    const children = mappedMessages.filter((mm) => mm.parentId === message.messageId && mm.parentEndpoint === message.receivingEndpoint);
    message.width =
      children.length === 0
        ? 1 //leaf node
        : children.map((child) => (child.width == null ? assignDescendantLevelsAndWidth(child, level + 1) : child)).reduce((sum, { width }) => sum + width!, 0);
    return message;
  };
  for (const root of mappedMessages.filter((message) => !message.parentId)) assignDescendantLevelsAndWidth(root);

  elements.value = [...constructNodes(mappedMessages), ...constructEdges(mappedMessages)];
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
</script>

<template>
  <div id="tree-container">
    <VueFlow v-model="elements" :min-zoom="0.1" :fit-view-on-init="true">
      <Controls />
      <template #node-message="nodeProps">
        <div class="node" :class="[nodeProps.data.isError && 'error', nodeProps.data.id === props.message.id && 'current-message']">
          <div class="node-text wordwrap">
            <i v-if="nodeProps.data.isError" class="fa pa-flow-failed" />
            <i class="fa" :class="typeIcon(nodeProps.data.type)" :title="nodeProps.data.type" />
            <div class="lead righ-side-ellipsis" :title="nodeProps.data.nodeName">
              <strong>
                <RouterLink v-if="nodeProps.data.isError" :to="{ path: routeLinks.messages.message.link(nodeProps.data.id), query: { back: route.path } }">{{ nodeProps.data.nodeName }}</RouterLink>
                <span v-else>{{ nodeProps.data.nodeName }}</span>
              </strong>
            </div>
            <span class="time-sent">
              <time-since class="time-since" :date-utc="nodeProps.data.timeSent" />
            </span>
            <template v-if="nodeProps.data.sagaName">
              <i class="fa pa-flow-saga" />
              <div class="saga lead righ-side-ellipsis" :title="nodeProps.data.sagaName">{{ nodeProps.data.sagaName }}</div>
            </template>
          </div>
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

#tree-container {
  width: 90vw;
  height: 60vh;
}

.node {
  --vf-handle: var(--vf-node-color, #1a192b);
  --vf-box-shadow: var(--vf-node-color, #1a192b);
  background: var(--vf-node-bg);
  border-color: var(--vf-node-color, #1a192b);
  padding: 10px;
  border-radius: 3px;
  font-size: 12px;
  text-align: center;
  border-width: 1px;
  border-style: solid;
  color: var(--vf-node-text);
  text-align: left;
}

.righ-side-ellipsis {
  direction: rtl;
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
  display: block;
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
  width: 204px;
  position: relative;
  top: 4px;
}

.error .node-text .lead,
.current-message.error .node-text .lead {
  width: 184px;
}

.node-text .lead.saga {
  font-weight: normal;
  width: 182px;
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
