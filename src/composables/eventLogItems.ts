import { useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";
import type EventLogItem from "@/resources/EventLogItem";

export async function getEventLogItems() {
  const [, data] = await useTypedFetchFromServiceControl<EventLogItem[]>("eventlogitems");

  return data;
}
