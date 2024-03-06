<script setup lang="ts">
function generateReport() {}
</script>

<template>
  <div class="box">
    <div class="row">
      <div class="col-6">
        <label class="form-label">Mask sensitive data</label>
        <textarea class="form-control" rows="3"></textarea>
        <div class="form-text">Masks sensitive information in the generated report. One word per line.</div>
      </div>
      <div class="col-6 text-end"><button class="btn btn-primary actions" type="button" @click="generateReport">Generate Report</button></div>
    </div>
  </div>
  <div class="extra-info">
    <p>For MSMQ, ask the user if they have more endpoints that do not have audit/monitoring turned on, and if so to also send us a screen shot of their MSMQqueues in addition to the report.</p>
  </div>
</template>

<style scoped>
.extra-info {
  margin: 15px 0;
}
</style>
