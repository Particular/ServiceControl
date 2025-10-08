import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref, watch } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";
import { Transport } from "@/views/throughputreport/transport";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";
import isThroughputSupported from "@/views/throughputreport/isThroughputSupported";

export const useThroughputStore = defineStore("ThroughputStore", () => {
  const testResults = ref<ConnectionTestResults | null>(null);
  const refresh = async () => {
    if (isThroughputSupported.value) {
      testResults.value = await throughputClient.test();
    }
  };

  const hasErrors = computed(() => {
    // if it is a broker transport, we return true if connection test is unsuccessful
    if (isBrokerTransport.value && !testResults.value?.broker_connection_result.connection_successful) {
      return true;
    }

    // if Audit connection test fails, we will return true.
    // the connection test will return true if there are no Audit instances configured.
    if (!testResults.value?.audit_connection_result.connection_successful) {
      return true;
    }

    // if Monitoring is enabled, we return whatever the value of the connection test
    if (useIsMonitoringEnabled()) {
      return !testResults.value?.monitoring_connection_result.connection_successful;
    }

    // Last but not least we assume no errors
    return false;
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
      case Transport.PostgreSQL:
        return "PostgreSQL";
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
      case Transport.PostgreSQL:
        return "https://docs.particular.net/servicepulse/usage-config#connection-setup-postgresql";
    }
  };

  watch(isThroughputSupported, (value) => {
    if (value) {
      refresh();
    }
  });

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
