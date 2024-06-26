import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";
import useAutoRefresh from "@/composables/autoRefresh";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";
import { Transport } from "@/views/throughputreport/transport";

export const useThroughputStore = defineStore("ThroughputStore", () => {
  const testResults = ref<ConnectionTestResults | null>(null);
  const refresh = () => dataRetriever.executeAndResetTimer();
  const hasErrors = computed(() => {
    if (isBrokerTransport) {
      return !testResults.value?.broker_connection_result.connection_successful;
    }

    return !(testResults.value?.audit_connection_result.connection_successful || testResults.value?.monitoring_connection_result.connection_successful);
  });
  const transport = computed(() => {
    if (testResults.value == null) {
      return Transport.None;
    }

    return testResults.value.transport as Transport;
  });
  const isBrokerTransport = computed(() => {
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
  const transportNameForInstructions = () => {
    switch (transport.value) {
      case Transport.AzureStorageQueue:
        return "Azure Storage Queue";
      case Transport.NetStandardAzureServiceBus:
        return "Azure Service Bus";
      case Transport.MSMQ:
        return "MSMQ";
      case Transport.LearningTransport:
        return "Learning Transport";
      case Transport.RabbitMQ:
        return "RabbitMQ";
      case Transport.SQLServer:
        return "Sql Server";
      case Transport.AmazonSQS:
        return "Amazon SQS";
    }
  };
  const transportDocsLinkForInstructions = () => {
    switch (transport.value) {
      case Transport.AzureStorageQueue:
      case Transport.LearningTransport:
      case Transport.MSMQ:
        return "https://docs.particular.net/servicepulse/usage-config#connection-setup-msmq-azure-storage-queues";
      case Transport.NetStandardAzureServiceBus:
        return "https://docs.particular.net/servicepulse/usage-config#connection-setup-azure-service-bus";      
      case Transport.RabbitMQ:
        return "https://docs.particular.net/servicepulse/usage-config#connection-setup-rabbitmq";
      case Transport.SQLServer:
        return "https://docs.particular.net/servicepulse/usage-config#connection-setup-sqlserver";
      case Transport.AmazonSQS:
        return "https://docs.particular.net/servicepulse/usage-config#connection-setup-amazon-sqs";
    }
  };
  const dataRetriever = useAutoRefresh(
    async () => {
      testResults.value = await throughputClient.test();
    },
    60 * 60 * 1000 /* 1 hour */
  );

  refresh();

  return {
    testResults,
    refresh,
    transportNameForInstructions,
    transportDocsLinkForInstructions,
    isBrokerTransport,
    hasErrors,
    transport,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useThroughputStore, import.meta.hot));
}

export type ThroughputStore = ReturnType<typeof useThroughputStore>;
