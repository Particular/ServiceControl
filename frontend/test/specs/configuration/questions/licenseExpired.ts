import { screen } from "@testing-library/vue";

export async function licenseExpired() {
  const licenseExpiredText = await screen.findByRole("note", { name: "license-expired" });
  return licenseExpiredText.textContent?.trim();
}
