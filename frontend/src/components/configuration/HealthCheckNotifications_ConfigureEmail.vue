<script setup lang="ts">
import { computed, ref } from "vue";
import type EmailSettings from "@/components/configuration/EmailSettings";
import type UpdateEmailNotificationsSettingsRequest from "@/resources/UpdateEmailNotificationsSettingsRequest";

const emit = defineEmits<{
  save: [settings: UpdateEmailNotificationsSettingsRequest];
  cancel: [];
}>();

const settings = defineProps<EmailSettings>();

const smtp_server = ref(settings.smtp_server);
const smtp_port = ref(settings.smtp_port);
const authentication_account = ref(settings.authentication_account);
const authentication_password = ref(settings.authentication_password);
const enable_tls = ref(settings.enable_tls);
const from = ref(settings.from);
const to = ref(settings.to);

const emailRe = /^(([^<>()[\]\\.,;:\s@"]+(\.[^<>()[\]\\.,;:\s@"]+)*)|(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/;

const smtpServerIsValid = computed(() => {
  return !!smtp_server.value;
});
const smtpPortIsValid = computed(() => {
  return !!(smtp_port.value ?? 0 > 0);
});
const fromIsValid = computed(() => {
  return !!(from.value && emailRe.test(from.value));
});
const toIsValid = computed(() => {
  return !!(to.value && validateMultipleEmailsCommaSeparated(to.value));
});
const formIsValid = computed(() => {
  return smtpServerIsValid.value && smtpPortIsValid.value && fromIsValid.value && toIsValid.value;
});

function validateMultipleEmailsCommaSeparated(value: string) {
  const result = value.split(",");
  return result.every((address) => emailRe.test(address));
}

function save() {
  const updatedSettings: UpdateEmailNotificationsSettingsRequest = {
    smtp_server: smtp_server.value,
    smtp_port: smtp_port.value ?? 0,
    authorization_account: authentication_account.value,
    authorization_password: authentication_password.value,
    enable_tls: enable_tls.value ?? false,
    from: from.value,
    to: to.value,
  };
  emit("save", updatedSettings);
}

function close() {
  emit("cancel");
}
</script>

<template>
  <div class="modal-mask">
    <div class="modal-wrapper">
      <div class="modal-container modal-content">
        <div class="modal-header">
          <h3 class="modal-title">Email configuration</h3>
        </div>

        <form name="notificationsForm" class="notificationsForm" novalidate @submit.prevent="save">
          <div class="modal-body">
            <div class="row">
              <div class="form-group" :class="{ 'has-error': !smtpServerIsValid }">
                <label for="smtpServerAddress">SMTP server address</label>
                <input type="text" id="smtpServerAddress" name="smtpServerAddress" v-model="smtp_server" class="form-control" required />
              </div>
              <div class="row"></div>
              <div class="form-group" :class="{ 'has-error': !smtpPortIsValid }">
                <label for="smtpServerPort">SMTP server port</label>
                <input type="number" id="smtpServerPort" name="smtpServerPort" v-model="smtp_port" class="form-control" required />
              </div>
              <div class="row"></div>
              <div class="form-group">
                <label for="account">Authentication account</label>
                <input type="text" id="account" name="account" v-model="authentication_account" class="form-control" />
              </div>
              <div class="row"></div>
              <div class="form-group">
                <label for="password">Authentication password</label>
                <input type="password" id="password" name="password" v-model="authentication_password" class="form-control" />
              </div>
              <div class="row"></div>
              <div class="form-group">
                <input type="checkbox" id="enableTLS" name="enableTLS" v-model="enable_tls" class="check-label" />
                <label for="enableTLS">Use TLS</label>
              </div>
              <div class="row"></div>
              <div class="form-group" :class="{ 'has-error': !fromIsValid }">
                <label for="from">From address</label>
                <input type="email" id="from" name="from" v-model="from" class="form-control" required />
              </div>
              <div class="row"></div>
              <div class="form-group" :class="{ 'has-error': !toIsValid }">
                <label for="to">To address <br />(Separate multiple email address with a comma. E.g. testing@test.com,testing2@test.com)</label>
                <input type="email" id="to" name="to" v-model="to" class="form-control" required />
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn btn-primary" type="submit" :disabled="!formIsValid">Save</button>
            <button type="button" class="btn btn-default" @click="close">Cancel</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "@/components/modal.css";

.modal-container {
  width: 800px;
  display: flex;
  flex-direction: column;
}

.notificationsForm {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}
</style>
