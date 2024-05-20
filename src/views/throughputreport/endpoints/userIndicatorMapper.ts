import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";

export const userIndicatorMapper = new Map<UserIndicator, string>([
  [UserIndicator.NServiceBusEndpoint, "NServiceBus Endpoint"],
  [UserIndicator.NServiceBusEndpointNoLongerInUse, "No longer in use"],
  [UserIndicator.SendOnlyOrTransactionSessionEndpoint, "SendOnly or Transactional Session Endpoint"],
  [UserIndicator.PlannedToDecommission, "Planned to be decommissioned"],
  [UserIndicator.NotNServiceBusEndpoint, "Not an NServiceBus Endpoint"],
]);