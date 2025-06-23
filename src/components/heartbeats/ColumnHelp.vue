<template>
  <tippy max-width="400px">
    <i class="fa fa-sm fa-info-circle text-primary ps-1" />
    <template #content>
      <slot />
    </template>
  </tippy>
</template>
