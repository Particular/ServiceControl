export enum RetryType {
  Unknown = "Unknown",
  SingleMessage = "SingleMessage",
  FailureGroup = "FailureGroup",
  MultipleMessages = "MultipleMessages",
  AllForEndpoint = "AllForEndpoint",
  All = "All",
  ByQueueAddress = "ByQueueAddress",
}
