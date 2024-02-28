<script setup lang="ts">
import { setupData } from "@/views/throughputreport/randomData";

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

function test() {}
</script>

<template>
  <div class="intro">
    <p>In order for ServicePulse to collect throughput data directly from {{ displayTransportNameForInstructions() }} you need to setup the following settings in ServiceControl.</p>
    <p>There are two options to set the settings, you can either set environment variables or alternative is to set it directly in the <code>ServiceControl.exe.config</code> file.</p>
    <p>For more information read this documentation.</p>
  </div>
  <div class="row">
    <div class="card">
      <div class="card-body">
        <h5 class="card-title">List of settings required</h5>
        <ul class="card-text settingsList">
          <li v-for="item in setupData" :key="item.name">
            <div>
              <strong>{{ item.name }}</strong>
            </div>
            <p>{{ item.description }}</p>
          </li>
        </ul>
      </div>
    </div>
  </div>
  <div class="row"><button class="btn btn-primary actions" type="button" @click="test">Test Connection</button></div>
</template>

<style scoped>
.settingsList {
  list-style: none;
  padding-left: 0;
}

.intro {
  margin: 10px 0;
}

.actions {
  margin: 10px 0;
  width: 200px;
}
</style>
