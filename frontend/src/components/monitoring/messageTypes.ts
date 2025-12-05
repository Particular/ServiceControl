import type { ExtendedMessageType, MessageType, MessageTypeDetails } from "@/resources/MonitoringEndpoint";

function shortenTypeName(typeName: string): string {
  return typeName.split(".").pop() ?? typeName;
}

function parseTheMessageTypeData(messageType: MessageType): ExtendedMessageType {
  if (messageType.typeName.indexOf(";") > 0) {
    const messageTypeHierarchy = messageType.typeName.split(";").map((item) => {
      const segments = item.split(",");
      const messageTypeDetails: MessageTypeDetails = {
        typeName: segments[0],
        assemblyName: segments[1],
        assemblyVersion: segments[2].substring(segments[2].indexOf("=") + 1),
      };

      if (!segments[4]?.endsWith("=null")) {
        //SC monitoring fills culture only if PublicKeyToken is filled
        messageTypeDetails.culture = segments[3];
        messageTypeDetails.publicKeyToken = segments[4];
      }
      return messageTypeDetails;
    });
    return {
      ...messageType,
      messageTypeHierarchy,
      typeName: messageTypeHierarchy.map((item) => item.typeName).join(", "),
      shortName: messageTypeHierarchy.map((item) => shortenTypeName(item.typeName)).join(", "),
      containsTypeHierarchy: true,
      tooltipText: messageTypeHierarchy.reduce(
        (sum, item) => (sum ? `${sum}\n ` : "") + `${item.typeName} |${item.assemblyName}-${item.assemblyVersion}` + (item.culture ? ` |${item.culture}` : "") + (item.publicKeyToken ? ` |${item.publicKeyToken}` : ""),
        ""
      ),
    };
  }
  const cultureSuffix = messageType.culture && messageType.culture !== "null" ? ` | Culture=${messageType.culture}` : "";
  const publicKeyTokenSuffix = messageType.publicKeyToken && messageType.publicKeyToken !== "null" ? ` | PublicKeyToken=${messageType.publicKeyToken}` : "";

  return {
    ...messageType,
    shortName: shortenTypeName(messageType.typeName),
    tooltipText: `${messageType.typeName} | ${messageType.assemblyName}-${messageType.assemblyVersion}${cultureSuffix}${publicKeyTokenSuffix}`,
  };
}

export default class MessageTypes {
  totalItems: number;
  data: ExtendedMessageType[];

  constructor(rawMessageTypes: MessageType[]) {
    this.totalItems = rawMessageTypes.length;
    this.data = rawMessageTypes
      // filter out system message types
      .filter((mt) => mt.id && mt.typeName)
      .map((mt) => parseTheMessageTypeData(mt))
      .sort((a, b) => a.typeName.localeCompare(b.typeName));
  }
}
