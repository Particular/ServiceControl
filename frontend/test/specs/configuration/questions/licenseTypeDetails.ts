import { screen } from "@testing-library/vue";

export async function licenseTypeDetails() {
  const licenseType = await screen.findByRole("note", { name: "license-type" });
  return licenseType.textContent;
}
