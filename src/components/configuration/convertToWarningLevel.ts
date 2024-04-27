import { LicenseWarningLevel } from "@/composables/LicenseStatus";
import { WarningLevel } from "@/components/WarningLevel";

export default function convertToWarningLevel(level: LicenseWarningLevel) {
  switch (level) {
    case LicenseWarningLevel.None:
      return WarningLevel.None;
    case LicenseWarningLevel.Warning:
      return WarningLevel.Warning;
    case LicenseWarningLevel.Danger:
      return WarningLevel.Danger;
  }
}
