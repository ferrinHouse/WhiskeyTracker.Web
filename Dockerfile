# syntax=docker/dockerfile:1

# Build on the machine's native architecture and cross-compile for the target,
# so multi-arch images don't need QEMU emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY WhiskeyTracker.Web/WhiskeyTracker.Web.csproj WhiskeyTracker.Web/
RUN dotnet restore WhiskeyTracker.Web/WhiskeyTracker.Web.csproj -a $TARGETARCH

COPY WhiskeyTracker.Web/ WhiskeyTracker.Web/
RUN dotnet publish WhiskeyTracker.Web/WhiskeyTracker.Web.csproj \
    -c Release -a $TARGETARCH --no-restore --self-contained false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# The aspnet image listens on 8080 by default. It runs as root, matching the current
# deployment, because uploads are written to an NFS volume that fsGroup doesn't cover.
EXPOSE 8080
ENTRYPOINT ["dotnet", "WhiskeyTracker.Web.dll"]
