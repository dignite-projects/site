import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44315/',
  redirectUri: baseUrl,
  clientId: 'Host_App',
  responseType: 'code',
  scope: 'offline_access openid profile email phone Host',
  requireHttps: true,
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: 'Host',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44315',
      rootNamespace: 'Dignite.Site.Host',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
    SiteAdmin: {
      url: 'https://localhost:44315',
      rootNamespace: 'Dignite.Site.Admin',
    },
  },
} as Environment;
