export default interface EmailSettings {
  enabled: boolean | null;
  enable_tls: boolean | null;
  smtp_server: string;
  smtp_port: number | null;
  authentication_account: string;
  authentication_password: string;
  from: string;
  to: string;
}
