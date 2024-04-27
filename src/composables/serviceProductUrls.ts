import type Release from "@/resources/Release";

async function getData(url: string) {
  const response = await fetch(url);
  return (await response.json()) as unknown as Release[];
}

export async function useServiceProductUrls() {
  const spURL = "https://platformupdate.particular.net/servicepulse.txt";
  const scURL = "https://platformupdate.particular.net/servicecontrol.txt";

  const servicePulse = getData(spURL);
  const serviceControl = getData(scURL);

  const [sp, sc] = await Promise.all([servicePulse, serviceControl]);
  const latestSP = sp[0];
  const latestSC = sc[0];

  return { latestSP, latestSC };
}
