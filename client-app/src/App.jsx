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
  Share2,
  Users,
  Pencil,
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

  const [accessibleBuckets, setAccessibleBuckets] = useState([]);
  const [selectedBucket, setSelectedBucket] = useState(null);
  const [activeBucketView, setActiveBucketView] = useState('objects');
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
  const [isShareModalOpen, setIsShareModalOpen] = useState(false);
  const [shareTargetBucket, setShareTargetBucket] = useState(null);
  const [shareEntries, setShareEntries] = useState([]);
  const [shareEmail, setShareEmail] = useState('');
  const [sharePermission, setSharePermission] = useState('ViewOnly');
  const [shareExpiryMode, setShareExpiryMode] = useState('indefinite');
  const [shareExpiryDateTime, setShareExpiryDateTime] = useState('');
  const [incomingShares, setIncomingShares] = useState([]);
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
      const response = await storageApi.listAccessibleBuckets();
      const nextBuckets = response.data || [];
      setAccessibleBuckets(nextBuckets);

      if (nextBuckets.length === 0) {
        setSelectedBucket(null);
        return;
      }

      if (!selectedBucket || !nextBuckets.some((bucket) => bucket.bucketName === selectedBucket)) {
        setSelectedBucket(nextBuckets[0].bucketName);
      }
    } catch (error) {
      console.error('Failed to fetch buckets', error);
    }
  };

  const selectedBucketMeta = accessibleBuckets.find((bucket) => bucket.bucketName === selectedBucket) || null;
  const topSidebarBuckets = accessibleBuckets.slice(0, 5);
  const ownedBuckets = accessibleBuckets.filter((bucket) => bucket.isOwner);
  const sharedBuckets = accessibleBuckets.filter((bucket) => !bucket.isOwner);
  const pendingIncomingShares = incomingShares.filter((share) => !share.isAcknowledged);

  const fetchIncomingShares = useCallback(async () => {
    try {
      const response = await storageApi.listIncomingShares();
      setIncomingShares(response.data || []);
    } catch (error) {
      console.error('Failed to fetch incoming shares', error);
      setIncomingShares([]);
    }
  }, []);

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
    fetchIncomingShares();
  }, [isAuthenticated, currentView, fetchIncomingShares]);

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

  const openShareModal = async (bucketName) => {
    setShareTargetBucket(bucketName);
    setShareEmail('');
    setSharePermission('ViewOnly');
    setShareExpiryMode('indefinite');
    setShareExpiryDateTime('');
    setIsShareModalOpen(true);
    try {
      const response = await storageApi.listBucketShares(bucketName);
      setShareEntries(response.data || []);
    } catch (error) {
      setShareEntries([]);
      alert('Failed to load shared users for this bucket.');
    }
  };

  const handleShareBucket = async () => {
    if (!shareTargetBucket || !shareEmail.trim() || !sharePermission) {
      return;
    }

    let expiresAt = null;
    if (shareExpiryMode === 'expires') {
      if (!shareExpiryDateTime) {
        alert('Select an expiration date and time or choose indefinite access.');
        return;
      }

      const parsedDate = new Date(shareExpiryDateTime);
      if (Number.isNaN(parsedDate.getTime())) {
        alert('Invalid expiration date and time.');
        return;
      }

      expiresAt = parsedDate.toISOString();
    }

    try {
      await storageApi.shareBucketWithEmail(shareTargetBucket, shareEmail.trim(), sharePermission, expiresAt);
      const response = await storageApi.listBucketShares(shareTargetBucket);
      setShareEntries(response.data || []);
      setShareEmail('');
      setSharePermission('ViewOnly');
      setShareExpiryMode('indefinite');
      setShareExpiryDateTime('');
    } catch (error) {
      alert(error?.response?.data || 'Failed to share bucket with this email.');
    }
  };

  const handleUnshareBucket = async (email) => {
    if (!shareTargetBucket) {
      return;
    }

    try {
      await storageApi.unshareBucketWithEmail(shareTargetBucket, email);
      const response = await storageApi.listBucketShares(shareTargetBucket);
      setShareEntries(response.data || []);
    } catch (error) {
      alert(error?.response?.data || 'Failed to remove bucket share.');
    }
  };

  const handleAcknowledgeShare = async (bucketName) => {
    try {
      await storageApi.acknowledgeShare(bucketName);
      await fetchIncomingShares();
      await fetchBuckets();
    } catch (error) {
      alert(error?.response?.data || 'Failed to acknowledge share.');
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
          {topSidebarBuckets.map((bucket) => (
            <div
              key={bucket.bucketName}
              onClick={() => {
                setSelectedBucket(bucket.bucketName);
                setActiveBucketView('objects');
              }}
              className={`group flex items-center justify-between p-3 rounded-xl cursor-pointer transition-all ${selectedBucket === bucket.bucketName
                  ? 'bg-blue-600/10 text-blue-400 border border-blue-500/20'
                  : 'hover:bg-premium-card text-slate-400 border border-transparent'
                }`}
            >
              <div className="flex items-center gap-3 overflow-hidden">
                <Folder className={`w-5 h-5 flex-shrink-0 ${selectedBucket === bucket.bucketName ? 'text-blue-500' : 'text-slate-500'}`} />
                <span className="text-sm font-medium truncate">{bucket.bucketName}</span>
                {!bucket.isOwner && <Share2 className="w-3.5 h-3.5 text-amber-400 flex-shrink-0" title="Shared with you" />}
              </div>
              <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-all">
                {bucket.isOwner && (
                  <>
                    <button
                      onClick={(e) => { e.stopPropagation(); openShareModal(bucket.bucketName); }}
                      className="p-1.5 hover:bg-amber-500/10 hover:text-amber-400 rounded-lg transition-all"
                      title="Share Bucket"
                    >
                      <Users className="w-4 h-4" />
                    </button>
                    <button
                      onClick={(e) => { e.stopPropagation(); openPolicyModal(bucket.bucketName); }}
                      className="p-1.5 hover:bg-blue-500/10 hover:text-blue-400 rounded-lg transition-all"
                      title="Bucket Policy"
                    >
                      <Shield className="w-4 h-4" />
                    </button>
                    <button
                      onClick={(e) => { e.stopPropagation(); handleDeleteBucket(bucket.bucketName); }}
                      className="p-1.5 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-all"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </>
                )}
              </div>
            </div>
          ))}
        </div>

        <div className="p-4 border-t border-premium-border glass space-y-2">
          <button
            onClick={() => setActiveBucketView('all')}
            className="w-full py-2 px-3 bg-premium-card hover:bg-premium-border border border-premium-border rounded-xl text-xs font-semibold uppercase tracking-wide"
          >
            View All Buckets
          </button>
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
              {activeBucketView === 'all' ? 'All Buckets' : (selectedBucket || 'Select a Bucket')}
              {activeBucketView !== 'all' && selectedBucket && <span className="text-slate-600 font-normal">/</span>}
              {activeBucketView !== 'all' && selectedBucketMeta && !selectedBucketMeta.isOwner && (
                <span className="text-xs px-2 py-0.5 rounded-full bg-amber-500/15 text-amber-300 border border-amber-500/30 font-semibold uppercase tracking-wide">
                  Shared
                </span>
              )}
            </h2>
            {activeBucketView !== 'all' && (
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
            )}
          </div>

          <div className="flex items-center gap-3">
            {activeBucketView !== 'all' && (
              <>
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

                {selectedBucket && (
                  <div className="inline-flex items-center gap-1 p-1 border border-premium-border rounded-xl bg-premium-card/50">
                    {selectedBucketMeta?.isOwner ? (
                      <>
                        <button
                          onClick={() => openShareModal(selectedBucket)}
                          className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg hover:bg-amber-500/15 text-amber-300 text-xs font-semibold uppercase tracking-wide"
                          title="Share / Unshare"
                        >
                          <Users className="w-3.5 h-3.5" />
                          Share
                        </button>
                        <button
                          onClick={() => openPolicyModal(selectedBucket)}
                          className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg hover:bg-blue-500/15 text-blue-300 text-xs font-semibold uppercase tracking-wide"
                          title="Bucket Policy"
                        >
                          <Shield className="w-3.5 h-3.5" />
                          Policy
                        </button>
                        <button
                          onClick={() => handleDeleteBucket(selectedBucket)}
                          className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg hover:bg-red-500/15 text-red-300 text-xs font-semibold uppercase tracking-wide"
                          title="Delete Bucket"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                          Delete
                        </button>
                        <button
                          disabled
                          className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-slate-500 text-xs font-semibold uppercase tracking-wide cursor-not-allowed"
                          title="Rename is not implemented yet"
                        >
                          <Pencil className="w-3.5 h-3.5" />
                          Rename
                        </button>
                      </>
                    ) : (
                      <div className="inline-flex items-center gap-1.5 px-3 py-2 text-xs text-slate-400">
                        <Share2 className="w-3.5 h-3.5 text-amber-400" />
                        Shared bucket permissions are managed by the owner.
                      </div>
                    )}
                  </div>
                )}
              </>
            )}
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
          {activeBucketView === 'all' ? (
            <div className="space-y-6">
              <div className="flex items-center justify-between">
                <h3 className="text-2xl font-bold">All Buckets</h3>
                <span className="text-xs uppercase tracking-widest text-slate-500">Grouped by ownership</span>
              </div>

              <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                <section className="glass rounded-2xl border border-premium-border p-5 space-y-3">
                  <h4 className="text-sm font-semibold uppercase tracking-wide text-blue-300">Owned by you</h4>
                  {ownedBuckets.length === 0 ? (
                    <p className="text-sm text-slate-500">No owned buckets yet.</p>
                  ) : ownedBuckets.map((bucket) => (
                    <div key={`owned-${bucket.bucketName}`} className="flex items-center justify-between px-3 py-2 rounded-lg border border-premium-border bg-premium-card/40">
                      <span className="font-medium">{bucket.bucketName}</span>
                      <div className="flex items-center gap-2">
                        <button onClick={() => { setSelectedBucket(bucket.bucketName); setActiveBucketView('objects'); }} className="px-3 py-1.5 text-xs rounded-lg bg-blue-600/20 hover:bg-blue-600/30 text-blue-300">Open</button>
                        <button onClick={() => openShareModal(bucket.bucketName)} className="px-3 py-1.5 text-xs rounded-lg bg-amber-600/20 hover:bg-amber-600/30 text-amber-300">Manage Shares</button>
                      </div>
                    </div>
                  ))}
                </section>

                <section className="glass rounded-2xl border border-premium-border p-5 space-y-3">
                  <h4 className="text-sm font-semibold uppercase tracking-wide text-amber-300">Shared with you</h4>
                  {sharedBuckets.length === 0 ? (
                    <p className="text-sm text-slate-500">No shared buckets yet.</p>
                  ) : sharedBuckets.map((bucket) => (
                    <div key={`shared-${bucket.bucketName}`} className="flex items-center justify-between px-3 py-2 rounded-lg border border-premium-border bg-premium-card/40">
                      <div className="flex items-center gap-2">
                        <Share2 className="w-4 h-4 text-amber-400" />
                        <span className="font-medium">{bucket.bucketName}</span>
                      </div>
                      <button onClick={() => { setSelectedBucket(bucket.bucketName); setActiveBucketView('objects'); }} className="px-3 py-1.5 text-xs rounded-lg bg-blue-600/20 hover:bg-blue-600/30 text-blue-300">Open</button>
                    </div>
                  ))}
                </section>
              </div>

              <section className="glass rounded-2xl border border-premium-border p-5 space-y-3">
                <h4 className="text-sm font-semibold uppercase tracking-wide text-emerald-300">Pending share acknowledgments</h4>
                {pendingIncomingShares.length === 0 ? (
                  <p className="text-sm text-slate-500">No pending share requests.</p>
                ) : pendingIncomingShares.map((share) => (
                  <div key={`incoming-${share.bucketName}-${share.sharedByEmail}`} className="flex items-center justify-between px-3 py-2 rounded-lg border border-premium-border bg-premium-card/40 gap-3">
                    <div>
                      <p className="font-medium">{share.bucketName}</p>
                      <p className="text-xs text-slate-500">From {share.sharedByEmail} • Shared {new Date(share.sharedAt).toLocaleString()}</p>
                      <p className="text-xs text-slate-500">
                        Permission: {share.permission}
                      </p>
                      <p className="text-xs text-slate-500">
                        {share.expiresAt ? `Expires ${new Date(share.expiresAt).toLocaleString()}` : 'No expiration'}
                      </p>
                    </div>
                    <button
                      onClick={() => handleAcknowledgeShare(share.bucketName)}
                      className="px-3 py-1.5 text-xs rounded-lg bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300"
                    >
                      Acknowledge
                    </button>
                  </div>
                ))}
              </section>
            </div>
          ) : !selectedBucket ? (
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
                              {selectedBucketMeta?.isOwner && (
                                <button
                                  onClick={() => handleDeleteObject(obj.key)}
                                  className="p-2 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-all"
                                  title="Delete"
                                >
                                  <Trash2 className="w-4.5 h-4.5" />
                                </button>
                              )}
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

      {isShareModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={() => setIsShareModalOpen(false)}></div>
          <div className="glass w-full max-w-xl rounded-2xl border border-premium-border shadow-2xl relative animate-in fade-in zoom-in duration-200 flex flex-col max-h-[85vh]">
            <div className="p-6 border-b border-premium-border flex justify-between items-center">
              <div>
                <h3 className="text-xl font-bold">Share Bucket</h3>
                <p className="text-xs text-slate-500 mt-1">{shareTargetBucket}</p>
              </div>
              <button onClick={() => setIsShareModalOpen(false)} className="text-slate-500 hover:text-white">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 border-b border-premium-border space-y-3">
              <label className="block text-xs uppercase tracking-wider text-slate-500">Share with user email</label>
              <div className="flex gap-2">
                <input
                  type="email"
                  value={shareEmail}
                  onChange={(e) => setShareEmail(e.target.value)}
                  placeholder="user@example.com"
                  className="flex-1 bg-premium-dark border border-premium-border rounded-xl px-4 py-2.5 focus:outline-none focus:border-blue-500"
                />
                <button
                  onClick={handleShareBucket}
                  className="px-4 py-2.5 bg-blue-600 hover:bg-blue-500 text-white rounded-xl font-semibold"
                >
                  Share
                </button>
              </div>

              <div className="space-y-2">
                <p className="text-xs uppercase tracking-wider text-slate-500">Permission</p>
                <select
                  value={sharePermission}
                  onChange={(e) => setSharePermission(e.target.value)}
                  className="w-full bg-premium-dark border border-premium-border rounded-xl px-4 py-2.5 focus:outline-none focus:border-blue-500"
                >
                  <option value="ViewOnly">ViewOnly (view objects only)</option>
                  <option value="Modify">Modify (view, upload, delete)</option>
                  <option value="ModifyOnly">ModifyOnly (view, upload, no delete)</option>
                </select>
              </div>

              <div className="space-y-2">
                <p className="text-xs uppercase tracking-wider text-slate-500">Access duration</p>
                <div className="flex items-center gap-5 text-sm">
                  <label className="inline-flex items-center gap-2">
                    <input
                      type="radio"
                      name="share-expiry"
                      checked={shareExpiryMode === 'indefinite'}
                      onChange={() => setShareExpiryMode('indefinite')}
                    />
                    Indefinite
                  </label>
                  <label className="inline-flex items-center gap-2">
                    <input
                      type="radio"
                      name="share-expiry"
                      checked={shareExpiryMode === 'expires'}
                      onChange={() => setShareExpiryMode('expires')}
                    />
                    Expires at
                  </label>
                </div>
                {shareExpiryMode === 'expires' && (
                  <input
                    type="datetime-local"
                    value={shareExpiryDateTime}
                    onChange={(e) => setShareExpiryDateTime(e.target.value)}
                    className="w-full bg-premium-dark border border-premium-border rounded-xl px-4 py-2.5 focus:outline-none focus:border-blue-500"
                  />
                )}
              </div>
            </div>

            <div className="p-6 overflow-auto custom-scrollbar space-y-2">
              {shareEntries.length === 0 ? (
                <p className="text-sm text-slate-500">No users have access to this bucket yet.</p>
              ) : shareEntries.map((entry) => (
                <div key={entry.sharedWithEmail} className="flex items-center justify-between p-3 rounded-xl border border-premium-border bg-premium-card/40">
                  <div>
                    <p className="font-medium text-sm">{entry.sharedWithEmail}</p>
                    <p className="text-xs text-slate-500">Shared {new Date(entry.sharedAt).toLocaleString()}</p>
                    <p className="text-xs text-slate-500">Permission: {entry.permission}</p>
                    <p className="text-xs text-slate-500">
                      {entry.expiresAt ? `Expires ${new Date(entry.expiresAt).toLocaleString()}` : 'No expiration'}
                    </p>
                    <p className={`text-xs ${entry.acknowledgedAt ? 'text-emerald-300' : 'text-amber-300'}`}>
                      {entry.acknowledgedAt
                        ? `Acknowledged ${new Date(entry.acknowledgedAt).toLocaleString()}`
                        : 'Pending acknowledgment'}
                    </p>
                  </div>
                  <button
                    onClick={() => handleUnshareBucket(entry.sharedWithEmail)}
                    className="px-3 py-1.5 rounded-lg text-xs bg-red-600/20 hover:bg-red-600/30 text-red-300"
                  >
                    Remove
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default App;
