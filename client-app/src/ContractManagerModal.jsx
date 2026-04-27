import React, { useState, useEffect, useCallback, useRef } from 'react';
import {
  X,
  Plus,
  Trash2,
  FileText,
  Send,
  CheckCircle,
  User,
  PenLine,
  Type,
  Calendar,
  CheckSquare,
  Loader2,
  AlertCircle,
  ShieldCheck,
  MousePointer,
  Edit3,
  Info,
  Lock,
} from 'lucide-react';
import { storageApi } from './api';

  // ─── constants ────────────────────────────────────────────────────────────────

  const FIELD_TYPES = [
    { value: 'text',      label: 'Text',      Icon: Type },
    { value: 'date',      label: 'Date',      Icon: Calendar },
    { value: 'checkbox',  label: 'Checkbox',  Icon: CheckSquare },
    { value: 'signature', label: 'Signature', Icon: PenLine },
    { value: 'initials',  label: 'Initials',  Icon: User },
  ];

  // Tailwind classes per field type for borders/bg/text
  const TYPE_COLOR = {
    text:      'border-blue-500/70 bg-blue-500/20 text-blue-300',
    date:      'border-purple-500/70 bg-purple-500/20 text-purple-300',
    checkbox:  'border-emerald-500/70 bg-emerald-500/20 text-emerald-300',
    signature: 'border-amber-500/70 bg-amber-500/20 text-amber-300',
    initials:  'border-pink-500/70 bg-pink-500/20 text-pink-300',
  };

  const MIN_BOX_WIDTH = 24;
  const MIN_BOX_HEIGHT = 24;

  // ─── tiny shared helpers ──────────────────────────────────────────────────────

  const FieldTypeIcon = ({ type, className = 'w-4 h-4' }) => {
    const match = FIELD_TYPES.find((f) => f.value === type);
    const Icon = match?.Icon ?? Type;
    return <Icon className={className} />;
  };

  const Badge = ({ children, color = 'slate' }) => {
    const colors = {
      slate: 'bg-slate-700 text-slate-300',
      blue:  'bg-blue-600/20 text-blue-400 border border-blue-500/30',
      amber: 'bg-amber-600/20 text-amber-400 border border-amber-500/30',
      green: 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/30',
      red:   'bg-red-600/20 text-red-400 border border-red-500/30',
    };
    return (
      <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-md ${colors[color] ?? colors.slate}`}>
        {children}
      </span>
    );
  };

  const inputCls = 'w-full bg-premium-dark border border-premium-border rounded-xl px-3 py-2 text-sm focus:outline-none focus:border-blue-500 transition-all text-white placeholder-slate-600';
  const labelCls = 'block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1';

  // ─── DragChip (palette item) ──────────────────────────────────────────────────

  function DragChip({ fieldType }) {
    const { label, Icon } = FIELD_TYPES.find((f) => f.value === fieldType);
    return (
      <div
        draggable
        onDragStart={(e) => {
          e.dataTransfer.setData('application/x-field-type', fieldType);
          e.dataTransfer.effectAllowed = 'copy';
        }}
        className={`flex items-center gap-2 px-3 py-2 rounded-xl border-2 cursor-grab active:cursor-grabbing select-none text-sm font-semibold transition-all hover:scale-105 active:scale-95 ${TYPE_COLOR[fieldType]}`}
      >
        <Icon className="w-4 h-4" />
        {label}
      </div>
    );
  }

  // ─── PlacedBox (overlay box on canvas) ───────────────────────────────────────

  function PlacedBox({
    box,
    isSelected,
    isPending,
    canDelete,
    canTransform,
    onSelect,
    onRemove,
    onStartMove,
    onStartResize,
  }) {
    const colorCls = TYPE_COLOR[box.fieldType] ?? TYPE_COLOR.text;
    return (
      <div
        onClick={(e) => { e.stopPropagation(); onSelect(); }}
        onMouseDown={(e) => {
          if (!canTransform || e.button !== 0) return;
          e.stopPropagation();
          onStartMove(e);
        }}
        style={{
          position: 'absolute',
          left: box.x,
          top: box.y,
          width: box.width,
          height: box.height,
          minWidth: 40,
          minHeight: 20,
        }}
        className={`border-2 rounded flex items-center justify-between px-1.5 gap-1 group transition-all
          ${colorCls}
          ${canTransform ? 'cursor-move' : 'cursor-pointer'}
          ${isSelected ? 'ring-2 ring-white/40 shadow-lg' : 'hover:brightness-125'}
          ${isPending ? 'animate-pulse' : ''}
        `}
      >
        <div className="flex items-center gap-1 overflow-hidden min-w-0 pointer-events-none">
          <FieldTypeIcon type={box.fieldType} className="w-3 h-3 flex-shrink-0" />
          <span className="text-[10px] font-bold truncate leading-tight">
            {box.label || box.fieldType}
          </span>
        </div>
        {canDelete && (isSelected || isPending) && (
          <button
            onClick={(e) => { e.stopPropagation(); onRemove(); }}
            className="flex-shrink-0 w-4 h-4 rounded-full bg-black/50 hover:bg-red-600 flex items-center justify-center transition-colors z-10"
          >
            <X className="w-2.5 h-2.5" />
          </button>
        )}
        {canTransform && isSelected && (
          <button
            onMouseDown={(e) => {
              if (e.button !== 0) return;
              e.stopPropagation();
              onStartResize(e);
            }}
            className="absolute -right-1 -bottom-1 w-3.5 h-3.5 rounded-sm border border-white/70 bg-blue-500/80 hover:bg-blue-400 cursor-se-resize"
            aria-label="Resize field"
            type="button"
          />
        )}
      </div>
    );
  }

  // ─── BoxConfigPanel (right panel config form) ─────────────────────────────────

  function BoxConfigPanel({ box, onSave, onCancel, saving, error }) {
    const [form, setForm] = useState({
      label:     box.label     ?? '',
      role:      box.role      ?? '',
      page:      box.page      ?? 1,
      x:         box.x         ?? 0,
      y:         box.y         ?? 0,
      width:     box.width     ?? 200,
      height:    box.height    ?? 40,
      required:  box.required  ?? true,
      maxLength: box.maxLength ?? '',
      order:     box.order     ?? 1,
      fieldType: box.fieldType ?? 'text',
    });

    const set    = (k, v)   => setForm((f) => ({ ...f, [k]: v }));
    const setNum = (k, raw) => set(k, raw === '' ? '' : Number(raw));

    const handleSubmit = (e) => {
      e.preventDefault();
      if (!form.label.trim() || !form.role.trim()) return;
      onSave({ ...box, ...form, maxLength: form.maxLength ? Number(form.maxLength) : null });
    };

    return (
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        {/* type badge */}
        <div className={`flex items-center gap-2 p-3 rounded-xl border-2 ${TYPE_COLOR[form.fieldType]}`}>
          <FieldTypeIcon type={form.fieldType} className="w-5 h-5" />
          <span className="font-bold text-sm capitalize">{form.fieldType} Field</span>
          <span className="ml-auto text-[10px] opacity-60 font-mono">new</span>
        </div>

        {/* type selector */}
        <div>
          <label className={labelCls}>Field Type</label>
          <select className={inputCls} value={form.fieldType} onChange={(e) => set('fieldType', e.target.value)}>
            {FIELD_TYPES.map((ft) => (
              <option key={ft.value} value={ft.value}>{ft.label}</option>
            ))}
          </select>
        </div>

        {/* label + role */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelCls}>Label *</label>
            <input className={inputCls} placeholder="e.g. Signature" value={form.label} onChange={(e) => set('label', e.target.value)} required />
          </div>
          <div>
            <label className={labelCls}>Signer Role *</label>
            <input className={inputCls} placeholder="e.g. Signee" value={form.role} onChange={(e) => set('role', e.target.value)} required />
          </div>
        </div>

        {/* page + order + maxLen */}
        <div className="grid grid-cols-3 gap-2">
          <div>
            <label className={labelCls}>Page</label>
            <input type="number" min="1" className={inputCls} value={form.page} onChange={(e) => setNum('page', e.target.value)} />
          </div>
          <div>
            <label className={labelCls}>Order</label>
            <input type="number" min="1" className={inputCls} value={form.order} onChange={(e) => setNum('order', e.target.value)} />
          </div>
          <div>
            <label className={labelCls}>Max Len</label>
            <input type="number" min="1" placeholder="∞" className={inputCls} value={form.maxLength} onChange={(e) => setNum('maxLength', e.target.value)} />
          </div>
        </div>

        {/* x/y + w/h (fine-tune) */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelCls}>X / Y (px)</label>
            <div className="flex gap-1">
              <input type="number" min="0" className={inputCls} value={form.x} onChange={(e) => setNum('x', e.target.value)} />
              <input type="number" min="0" className={inputCls} value={form.y} onChange={(e) => setNum('y', e.target.value)} />
            </div>
          </div>
          <div>
            <label className={labelCls}>W / H (px)</label>
            <div className="flex gap-1">
              <input type="number" min="10" className={inputCls} value={form.width}  onChange={(e) => setNum('width',  e.target.value)} />
              <input type="number" min="10" className={inputCls} value={form.height} onChange={(e) => setNum('height', e.target.value)} />
            </div>
          </div>
        </div>

        {/* required */}
        <label className="flex items-center gap-2 cursor-pointer select-none">
          <input type="checkbox" checked={form.required} onChange={(e) => set('required', e.target.checked)} className="w-4 h-4 rounded accent-blue-500" />
          <span className="text-sm text-slate-300">Required</span>
        </label>

        {error && (
          <p className="text-xs text-red-400 flex items-center gap-1.5">
            <AlertCircle className="w-3.5 h-3.5" />{error}
          </p>
        )}

        <div className="flex gap-2 pt-1">
          <button type="button" onClick={onCancel} className="flex-1 py-2 rounded-xl text-sm font-semibold hover:bg-premium-border transition-all">
            Discard
          </button>
          <button
            type="submit"
            disabled={saving || !form.label.trim() || !form.role.trim()}
            className="flex-1 py-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-white rounded-xl text-sm font-bold transition-all flex items-center justify-center gap-2"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
            Save Field
          </button>
        </div>
      </form>
    );
  }

  // ─── Placeholders Tab (drag-and-drop editor) ──────────────────────────────────

  function PlaceholdersTab({ templateId, pdfUrl }) {
    const [savedPlaceholders, setSavedPlaceholders] = useState([]);
    const [pendingBox,        setPendingBox]        = useState(null);
    const [selectedId,        setSelectedId]        = useState(null);
    const [editMode,          setEditMode]          = useState(false);
    const [saving,            setSaving]            = useState(false);
    const [saveError,         setSaveError]         = useState(null);
    const [loadError,         setLoadError]         = useState(null);
    const [interaction,       setInteraction]       = useState(null);

    const canvasRef = useRef(null);
    const pendingBoxRef = useRef(pendingBox);
    const savedPlaceholdersRef = useRef(savedPlaceholders);
    const interactionChangedRef = useRef(false);

    useEffect(() => {
      pendingBoxRef.current = pendingBox;
    }, [pendingBox]);

    useEffect(() => {
      savedPlaceholdersRef.current = savedPlaceholders;
    }, [savedPlaceholders]);

    const load = useCallback(async () => {
      try {
        const res = await storageApi.listContractPlaceholders(templateId);
        setSavedPlaceholders(res.data);
      } catch {
        setLoadError('Failed to load placeholders.');
      }
    }, [templateId]);

    useEffect(() => { load(); }, [load]);

    // ── drag-drop from palette ──
    const handleDrop = useCallback((e) => {
      e.preventDefault();
      const fieldType = e.dataTransfer.getData('application/x-field-type');
      if (!fieldType || !canvasRef.current) return;

      const rect = canvasRef.current.getBoundingClientRect();
      const x = Math.round(e.clientX - rect.left);
      const y = Math.round(e.clientY - rect.top);

      const defaultSizes = {
        checkbox:  { w: 24,  h: 24  },
        signature: { w: 200, h: 60  },
        initials:  { w: 80,  h: 40  },
        date:      { w: 140, h: 40  },
        text:      { w: 200, h: 40  },
      };
      const { w, h } = defaultSizes[fieldType] ?? { w: 200, h: 40 };

      const box = {
        _id:       `pending-${Date.now()}`,
        fieldType,
        x:         Math.max(0, x - Math.round(w / 2)),
        y:         Math.max(0, y - Math.round(h / 2)),
        width:     w,
        height:    h,
        label:     '',
        role:      '',
        page:      1,
        required:  true,
        order:     savedPlaceholders.length + 1,
        maxLength: null,
      };

      setPendingBox(box);
      setSelectedId(box._id);
      setSaveError(null);
    }, [savedPlaceholders.length]);

    const handleSaveBox = async (boxData) => {
      setSaving(true);
      setSaveError(null);
      try {
        const res = await storageApi.createContractPlaceholder(templateId, {
          fieldType: boxData.fieldType,
          role:      boxData.role,
          label:     boxData.label,
          page:      boxData.page,
          x:         boxData.x,
          y:         boxData.y,
          width:     boxData.width,
          height:    boxData.height,
          required:  boxData.required,
          maxLength: boxData.maxLength,
          order:     boxData.order,
        });
        setSavedPlaceholders((prev) => [...prev, res.data]);
        setPendingBox(null);
        setSelectedId(null);
      } catch (err) {
        setSaveError(err.response?.data || 'Failed to save field.');
      } finally {
        setSaving(false);
      }
    };

    const getCanvasSize = () => {
      const rect = canvasRef.current?.getBoundingClientRect();
      return {
        width: Math.max(1, Math.round(rect?.width ?? 1)),
        height: Math.max(1, Math.round(rect?.height ?? 1)),
      };
    };

    const applyBoxPatch = useCallback((boxId, patcher) => {
      if (pendingBoxRef.current?._id === boxId) {
        setPendingBox((prev) => (prev ? patcher(prev) : prev));
        return;
      }

      setSavedPlaceholders((prev) => prev.map((box) =>
        box.placeholderId === boxId ? patcher(box) : box
      ));
    }, []);

    const persistSavedPlaceholder = useCallback(async (boxId) => {
      const box = savedPlaceholdersRef.current.find((item) => item.placeholderId === boxId);
      if (!box) return;

      try {
        const res = await storageApi.updateContractPlaceholder(templateId, box.placeholderId, {
          fieldType: box.fieldType,
          role: box.role,
          label: box.label,
          page: box.page,
          x: box.x,
          y: box.y,
          width: box.width,
          height: box.height,
          required: box.required,
          maxLength: box.maxLength,
          order: box.order,
        });

        setSavedPlaceholders((prev) => prev.map((item) => (
          item.placeholderId === boxId ? res.data : item
        )));
      } catch (err) {
        setSaveError(err.response?.data || 'Failed to persist field position/size.');
        load();
      }
    }, [load, templateId]);

    const startMove = useCallback((box, e) => {
      setSelectedId(box.placeholderId ?? box._id);
      interactionChangedRef.current = false;
      setInteraction({
        mode: 'move',
        boxId: box.placeholderId ?? box._id,
        startX: e.clientX,
        startY: e.clientY,
        startBox: {
          x: Number(box.x) || 0,
          y: Number(box.y) || 0,
          width: Number(box.width) || MIN_BOX_WIDTH,
          height: Number(box.height) || MIN_BOX_HEIGHT,
        },
      });
      setSaveError(null);
    }, []);

    const startResize = useCallback((box, e) => {
      setSelectedId(box.placeholderId ?? box._id);
      interactionChangedRef.current = false;
      setInteraction({
        mode: 'resize',
        boxId: box.placeholderId ?? box._id,
        startX: e.clientX,
        startY: e.clientY,
        startBox: {
          x: Number(box.x) || 0,
          y: Number(box.y) || 0,
          width: Number(box.width) || MIN_BOX_WIDTH,
          height: Number(box.height) || MIN_BOX_HEIGHT,
        },
      });
      setSaveError(null);
    }, []);

    useEffect(() => {
      if (!interaction) return undefined;

      const onMouseMove = (e) => {
        const dx = e.clientX - interaction.startX;
        const dy = e.clientY - interaction.startY;
        const canvas = getCanvasSize();

        if (interaction.mode === 'move') {
          const nextX = Math.max(0, Math.min(interaction.startBox.x + dx, canvas.width - interaction.startBox.width));
          const nextY = Math.max(0, Math.min(interaction.startBox.y + dy, canvas.height - interaction.startBox.height));
          applyBoxPatch(interaction.boxId, (box) => ({ ...box, x: Math.round(nextX), y: Math.round(nextY) }));
        } else {
          const maxW = Math.max(MIN_BOX_WIDTH, canvas.width - interaction.startBox.x);
          const maxH = Math.max(MIN_BOX_HEIGHT, canvas.height - interaction.startBox.y);
          const nextW = Math.max(MIN_BOX_WIDTH, Math.min(interaction.startBox.width + dx, maxW));
          const nextH = Math.max(MIN_BOX_HEIGHT, Math.min(interaction.startBox.height + dy, maxH));
          applyBoxPatch(interaction.boxId, (box) => ({ ...box, width: Math.round(nextW), height: Math.round(nextH) }));
        }

        interactionChangedRef.current = true;
      };

      const onMouseUp = () => {
        if (interactionChangedRef.current && !String(interaction.boxId).startsWith('pending-')) {
          persistSavedPlaceholder(interaction.boxId);
        }
        setInteraction(null);
      };

      window.addEventListener('mousemove', onMouseMove);
      window.addEventListener('mouseup', onMouseUp);

      return () => {
        window.removeEventListener('mousemove', onMouseMove);
        window.removeEventListener('mouseup', onMouseUp);
      };
    }, [applyBoxPatch, interaction, persistSavedPlaceholder]);

    const handleDeleteSaved = async (placeholderId) => {
      setSaveError(null);
      try {
        await storageApi.deleteContractPlaceholder(templateId, placeholderId);
        setSavedPlaceholders((prev) => prev.filter((p) => p.placeholderId !== placeholderId));
        if (selectedId === placeholderId) {
          setSelectedId(null);
        }
      } catch (err) {
        setSaveError(err.response?.data || 'Failed to delete field.');
      }
    };

    const cancelPending = () => {
      setPendingBox(null);
      setSelectedId(null);
      setSaveError(null);
      setInteraction(null);
    };

    const toggleEditMode = () => {
      setEditMode((v) => !v);
      cancelPending();
    };

    return (
      <div className="flex flex-col gap-3 h-full">

        {/* ── toolbar ── */}
        <div className="flex items-center gap-3 flex-wrap justify-between flex-shrink-0">
          {editMode ? (
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider mr-1">
                Drag to place:
              </span>
              {FIELD_TYPES.map((ft) => (
                <DragChip key={ft.value} fieldType={ft.value} />
              ))}
            </div>
          ) : (
            <p className="text-sm text-slate-500 flex items-center gap-2">
              <Info className="w-4 h-4 flex-shrink-0" />
              Switch to <strong className="text-slate-300">Edit Fields</strong> to drag field types onto the PDF.
            </p>
          )}

          <button
            onClick={toggleEditMode}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-bold transition-all flex-shrink-0 ${
              editMode
                ? 'bg-blue-600 hover:bg-blue-500 text-white shadow-lg shadow-blue-500/20'
                : 'bg-premium-card hover:bg-premium-border border border-premium-border text-slate-300'
            }`}
          >
            {editMode ? <Edit3 className="w-4 h-4" /> : <MousePointer className="w-4 h-4" />}
            {editMode ? 'Editing Fields' : 'Edit Fields'}
          </button>
        </div>

        {loadError && (
          <p className="text-xs text-red-400 flex items-center gap-1.5 flex-shrink-0">
            <AlertCircle className="w-3.5 h-3.5" />{loadError}
          </p>
        )}

        {/* ── main area: canvas + right panel ── */}
        <div className="flex gap-4 flex-1 min-h-0">

          {/* PDF canvas */}
          <div className="flex-1 relative rounded-xl overflow-hidden border border-premium-border bg-[#08090b] min-h-[520px]">
            <iframe
              title="PDF Preview"
              src={pdfUrl}
              className="w-full h-full border-0"
              style={{ pointerEvents: editMode ? 'none' : 'auto' }}
            />

            {/* edit-mode overlay */}
            {editMode && (
              <div
                ref={canvasRef}
                className="absolute inset-0 cursor-crosshair"
                onDragOver={(e) => e.preventDefault()}
                onDrop={handleDrop}
                onClick={() => { setSelectedId(null); }}
              >
                {/* saved placeholders */}
                {savedPlaceholders.map((p) => (
                  <PlacedBox
                    key={p.placeholderId}
                    box={p}
                    isSelected={selectedId === p.placeholderId}
                    isPending={false}
                    canDelete
                    canTransform
                    onSelect={() => setSelectedId(p.placeholderId)}
                    onRemove={() => handleDeleteSaved(p.placeholderId)}
                    onStartMove={(e) => startMove(p, e)}
                    onStartResize={(e) => startResize(p, e)}
                  />
                ))}
                {/* pending (unsaved) box */}
                {pendingBox && (
                  <PlacedBox
                    key={pendingBox._id}
                    box={pendingBox}
                    isSelected
                    isPending
                    canDelete
                    canTransform
                    onSelect={() => setSelectedId(pendingBox._id)}
                    onRemove={cancelPending}
                    onStartMove={(e) => startMove(pendingBox, e)}
                    onStartResize={(e) => startResize(pendingBox, e)}
                  />
                )}

                {/* empty hint */}
                {savedPlaceholders.length === 0 && !pendingBox && (
                  <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                    <div className="text-center space-y-2 opacity-30 select-none">
                      <p className="text-5xl">↑</p>
                      <p className="text-sm text-slate-300 font-semibold">Drag a field type here</p>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* ── right panel ── */}
          <div className="w-72 flex-shrink-0 flex flex-col gap-3 overflow-y-auto custom-scrollbar">
            {pendingBox ? (
              <BoxConfigPanel
                box={pendingBox}
                onSave={handleSaveBox}
                onCancel={cancelPending}
                saving={saving}
                error={saveError}
              />
            ) : (
              <>
                <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
                  {savedPlaceholders.length === 0
                    ? 'No fields yet'
                    : `${savedPlaceholders.length} field${savedPlaceholders.length !== 1 ? 's' : ''} defined`}
                </p>
                {savedPlaceholders.length === 0 ? (
                  <p className="text-sm text-slate-600 italic">
                    Enable Edit Fields and drag a type onto the PDF to create a field.
                  </p>
                ) : (
                  savedPlaceholders.map((p) => (
                    <div
                      key={p.placeholderId}
                      onClick={() => editMode && setSelectedId(p.placeholderId)}
                      className={`flex items-start gap-2 p-3 rounded-xl border transition-all ${
                        selectedId === p.placeholderId
                          ? 'border-blue-500/40 bg-blue-600/5'
                          : 'border-premium-border bg-premium-card'
                      } ${editMode ? 'cursor-pointer hover:border-blue-500/30' : ''}`}
                    >
                      <div className={`w-7 h-7 rounded-lg border-2 flex items-center justify-center flex-shrink-0 ${TYPE_COLOR[p.fieldType]}`}>
                        <FieldTypeIcon type={p.fieldType} className="w-3.5 h-3.5" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1.5 flex-wrap">
                          <span className="font-medium text-sm text-slate-200 truncate">{p.label}</span>
                          {p.required && <Badge color="red">Req</Badge>}
                        </div>
                        <p className="text-xs text-slate-500 mt-0.5 truncate">
                          Role: {p.role} · Page {p.page} · #{p.order}
                        </p>
                        <p className="text-xs text-slate-600 font-mono mt-0.5">
                          ({p.x}, {p.y}) {p.width}×{p.height}
                        </p>
                      </div>
                    </div>
                  ))
                )}
              </>
            )}
          </div>
        </div>
      </div>
    );
  }

// ─── Signing Tab ──────────────────────────────────────────────────────────────

function SigningTab({ template, signingContext }) {
  const EMPTY_SIGNER = {
    signerId: '',
    role: '',
    displayName: '',
    email: '',
    routingOrder: 1,
  };

  // signingContext = { instanceId, signerId, canEdit } when opened from Incoming Contracts
  const isIncomingSigner = !!signingContext;
  const canEditAsSigner = !isIncomingSigner || !!signingContext?.canEdit;

  const [phase, setPhase] = useState(isIncomingSigner ? 'signing' : 'setup');
  const [instanceName, setInstanceName] = useState(`${template.title} - Signature Packet`);
  const [signers, setSigners] = useState([{ ...EMPTY_SIGNER }]);
  const [instance, setInstance] = useState(null);
  const [placeholders, setPlaceholders] = useState([]);
  const [fieldValues, setFieldValues] = useState({}); // { placeholderId: value }
  const [submitting, setSubmitting] = useState(false);
  const [finalizing, setFinalizing] = useState(false);
  const [finalized, setFinalized] = useState(null);
  const [error, setError] = useState(null);
  const [currentSignerId, setCurrentSignerId] = useState(signingContext?.signerId ?? '');
  const [loadingContext, setLoadingContext] = useState(isIncomingSigner);

  // When opened as an existing signer, load the instance and its signers
  useEffect(() => {
    if (!isIncomingSigner) return;
    storageApi.getContractInstance(signingContext.instanceId)
      .then((r) => {
        const inst = r.data;
        setInstance(inst);
        if (inst.signers?.length) {
          setSigners(inst.signers.map((s) => ({
            signerId: s.signerId,
            role: s.role,
            displayName: s.displayName,
            email: s.email ?? '',
            routingOrder: s.routingOrder,
          })));
        }
        // Use the provided signerId, or auto-select the first signer if none provided
        const resolvedSignerId = signingContext.signerId
          ?? inst.signers?.[0]?.signerId
          ?? '';
        setCurrentSignerId(resolvedSignerId);
      })
      .catch(() => setError('Could not load contract instance.'))
      .finally(() => setLoadingContext(false));
  }, [isIncomingSigner]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (phase === 'signing' && instance && !loadingContext) {
      storageApi.listContractPlaceholders(template.templateId)
        .then((r) => setPlaceholders(r.data))
        .catch(() => setError('Could not load fields.'));
    }
  }, [phase, instance, loadingContext, template.templateId]);

  useEffect(() => {
    if (!instance || placeholders.length === 0 || !currentSignerId) {
      return;
    }

    const existingValues = {};
    placeholders.forEach((placeholder) => {
      const submitted = instance.fieldValues?.find(
        (value) => value.placeholderId === placeholder.placeholderId && value.signerId === currentSignerId
      );
      if (submitted) {
        existingValues[placeholder.placeholderId] = submitted.signatureData ?? submitted.value ?? '';
      }
    });

    setFieldValues((prev) => ({ ...existingValues, ...prev }));
  }, [currentSignerId, instance, placeholders]);

  const addSigner = () =>
    setSigners((s) => [...s, { ...EMPTY_SIGNER, routingOrder: s.length + 1 }]);

  const removeSigner = (i) =>
    setSigners((s) => s.filter((_, idx) => idx !== i));

  const updateSigner = (i, key, value) =>
    setSigners((s) => s.map((sig, idx) => (idx === i ? { ...sig, [key]: value } : sig)));

  const handleSend = async (e) => {
    e.preventDefault();
    setError(null);
    if (!instanceName.trim()) { setError('Instance name is required.'); return; }
    const invalid = signers.find((s) => !s.signerId.trim() || !s.role.trim() || !s.displayName.trim());
    if (invalid) { setError('Every signer needs an ID, role, and display name.'); return; }

    setSubmitting(true);
    try {
      const res = await storageApi.createContractInstance({
        templateId: template.templateId,
        name: instanceName,
        signers,
      });
      setInstance(res.data);
      setCurrentSignerId(signers[0]?.signerId ?? '');
      setPhase('signing');
    } catch (err) {
      setError(err.response?.data || 'Failed to create signing instance.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitField = async (placeholderId) => {
    if (!canEditAsSigner) { setError('You do not have permission to edit fields for this step.'); return; }
    if (!currentSignerId) { setError('Select which signer is filling this field.'); return; }
    const value = fieldValues[placeholderId] ?? '';
    const placeholder = placeholders.find((p) => p.placeholderId === placeholderId);
    const isSignature = placeholder?.fieldType === 'signature' || placeholder?.fieldType === 'initials';

    setError(null);
    try {
      await storageApi.submitContractFieldValue(instance.instanceId, {
        signerId: currentSignerId,
        placeholderId,
        value: isSignature ? null : value,
        signatureData: isSignature ? value : null,
      });
      // refresh instance to update readyForFinalization
      const updated = await storageApi.getContractInstance(instance.instanceId);
      setInstance(updated.data);
    } catch (err) {
      setError(err.response?.data || 'Failed to submit field value.');
    }
  };

  const getSubmittedValue = (placeholderId, signerId = currentSignerId) =>
    instance?.fieldValues?.find(
      (value) => value.placeholderId === placeholderId && value.signerId === signerId
    ) ?? null;

  const currentSigner = signers.find((signer) => signer.signerId === currentSignerId) ?? null;
  const currentSignerRole = currentSigner?.role ?? null;
  const sortedPlaceholders = [...placeholders].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  const assignedPlaceholders = currentSignerRole
    ? sortedPlaceholders.filter((placeholder) => placeholder.role === currentSignerRole)
    : [];
  const pendingAssignedPlaceholders = assignedPlaceholders.filter(
    (placeholder) => !getSubmittedValue(placeholder.placeholderId)
  );

  const handleSubmitAssignedFields = async () => {
    if (!canEditAsSigner) {
      setError('You do not have permission to edit fields for this step.');
      return;
    }

    if (!currentSignerId) {
      setError('Could not determine which signer is filling the contract.');
      return;
    }

    if (pendingAssignedPlaceholders.length === 0) {
      return;
    }

    const missingRequired = pendingAssignedPlaceholders.find((placeholder) => {
      if (!placeholder.required) {
        return false;
      }
      const value = `${fieldValues[placeholder.placeholderId] ?? ''}`.trim();
      return value.length === 0;
    });

    if (missingRequired) {
      setError(`Fill the required field "${missingRequired.label}" before submitting.`);
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      for (const placeholder of pendingAssignedPlaceholders) {
        await handleSubmitField(placeholder.placeholderId);
      }
      const updated = await storageApi.getContractInstance(instance.instanceId);
      setInstance(updated.data);
    } catch (err) {
      setError(err.response?.data || 'Failed to submit your fields.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleFinalize = async () => {
    setFinalizing(true);
    setError(null);
    try {
      const res = await storageApi.finalizeContractInstance(instance.instanceId, {
        finalizedBy: currentSignerId || 'author',
        applyDigitalSignature: true,
      });
      setFinalized(res.data);
    } catch (err) {
      setError(err.response?.data || 'Finalization failed. Make sure all required fields are filled.');
    } finally {
      setFinalizing(false);
    }
  };

  if (loadingContext) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="w-6 h-6 animate-spin text-blue-400" />
        <span className="ml-3 text-sm text-slate-400">Loading contract…</span>
      </div>
    );
  }

  if (finalized) {
    return (
      <div className="flex flex-col items-center justify-center py-10 space-y-4 text-center">
        <div className="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center">
          <ShieldCheck className="w-8 h-8 text-emerald-400" />
        </div>
        <h3 className="text-lg font-bold text-emerald-400">Contract Finalized</h3>
        <p className="text-sm text-slate-400">Digital signature applied and integrity hash computed.</p>
        <div className="w-full bg-premium-card border border-premium-border rounded-2xl p-4 text-left space-y-2">
          <Row label="Instance ID" value={finalized.instanceId} mono />
          <Row label="Status" value={finalized.status} />
          <Row label="Finalized At" value={new Date(finalized.finalizedAt).toLocaleString()} />
          {finalized.finalArtifactHash && (
            <Row label="Artifact Hash (SHA-256)" value={finalized.finalArtifactHash} mono truncate />
          )}
        </div>
      </div>
    );
  }

  if (phase === 'signing' && instance) {
    return (
      <div className="flex flex-col gap-4 h-full min-h-[620px]">
        <div className="bg-premium-card rounded-xl border border-premium-border p-4 space-y-3 flex-shrink-0">
          <div className="flex items-center justify-between gap-3 flex-wrap">
            <div>
              <p className="font-semibold text-white">{instance.name}</p>
              <p className="text-xs text-slate-500 font-mono">{instance.instanceId}</p>
            </div>
            <Badge color={instance.readyForFinalization ? 'green' : 'amber'}>
              {instance.status}
            </Badge>
          </div>

          <div>
            <label className={labelCls}>Signing as</label>
            {isIncomingSigner ? (
              <div className={`${inputCls} flex items-center gap-2 cursor-default select-none opacity-80`}>
                <span className="flex-1">
                  {currentSigner?.displayName ?? currentSignerId}
                  {' '}({currentSigner?.role ?? ''})
                </span>
                <Lock className="w-3.5 h-3.5 text-slate-500 flex-shrink-0" />
              </div>
            ) : (
              <select
                className={inputCls}
                value={currentSignerId}
                onChange={(e) => setCurrentSignerId(e.target.value)}
              >
                {signers.map((s) => (
                  <option key={s.signerId} value={s.signerId}>
                    {s.displayName} ({s.role})
                  </option>
                ))}
              </select>
            )}
          </div>
        </div>

        <div className="flex gap-4 flex-1 min-h-0">
          <div className="flex-1 relative rounded-xl overflow-hidden border border-premium-border bg-[#08090b] min-h-[540px]">
            <iframe
              title="Contract Document"
              src={storageApi.getDownloadUrl(template.bucket, template.objectKey)}
              className="w-full h-full border-0"
            />

            <div className="absolute inset-0 pointer-events-none">
              {sortedPlaceholders.map((placeholder) => {
                const submittedValue = getSubmittedValue(placeholder.placeholderId);
                const isAssignedToCurrentSigner = !!currentSignerRole && placeholder.role === currentSignerRole;
                const displayValue = submittedValue?.signatureData ?? submittedValue?.value ?? fieldValues[placeholder.placeholderId] ?? '';
                const isSubmitted = !!submittedValue;
                const isSignature = placeholder.fieldType === 'signature' || placeholder.fieldType === 'initials';

                return (
                  <div
                    key={placeholder.placeholderId}
                    style={{
                      position: 'absolute',
                      left: Number(placeholder.x) || 0,
                      top: Number(placeholder.y) || 0,
                      width: Math.max(MIN_BOX_WIDTH, Number(placeholder.width) || MIN_BOX_WIDTH),
                      height: Math.max(MIN_BOX_HEIGHT, Number(placeholder.height) || MIN_BOX_HEIGHT),
                    }}
                    className="pointer-events-auto"
                  >
                    <div className={`absolute -top-6 left-0 flex items-center gap-1 px-2 py-0.5 rounded-md border text-[10px] font-bold uppercase tracking-wider shadow-sm ${
                      isSubmitted
                        ? 'border-emerald-500/40 bg-emerald-600/20 text-emerald-300'
                        : isAssignedToCurrentSigner
                          ? 'border-blue-500/40 bg-blue-600/20 text-blue-300'
                          : 'border-slate-700 bg-slate-900/80 text-slate-400'
                    }`}>
                      <FieldTypeIcon type={placeholder.fieldType} className="w-3 h-3" />
                      <span className="truncate max-w-[160px]">{placeholder.label}</span>
                    </div>

                    {isAssignedToCurrentSigner && !isSubmitted && canEditAsSigner && (
                      <InlineSigningField
                        placeholder={placeholder}
                        value={fieldValues[placeholder.placeholderId] ?? ''}
                        onChange={(value) => setFieldValues((prev) => ({ ...prev, [placeholder.placeholderId]: value }))}
                      />
                    )}

                    {isAssignedToCurrentSigner && isSubmitted && (
                      <div className="w-full h-full rounded-lg border border-emerald-500/40 bg-emerald-600/15 text-emerald-100 px-2 py-1.5 text-xs overflow-hidden">
                        <div className="flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider text-emerald-300 mb-1">
                          <CheckCircle className="w-3 h-3" /> Submitted
                        </div>
                        <div className={`${isSignature ? 'font-signature text-lg leading-tight' : 'leading-tight'} truncate`}>
                          {displayValue || 'Completed'}
                        </div>
                      </div>
                    )}

                    {(!isAssignedToCurrentSigner || !canEditAsSigner) && (
                      <div className="w-full h-full rounded-lg border border-slate-700/80 bg-slate-950/65 text-slate-500 px-2 py-1.5 text-[11px] flex items-center justify-between gap-2 overflow-hidden">
                        <span className="truncate">{isAssignedToCurrentSigner ? 'Locked until your turn' : placeholder.role}</span>
                        {isSubmitted ? <CheckCircle className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" /> : <Lock className="w-3.5 h-3.5 flex-shrink-0" />}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          <div className="w-80 flex-shrink-0 flex flex-col gap-3 overflow-y-auto custom-scrollbar">
            <div className="bg-premium-card border border-premium-border rounded-xl p-4 space-y-3">
              <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Your fields</p>
              {assignedPlaceholders.length === 0 ? (
                <p className="text-sm text-slate-500 italic">No fields are assigned to your role in this document.</p>
              ) : (
                assignedPlaceholders.map((placeholder) => {
                  const isSubmitted = !!getSubmittedValue(placeholder.placeholderId);
                  return (
                    <div
                      key={placeholder.placeholderId}
                      className={`rounded-xl border px-3 py-2 ${isSubmitted ? 'border-emerald-500/30 bg-emerald-600/5' : 'border-premium-border bg-premium-dark'}`}
                    >
                      <div className="flex items-center gap-2 justify-between">
                        <div className="flex items-center gap-2 min-w-0">
                          <FieldTypeIcon type={placeholder.fieldType} className="w-3.5 h-3.5 text-blue-400 flex-shrink-0" />
                          <span className="text-sm text-slate-200 truncate">{placeholder.label}</span>
                        </div>
                        {isSubmitted ? <CheckCircle className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" /> : <Badge color="amber">Pending</Badge>}
                      </div>
                    </div>
                  );
                })
              )}
            </div>

            {error && (
              <p className="text-xs text-red-400 flex items-center gap-1.5">
                <AlertCircle className="w-3.5 h-3.5" />{error}
              </p>
            )}

            <div className="bg-premium-card border border-premium-border rounded-xl p-4 space-y-3 mt-auto">
              <p className="text-xs text-slate-500">
                {canEditAsSigner
                  ? 'Fill your assigned fields directly on the document, then submit them to advance the workflow.'
                  : 'This contract is visible to you, but fields stay locked until the workflow reaches your step.'}
              </p>
              <button
                type="button"
                onClick={handleSubmitAssignedFields}
                disabled={!canEditAsSigner || submitting || pendingAssignedPlaceholders.length === 0}
                className="w-full px-4 py-2.5 bg-blue-600 hover:bg-blue-500 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl font-bold text-sm transition-all flex items-center justify-center gap-2 shadow-lg shadow-blue-500/20"
              >
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                {!canEditAsSigner ? 'Read Only Until Your Turn' : pendingAssignedPlaceholders.length === 0 ? 'All Your Fields Submitted' : 'Submit My Fields'}
              </button>
              <button
                type="button"
                onClick={handleFinalize}
                disabled={!canEditAsSigner || finalizing || !instance.readyForFinalization}
                title={!instance.readyForFinalization ? 'Fill all required fields first' : 'Finalize and apply digital signature'}
                className="w-full px-4 py-2.5 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl font-bold text-sm transition-all flex items-center justify-center gap-2 shadow-lg shadow-emerald-500/20"
              >
                {finalizing ? <Loader2 className="w-4 h-4 animate-spin" /> : <ShieldCheck className="w-4 h-4" />}
                Finalize Contract
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ── setup phase ──
  return (
    <form onSubmit={handleSend} className="space-y-5">
      <div>
        <label className={labelCls}>Contract / Envelope Name</label>
        <input
          className={inputCls}
          placeholder="e.g. NDA — Acme Corp"
          value={instanceName}
          onChange={(e) => setInstanceName(e.target.value)}
        />
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <label className={labelCls}>Signers</label>
          <button
            type="button"
            onClick={addSigner}
            className="flex items-center gap-1 text-xs font-semibold text-blue-400 hover:text-blue-300 transition-colors"
          >
            <Plus className="w-3.5 h-3.5" /> Add Signer
          </button>
        </div>

        {signers.map((signer, i) => (
          <div key={i} className="bg-premium-card border border-premium-border rounded-xl p-4 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Signer {i + 1}</span>
              {signers.length > 1 && (
                <button type="button" onClick={() => removeSigner(i)} className="text-slate-600 hover:text-red-500 transition-colors">
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              )}
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className={labelCls}>Display Name</label>
                <input className={inputCls} placeholder="Jane Smith" value={signer.displayName} onChange={(e) => updateSigner(i, 'displayName', e.target.value)} />
              </div>
              <div>
                <label className={labelCls}>Role</label>
                <input className={inputCls} placeholder="e.g. Signee" value={signer.role} onChange={(e) => updateSigner(i, 'role', e.target.value)} />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className={labelCls}>Signer ID</label>
                <input className={inputCls} placeholder="user-123" value={signer.signerId} onChange={(e) => updateSigner(i, 'signerId', e.target.value)} />
              </div>
              <div>
                <label className={labelCls}>Email</label>
                <input type="email" className={inputCls} placeholder="jane@example.com" value={signer.email} onChange={(e) => updateSigner(i, 'email', e.target.value)} />
              </div>
            </div>

            <div className="w-28">
              <label className={labelCls}>Routing Order</label>
              <input type="number" min="1" className={inputCls} value={signer.routingOrder} onChange={(e) => updateSigner(i, 'routingOrder', Number(e.target.value))} />
            </div>
          </div>
        ))}
      </div>

      {error && (
        <p className="text-xs text-red-400 flex items-center gap-1.5">
          <AlertCircle className="w-3.5 h-3.5" />{error}
        </p>
      )}

      <div className="flex justify-end">
        <button
          type="submit"
          disabled={submitting}
          className="px-6 py-2.5 bg-blue-600 hover:bg-blue-500 text-white rounded-xl font-bold text-sm transition-all flex items-center gap-2 shadow-lg shadow-blue-500/20 disabled:opacity-60"
        >
          {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
          Send for Signing
        </button>
      </div>
    </form>
  );
}

// ─── FieldInput ───────────────────────────────────────────────────────────────

function FieldInput({ placeholder: p, value, onChange, onSubmit }) {
  const isSignature = p.fieldType === 'signature' || p.fieldType === 'initials';

  return (
    <div className="flex gap-2 items-end">
      <div className="flex-1">
        {p.fieldType === 'checkbox' ? (
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={value === 'true'}
              onChange={(e) => onChange(e.target.checked ? 'true' : 'false')}
              className="w-4 h-4 rounded accent-blue-500"
            />
            <span className="text-sm text-slate-300">Checked</span>
          </label>
        ) : p.fieldType === 'date' ? (
          <input
            type="date"
            className={inputCls}
            value={value}
            onChange={(e) => onChange(e.target.value)}
          />
        ) : isSignature ? (
          <textarea
            className={`${inputCls} resize-none font-signature text-xl leading-tight`}
            rows={2}
            placeholder={p.fieldType === 'initials' ? 'Type initials…' : 'Type your full name as signature…'}
            value={value}
            onChange={(e) => onChange(e.target.value)}
          />
        ) : (
          <input
            type="text"
            className={inputCls}
            placeholder={p.label}
            maxLength={p.maxLength ?? undefined}
            value={value}
            onChange={(e) => onChange(e.target.value)}
          />
        )}
      </div>
      <button
        type="button"
        onClick={onSubmit}
        className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-sm font-bold transition-all flex items-center gap-1.5 flex-shrink-0"
      >
        <CheckCircle className="w-3.5 h-3.5" />
        Submit
      </button>
    </div>
  );
}

function InlineSigningField({ placeholder: p, value, onChange }) {
  const isSignature = p.fieldType === 'signature' || p.fieldType === 'initials';
  const frameCls = `w-full h-full rounded-lg border-2 ${TYPE_COLOR[p.fieldType] ?? TYPE_COLOR.text} bg-slate-950/85 shadow-lg overflow-hidden`;

  if (p.fieldType === 'checkbox') {
    return (
      <label className={`${frameCls} flex items-center justify-center gap-2 px-2 cursor-pointer select-none`}>
        <input
          type="checkbox"
          checked={value === 'true'}
          onChange={(e) => onChange(e.target.checked ? 'true' : 'false')}
          className="w-4 h-4 rounded accent-blue-500"
        />
        <span className="text-xs font-semibold text-slate-100">Check</span>
      </label>
    );
  }

  if (p.fieldType === 'date') {
    return (
      <input
        type="date"
        className={`${frameCls} px-2 text-xs text-slate-100 focus:outline-none`}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    );
  }

  if (isSignature) {
    return (
      <textarea
        className={`${frameCls} resize-none px-2 py-1 text-slate-100 focus:outline-none ${p.fieldType === 'signature' ? 'font-signature text-xl leading-tight' : 'font-signature text-lg leading-tight'}`}
        placeholder={p.fieldType === 'initials' ? 'Type initials…' : 'Type your signature…'}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    );
  }

  return (
    <input
      type="text"
      className={`${frameCls} px-2 text-sm text-slate-100 focus:outline-none`}
      placeholder={p.label}
      maxLength={p.maxLength ?? undefined}
      value={value}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}

// ─── Row helper ───────────────────────────────────────────────────────────────

function Row({ label, value, mono, truncate }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-[10px] uppercase tracking-wider font-bold text-slate-500">{label}</span>
      <span className={`text-sm text-slate-200 ${mono ? 'font-mono text-xs' : ''} ${truncate ? 'truncate' : ''}`}>{value}</span>
    </div>
  );
}

// ─── Main Modal ───────────────────────────────────────────────────────────────

const TABS = [
  { id: 'placeholders', label: 'Placeholders', Icon: FileText },
  { id: 'sign',         label: 'Sign',         Icon: PenLine },
];

export default function ContractManagerModal({ template, onClose, signingContext }) {
  const [activeTab, setActiveTab] = useState(signingContext ? 'sign' : 'placeholders');

  const availableTabs = signingContext ? TABS.filter((tab) => tab.id === 'sign') : TABS;
  const isCanvasLayout = activeTab === 'placeholders' || activeTab === 'sign';
  const pdfUrl = storageApi.getDownloadUrl(template.bucket, template.objectKey);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={onClose} />
      <div
        className={`glass w-full rounded-2xl border border-premium-border shadow-2xl relative flex flex-col max-h-[90vh] animate-in fade-in zoom-in duration-200 transition-all ${
          isCanvasLayout ? 'max-w-6xl' : 'max-w-2xl'
        }`}
      >
        {/* header */}
        <div className="p-5 border-b border-premium-border flex items-center justify-between flex-shrink-0">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-9 h-9 rounded-xl bg-blue-600/10 border border-blue-500/30 flex items-center justify-center flex-shrink-0">
              <FileText className="w-5 h-5 text-blue-400" />
            </div>
            <div className="min-w-0">
              <h3 className="font-bold text-white truncate">{template.title}</h3>
              <p className="text-xs text-slate-500 font-mono truncate">
                {template.bucket}/{template.objectKey}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="text-slate-500 hover:text-white flex-shrink-0 ml-4">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* tabs */}
        {availableTabs.length > 1 && (
        <div className="flex border-b border-premium-border flex-shrink-0">
          {availableTabs.map(({ id, label, Icon }) => (
            <button
              key={id}
              onClick={() => setActiveTab(id)}
              className={`flex items-center gap-2 px-6 py-3.5 text-sm font-semibold transition-all relative ${
                activeTab === id ? 'text-blue-400' : 'text-slate-500 hover:text-slate-300'
              }`}
            >
              <Icon className="w-4 h-4" />
              {label}
              {activeTab === id && (
                <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-500 rounded-t-full" />
              )}
            </button>
          ))}
        </div>
        )}

        {/* body — no scroll on placeholders tab so the editor fills the space */}
        <div className={`flex-1 min-h-0 p-4 ${isCanvasLayout ? 'overflow-hidden' : 'overflow-y-auto custom-scrollbar p-6'}`}>
          {activeTab === 'placeholders' && (
            <PlaceholdersTab templateId={template.templateId} pdfUrl={pdfUrl} />
          )}
          {activeTab === 'sign' && (
            <SigningTab template={template} signingContext={signingContext} />
          )}
        </div>
      </div>
    </div>
  );
}
