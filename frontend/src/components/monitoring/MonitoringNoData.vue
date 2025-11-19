<script setup lang="ts"></script>

<template>
  <div class="row">
    <div class="col-sm-12">
      <div class="row">
        <div class="col-sm-12">
          <div class="row">
            <div class="col-sm-12">
              <div class="text-center monitoring-no-data">
                <h1>No monitoring data available</h1>
                <p>The monitoring service is active but no data is being returned.</p>
                <p>This may be due to one or more of the following causes:</p>
                <ul>
                  <li>No endpoints running.</li>
                  <li>No endpoints with <strong>monitoring plugin</strong> enabled.</li>
                  <li>Endpoints sending data to incorrect queue or monitoring server listening.</li>
                </ul>
                <div class="action-toolbar">
                  <a class="btn btn-default btn-primary" href="https://docs.particular.net/monitoring/metrics/" target="_blank">Learn how to enable endpoint monitoring</a>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./endpoint.css";
</style>
