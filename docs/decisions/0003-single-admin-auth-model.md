# ADR 0003 — Single-Admin Authentication for V1

## Status

Accepted

## Context

Amanah Drive V1 is explicitly a personal, single-user product. A full multi-user auth system (open registration, roles, per-user data isolation at scale, invitation flows, password reset via email, etc.) is standard for a SaaS product, but it's a meaningfully larger surface to build and secure correctly — and none of it is needed for a system with exactly one administrator.

## Decision

V1 supports exactly one admin account, created once via a bootstrap flow rather than open registration: `POST /auth/register` only succeeds if no admin account exists yet, and requires a shared `X-Bootstrap-Token` secret (configured via environment variable) rather than being a public endpoint. There is no registration UI in the frontend — bootstrapping is a one-time operation done directly against the API.

Everything downstream of that account still uses production-grade practices: Argon2id password hashing, JWT access tokens with short expiry, refresh-token rotation with reuse detection (a reused/stale refresh token revokes the whole token family), HttpOnly `__Host-`-prefixed cookies for the refresh token, login rate limiting, and account lockout after repeated failed attempts.

## Consequences

- The auth surface is small and fully covered by tests (login, refresh rotation, reuse detection, lockout, bootstrap-token validation) rather than partially covering a much larger multi-user surface.
- Adding multi-user support later is a real, scoped feature addition (roles, per-user data isolation checks throughout `Drive`/`Processing`/`SearchChat`, self-service registration) — not a security retrofit, since the underlying token/session/hashing mechanics are already correct and would carry forward unchanged.
- The bootstrap token is a one-time secret: once an admin account exists, `/auth/register` always rejects further attempts regardless of the token supplied, closing that door immediately after first use.
