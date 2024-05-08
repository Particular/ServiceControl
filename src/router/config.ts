import DashboardView from "@/views/DashboardView.vue";
import type { RouteComponent } from "vue-router";
import FailedMessagesView from "@/views/FailedMessagesView.vue";
import MonitoringView from "@/views/MonitoringView.vue";
import EventsView from "@/views/EventsView.vue";
import ConfigurationView from "@/views/ConfigurationView.vue";
import routeLinks from "@/router/routeLinks";
import CustomChecksView from "@/views/CustomChecksView.vue";
import HeartbeatsView from "@/views/HeartbeatsView.vue";
import ThroughputReportView from "@/views/ThroughputReportView.vue";

export interface RouteItem {
  path: string;
  alias?: string;
  redirect?: string;
  title: string;
  component: RouteComponent | (() => Promise<RouteComponent>);
  children?: RouteItem[];
}

const config: RouteItem[] = [
  {
    path: routeLinks.dashboard,
    component: DashboardView,
    title: "Dashboard",
  },
  {
    path: routeLinks.heartbeats.root,
    component: HeartbeatsView,
    title: "Heartbeats",
    redirect: routeLinks.heartbeats.inactive.link,
    children: [
      {
        title: "Inactive Endpoints",
        path: routeLinks.heartbeats.inactive.link,
        component: () => import("@/components/heartbeats/InactiveEndpoints.vue"),
      },
      {
        title: "Active Endpoints",
        path: routeLinks.heartbeats.active.link,
        component: () => import("@/components/heartbeats/ActiveEndpoints.vue"),
      },
      {
        title: "Heartbeat Configuration",
        path: routeLinks.heartbeats.configuration.link,
        component: () => import("@/components/heartbeats/HeartbeatConfiguration.vue"),
      },
    ],
  },
  {
    path: routeLinks.failedMessage.root,
    component: FailedMessagesView,
    title: "Failed Messages",
    redirect: routeLinks.failedMessage.failedMessagesGroups.link,
    children: [
      {
        title: "Failed Message Groups",
        path: routeLinks.failedMessage.failedMessagesGroups.template,
        component: () => import("@/components/failedmessages/FailedMessageGroups.vue"),
      },
      {
        path: routeLinks.failedMessage.failedMessages.template,
        title: "All Failed Messages",
        component: () => import("@/components/failedmessages/FailedMessages.vue"),
      },
      {
        path: routeLinks.failedMessage.deletedMessagesGroup.template,
        title: "Deleted Message Groups",
        component: () => import("@/components/failedmessages/DeletedMessageGroups.vue"),
      },
      {
        path: routeLinks.failedMessage.deletedMessages.template,
        title: "All Deleted Messages",
        component: () => import("@/components/failedmessages/DeletedMessages.vue"),
      },
      {
        path: routeLinks.failedMessage.pendingRetries.template,
        title: "Pending Retries",
        component: () => import("@/components/failedmessages/PendingRetries.vue"),
      },
      {
        title: "Failed Messages",
        path: routeLinks.failedMessage.group.template,
        component: () => import("@/components/failedmessages/FailedMessages.vue"),
      },
      {
        title: "Deleted Messages",
        path: routeLinks.failedMessage.deletedGroup.template,
        component: () => import("@/components/failedmessages/DeletedMessages.vue"),
      },
      {
        path: routeLinks.failedMessage.message.template,
        title: "Message",
        component: () => import("@/components/failedmessages/MessageView.vue"),
      },
    ],
  },
  {
    path: routeLinks.monitoring.root,
    component: MonitoringView,
    title: "Monitored Endpoints",
  },
  {
    path: routeLinks.monitoring.endpointDetails.template,
    component: () => import("@/components/monitoring/EndpointDetails.vue"),
    title: "Endpoint Details",
  },
  {
    path: routeLinks.customChecks,
    title: "Custom checks",
    component: CustomChecksView,
  },
  {
    path: routeLinks.events,
    component: EventsView,
    title: "Events",
  },
  {
    path: routeLinks.throughput.root,
    component: ThroughputReportView,
    title: "Throughput",
    children: [
      {
        title: "Endpoints",
        path: routeLinks.throughput.endpoints.template,
        component: () => import("@/views/throughputreport/EndpointsView.vue"),
      },
      {
        title: "Setup",
        path: routeLinks.throughput.setup.root,
        redirect: routeLinks.throughput.setup.setupConnection.link,
        component: () => import("@/views/throughputreport/SetupView.vue"),
        children: [
          {
            title: "Connection Setup",
            path: routeLinks.throughput.setup.setupConnection.template,
            component: () => import("@/views/throughputreport/setup/ConnectionSetupView.vue"),
          },
          {
            title: "Mask Report Data",
            path: routeLinks.throughput.setup.mask.template,
            component: () => import("@/views/throughputreport/setup/MasksView.vue"),
          },
          {
            title: "Diagnostics",
            path: routeLinks.throughput.setup.diagnostics.template,
            component: () => import("@/views/throughputreport/setup/DiagnosticsView.vue"),
          }
        ]
      },
    ],
  },
  {
    path: routeLinks.configuration.root,
    title: "Configuration",
    component: ConfigurationView,
    redirect: routeLinks.configuration.license.link,
    children: [
      {
        title: "License",
        path: routeLinks.configuration.license.template,
        component: () => import("@/components/configuration/PlatformLicense.vue"),
      },
      {
        title: "Health Check Notifications",
        path: routeLinks.configuration.healthCheckNotifications.template,
        component: () => import("@/components/configuration/HealthCheckNotifications.vue"),
      },
      {
        title: "Retry Redirects",
        path: routeLinks.configuration.retryRedirects.template,
        component: () => import("@/components/configuration/RetryRedirects.vue"),
      },
      {
        title: "Connections",
        path: routeLinks.configuration.connections.template,
        component: () => import("@/components/configuration/PlatformConnections.vue"),
      },
      {
        title: "Endpoint Connection",
        path: routeLinks.configuration.endpointConnection.template,
        component: () => import("@/components/configuration/EndpointConnection.vue"),
      },
    ],
  },
];

export default config;
