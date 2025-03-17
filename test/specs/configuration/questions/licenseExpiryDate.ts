import { screen } from "@testing-library/vue";

export async function licenseExpiryDate() {
  const licenseExpiryDateElement = await screen.findByRole("note", { name: "license-expiry-date" });
  return licenseExpiryDateElement;
}
