<script setup lang="ts">
withDefaults(
  defineProps<{
    options?: number[];
  }>(),
  { options: () => [20, 35, 50, 75] }
);

const current = defineModel<number>({ default: 50 });
</script>

<template>
  <div class="pagination col-md-2">
    <div class="dropdown">
      <label class="control-label">Items Per Page:</label>
      <button type="button" class="btn btn-default dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
        {{ current }}
        <span class="caret" />
      </button>
      <ul class="dropdown-menu">
        <li v-for="option of options" :key="option">
          <a @click.prevent="current = option" href="#">{{ option }}</a>
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped>
ul.dropdown-menu li a span {
  color: #aaa;
}

.btn.sp-btn-menu:active,
.btn-default.sp-btn-menu:active,
.btn-default.sp-btn-menu.active,
.open > .dropdown-toggle.btn-default.sp-btn-menu {
  background: none;
  border: none;
  color: var(--sp-blue);
  text-decoration: underline;
  box-shadow: none;
}
</style>
