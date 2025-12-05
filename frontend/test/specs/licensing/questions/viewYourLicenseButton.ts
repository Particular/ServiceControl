import { screen } from "@testing-library/vue";
export function viewYourLicenseButton() {
  const elm = screen.getByRole("link", { name: "View license details" });
  return {
    address: elm.getAttribute("href"),
  };
}
