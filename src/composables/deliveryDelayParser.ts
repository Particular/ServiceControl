export function parseDeliveryDelay(delay: string): { days: number; hours: number; minutes: number; seconds: number } {
  // Split on period first to handle multi-digit days
  const parts = delay.split(".");
  let days = 0;
  let timeComponent = delay;

  if (parts.length > 1) {
    days = parseInt(parts[0], 10);
    timeComponent = parts[1];
  }

  const [hours, minutes, seconds] = timeComponent.split(":").map(Number);
  return { days, hours, minutes, seconds };
}

function getFriendly(time: number, text: string): string {
  return time > 0 ? `${time}${text}` : "";
}

export function getTimeoutFriendly(delivery_delay: string): string {
  const { days, hours, minutes, seconds } = parseDeliveryDelay(delivery_delay);

  return `${getFriendly(days, "d")}${getFriendly(hours, "h")}${getFriendly(minutes, "m")}${getFriendly(seconds, "s")}`;
}
