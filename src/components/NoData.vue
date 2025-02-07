<script setup lang="ts">
const props = defineProps<{
  message: string;
}>();
</script>

<template>
  <div class="row">
    <div class="col-sm-12">
      <div class="row box box-no-click">
        <div class="col-sm-12">
          <div class="row box-header">
            <div class="col-sm-12">
              <p class="lead hard-wrap" role="status">{{ props.message }}</p>
              <slot>
                <p>&nbsp;</p>
              </slot>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "@/components/list.css";
</style>
