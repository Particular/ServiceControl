export interface DefaultConfig {
  default_route: string;
  version: string;
  service_control_url: string;
  monitoring_url: string;
  showPendingRetry: boolean;
}

let config: DefaultConfig | null = null;

export function setDefaultConfig(defaultConfig: DefaultConfig): void {
  config = defaultConfig;
}

export function getDefaultConfig(): DefaultConfig {
  if (!config) {
    throw new Error("defaultConfig has not been initialized");
  }
  return config;
}
