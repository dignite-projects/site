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
  production: true,
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
    // Dignite.FileExplorer's own HttpApi, mounted in-process in this same Host (GitHub issue #41's
    // follow-up). The Angular file-explorer picker's generated services hardcode apiName: 'FileExplorer'
    // (see FileExplorerRemoteServiceConsts.RemoteServiceName on the server) - this key has to match.
    FileExplorer: {
      url: 'https://localhost:44315',
      rootNamespace: 'Dignite.FileExplorer',
    },
  },
} as Environment;
