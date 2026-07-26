# GEMINI.md

Follow [AGENTS.md](AGENTS.md) as the authoritative repository guidance.

Common validation commands:

```powershell
dotnet restore jellyfin.sln
dotnet build jellyfin/jellyfin.csproj -c Debug
dotnet build jellyfin/jellyfin.csproj -c Release
dotnet test jellyfin.CacheHelper.Tests/jellyfin.CacheHelper.Tests.csproj
```

Application packages are written beneath `jellyfin/bin/<Configuration>/net6.0-tizen9.0/`.
