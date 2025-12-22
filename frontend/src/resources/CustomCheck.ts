import type EndpointDetails from "@/resources/EndpointDetails";

export default interface CustomCheck {
  id: string;
  custom_check_id: string;
  category: string;
  status: Status;
  reported_at: string;
  failure_reason: string;
  originating_endpoint: EndpointDetails;
}

export enum Status {
  Fail = "Fail",
  Pass = "Pass",
}
