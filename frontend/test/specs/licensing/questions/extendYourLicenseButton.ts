import { screen } from "@testing-library/vue";
export async function extendYourLicenseButton() {
  const elm = await screen.findByRole("link", { name: "Extend your license" });
  return {
    address: elm.getAttribute("href"),
  };
}
