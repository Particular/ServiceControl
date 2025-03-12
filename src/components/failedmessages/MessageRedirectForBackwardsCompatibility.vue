<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import routeLinks from "@/router/routeLinks";

const route = useRoute();
const router = useRouter();
const id = route.params.id;

onMounted(async () => {
  await router.push({ path: routeLinks.messages.message.link(id.toString()), query: { back: routeLinks.failedMessage.failedMessages.link } });
});
</script>
