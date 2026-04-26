# S3 Compatibility Matrix (v1)

## Status Legend
- `supported`: Implemented and covered by automated tests
- `partial`: Implemented with documented limitations
- `planned`: In roadmap, not yet implemented
- `not-supported`: Explicitly out of scope for v1

## SDK Validation Targets
- AWS SDK for .NET
- AWS SDK for Java v2
- AWS SDK for JavaScript v3
- boto3 (Python)
- AWS SDK for Go v2

## Bucket APIs
| API | Status | Notes | Test Coverage |
|---|---|---|---|
| CreateBucket | planned | Basic create semantics | pending |
| ListBuckets | planned | Owner fields may be simplified in v1 | pending |
| DeleteBucket | planned | Must fail when bucket not empty | pending |
| GetBucketLocation | planned | Region response required for SDK parity | pending |
| GetBucketPolicy | planned | JSON policy retrieval | pending |
| PutBucketPolicy | planned | Policy validation subset for v1 | pending |

## Object APIs
| API | Status | Notes | Test Coverage |
|---|---|---|---|
| PutObject | partial | Core upload works; metadata persistence needs RFC-0001 alignment | partial |
| GetObject | partial | Inline content serving works; conditional and metadata semantics incomplete | partial |
| HeadObject | partial | Existence check works; full metadata header replay incomplete | partial |
| DeleteObject | planned | Must return idempotent delete behavior | pending |
| ListObjectsV2 | planned | Prefix, delimiter, continuation tokens required | pending |
| CopyObject | planned | Metadata-directive behavior required (`COPY`/`REPLACE`) | pending |

## Multipart Upload APIs
| API | Status | Notes | Test Coverage |
|---|---|---|---|
| CreateMultipartUpload | planned | Initialize upload ID and metadata | pending |
| UploadPart | planned | Part number and ETag required | pending |
| ListParts | planned | Required for completion workflows | pending |
| CompleteMultipartUpload | planned | Atomic object commit required | pending |
| AbortMultipartUpload | planned | Background cleanup should also exist | pending |

## Auth and Signing
| Capability | Status | Notes | Test Coverage |
|---|---|---|---|
| SigV4 Header Auth | planned | Canonical request parity is critical path | pending |
| Presigned GET URL | planned | Expiry and signed headers must be enforced | pending |
| Presigned PUT URL | planned | Payload hash handling defined in RFC-0002 | pending |
| Access Key Rotation | planned | Admin and automation API required | pending |

## Data Features
| Capability | Status | Notes | Test Coverage |
|---|---|---|---|
| System Metadata Persistence | partial | Bucket/object metadata now persisted in DB (bucket policy, content-type, object size, object timestamps). Full header replay parity is still pending. | partial |
| User Metadata (`x-amz-meta-*`) | planned | Case-insensitive keys, exact replay semantics | pending |
| Object Versioning (Basic) | planned | Enable/suspend and latest/version retrieval | pending |
| Range GET (single range) | partial | Basic range support exists in ASP.NET file streaming | partial |
| SSE-S3 | planned | Service-managed key encryption at rest | pending |

## Explicitly Out of Scope for v1
- Full AWS IAM parity
- Object Lock compliance mode and legal hold
- Cross-region replication
- SSE-KMS and SSE-C
- Glacier-style tiers and lifecycle transitions

## Conformance Goals
- `v1.0.0-rc1`: 80% pass rate on in-scope APIs across all target SDK suites
- `v1.0.0`: 95% pass rate on in-scope APIs across all target SDK suites

## Change Log
- 2026-04-24: Initial matrix created from project charter v1
