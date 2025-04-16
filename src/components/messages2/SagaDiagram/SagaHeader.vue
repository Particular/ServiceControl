<script setup lang="ts">
import { RouterLink } from "vue-router";

defineProps<{
  sagaTitle: string;
  sagaGuid: string;
  messageIdUrl: string;
}>();
</script>

<template>
  <div class="block">
    <div class="row row--center">
      <div class="cell cell--center">
        <div class="cell-inner cell-inner-center">
          <RouterLink :to="messageIdUrl">← Back to Messages</RouterLink>
          <h1 aria-label="saga name" class="main-title">{{ sagaTitle }}</h1>
          <div>
            <b>guid</b> <span role="note" aria-label="saga guid">{{ sagaGuid }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.block {
  /* empty block class for component structure */
}

.row {
  display: flex;
}

.row--center {
  justify-content: center;
}

.cell--center {
  width: 50%;
  background-color: #f2f2f2;
  border: 0;
}

.cell-inner-center {
  padding: 0.5rem;
}

.main-title {
  margin: 0.3rem 0;
  padding-bottom: 0.5rem;
  border-bottom: solid 2px #00a3c4;
  font-size: 1.5rem;
}
</style>
