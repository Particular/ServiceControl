import moment from "moment";

const secondDuration = moment.duration(1000);
const minuteDuration = moment.duration(60 * 1000);
const hourDuration = moment.duration(60 * 60 * 1000); //this ensures that we never use minute formatting
const dayDuration = moment.duration(24 * 60 * 60 * 1000);

export interface ValueWithUnit {
  value: string;
  unit: string;
}

export function useFormatTime(value?: number): ValueWithUnit {
  const time = { value: "0", unit: "ms" };
  if (value) {
    const duration = moment.duration(value);
    if (duration >= dayDuration) {
      time.value = formatTimeValue(duration.days()) + " d " + formatTimeValue(duration.hours()) + " hrs";
    } else if (duration >= hourDuration) {
      time.value = formatTimeValue(duration.hours(), true) + ":" + formatTimeValue(duration.minutes(), true);
      time.unit = "hr";
    } else if (duration >= minuteDuration) {
      time.value = formatTimeValue(duration.minutes()) + ":" + formatTimeValue(duration.seconds());
      time.unit = "min";
    } else if (duration >= secondDuration) {
      time.value = formatTimeValue(duration.seconds());
      time.unit = "sec";
    } else {
      time.value = formatTimeValue(duration.asMilliseconds());
      time.unit = "ms";
    }
  }

  return time;
}

export function useGetDayDiffFromToday(value: string) {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const diff = new Date(value.replace("Z", "")).getTime() - today.getTime();
  return Math.round(diff / 1000 / 60 / 60 / 24);
}

export function useFormatLargeNumber(num: number, decimals: number) {
  const suffixes = ["k", "M", "G", "T", "P", "E"];

  if (isNaN(num)) {
    return "";
  }

  if (num < 1000000) {
    return round(num, decimals).toLocaleString();
  }

  const exp = Math.floor(Math.log(num) / Math.log(1000));

  return `${round(num / Math.pow(1000, exp), decimals).toLocaleString()}${suffixes[exp - 1]}`;
}

function round(num: number, decimals: number) {
  return Number(num.toFixed(decimals));
}

function formatTimeValue(timeValue: number, displayTwoDigits = false) {
  const strValue = Math.floor(timeValue);
  return `${displayTwoDigits ? ("0" + strValue).slice(-2) : strValue.toLocaleString()}`;
}
