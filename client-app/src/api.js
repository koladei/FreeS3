import axios from 'axios';

const API_BASE_URL = 'http://localhost:5289/api';
const S3_BASE_URL = 'http://localhost:5289/s3';

const api = axios.create({
  baseURL: API_BASE_URL,
});

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
  
  // Download URL
  getDownloadUrl: (bucket, key) => `${S3_BASE_URL}/${bucket}/${key}`,
};
