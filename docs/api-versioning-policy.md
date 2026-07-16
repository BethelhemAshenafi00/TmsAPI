## Communication

API migrations must be communicated before a version is retired.

The process includes:

- Adding Deprecation, Sunset, and Link headers.
- Updating the CHANGELOG.
- Informing teams that use the API.
- Scheduling the V1 shutdown date.

Documentation should explain how clients can migrate to the new version.
## Version Skipping

Clients are not required to migrate through every API version. For example, a client can move from V1 directly to V3 if needed.

Each version must have clear documentation and a defined support period.
## Version Selection

The default versioning approach is URL-based versioning:

/api/v1/courses
/api/v2/courses

URL versioning is preferred because it is easy to understand during development, monitoring, and troubleshooting.

Header-based versioning using X-Api-Version may be enabled for specific partners when URL changes are not possible.

Header-based versioning using X-Api-Version is supported only for specific partners when URL versioning is not possible. URL-based versioning remains the default because it is clearer during monitoring and troubleshooting.