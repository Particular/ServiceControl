import { computed, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";

export enum Transport {
  None = "None",
  MSMQ = "MSMQ",
  AzureStorageQueue = "AzureStorageQueue",
  NetStandardAzureServiceBus = "NetStandardAzureServiceBus",
  LearningTransport = "LearningTransport",
  RabbitMQ = "RabbitMQ",
  SQLServer = "SQLServer",
  AmazonSQS = "AmazonSQS",
}

const testResults = ref<ConnectionTestResults | null>(null);
throughputClient.test().then(value => testResults.value = value);

export const transport = computed(() => {
  if (testResults.value == null) {
    return Transport.None;
  }

  return testResults.value.transport as Transport;
});

export const isBrokerTransport = computed(() => {
  switch (transport.value) {
    case Transport.None:
    case Transport.MSMQ:
    case Transport.AzureStorageQueue:
    case Transport.LearningTransport:
      return false;
    default:
      return true;
  }
});

export function transportNameForInstructions() {
  switch (transport.value) {
    case Transport.AzureStorageQueue:
    case Transport.NetStandardAzureServiceBus:
      return "Azure";
    case Transport.LearningTransport:
      return "Learning Transport";
    case Transport.RabbitMQ:
      return "RabbitMQ";
    case Transport.SQLServer:
      return "Sql Server";
    case Transport.AmazonSQS:
      return "AWS";
  }
}