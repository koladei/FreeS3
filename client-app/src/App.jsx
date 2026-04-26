import React, { useState, useEffect, useCallback } from 'react';
import {
  Database,
  Folder,
  File,
  Upload,
  Trash2,
  Download,
  Eye,
  Plus,
  X,
  RefreshCw,
  HardDrive,
  Clock,
  Layers,
  Search,
  Shield,
  Code,
  Check,
  FileText,
  FilePlus,
  LogOut,
  User,
} from 'lucide-react';
import { storageApi } from './api';
import ContractManagerModal from './ContractManagerModal';
import { useAuth } from './AuthContext';
import Login from './Login';
import Register from './Register';

const POLICY_PRESETS = {
  private: {
    Version: "2012-10-17",
    Statement: [{ Effect: "Deny", Principal: "*", Action: "s3:*", Resource: "arn:aws:s3:::*" }]
  },
  publicRead: {
    Version: "2012-10-17",
    Statement: [{ Effect: "Allow", Principal: "*", Action: ["s3:GetObject"], Resource: "arn:aws:s3:::*/*" }]
  },
  fullAccess: {
    Version: "2012-10-17",
    Statement: [{ Effect: "Allow", Principal: "*", Action: "s3:*", Resource: "arn:aws:s3:::*" }]
  }
};

const App = () => {
  const { isAuthenticated, loading, user, logout } = useAuth();
  const getAuthViewFromPath = useCallback(() => {
    const path = window.location.pathname.toLowerCase();
    return path === '/register' ? 'register' : 'login';
  }, []);

  const [currentView, setCurrentView] = useState(() => getAuthViewFromPath()); // 'app', 'login', 'register'

  const [buckets, setBuckets] = useState([]);
  const [selectedBucket, setSelectedBucket] = useState(null);
  const [objects, setObjects] = useState([]);
  const [appLoading, setAppLoading] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isPolicyModalOpen, setIsPolicyModalOpen] = useState(false);
  const [policyTarget, setPolicyTarget] = useState(null);
  const [policyJson, setPolicyJson] = useState('');
  const [newBucketName, setNewBucketName] = useState('');
  const [uploadProgress, setUploadProgress] = useState(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [previewObject, setPreviewObject] = useState(null);
  // map of objectKey -> contractTemplate (populated on upload or manual register)
  const [contractTemplates, setContractTemplates] = useState({});
  const [contractTarget, setContractTarget] = useState(null);

  const navigateToAuthView = useCallback((view) => {
    const path = view === 'register' ? '/register' : '/login';
    setCurrentView(view);
    if (window.location.pathname !== path) {
      window.history.pushState({}, '', path);
    }
  }, []);

  useEffect(() => {
    const onPopState = () => {
      if (!isAuthenticated) {
        setCurrentView(getAuthViewFromPath());
      }
    };

    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, [getAuthViewFromPath, isAuthenticated]);

  // Redirect based on auth state and requested path.
  useEffect(() => {
    if (loading) {
      return;
    }

    if (isAuthenticated) {
      setCurrentView('app');
      if (window.location.pathname !== '/') {
        window.history.replaceState({}, '', '/');
      }
      return;
    }

    setCurrentView(getAuthViewFromPath());
  }, [isAuthenticated, loading, getAuthViewFromPath]);

  const fetchBuckets = async () => {
    try {
      const response = await storageApi.listBuckets();
      setBuckets(response.data);
      if (response.data.length > 0 && !selectedBucket) {
        setSelectedBucket(response.data[0]);
      }
    } catch (error) {
      console.error('Failed to fetch buckets', error);
    }
  };

  const fetchObjects = useCallback(async (bucket) => {
    if (!bucket) return;
     setAppLoading(true);
    try {
      const response = await storageApi.listObjects(bucket);
      setObjects(response.data);
    } catch (error) {
      console.error('Failed to fetch objects', error);
    } finally {
       setAppLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated || currentView !== 'app') {
      return;
    }

    fetchBuckets();
  }, [isAuthenticated, currentView]);

  useEffect(() => {
    if (isAuthenticated && currentView === 'app' && selectedBucket) {
      fetchObjects(selectedBucket);
    }
  }, [isAuthenticated, currentView, selectedBucket, fetchObjects]);

  const handleCreateBucket = async (e) => {
    e.preventDefault();
    if (!newBucketName) return;
    try {
      await storageApi.createBucket(newBucketName);
      setNewBucketName('');
      setIsCreateModalOpen(false);
      fetchBuckets();
    } catch (error) {
      alert('Failed to create bucket');
    }
  };

  const handleDeleteBucket = async (name) => {
    if (!confirm(`Are you sure you want to delete bucket "${name}" and all its contents?`)) return;
    try {
      await storageApi.deleteBucket(name);
      if (selectedBucket === name) setSelectedBucket(null);
      fetchBuckets();
    } catch (error) {
      alert('Failed to delete bucket');
    }
  };

  const openPolicyModal = async (bucket) => {
    setPolicyTarget(bucket);
    try {
      const response = await storageApi.getBucketPolicy(bucket);
      setPolicyJson(JSON.stringify(JSON.parse(response.data), null, 2));
      setIsPolicyModalOpen(true);
    } catch (error) {
      setPolicyJson('{}');
      setIsPolicyModalOpen(true);
    }
  };

  const handleSavePolicy = async () => {
    try {
      const parsed = JSON.parse(policyJson);
      await storageApi.setBucketPolicy(policyTarget, parsed);
      setIsPolicyModalOpen(false);
    } catch (error) {
      alert('Invalid JSON policy format');
    }
  };

  const applyPreset = (preset) => {
    setPolicyJson(JSON.stringify(POLICY_PRESETS[preset], null, 2));
  };

  const handleDeleteObject = async (key) => {
    if (!confirm(`Delete object "${key}"?`)) return;
    try {
      await storageApi.deleteObject(selectedBucket, key);
      fetchObjects(selectedBucket);
    } catch (error) {
      alert('Failed to delete object');
    }
  };

  const isPdfObject = (obj) =>
    obj.contentType === 'application/pdf' || obj.key.toLowerCase().endsWith('.pdf');

  const handleFileUpload = async (e) => {
    const file = e.target.files[0];
    if (!file || !selectedBucket) return;

    setUploadProgress(0);
    try {
      const result = await storageApi.uploadDocument(selectedBucket, file.name, file);
      if (result.contractTemplate) {
        setContractTemplates((prev) => ({
          ...prev,
          [file.name]: result.contractTemplate,
        }));
      }
      fetchObjects(selectedBucket);
    } catch (error) {
      alert('Upload failed');
    } finally {
      setUploadProgress(null);
    }
  };

  const handleRegisterContract = async (obj) => {
    try {
      const res = await storageApi.registerPdfAsContract(selectedBucket, obj.key, obj.key, {
        title: obj.key,
      });
      setContractTemplates((prev) => ({ ...prev, [obj.key]: res.data }));
      setContractTarget(res.data);
    } catch (err) {
      alert(err.response?.data || 'Failed to register contract template.');
    }
  };

  const openContractManager = (obj) => {
    const tpl = contractTemplates[obj.key];
    if (tpl) {
      setContractTarget(tpl);
    } else {
      handleRegisterContract(obj);
    }
  };

  const filteredObjects = objects.filter(obj =>
    (obj.key || '').toLowerCase().includes(searchQuery.toLowerCase())
  );

  const formatSize = (bytes) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const getPreviewType = (contentType) => {
    if (!contentType) return 'unsupported';

    const normalized = contentType.toLowerCase();
    if (normalized.startsWith('image/')) return 'image';
    if (normalized === 'application/pdf') return 'pdf';
    if (normalized.startsWith('text/')
      || normalized === 'application/json'
      || normalized === 'application/xml'
      || normalized === 'application/javascript') return 'text';
    if (normalized.startsWith('audio/')) return 'audio';
    if (normalized.startsWith('video/')) return 'video';

    return 'unsupported';
  };

  const canPreview = (contentType) => getPreviewType(contentType) !== 'unsupported';

  const openPreview = async (obj) => {
    const url = storageApi.getDownloadUrl(selectedBucket, obj.key);
    const previewType = getPreviewType(obj.contentType);

    if (previewType === 'text') {
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load text preview');
        const text = await response.text();
        setPreviewObject({ ...obj, previewType, url, text });
      } catch (error) {
        alert('Unable to load file preview.');
      }
      return;
    }

    setPreviewObject({ ...obj, previewType, url });
  };

  const closePreview = () => setPreviewObject(null);

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-600 mx-auto mb-4"></div>
          <p className="text-gray-600 font-medium">Loading...</p>
        </div>
      </div>
    );
  }

  if (currentView === 'login') {
    return (
      <Login
        onLoginSuccess={() => {
          setCurrentView('app');
          window.history.replaceState({}, '', '/');
        }}
        onShowRegister={() => navigateToAuthView('register')}
      />
    );
  }

  if (currentView === 'register') {
    return (
      <Register
        onRegisterSuccess={() => navigateToAuthView('login')}
        onBackToLogin={() => navigateToAuthView('login')}
      />
    );
  }

  // Main App - only shown when authenticated
  if (!isAuthenticated) {
    return (
      <Login
        onLoginSuccess={() => {
          setCurrentView('app');
          window.history.replaceState({}, '', '/');
        }}
        onShowRegister={() => navigateToAuthView('register')}
      />
    );
  }

  return (
    <div className="flex h-screen bg-premium-dark text-slate-200 overflow-hidden">
      {/* Sidebar */}
      <aside className="w-72 glass border-r border-premium-border flex flex-col">
        <div className="p-6 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-blue-600 flex items-center justify-center shadow-lg shadow-blue-500/20">
            <Layers className="text-white w-6 h-6" />
          </div>
          <div>
            <h1 className="font-bold text-lg leading-tight">DMS</h1>
            <p className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Local Storage</p>
          </div>
        </div>

        <div className="px-4 mb-4">
          <button
            onClick={() => setIsCreateModalOpen(true)}
            className="w-full py-2.5 px-4 bg-premium-card hover:bg-premium-border border border-premium-border rounded-xl flex items-center justify-center gap-2 transition-all duration-200 group"
          >
            <Plus className="w-4 h-4 text-blue-500 group-hover:scale-125 transition-transform" />
            <span className="text-sm font-medium">New Bucket</span>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-2 space-y-1 custom-scrollbar">
          {buckets.map((bucket) => (
            <div
              key={bucket}
              onClick={() => setSelectedBucket(bucket)}
              className={`group flex items-center justify-between p-3 rounded-xl cursor-pointer transition-all ${selectedBucket === bucket
                  ? 'bg-blue-600/10 text-blue-400 border border-blue-500/20'
                  : 'hover:bg-premium-card text-slate-400 border border-transparent'
                }`}
            >
              <div className="flex items-center gap-3 overflow-hidden">
                <Folder className={`w-5 h-5 flex-shrink-0 ${selectedBucket === bucket ? 'text-blue-500' : 'text-slate-500'}`} />
                <span className="text-sm font-medium truncate">{bucket}</span>
              </div>
              <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-all">
                <button
                  onClick={(e) => { e.stopPropagation(); openPolicyModal(bucket); }}
                  className="p-1.5 hover:bg-blue-500/10 hover:text-blue-400 rounded-lg transition-all"
                  title="Bucket Policy"
                >
                  <Shield className="w-4 h-4" />
                </button>
                <button
                  onClick={(e) => { e.stopPropagation(); handleDeleteBucket(bucket); }}
                  className="p-1.5 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-all"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))}
        </div>

        <div className="p-4 border-t border-premium-border glass">
          <div className="flex items-center gap-3 text-slate-500">
            <HardDrive className="w-4 h-4" />
            <span className="text-xs font-medium">Storage Root: App_Data</span>
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 flex flex-col relative">
        {/* Header */}
        <header className="h-20 glass border-b border-premium-border flex items-center justify-between px-8 z-10">
          <div className="flex items-center gap-4">
            <h2 className="text-xl font-bold text-white flex items-center gap-2">
              {selectedBucket || 'Select a Bucket'}
              {selectedBucket && <span className="text-slate-600 font-normal">/</span>}
            </h2>
            <div className="relative group">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <input
                type="text"
                placeholder="Search objects..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="bg-premium-card border border-premium-border rounded-lg pl-10 pr-4 py-1.5 text-sm focus:outline-none focus:border-blue-500/50 w-64 transition-all"
              />
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => fetchObjects(selectedBucket)}
              className="p-2.5 hover:bg-premium-card rounded-xl border border-transparent hover:border-premium-border transition-all"
            >
              <RefreshCw className={`w-5 h-5 ${appLoading ? 'animate-spin' : ''}`} />
            </button>
            <label className="flex items-center gap-2 px-5 py-2.5 bg-blue-600 hover:bg-blue-500 text-white rounded-xl font-semibold text-sm cursor-pointer transition-all shadow-lg shadow-blue-500/20 active:scale-95">
              <Upload className="w-4 h-4" />
              Upload Object
              <input type="file" className="hidden" onChange={handleFileUpload} />
            </label>
              <div className="flex items-center gap-4 ml-4 pl-4 border-l border-premium-border">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center">
                    <User className="w-4 h-4 text-white" />
                  </div>
                  <span className="text-sm font-medium">{user?.username || 'User'}</span>
                </div>
                <button
                  onClick={() => {
                    logout();
                    navigateToAuthView('login');
                  }}
                  className="flex items-center gap-2 px-4 py-2 bg-red-600/10 hover:bg-red-600/20 text-red-500 rounded-lg transition-all"
                >
                  <LogOut className="w-4 h-4" />
                  Logout
                </button>
              </div>
          </div>
        </header>

        {/* Content Area */}
        <div className="flex-1 overflow-y-auto p-8 custom-scrollbar">
          {!selectedBucket ? (
            <div className="h-full flex flex-col items-center justify-center text-slate-500 space-y-4">
              <div className="w-20 h-20 rounded-3xl bg-premium-card flex items-center justify-center border border-premium-border">
                <Database className="w-10 h-10 opacity-20" />
              </div>
              <p className="font-medium">Choose a bucket from the sidebar to manage objects</p>
            </div>
          ) : (
            <div className="space-y-6">
              <div className="glass rounded-2xl border border-premium-border overflow-hidden shadow-2xl">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-premium-card/50 text-slate-500 text-xs uppercase tracking-widest font-bold border-b border-premium-border">
                      <th className="px-6 py-4">Name</th>
                      <th className="px-6 py-4">Size</th>
                      <th className="px-6 py-4">Last Modified</th>
                      <th className="px-6 py-4">Type</th>
                      <th className="px-6 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-premium-border">
                    {filteredObjects.length === 0 ? (
                      <tr>
                        <td colSpan="5" className="px-6 py-12 text-center text-slate-500 italic">
                          No objects found in this bucket.
                        </td>
                      </tr>
                    ) : (
                      filteredObjects.map((obj) => (
                        <tr key={obj.key} className="hover:bg-white/5 transition-colors group">
                          <td className="px-6 py-4">
                            <div className="flex items-center gap-3">
                              <div className="w-9 h-9 rounded-lg bg-slate-800 flex items-center justify-center">
                                <File className="w-5 h-5 text-blue-400" />
                              </div>
                              <span className="font-medium text-slate-200">{obj.key}</span>
                            </div>
                          </td>
                          <td className="px-6 py-4 text-sm text-slate-400">{formatSize(obj.size)}</td>
                          <td className="px-6 py-4 text-sm text-slate-400">
                            <div className="flex items-center gap-2">
                              <Clock className="w-3.5 h-3.5" />
                              {new Date(obj.lastModified).toLocaleString()}
                            </div>
                          </td>
                          <td className="px-6 py-4">
                            <span className="text-[10px] px-2 py-1 bg-slate-800 rounded-md text-slate-400 font-bold uppercase border border-premium-border">
                              {obj.contentType?.split('/')[1] || 'binary'}
                            </span>
                          </td>
                          <td className="px-6 py-4">
                            <div className="flex items-center justify-end gap-1">
                              <a
                                href={storageApi.getDownloadUrl(selectedBucket, obj.key)}
                                download
                                className="p-2 hover:bg-blue-500/10 hover:text-blue-400 rounded-lg transition-all"
                                title="Download"
                              >
                                <Download className="w-4.5 h-4.5" />
                              </a>
                              <button
                                onClick={() => openPreview(obj)}
                                className="p-2 hover:bg-emerald-500/10 hover:text-emerald-400 rounded-lg transition-all disabled:opacity-40 disabled:cursor-not-allowed"
                                title={canPreview(obj.contentType) ? 'Preview' : 'Preview not supported'}
                                disabled={!canPreview(obj.contentType)}
                              >
                                <Eye className="w-4.5 h-4.5" />
                              </button>
                              {isPdfObject(obj) && (
                                <button
                                  onClick={() => openContractManager(obj)}
                                  className={`p-2 rounded-lg transition-all ${
                                    contractTemplates[obj.key]
                                      ? 'hover:bg-blue-500/10 hover:text-blue-400 text-blue-500'
                                      : 'hover:bg-slate-500/10 hover:text-slate-300 text-slate-500'
                                  }`}
                                  title={contractTemplates[obj.key] ? 'Manage Contract' : 'Register as Contract'}
                                >
                                  {contractTemplates[obj.key]
                                    ? <FileText className="w-4.5 h-4.5" />
                                    : <FilePlus className="w-4.5 h-4.5" />}
                                </button>
                              )}
                              <button
                                onClick={() => handleDeleteObject(obj.key)}
                                className="p-2 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-all"
                                title="Delete"
                              >
                                <Trash2 className="w-4.5 h-4.5" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>

        {/* Progress Overlay */}
        {uploadProgress !== null && (
          <div className="absolute inset-x-0 top-0 h-1 bg-premium-border overflow-hidden">
            <div className="h-full bg-blue-500 animate-pulse w-full"></div>
          </div>
        )}
      </main>

      {/* Create Bucket Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={() => setIsCreateModalOpen(false)}></div>
          <div className="glass w-full max-w-md rounded-2xl border border-premium-border shadow-2xl relative animate-in fade-in zoom-in duration-200">
            <div className="p-6 border-b border-premium-border flex justify-between items-center">
              <h3 className="text-xl font-bold">Create New Bucket</h3>
              <button onClick={() => setIsCreateModalOpen(false)} className="text-slate-500 hover:text-white">
                <X className="w-5 h-5" />
              </button>
            </div>
            <form onSubmit={handleCreateBucket} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-400 mb-1.5 uppercase tracking-wider">Bucket Name</label>
                <input
                  type="text"
                  autoFocus
                  placeholder="e.g. static-assets"
                  value={newBucketName}
                  onChange={(e) => setNewBucketName(e.target.value)}
                  className="w-full bg-premium-dark border border-premium-border rounded-xl px-4 py-3 focus:outline-none focus:border-blue-500 transition-all text-white"
                />
              </div>
              <div className="pt-2">
                <button
                  type="submit"
                  className="w-full py-3 bg-blue-600 hover:bg-blue-500 text-white rounded-xl font-bold transition-all shadow-lg shadow-blue-500/20"
                >
                  Create Bucket
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Policy Modal */}
      {isPolicyModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={() => setIsPolicyModalOpen(false)}></div>
          <div className="glass w-full max-w-2xl rounded-2xl border border-premium-border shadow-2xl relative animate-in fade-in zoom-in duration-200 flex flex-col max-h-[90vh]">
            <div className="p-6 border-b border-premium-border flex justify-between items-center">
              <div className="flex items-center gap-3">
                <Shield className="text-blue-500 w-6 h-6" />
                <div>
                  <h3 className="text-xl font-bold">Bucket Policy</h3>
                  <p className="text-xs text-slate-500 truncate w-64">{policyTarget}</p>
                </div>
              </div>
              <button onClick={() => setIsPolicyModalOpen(false)} className="text-slate-500 hover:text-white">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 flex-1 flex flex-col space-y-4 overflow-hidden">
              <div className="flex gap-2">
                <button onClick={() => applyPreset('private')} className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 rounded-lg text-xs font-semibold transition-all">Private</button>
                <button onClick={() => applyPreset('publicRead')} className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 rounded-lg text-xs font-semibold transition-all">Public Read</button>
                <button onClick={() => applyPreset('fullAccess')} className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 rounded-lg text-xs font-semibold transition-all">Full Access</button>
              </div>

              <div className="flex-1 relative font-mono text-sm">
                <div className="absolute top-3 left-3 text-slate-600 pointer-events-none">
                  <Code className="w-4 h-4" />
                </div>
                <textarea
                  value={policyJson}
                  onChange={(e) => setPolicyJson(e.target.value)}
                  className="w-full h-full bg-premium-dark border border-premium-border rounded-xl p-4 pl-10 focus:outline-none focus:border-blue-500 transition-all text-blue-300 resize-none custom-scrollbar"
                  placeholder='{ "Version": "2012-10-17", ... }'
                />
              </div>
            </div>

            <div className="p-6 border-t border-premium-border flex justify-end gap-3 bg-premium-card/30">
              <button
                onClick={() => setIsPolicyModalOpen(false)}
                className="px-6 py-2.5 rounded-xl text-sm font-semibold hover:bg-premium-border transition-all"
              >
                Cancel
              </button>
              <button
                onClick={handleSavePolicy}
                className="px-6 py-2.5 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-sm font-bold transition-all shadow-lg shadow-blue-500/20 flex items-center gap-2"
              >
                <Check className="w-4 h-4" />
                Save Policy
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Contract Manager Modal */}
      {contractTarget && (
        <ContractManagerModal
          template={contractTarget}
          onClose={() => setContractTarget(null)}
        />
      )}

      {/* Preview Modal */}
      {previewObject && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={closePreview}></div>
          <div className="glass w-full max-w-5xl h-[85vh] rounded-2xl border border-premium-border shadow-2xl relative flex flex-col overflow-hidden">
            <div className="p-4 border-b border-premium-border flex justify-between items-center">
              <div className="min-w-0">
                <h3 className="text-base font-bold truncate">{previewObject.key}</h3>
                <p className="text-xs text-slate-500">{previewObject.contentType}</p>
              </div>
              <div className="flex items-center gap-2">
                <a
                  href={previewObject.url}
                  download
                  className="inline-flex items-center gap-2 px-3 py-2 text-sm bg-blue-600 hover:bg-blue-500 text-white rounded-lg transition-all"
                >
                  <Download className="w-4 h-4" />
                  Download
                </a>
                <button onClick={closePreview} className="p-2 text-slate-500 hover:text-white">
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>

            <div className="flex-1 bg-[#08090b] overflow-auto">
              {previewObject.previewType === 'image' && (
                <div className="w-full h-full p-4 flex items-center justify-center">
                  <img
                    src={previewObject.url}
                    alt={previewObject.key}
                    className="max-w-full max-h-full object-contain rounded-lg border border-premium-border"
                  />
                </div>
              )}

              {previewObject.previewType === 'pdf' && (
                <iframe
                  title={previewObject.key}
                  src={previewObject.url}
                  className="w-full h-full border-0"
                />
              )}

              {previewObject.previewType === 'text' && (
                <pre className="p-4 text-sm leading-6 text-slate-200 whitespace-pre-wrap break-words font-mono">
                  {previewObject.text}
                </pre>
              )}

              {previewObject.previewType === 'audio' && (
                <div className="w-full h-full flex items-center justify-center p-6">
                  <audio controls src={previewObject.url} className="w-full max-w-2xl">
                    Your browser does not support audio playback.
                  </audio>
                </div>
              )}

              {previewObject.previewType === 'video' && (
                <div className="w-full h-full flex items-center justify-center p-4">
                  <video controls src={previewObject.url} className="max-w-full max-h-full rounded-lg border border-premium-border">
                    Your browser does not support video playback.
                  </video>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default App;
