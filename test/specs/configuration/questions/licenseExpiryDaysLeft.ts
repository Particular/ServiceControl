import { screen } from "@testing-library/vue";

export async function licenseExpiryDaysLeft() {
  const licenseExpiryDaysLeftElement = await screen.findByRole("note", { name: "license-days-left" });
  return licenseExpiryDaysLeftElement;
}
