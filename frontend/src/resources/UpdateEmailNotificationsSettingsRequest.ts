export default interface UpdateEmailNotificationsSettingsRequest {
  smtp_server: string;
  smtp_port: number;
  authorization_account: string;
  authorization_password: string;
  enable_tls: boolean;
  to: string;
  from: string;
}
