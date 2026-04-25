# Project Charter: Open S3-Compatible Object Store (v1)

## 1. Mission
Build an open source, commercially friendly object storage server that is S3-compatible for core workloads, self-hostable, and production-ready for SMB and mid-market teams.

## 2. Vision and Product Position
Provide a practical alternative to mainstream S3 implementations by prioritizing:
- Predictable S3 compatibility for the most used APIs
- Operational simplicity for self-hosting
- Strong default security and observability
- Commercially safe licensing and governance

## 3. License and Governance
- License: Apache-2.0
- Contributor policy: DCO sign-off required
- Security policy: responsible disclosure with private reporting channel
- Governance model: maintainers + public RFC process for breaking changes
- Release policy: semantic versioning, signed release artifacts, changelog per release

## 4. v1 Scope
### 4.1 In Scope (Must Have)
- Bucket operations: create, list, delete
- Object operations: put, get, head, delete
- Object listing: ListObjectsV2 with prefix and delimiter
- Multipart upload: initiate, upload part, complete, abort, list parts
- Request auth: SigV4 for header-signed requests
- Presigned URLs: get/put with expiration and canonical request validation
- Metadata: system metadata and x-amz-meta-* user metadata
- Range requests: single range support for GET
- Conditional requests: If-Match, If-None-Match, If-Modified-Since, If-Unmodified-Since
- Encryption: SSE-S3 (service-managed keys)
- Access control: bucket policies and access keys with scoped permissions
- Object integrity: ETag behavior for non-multipart and multipart objects
- Basic versioning: enable/suspend, read latest and specific version
- Observability: Prometheus metrics, JSON logs, trace IDs on requests

### 4.2 Out of Scope (v1)
- Full IAM parity with AWS IAM policy language
- Object Lock compliance mode and legal hold
- Cross-region replication
- Glacier-style storage classes and lifecycle transitions
- Batch operations and advanced inventory reporting
- SSE-KMS and customer-provided keys (SSE-C)

## 5. Compatibility Contract
- Publish a public API compatibility matrix with status: supported, partial, not supported
- For every supported operation, document exact behavior differences vs AWS S3
- Maintain SDK interoperability tests for AWS SDKs: .NET, Java, JavaScript, Python, Go
- Avoid claiming "full S3 compatibility" until conformance target is met

## 6. Non-Functional Requirements (v1)
- Durability target: 11 9s logical durability with erasure coding or replicated storage profiles
- Availability target: 99.9% in HA deployment profile
- Performance target: p95 GET/PUT latency under defined benchmark profile
- Scalability target: horizontal API stateless scale-out
- Multi-tenancy: account and bucket isolation by access policy
- Backup and restore: documented and tested procedures

## 7. Architecture (v1)
### 7.1 Control and Data Planes
- API Gateway/Frontend: S3 HTTP parsing, SigV4 validation, policy enforcement
- Metadata Service: bucket/object/version/multipart metadata source of truth
- Data Service: chunked blob persistence, erasure coding or replication profile
- Background Workers: multipart cleanup, garbage collection, healing, compaction

### 7.2 Storage Layout
- Object data: immutable chunks addressed by content hash
- Object index: key -> object version pointer
- Version records: metadata + part manifest + checksum state
- Metadata store: strongly consistent store for transactional object state updates

### 7.3 Consistency Model
- Strong read-after-write consistency for PUT, DELETE, and LIST within a bucket namespace
- Atomic metadata commit for complete multipart upload
- Idempotency keys for retried mutating operations

## 8. Security Baseline (v1)
- TLS by default for all endpoints
- SigV4 canonical request verification hardening
- Replay protection with strict timestamp drift window
- Access key lifecycle: create, rotate, revoke, audit
- Secrets in external secret manager support
- Audit logs for all mutating operations with actor identity and source IP
- Dependency and image scanning in CI

## 9. Operations and SRE Baseline
- Health endpoints: liveness/readiness/startup
- Metrics: request rate, error rate, latency, saturation, auth failures
- Distributed tracing hooks (OpenTelemetry)
- Structured logging with request IDs and bucket/key fields
- Runbooks for: node loss, metadata outage, disk pressure, restore process
- Upgrade playbook with rollback path

## 10. v1 API Surface
### 10.1 Bucket APIs
- PUT Bucket
- GET Service (List Buckets)
- DELETE Bucket
- GET Bucket (ListObjectsV2)
- GET/PUT Bucket policy

### 10.2 Object APIs
- PUT Object
- GET Object
- HEAD Object
- DELETE Object
- Multipart APIs (CreateMultipartUpload, UploadPart, CompleteMultipartUpload, AbortMultipartUpload, ListParts)

### 10.3 Presigned URLs
- GET and PUT presigned URLs
- Signature and expiration validation compatible with SigV4

## 11. 90-Day Execution Plan
### Phase A (Days 1-30): Foundations
- Finalize architecture RFC and compatibility matrix format
- Implement metadata schema and transactional write path
- Implement SigV4 verifier with canonical request test vectors
- Implement core bucket/object CRUD and HEAD semantics
- Add OpenAPI-like protocol docs and error code catalog

### Phase B (Days 31-60): Core Compatibility
- Implement ListObjectsV2 prefix/delimiter pagination
- Implement multipart upload lifecycle with atomic completion
- Implement metadata persistence and replay on GET/HEAD
- Implement presigned GET/PUT with integration tests
- Add bucket policy enforcement and key rotation endpoints

### Phase C (Days 61-90): Production Hardening
- Add HA deployment profile and resilience tests
- Add Prometheus metrics, tracing, and audit event pipeline
- Run SDK compatibility suite across 5 AWS SDKs
- Add chaos/failure tests for partial writes and node failures
- Publish v1.0.0-rc1 with benchmark and conformance report

## 12. CI and Conformance Pipeline
- Stage 1: lint, unit tests, static analysis, dependency vulnerability scan
- Stage 2: protocol tests for canonical request parsing and SigV4 edge cases
- Stage 3: integration tests for bucket/object APIs against ephemeral cluster
- Stage 4: SDK interoperability tests (.NET, Java, JS, Python, Go)
- Stage 5: resilience tests (network partitions, restart during multipart complete)
- Stage 6: publish conformance matrix artifact and benchmark results

## 13. Risks and Mitigations
- Risk: protocol edge-case drift from AWS behavior
- Mitigation: golden test corpus and differential tests against AWS S3 behavior

- Risk: metadata inconsistency under concurrent writes
- Mitigation: optimistic concurrency control + transactional metadata commits

- Risk: operational complexity for self-hosters
- Mitigation: opinionated defaults, single-binary mode, and deployment templates

- Risk: legal uncertainty for adopters
- Mitigation: Apache-2.0, clear trademark and branding policy

## 14. Success Criteria for v1.0
- 95%+ pass rate on defined v1 compatibility test suite
- All in-scope APIs documented and validated in CI
- HA deployment passing failover and restore drills
- Public benchmark and conformance reports published
- At least 3 external pilot users running non-test workloads

## 15. Immediate Next Deliverables (This Repository)
- Create docs/compatibility-matrix.md and populate v1 API statuses
- Create docs/rfcs/0001-metadata-model.md
- Create docs/rfcs/0002-sigv4-validation.md
- Create test harness for SigV4 vectors and SDK integration smoke tests
- Replace emulator-only assumptions in API layer with protocol-correct metadata handling
