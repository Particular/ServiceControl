import { useFormatTime } from "@/composables/formatter.ts";

export function formatTypeName(type: string) {
  const clazz = type.split(",")[0];
  let objectName = clazz.split(".").pop() ?? "";
  objectName = objectName.replace("+", ".");
  return objectName;
}

export function formatDotNetTimespan(timespan: string) {
  const time = useFormatTime(dotNetTimespanToMilliseconds(timespan));
  return `${time.value} ${time.unit}`;
}

export function dotNetTimespanToMilliseconds(timespan: string) {
  //assuming if we have days in the timespan then something is very, very wrong
  const [hh, mm, ss] = timespan.split(":");
  return ((parseInt(hh) * 60 + parseInt(mm)) * 60 + parseFloat(ss)) * 1000;
}
