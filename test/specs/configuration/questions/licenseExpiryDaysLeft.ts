import { screen } from "@testing-library/vue";

export async function licenseExpiryDaysLeft() {
  //TODO: determine why timeout had to be increased
  const licenseExpiryDaysLeftElement = await screen.findByRole("note", { name: "license-days-left" }, { timeout: 5000 });
  return licenseExpiryDaysLeftElement;
}
