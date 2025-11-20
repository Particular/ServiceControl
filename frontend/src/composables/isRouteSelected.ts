import { useLink, useRoute } from "vue-router";

export default function isRouteSelected(path: string) {
  const route = useRoute();
  const pathRoute = useLink({ to: path }).route.value;

  return route.matched.some((match) => match.name === pathRoute.name);
}
