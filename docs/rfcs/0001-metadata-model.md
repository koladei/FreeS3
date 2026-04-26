# RFC 0001: Object Metadata Model

- Status: Draft
- Authors: Core Maintainers
- Created: 2026-04-24
- Target Version: v1.0.0

## 1. Summary
Define the canonical metadata model for S3-compatible object operations so GET/HEAD semantics match expected SDK behavior.

## 2. Motivation
Current implementation can serve object content but does not fully persist and replay protocol metadata. This causes SDK differences and weak compatibility guarantees.

## 3. Goals
- Persist object metadata atomically with object write completion
- Replay metadata accurately on GET/HEAD
- Support user-defined metadata (`x-amz-meta-*`)
- Support multipart completion metadata semantics

## 4. Non-Goals
- Full object-lock compliance fields in v1
- SSE-KMS and SSE-C metadata fields in v1

## 5. Metadata Schema (Logical)
Each object version record stores:
- `bucket`: string
- `key`: string
- `versionId`: string
- `isLatest`: bool
- `etag`: string
- `sizeBytes`: long
- `lastModifiedUtc`: datetime
- `storageClass`: string (default `STANDARD`)
- `checksum`: optional object for future checksum variants
- `systemMetadata`: map
  - `Content-Type`
  - `Content-Encoding`
  - `Cache-Control`
  - `Content-Disposition`
  - `Content-Language`
  - `Expires`
- `userMetadata`: case-insensitive map for `x-amz-meta-*`
- `multipart`: optional
  - `partCount`
  - `parts[]` with part number, etag, size

## 6. API Semantics
### 6.1 PUT Object
- Persist object bytes and metadata as one logical commit
- If `Content-Type` not provided, infer or default to `application/octet-stream`
- Persist all `x-amz-meta-*` request headers

### 6.2 GET Object
- Return stored object bytes
- Replay persisted system metadata headers
- Replay `x-amz-meta-*` headers exactly

### 6.3 HEAD Object
- Return all metadata headers without response body
- Ensure parity with GET headers for same object version

### 6.4 CopyObject
- `x-amz-metadata-directive: COPY`: preserve source metadata
- `x-amz-metadata-directive: REPLACE`: replace metadata with destination request values

## 7. Consistency and Transactions
- Metadata commit must occur only after data write integrity verification
- Multipart completion must atomically publish final version record
- In case of partial failure, system must not expose half-committed metadata

## 8. Edge Cases
- Case-insensitive metadata keys should preserve original casing on replay where possible
- Duplicate metadata keys in request are normalized per protocol rules
- Zero-byte objects still require full metadata replay support

## 9. Security Considerations
- Validate metadata header size limits to prevent abuse
- Sanitize and validate response headers to avoid injection issues

## 10. Testing Requirements
- Unit tests for metadata normalization and persistence
- Integration tests for GET/HEAD parity
- SDK tests verifying `x-amz-meta-*` roundtrip correctness
- Multipart metadata replay tests

## 11. Rollout Plan
- Milestone A: Add metadata schema and persistence abstraction
- Milestone B: Wire PUT/GET/HEAD to schema
- Milestone C: Add CopyObject metadata directive handling

## 12. Implementation Notes (2026-04-25)
Current implementation persists bucket and object metadata in relational storage through `AppDbContext`:
- `StorageBuckets`
  - `bucketName` (PK)
  - `createdAt`, `updatedAt`
  - `policyJson`
  - `objectCount`, `totalSizeBytes`
- `StorageObjects`
  - `id` (PK)
  - `bucketName` (FK to `StorageBuckets`)
  - `objectKey` (unique with `bucketName`)
  - `sizeBytes`, `contentType`
  - `createdAt`, `updatedAt`

Operational behavior:
- Bucket creation/deletion updates metadata tables.
- Object upload and delete update object records and bucket aggregates.
- Object listing performs metadata reconciliation for local-disk state vs DB state.
- Bucket policy updates persist both file (`.policy.json`) and DB metadata (`policyJson`).

### 12.1 Runtime Database Provider Choice
`App_Storage` supports provider selection through config:
- `DatabaseProvider = SqlServer` (default)
- `DatabaseProvider = PostgreSql`

Required config keys:
- `ConnectionStrings:DefaultConnection`
- `DatabaseProvider`

Example SQL Server connection string:
`Server=localhost;Database=Escrow;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True;`

Example PostgreSQL connection string:
`Host=localhost;Port=5432;Database=escrow;Username=postgres;Password=...`

For PostgreSQL deployments, the runtime must include the Npgsql EF provider assembly (`Npgsql.EntityFrameworkCore.PostgreSQL`) so `UseNpgsql` can be activated.

## 13. Open Questions
- Preferred backing store for metadata (SQL vs embedded KV vs external strongly consistent store)
- Version ID generation format for deterministic testing
