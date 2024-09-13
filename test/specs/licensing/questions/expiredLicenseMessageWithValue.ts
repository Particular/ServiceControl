import { screen } from "@testing-library/vue";
export async function expiredLicenseMessageWithValue(message: string | RegExp): Promise<boolean> {
  const elm = await screen.findByText(message, { selector: "p" });
  return elm !== null;
}
