import { ServiceControlStore, useServiceControlStore } from "@/stores/ServiceControlStore";
import type GroupOperation from "@/resources/GroupOperation";

// eslint-disable-next-line @typescript-eslint/no-empty-object-type
export interface SuccessResponse {}
export interface ErrorResponse {
  message: string;
}

class MessageGroupClient {
  serviceControlStore: ServiceControlStore;
  constructor() {
    //this module is only called from within view setup or other pinia stores, so this call is lifecycle safe
    this.serviceControlStore = useServiceControlStore();
  }

  public async getExceptionGroups(classifier: string = "") {
    const [, data] = await this.serviceControlStore.fetchTypedFromServiceControl<GroupOperation[]>(`recoverability/groups/${classifier}`);
    return data;
  }

  public async getExceptionGroupsForEndpoint(classifier: string = "", classiferFilter = "") {
    const [, data] = await this.serviceControlStore.fetchTypedFromServiceControl<GroupOperation[]>(`recoverability/groups/${classifier}?classifierFilter=${classiferFilter}`);
    return data;
  }

  //delete note by group id
  public async deleteNote(groupId: string) {
    return this.evaluateResponse(await this.serviceControlStore.deleteFromServiceControl(`recoverability/groups/${groupId}/comment`));
  }

  //edit or create note by group id
  public async editOrCreateNote(groupId: string, comment: string) {
    return this.evaluateResponse(await this.serviceControlStore.postToServiceControl(`recoverability/groups/${groupId}/comment?comment=${comment}`));
  }

  //archive exception group by group id
  //archiveGroup
  public async archiveExceptionGroup(groupId: string) {
    return this.evaluateResponse(await this.serviceControlStore.postToServiceControl(`recoverability/groups/${groupId}/errors/archive`));
  }

  //restore group by group id
  public async restoreGroup(groupId: string) {
    return this.evaluateResponse(await this.serviceControlStore.postToServiceControl(`recoverability/groups/${groupId}/errors/unarchive`));
  }

  //retry exception group by group id
  //retryGroup
  public async retryExceptionGroup(groupId: string) {
    return this.evaluateResponse(await this.serviceControlStore.postToServiceControl(`recoverability/groups/${groupId}/errors/retry`));
  }

  //acknowledge archive exception group by group id
  public async acknowledgeArchiveGroup(groupId: string) {
    return this.evaluateResponse(await this.serviceControlStore.deleteFromServiceControl(`recoverability/unacknowledgedgroups/${groupId}`));
  }

  evaluateResponse(response: Response): SuccessResponse | ErrorResponse {
    return response.ok ? ({} as SuccessResponse) : ({ message: response.statusText } as ErrorResponse);
  }

  public isError(obj: SuccessResponse | ErrorResponse): obj is ErrorResponse {
    return (obj as ErrorResponse).message !== undefined;
  }
}

export default () => new MessageGroupClient();
