import RootUrls from "@/resources/RootUrls";
import { LicenseStatus } from "@/resources/LicenseInfo";
import { ServiceControlMonitoringInstance } from "@/resources/ServiceControlMonitoringInstance";

export const serviceControlMainInstance = <RootUrls>{
  description: "The management backend for the Particular Service Platform",
  endpoints_error_url: "http://localhost:33333/api/endpoints/{name}/errors/{?page,per_page,direction,sort}",
  known_endpoints_url: "/endpoints/known",
  endpoints_message_search_url: "http://localhost:33333/api/endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
  endpoints_messages_url: "http://localhost:33333/api/endpoints/{name}/messages/{?page,per_page,direction,sort}",
  audit_count_url: "http://localhost:33333/api/endpoints/{name}/audit-count",
  endpoints_url: "http://localhost:33333/api/endpoints",
  errors_url: "http://localhost:33333/api/errors/{?page,per_page,direction,sort}",
  configuration: "http://localhost:33333/api/configuration",
  remote_configuration: "http://localhost:33333/api/configuration/remotes",
  message_search_url: "http://localhost:33333/api/messages/search/{keyword}/{?page,per_page,direction,sort}",
  license_status: LicenseStatus.Valid,
  license_details: "http://localhost:33333/api/license",
  name: "ServiceControl",
  sagas_url: "http://localhost:33333/api/sagas",
  event_log_items: "http://localhost:33333/api/eventlogitems",
  archived_groups_url: "http://localhost:33333/api/errors/groups/{classifier?}",
  get_archive_group: "http://localhost:33333/api/archive/groups/id/{groupId}",
};

export const monitoredInstanceTemplate = <ServiceControlMonitoringInstance>{
  instanceType: "monitoring",
  version: "5.0.0-alpha.2",
};
