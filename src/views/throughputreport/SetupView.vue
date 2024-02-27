<script setup lang="ts">
enum Transport {
  AzureStorageQueue,
  NetStandardAzureServiceBus,
  LearningTransport,
  "RabbitMQ.ClassicConventionalRouting",
  "RabbitMQ.ClassicDirectRouting",
  "SQLServer",
  "AmazonSQS",
}

const transport: Transport = Transport["RabbitMQ.ClassicConventionalRouting"];

function displayTransportNameForInstructions() {
  switch (transport) {
    case Transport.AzureStorageQueue:
    case Transport.NetStandardAzureServiceBus:
      return "Azure";
    case Transport.LearningTransport:
      return "Learning Transport";
    case Transport["RabbitMQ.ClassicConventionalRouting"]:
    case Transport["RabbitMQ.ClassicDirectRouting"]:
      return "RabbitMQ";
    case Transport.SQLServer:
      return "Sql Server";
    case Transport.AmazonSQS:
      return "AWS";
  }
}
</script>

<template>
  <p>In order for ServicePulse to collect throughput data directly from {{ displayTransportNameForInstructions() }} you need to setup the following settings in ServiceControl.</p>
  <p>There are two options to set the settings, you can either set environment variables or alternative is to set it directly in the <code>ServiceControl.exe.config</code> file.</p>
  <div class="row">
    <div class="col">
      <p>Environment variables</p>
    </div>
    <div class="col"><p>App config settings</p></div>
  </div>
</template>

<style scoped></style>
