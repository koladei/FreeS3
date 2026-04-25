# RFC 0002: SigV4 Validation and Presigned URL Semantics

- Status: Draft
- Authors: Core Maintainers
- Created: 2026-04-24
- Target Version: v1.0.0

## 1. Summary
Define Signature Version 4 (SigV4) validation behavior for header-signed and presigned S3 requests to ensure client and SDK interoperability.

## 2. Motivation
S3 compatibility depends heavily on exact canonicalization and signature verification behavior. Small deviations can break SDKs and CLI tooling.

## 3. Goals
- Validate SigV4 signatures for authenticated requests
- Support presigned URLs for GET and PUT
- Enforce expiration and request-time skew windows
- Provide deterministic and debuggable auth error responses

## 4. Non-Goals
- SigV2 support
- STS federation and temporary credential issuance in v1

## 5. Canonical Request Rules
Validation implementation must include:
- HTTP method exact match
- Canonical URI normalization rules
- Canonical query string sorting and encoding
- Canonical headers (lowercased, trimmed, sorted)
- Signed headers exact match
- Payload hash behavior (`x-amz-content-sha256`), including `UNSIGNED-PAYLOAD` where applicable

## 6. Credential Scope
Parse and validate:
- Access key ID
- Date (`yyyymmdd`)
- Region
- Service (`s3`)
- Terminal (`aws4_request`)

Reject mismatched or malformed scopes with protocol-correct error responses.

## 7. Clock and Expiration Policy
- Allowed clock skew: +/- 5 minutes (configurable)
- Presigned URL max expiration: 7 days
- Requests outside skew or expiration fail with auth error

## 8. Presigned URL Semantics
### 8.1 Presigned GET
- Must validate all signed query parameters
- Must enforce signed headers if present in signature

### 8.2 Presigned PUT
- Must validate content hash mode used when signing
- If signed with explicit content-type, enforce exact match

## 9. Error Model
Return S3-style auth errors:
- `SignatureDoesNotMatch`
- `InvalidAccessKeyId`
- `RequestTimeTooSkewed`
- `AccessDenied`

Include request ID and host ID in error response for diagnostics.

## 10. Security Considerations
- Constant-time compare for computed vs received signature
- Replay protections for narrow validity windows
- Strict normalization to prevent ambiguous parsing attacks
- Audit logging for authentication failures (without leaking secrets)

## 11. Testing Requirements
- Golden canonical request test vectors
- Differential tests against AWS behavior for edge cases
- SDK integration tests for .NET/Java/JS/Python/Go
- Negative tests for malformed signatures and query tampering

## 12. Rollout Plan
- Milestone A: Canonicalization library + vector tests
- Milestone B: Header SigV4 middleware in API path
- Milestone C: Presigned URL validation and integration tests

## 13. Open Questions
- Region strategy for self-hosted deployments (fixed vs configurable multi-region)
- Whether to support anonymous bucket policy access in v1 or strictly key-based access first
