import axios from 'axios';

const API_BASE_URL = 'http://localhost:5289/api';
const S3_BASE_URL = 'http://localhost:5289/s3';
const CONTRACT_BASE_URL = 'http://localhost:5290/api/contracts';

const api = axios.create({
  baseURL: API_BASE_URL,
});

const contractApi = axios.create({
  baseURL: CONTRACT_BASE_URL,
});

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
  listBuckets: () => api.get('/buckets'),
  createBucket: (name) => api.post(`/buckets/${name}`),
  deleteBucket: (name) => api.delete(`/buckets/${name}`),
  
  // Policies
  getBucketPolicy: (name) => api.get(`/buckets/${name}/policy`),
  setBucketPolicy: (name, policy) => api.put(`/buckets/${name}/policy`, JSON.stringify(policy), {
    headers: { 'Content-Type': 'application/json' }
  }),
  
  // Objects
  listObjects: (bucket) => api.get(`/buckets/${bucket}/objects`),
  deleteObject: (bucket, key) => api.delete(`/buckets/${bucket}/objects/${key}`),
  
  // S3 Native Upload (Emulator)
  uploadObject: (bucket, key, file) => {
    return axios.put(`${S3_BASE_URL}/${bucket}/${key}`, file, {
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
