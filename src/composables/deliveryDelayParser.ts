export function parseDeliveryDelay(delay: string): { hours: number; minutes: number; seconds: number } {
  const [hours, minutes, seconds] = delay.split(":").map(Number);
  return { hours, minutes, seconds };
}

function getFriendly(time: number, text: string): string {
  return time > 0 ? `${time}${text}` : "";
}

export function getTimeoutFriendly(delivery_delay: string): string {
  const { hours, minutes, seconds } = parseDeliveryDelay(delivery_delay);

  return `${getFriendly(hours, "h")}${getFriendly(minutes, "m")}${getFriendly(seconds, "s")}`;
}
