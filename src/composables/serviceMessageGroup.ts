import { deleteFromServiceControl, postToServiceControl, useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";
import type GroupOperation from "@/resources/GroupOperation";
import type FailureGroupView from "@/resources/FailureGroupView";

export async function useGetExceptionGroups(classifier: string = "") {
  const [, data] = await useTypedFetchFromServiceControl<GroupOperation[]>(`recoverability/groups/${classifier}`);
  return data;
}

export async function useGetExceptionGroupsForEndpoint(classifier: string = "", classiferFilter = "") {
  const [, data] = await useTypedFetchFromServiceControl<GroupOperation[]>(`recoverability/groups/${classifier}?classifierFilter=${classiferFilter}`);
  return data;
}

//get all deleted message groups
export async function useGetArchiveGroups(classifier: string = "") {
  const [, data] = await useTypedFetchFromServiceControl<FailureGroupView[]>(`errors/groups/${classifier}`);
  return data;
}

//delete note by group id
export async function useDeleteNote(groupId: string) {
  return evaluateResponse(await deleteFromServiceControl(`recoverability/groups/${groupId}/comment`));
}

//edit or create note by group id
export async function useEditOrCreateNote(groupId: string, comment: string) {
  return evaluateResponse(await postToServiceControl(`recoverability/groups/${groupId}/comment?comment=${comment}`));
}

//archive exception group by group id
//archiveGroup
export async function useArchiveExceptionGroup(groupId: string) {
  return evaluateResponse(await postToServiceControl(`recoverability/groups/${groupId}/errors/archive`));
}

//restore group by group id
export async function useRestoreGroup(groupId: string) {
  return evaluateResponse(await postToServiceControl(`recoverability/groups/${groupId}/errors/unarchive`));
}

//retry exception group by group id
//retryGroup
export async function useRetryExceptionGroup(groupId: string) {
  return evaluateResponse(await postToServiceControl(`recoverability/groups/${groupId}/errors/retry`));
}

//acknowledge archive exception group by group id
export async function useAcknowledgeArchiveGroup(groupId: string) {
  return evaluateResponse(await deleteFromServiceControl(`recoverability/unacknowledgedgroups/${groupId}`));
}

function evaluateResponse(response: Response): SuccessResponse | ErrorResponse {
  return response.ok ? ({} as SuccessResponse) : ({ message: response.statusText } as ErrorResponse);
}

// eslint-disable-next-line @typescript-eslint/no-empty-object-type
export interface SuccessResponse {}
export interface ErrorResponse {
  message: string;
}
export function isError(obj: SuccessResponse | ErrorResponse): obj is ErrorResponse {
  return (obj as ErrorResponse).message !== undefined;
}
