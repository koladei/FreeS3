import axios from 'axios';

const DEFAULT_GATEWAY_ORIGIN = 'http://localhost:5270';
const API_ORIGIN = typeof window !== 'undefined' && window.location.port !== '5174'
  ? window.location.origin
  : DEFAULT_GATEWAY_ORIGIN;

const AUTH_BASE_URL = `${API_ORIGIN}/api/auth`;
const API_BASE_URL = `${API_ORIGIN}/api`;
const S3_BASE_URL = `${API_ORIGIN}/s3`;
const CONTRACT_BASE_URL = `${API_ORIGIN}/api/contracts`;

// Create separate axios instances
const authClient = axios.create({
  baseURL: AUTH_BASE_URL,
  withCredentials: true,
});

const s3Api = axios.create({
  baseURL: S3_BASE_URL,
  withCredentials: true,
});

const contractApi = axios.create({
  baseURL: CONTRACT_BASE_URL,
  withCredentials: true,
});

// Auth API functions
export const auth = {
  register: (username, email, password, firstName, lastName) =>
    authClient.post('/register-user', {
      username,
      email,
      password,
      firstName,
      lastName,
    }),

  login: (username, password) =>
    authClient.post('/login', {
      username,
      password,
    }),

  refreshToken: (token, refreshToken) =>
    authClient.post('/refresh-token', {
      token,
      refreshToken,
    }),

  logout: () => authClient.post('/logout'),

  getSession: () =>
    authClient.get('/session'),
};

const isPdfAsset = (file, key = '') => {
  const fileType = (file?.type || '').toLowerCase();
  if (fileType === 'application/pdf') {
    return true;
  }

  const normalizedKey = (key || file?.name || '').toLowerCase();
  return normalizedKey.endsWith('.pdf');
};

export const storageApi = {
  // Buckets
  listBuckets: () => s3Api.get('/buckets'),
  listAccessibleBuckets: () => s3Api.get('/buckets/access'),
  createBucket: (name) => s3Api.post(`/buckets/${name}`),
  deleteBucket: (name) => s3Api.delete(`/buckets/${name}`),
  listBucketShares: (name) => s3Api.get(`/buckets/${name}/shares`),
  listIncomingShares: () => s3Api.get('/buckets/shares/incoming'),
  acknowledgeShare: (name) => s3Api.post(`/buckets/${name}/shares/acknowledge`),
  shareBucketWithEmail: (name, email, permission, expiresAt = null) => s3Api.post(`/buckets/${name}/shares`, { email, permission, expiresAt }),
  unshareBucketWithEmail: (name, email) => s3Api.delete(`/buckets/${name}/shares`, { params: { email } }),
  listObjectShares: (name, key) => s3Api.get(`/buckets/${name}/object-shares`, { params: { key } }),
  listIncomingObjectShares: () => s3Api.get('/buckets/object-shares/incoming'),
  shareObjectWithEmail: (name, key, email, expiresAt = null) => s3Api.post(`/buckets/${name}/object-shares`, { key, email, expiresAt }),
  unshareObjectWithEmail: (name, key, email) => s3Api.delete(`/buckets/${name}/object-shares`, { params: { key, email } }),
  
  // Policies
  getBucketPolicy: (name) => s3Api.get(`/buckets/${name}/policy`),
  setBucketPolicy: (name, policy) => s3Api.put(`/buckets/${name}/policy`, JSON.stringify(policy), {
    headers: { 'Content-Type': 'application/json' }
  }),
  
  // Objects
  listObjects: (bucket) => s3Api.get(`/buckets/${bucket}/objects`),
  deleteObject: (bucket, key) => s3Api.delete(`/buckets/${bucket}/objects/${key}`),
  
  // S3 Native Upload (Emulator)
  uploadObject: (bucket, key, file) => {
    return axios.put(`${S3_BASE_URL}/${bucket}/${key}`, file, {
      withCredentials: true,
      headers: {
        'Content-Type': file.type || 'application/octet-stream',
      },
    });
  },

  // Upload flow that also registers PDF templates in ContractService.
  uploadDocument: async (bucket, key, file, metadata = {}) => {
    const uploadResponse = await storageApi.uploadObject(bucket, key, file);

    if (!isPdfAsset(file, key)) {
      return {
        upload: uploadResponse.data,
        contractTemplate: null,
      };
    }

    const contractResponse = await contractApi.post('/templates', {
      bucket,
      objectKey: key,
      fileName: file.name || key,
      contentType: file.type || 'application/pdf',
      title: metadata.title || file.name || key,
      authorId: metadata.authorId || null,
    });

    return {
      upload: uploadResponse.data,
      contractTemplate: contractResponse.data,
    };
  },

  // ContractService contract workflow endpoints
  registerPdfAsContract: (bucket, key, fileName, metadata = {}) =>
    contractApi.post('/templates', {
      bucket,
      objectKey: key,
      fileName,
      contentType: 'application/pdf',
      title: metadata.title || fileName,
      authorId: metadata.authorId || null,
    }),
  getContractTemplate: (templateId) =>
    contractApi.get(`/templates/${templateId}`),
  createContractPlaceholder: (templateId, placeholder) =>
    contractApi.post(`/templates/${templateId}/placeholders`, placeholder),
  updateContractPlaceholder: (templateId, placeholderId, placeholder) =>
    contractApi.put(`/templates/${templateId}/placeholders/${placeholderId}`, placeholder),
  deleteContractPlaceholder: (templateId, placeholderId) =>
    contractApi.delete(`/templates/${templateId}/placeholders/${placeholderId}`),
  listContractPlaceholders: (templateId) =>
    contractApi.get(`/templates/${templateId}/placeholders`),
  createContractInstance: (payload) =>
    contractApi.post('/instances', payload),
  getContractInstance: (instanceId) =>
    contractApi.get(`/instances/${instanceId}`),
  submitContractFieldValue: (instanceId, payload) =>
    contractApi.post(`/instances/${instanceId}/field-values`, payload),
  finalizeContractInstance: (instanceId, payload) =>
    contractApi.post(`/instances/${instanceId}/finalize`, payload),
  
  // Download URL
  getDownloadUrl: (bucket, key) => `${S3_BASE_URL}/${bucket}/${key}`,
};
