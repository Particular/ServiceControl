import { patchToServiceControl, postToServiceControl } from "./serviceServiceControlUrls";

export async function useUnarchiveMessage(ids: string[]) {
  const response = await patchToServiceControl("errors/unarchive/", ids);
  if (!response.ok) {
    throw new Error(response.statusText);
  }
}

export async function useArchiveMessage(ids: string[]) {
  const response = await patchToServiceControl("errors/archive/", ids);
  if (!response.ok) {
    throw new Error(response.statusText);
  }
}

export async function useRetryMessages(ids: string[]) {
  await postToServiceControl("errors/retry", ids);
}
