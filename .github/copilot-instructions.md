## Purpose
Short instructions for Copilot/AI coding agents working on this repository (Azure Functions .NET 8). Focus on facts discovered in the codebase so you can be productive immediately.

## Quick facts
- Project type: Azure Functions (C#) targeting .NET 8 (in-process). See `EncryptFunctionApp.csproj` (TargetFramework: net8.0).
- Single function: `EncryptBlobFunction` defined in `EncryptBlobFunction.cs` (HTTP-triggered POST).
- Uses: `Azure.Identity` (DefaultAzureCredential), `Azure.Security.KeyVault.Keys`, `Azure.Storage.Blobs`.
- Local dev config: `local.settings.json` includes `FUNCTIONS_INPROC_NET8_ENABLED=1`.

## How to run locally (exact, repeatable)
1. Ensure .NET 8 SDK and Azure Functions Core Tools v4 are installed. Verified working commands:

   dotnet --info
   func --version

2. Build then start the function host with verbose logs:

   dotnet build
   func start --verbose

Notes: the in-proc .NET 8 helper is a small binary installed with the Core Tools. If `func start` fails with "Permission denied" when launching the inproc8 helper, run:

   chmod +x /opt/homebrew/Cellar/azure-functions-core-tools@4/<version>/in-proc8/func

Replace `<version>` with the installed Homebrew Core Tools version (example: `4.4.1`). The build output (DLL and function.json) is placed under `bin/output/` during local starts.

## Authentication & secrets
- The code uses `DefaultAzureCredential()` (see `EncryptBlobFunction.cs`). For local development you must provide credentials via one of the DefaultAzureCredential methods: `az login`, environment variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`), or VS Code/Visual Studio sign-in. The function expects to be able to access Key Vault and Blob Storage using those credentials.
- `local.settings.json` is used for local config only and is copied to output but not published (see csproj). Do NOT add production secrets here.

## Request/Response contract (concrete example)
The function expects a JSON body matching this shape (see `EncryptRequest` class in `EncryptBlobFunction.cs`):

```json
{
  "StorageAccountName": "mystorageaccount",
  "BlobContainerName": "mycontainer",
  "BlobFileName": "file.txt",
  "KeyVaultUrl": "https://<your-keyvault-name>.vault.azure.net/",
  "KeyName": "my-key-name"
}
```

Success response: HTTP 200 with message `Encrypted file uploaded as <name>.encrypted`.

## Key files to inspect / edit
- `EncryptBlobFunction.cs` — core function logic, AES file encryption and RSA wrapping using Key Vault key.
- `EncryptFunctionApp.csproj` — dependencies and publish/copy rules; target framework is `net8.0`.
- `host.json` — Functions host configuration (Application Insights sampling enabled).
- `local.settings.json` — local-only settings (see `FUNCTIONS_INPROC_NET8_ENABLED` flag).

## Patterns & conventions observed
- Uses in-process .NET 8 model (inproc) rather than out-of-process worker; that is controlled by `FUNCTIONS_INPROC_NET8_ENABLED`.
- Uses `DefaultAzureCredential` everywhere — local developers should authenticate with `az login` or set managed identity/service principal vars for CI.
- Build/publish puts outputs under `bin/output/` (solution-level build prints a NETSDK1194 warning but is expected; artifacts are still produced there).

## Troubleshooting tips (from repo investigations)
- "Permission denied" when starting host: make sure the inproc8 helper binary is executable (see above chmod). Also check `xattr -l` and Gatekeeper if macOS blocks the binary.
- If Key Vault or Blob access fails locally, confirm `az account show` is valid and/or set env vars for a service principal.

## When changing behavior
- If you change the entrypoint or function name, update the `FunctionName` attribute in `EncryptBlobFunction.cs` and re-run `dotnet build` before `func start`.
- If you add new NuGet references, restore and rebuild via `dotnet restore` / `dotnet build`.

## Minimal tests to add (suggested, not present)
- Unit tests for the AES+RSA packaging logic (parsing the final combined stream). Keep test assets small and deterministic.

---
If any of these notes are unclear or you'd like the file to include CI/publish instructions, tell me what CI provider you use (GitHub Actions, Azure DevOps) and I will expand the file.
